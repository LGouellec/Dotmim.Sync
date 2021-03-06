﻿using Dotmim.Sync.Builders;
using Dotmim.Sync.Data;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OracleClient;
using System.Linq;
using System.Text;

namespace Dotmim.Sync.Oracle
{
    public static class OracleManagementUtils
    {
        /// <summary>
        /// Get columns for table
        /// </summary>
        public static DmTable ColumnsForTable(OracleConnection connection, OracleTransaction transaction, string tableName)
        {

            var commandColumn = @"SELECT column_name AS name, column_id, data_type, data_length, data_precision, data_scale, decode(nullable, 'N', 0, 1) AS is_nullable
                                    FROM USER_TAB_COLUMNS
                                    WHERE TABLE_NAME = :tableName";

            ObjectNameParser tableNameParser = new ObjectNameParser(tableName);
            DmTable dmTable = new DmTable(tableNameParser.UnquotedStringWithUnderScore);
            using (OracleCommand oracleCommand = new OracleCommand(commandColumn, connection, transaction))
            {
                oracleCommand.Parameters.Add("tableName", tableNameParser.ObjectName);

                using (var reader = oracleCommand.ExecuteReader())
                {
                    dmTable.Fill(reader);
                }
            }
            return dmTable;
        }

        /// <summary>
        /// Get columns for table
        /// </summary>
        internal static DmTable PrimaryKeysForTable(OracleConnection connection, OracleTransaction transaction, string tableName)
        {
            var commandColumn = @"SELECT ac.constraint_name as name, column_name as columnName, position as column_id
                                    FROM all_cons_columns acc
                                    INNER JOIN all_constraints ac
                                    ON acc.constraint_name = ac.constraint_name AND acc.table_name = ac.table_name
                                    WHERE CONSTRAINT_TYPE IN ('P') AND ac.table_name = :tableName";

            ObjectNameParser tableNameParser = new ObjectNameParser(tableName);
            DmTable dmTable = new DmTable(tableNameParser.UnquotedStringWithUnderScore);
            using (OracleCommand oracleCommand = new OracleCommand(commandColumn, connection, transaction))
            {
                oracleCommand.Parameters.Add("tableName", tableNameParser.ObjectName);

                using (var reader = oracleCommand.ExecuteReader())
                {
                    dmTable.Fill(reader);
                }
            }

            return dmTable;
        }

        /// <summary>
        /// Get relations table
        /// </summary>
        internal static DmTable RelationsForTable(OracleConnection connection, OracleTransaction transaction, string tableName)
        {
            var commandRelations = @"SELECT a.constraint_name AS ForeignKey, a.table_name AS TableName, a.column_name AS ColumnName, 
                                           c_pk.table_name AS ReferenceTableName,  b.column_name AS ReferenceColumnName
                                      FROM user_cons_columns a
                                      JOIN user_constraints c ON a.owner = c.owner
                                           AND a.constraint_name = c.constraint_name
                                      JOIN user_constraints c_pk ON c.r_owner = c_pk.owner
                                           AND c.r_constraint_name = c_pk.constraint_name
                                      JOIN user_cons_columns b ON C_PK.owner = b.owner
                                           AND  C_PK.CONSTRAINT_NAME = b.constraint_name AND b.POSITION = a.POSITION     
                                     WHERE c.constraint_type = 'R' AND a.table_name = :tableName";

            ObjectNameParser tableNameParser = new ObjectNameParser(tableName);
            DmTable dmTable = new DmTable(tableNameParser.UnquotedStringWithUnderScore);
            using (OracleCommand oracleCommand = new OracleCommand(commandRelations, connection, transaction))
            {
                oracleCommand.Parameters.Add("tableName", tableNameParser.ObjectName);

                using (var reader = oracleCommand.ExecuteReader())
                {
                    dmTable.Fill(reader);
                }
            }


            return dmTable;

        }

        internal static bool TriggerExists(OracleConnection connection, OracleTransaction transaction, string triggerName)
        {
            bool triggerExist;
            ObjectNameParser objectNameParser = new ObjectNameParser(triggerName);
            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "select count(1) from USER_TRIGGERS where TRIGGER_NAME = upper(:triggerName)";

                OracleParameter sqlParameter = new OracleParameter()
                {
                    ParameterName = "triggerName",
                    Value = objectNameParser.ObjectName
                };
                dbCommand.Parameters.Add(sqlParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                triggerExist = Convert.ToInt32(dbCommand.ExecuteScalar()) != 0;
            }
            return triggerExist;
        }

        public static bool TableExists(OracleConnection connection, OracleTransaction transaction, string quotedTableName)
        {
            bool tableExist;
            ObjectNameParser objectNameParser = new ObjectNameParser(quotedTableName);
            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "select count(1) from user_tables where upper(table_name) = upper(:tableName)";

                OracleParameter sqlParameter = new OracleParameter() {
                    ParameterName = "tableName",
                    Value = objectNameParser.ObjectName
                };
                dbCommand.Parameters.Add(sqlParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                tableExist = Convert.ToInt32(dbCommand.ExecuteScalar()) != 0;
            }
            return tableExist;
        }

        public static bool SchemaExists(OracleConnection connection, OracleTransaction transaction, string schemaName)
        {
            bool schemaExist;
            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "SELECT COUNT(1) FROM dba_users WHERE username = @schemaName";

                var sqlParameter = new OracleParameter()
                {
                    ParameterName = "@schemaName",
                    Value = schemaName
                };
                dbCommand.Parameters.Add(sqlParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                schemaExist = (int)dbCommand.ExecuteScalar() != 0;
            }
            return schemaExist;
        }

        internal static string JoinTwoTablesOnClause(DmColumn[] columns, string leftName, string rightName)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strRightName = (string.IsNullOrEmpty(rightName) ? string.Empty : string.Concat(rightName, "."));
            string strLeftName = (string.IsNullOrEmpty(leftName) ? string.Empty : string.Concat(leftName, "."));

            string str = "";
            foreach (DmColumn column in columns)
            {
                ObjectNameParser quotedColumn = new ObjectNameParser(column.ColumnName);

                stringBuilder.Append(str);
                stringBuilder.Append(strLeftName);
                stringBuilder.Append(quotedColumn.UnquotedString);
                stringBuilder.Append(" = ");
                stringBuilder.Append(strRightName);
                stringBuilder.Append(quotedColumn.UnquotedString);

                str = " AND ";
            }
            return stringBuilder.ToString();
        }

        internal static bool ProcedureExists(OracleConnection connection, OracleTransaction transaction, string commandName)
        {
            bool tableExist;
            ObjectNameParser objectNameParser = new ObjectNameParser(commandName);
            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "SELECT count(1) FROM user_objects WHERE OBJECT_TYPE IN ('PROCEDURE') and UPPER(object_name) = upper(:commandName)";

                OracleParameter sqlParameter = new OracleParameter()
                {
                    ParameterName = "commandName",
                    Value = objectNameParser.ObjectName
                };
                dbCommand.Parameters.Add(sqlParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                tableExist = Convert.ToInt32(dbCommand.ExecuteNonQuery()) != 0;
            }
            return tableExist;
        }

        internal static bool TypeExists(OracleConnection connection, OracleTransaction transaction, string commandName)
        {
            bool typeExist;
            ObjectNameParser objectNameParser = new ObjectNameParser(commandName);
            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "SELECT count(1) FROM user_types WHERE UPPER(TYPE_NAME) = upper(:commandName)";

                OracleParameter sqlParameter = new OracleParameter()
                {
                    ParameterName = "commandName",
                    Value = objectNameParser.ObjectName
                };
                dbCommand.Parameters.Add(sqlParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                typeExist = Convert.ToInt32(dbCommand.ExecuteNonQuery()) != 0;
            }
            return typeExist;
        }
    
        internal static string ColumnsAndParameters(DmColumn[] columns, string fromPrefix)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string str1 = "";
            foreach (DmColumn column in columns)
            {
                ObjectNameParser quotedColumn = new ObjectNameParser(column.ColumnName);

                stringBuilder.Append(str1);
                stringBuilder.Append(strFromPrefix);
                stringBuilder.Append(quotedColumn.UnquotedString);
                stringBuilder.Append(" = ");
                stringBuilder.Append($":{column.ColumnName}");
                str1 = " AND ";
            }
            return stringBuilder.ToString();
        }

        internal static string ColumnsAndParametersStoredProcedure(DmColumn[] columns, string fromPrefix)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string str1 = "";
            foreach (DmColumn column in columns)
            {
                ObjectNameParser quotedColumn = new ObjectNameParser(column.ColumnName);

                stringBuilder.Append(str1);
                stringBuilder.Append(strFromPrefix);
                stringBuilder.Append(quotedColumn.UnquotedString);
                stringBuilder.Append(" = ");
                stringBuilder.Append($"{column.ColumnName}0");
                str1 = " AND ";
            }
            return stringBuilder.ToString();
        }

        internal static object CommaSeparatedUpdateFromParameters(DmTable table, string fromPrefix = "")
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string strSeparator = "";
            foreach (DmColumn column in table.NonPkColumns.Where(c => !c.ReadOnly))
            {
                ObjectNameParser quotedColumn = new ObjectNameParser(column.ColumnName);
                stringBuilder.AppendLine($"{strSeparator} {strFromPrefix}{quotedColumn.UnquotedString} = :{quotedColumn.UnquotedString}");
                strSeparator = ", ";
            }
            return stringBuilder.ToString();
        }

        internal static object CommaSeparatedUpdateFromParametersStoredProcedure(DmTable table, string fromPrefix = "")
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string strSeparator = "";
            foreach (DmColumn column in table.NonPkColumns.Where(c => !c.ReadOnly))
            {
                ObjectNameParser quotedColumn = new ObjectNameParser(column.ColumnName);
                stringBuilder.AppendLine($"{strSeparator} {strFromPrefix}{quotedColumn.UnquotedString} = {quotedColumn.UnquotedString}0");
                strSeparator = ", ";
            }
            return stringBuilder.ToString();
        }
    }
}
