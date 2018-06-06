using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Converter.Logic.Schema;
using log4net;

namespace Converter.Logic
{
    public class SqlServerSchemaReader
    {
        #region Events

        /// <summary>
        /// Raised when a table schema has been read from the database.  Also contains progress information for the operation.
        /// </summary>
        public event EventHandler<TableSchemaReaderProgressChangedEventArgs> TableSchemaReaderProgressChanged;

        /// <summary>
        /// Raised when a table schema has been read from the database.  Also contains progress information for the operation.
        /// </summary>
        public event EventHandler<ViewSchemaReaderProgressChangedEventArgs> ViewSchemaReaderProgressChanged;

        private void OnTableSchemaReaderProgressChanged(TableSchema lastProcessed, int processed, int remaining)
        {
            var handler = TableSchemaReaderProgressChanged;
            if (handler != null)
            {
                handler(this, new TableSchemaReaderProgressChangedEventArgs(lastProcessed, processed, remaining));
            }
        }

        private void OnViewSchemaReaderProgressChanged(ViewSchema lastProcessed, int processed, int remaining)
        {
            var handler = ViewSchemaReaderProgressChanged;
            if (handler != null)
            {
                handler(this, new ViewSchemaReaderProgressChangedEventArgs(lastProcessed, processed, remaining));
            }
        }
        

        #endregion

        private readonly Regex _keyRx = new Regex(@"(([a-zA-Z_äöüÄÖÜß0-9\.]|(\s+))+)(\(\-\))?");

        private readonly String _connectionString;
        private readonly ILog _log;
        private List<TableSchema> _tables;
        private List<TableSchema> _tableSchemas;
        private List<TableSchema> _tableData;
        private List<ViewSchema> _views;

        #region Properties

        /// <summary>
        /// Gets a list of all tables in the database.
        /// </summary>
        public List<TableSchema> Tables
        {
            get
            {
                if (_tables == null || !_tables.Any())
                {
                    return new List<TableSchema>();
                }
                return _tables;
                
            }
        }

        /// <summary>
        /// Gets or sets the list of tables that will be included in the SQLite database.
        /// </summary>
        public List<TableSchema> TablesIncludeSchema
        {
            get
            {
                if (_tableSchemas == null || !_tableSchemas.Any())
                {
                    return new List<TableSchema>();
                }
                return _tableSchemas;
            }
            set { _tableSchemas = value; }
        }

        /// <summary>
        /// Gets or sets the list of tables that will have their data included in the SQLite database.
        /// </summary>
        public List<TableSchema> TablesIncludeData
        {
            get
            {
                if (_tableData == null || !_tableData.Any())
                {
                    return new List<TableSchema>();
                }
                return _tableData;
            }
            set { _tableData = value; }
        }

        /// <summary>
        /// Gets a list of all views in the database.
        /// </summary>
        public List<ViewSchema> Views
        {
            get { return this._views; }
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSchemaReader"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string for SQL Server.</param>
        /// <param name="log">The log.</param>
        public SqlServerSchemaReader(String connectionString, ILog log)
        {
            _connectionString = connectionString;
            _log = log;
        }

        /// <summary>
        /// Populates the list of table schemas.
        /// </summary>
        public void PopulateTableSchema()
        {
            _tables = GetTableSchemas();
        }

        /// <summary>
        /// Populates the list of view schemas.
        /// </summary>
        public void PopulateViewSchema()
        {
            _views = GetViews();
        }

        public DatabaseSchema GetDatabaseSchema()
        {
            var ds = new DatabaseSchema
            {
                Tables = this.TablesIncludeSchema,
                Views = this.Views,
            };
            return ds;   
        }

        /// <summary>
        /// Gets a list of table schemas from the specified connection string.
        /// </summary>
        private List<TableSchema> GetTableSchemas(SqlConversionProgressReportingHandler progressReportingHandler = null)
        {
            List<Tuple<String, String>> tableNamesAndSchemas = new List<Tuple<String, String>>();

            // First step is to read the names of all tables in the database
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // This command will read the names of all tables in the database
                var cmd = new SqlCommand(@"SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA ASC, TABLE_NAME ASC", conn);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["TABLE_SCHEMA"] == DBNull.Value) { continue; }
                        if (reader["TABLE_NAME"] == DBNull.Value) { continue; }
                        
                        var tableSchema = (String)reader["TABLE_SCHEMA"];
                        var tableName = (String) reader["TABLE_NAME"];
                        
                        tableNamesAndSchemas.Add(new Tuple<String, String>(tableSchema, tableName));
                    }
                }
            }

            tableNamesAndSchemas = tableNamesAndSchemas.OrderBy(obj => obj.Item1).ThenBy(obj => obj.Item2).ToList();

            // Next step is to use ADO APIs to query the schema of each table.
            List<TableSchema> tables = new List<TableSchema>();
            Object stateLocker = new Object();
            int count = 0;
            int totalTables = tableNamesAndSchemas.Count;

            Parallel.ForEach(tableNamesAndSchemas, tableTuple =>
            {
                String tableSchema = tableTuple.Item1;
                String tableName = tableTuple.Item2;

                TableSchema ts = CreateTableSchema(tableName, tableSchema);
                CreateForeignKeySchema(ts);
                int tablesProcessed;
                lock (stateLocker)
                {
                    tables.Add(ts);
                    count++;
                    tablesProcessed = count; // Copy the current number of processed tables to a local for future usage.
                }
                SqlServerToSQLite.CheckCancelled();
                if (progressReportingHandler != null)
                {
                    progressReportingHandler(false, true, (int)(count * 50.0 / totalTables), "Parsed table " + tableName);
                }
                _log.Debug("parsed table schema for [" + tableName + "]");

                int remaining = totalTables - tablesProcessed;
                OnTableSchemaReaderProgressChanged(ts, tablesProcessed, remaining);
            });

            _log.Debug("finished parsing all tables in SQL Server schema");

            // Sort the resulting list of TableSchema objects by the underlying table's name.
            tables = tables.OrderBy(obj => obj.TableName).ToList();
            _log.Debug("finished sorting all tables in SQL Server schema");

            return tables;
        }

        /// <summary>
        /// Creates a TableSchema object using the specified SQL Server connection and the name of the table for which we need to create the schema.
        /// </summary>
        /// <param name="tableName">The name of the table for which we want to create a table schema.</param>
        /// <param name="tableSchemaName">The name of the schema containing the table for which we want to create a table schema.</param>
        /// <returns>A table schema object that represents our knowledge of the table schema</returns>
        private TableSchema CreateTableSchema(string tableName, string tableSchemaName)
        {
            TableSchema res = new TableSchema();
            res.TableName = tableName;
            res.TableSchemaName = tableSchemaName;
            res.Columns = new List<ColumnSchema>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(@"SELECT COLUMN_NAME,COLUMN_DEFAULT,IS_NULLABLE,DATA_TYPE, " +
                                                @" (columnproperty(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity')) AS [IDENT], " +
                                                @"CHARACTER_MAXIMUM_LENGTH AS CSIZE " +
                                                "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + tableName + "' ORDER BY " +
                                                "ORDINAL_POSITION ASC", conn);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        object tmp = reader["COLUMN_NAME"];
                        if (tmp is DBNull)
                        {
                            continue;
                        }
                        string colName = (string)reader["COLUMN_NAME"];

                        tmp = reader["COLUMN_DEFAULT"];
                        string colDefault;
                        if (tmp is DBNull)
                        {
                            colDefault = String.Empty;
                        }
                        else
                        {
                            colDefault = (string)tmp;
                        }

                        tmp = reader["IS_NULLABLE"];
                        bool isNullable = ((string)tmp == "YES");
                        string dataType = (string)reader["DATA_TYPE"];
                        bool isIdentity = false;
                        if (reader["IDENT"] != DBNull.Value)
                        {
                            isIdentity = (((int)reader["IDENT"]) == 1);
                        }
                        int length = reader["CSIZE"] != DBNull.Value ? Convert.ToInt32(reader["CSIZE"]) : 0;

                        SqlServerToSQLite.ValidateDataType(dataType);

                        // Note that not all data type names need to be converted because
                        // SQLite establishes type affinity by searching certain strings
                        // in the type name. For example - everything containing the string
                        // 'int' in its type name will be assigned an INTEGER affinity
                        if (dataType == "timestamp")
                        {
                            dataType = "blob";
                        }
                        else if (dataType == "datetime" || dataType == "smalldatetime" || dataType == "date" || dataType == "datetime2" || dataType == "time")
                        {
                            dataType = "datetime";
                        }
                        else if (dataType == "decimal")
                        {
                            dataType = "numeric";
                        }
                        else if (dataType == "money" || dataType == "smallmoney")
                        {
                            dataType = "numeric";
                        }
                        else if (dataType == "binary" || dataType == "varbinary" || dataType == "image")
                        {
                            dataType = "blob";
                        }
                        else if (dataType == "tinyint")
                        {
                            dataType = "smallint";
                        }
                        else if (dataType == "bigint")
                        {
                            dataType = "integer";
                        }
                        else if (dataType == "sql_variant")
                        {
                            dataType = "blob";
                        }
                        else if (dataType == "xml")
                        {
                            dataType = "varchar";
                        }
                        else if (dataType == "uniqueidentifier")
                        {
                            dataType = "guid";
                        }
                        else if (dataType == "ntext")
                        {
                            dataType = "text";
                        }
                        else if (dataType == "nchar")
                        {
                            dataType = "char";
                        }

                        if (dataType == "bit" || dataType == "int")
                        {
                            if (colDefault == "('False')")
                            {
                                colDefault = "(0)";
                            }
                            else if (colDefault == "('True')")
                            {
                                colDefault = "(1)";
                            }
                        }

                        colDefault = SqlServerToSQLite.FixDefaultValueString(colDefault);

                        ColumnSchema col = new ColumnSchema();
                        col.ColumnName = colName;
                        col.ColumnType = dataType;
                        col.Length = length;
                        col.IsNullable = isNullable;
                        col.IsIdentity = isIdentity;
                        col.DefaultValue = SqlServerToSQLite.AdjustDefaultValue(colDefault);
                        res.Columns.Add(col);
                    }
                }

                // Find PRIMARY KEY information
                SqlCommand cmd2 = new SqlCommand(@"EXEC sp_pkeys '" + tableName + "'", conn);
                using (SqlDataReader reader = cmd2.ExecuteReader())
                {
                    res.PrimaryKey = new List<string>();
                    while (reader.Read())
                    {
                        string colName = (string)reader["COLUMN_NAME"];
                        res.PrimaryKey.Add(colName);
                    }
                }

                // Find COLLATE information for all columns in the table
                SqlCommand cmd4 = new SqlCommand(@"EXEC sp_tablecollations '" + tableSchemaName + "." + tableName + "'", conn);
                using (SqlDataReader reader = cmd4.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        bool? isCaseSensitive = null;
                        string colName = (string)reader["name"];
                        if (reader["tds_collation"] != DBNull.Value)
                        {
                            byte[] mask = (byte[])reader["tds_collation"];
                            isCaseSensitive = (mask[2] & 0x10) == 0;
                        }

                        if (isCaseSensitive.HasValue)
                        {
                            // Update the corresponding column schema.
                            foreach (ColumnSchema csc in res.Columns)
                            {
                                if (csc.ColumnName == colName)
                                {
                                    csc.IsCaseSensitive = isCaseSensitive;
                                    break;
                                }
                            }
                        }
                    }
                }

                try
                {
                    // Find index information
                    SqlCommand cmd3 = new SqlCommand(@"exec sp_helpindex '" + tableSchemaName + "." + tableName + "'", conn);
                    using (SqlDataReader reader = cmd3.ExecuteReader())
                    {
                        res.Indexes = new List<IndexSchema>();
                        while (reader.Read())
                        {
                            string indexName = (string)reader["index_name"];
                            string desc = (string)reader["index_description"];
                            string keys = (string)reader["index_keys"];

                            // Don't add the index if it is actually a primary key index
                            if (desc.Contains("primary key")) { continue; }

                            IndexSchema index = BuildIndexSchema(indexName, desc, keys);
                            res.Indexes.Add(index);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Error in \"CreateTableSchema\"", ex);
                    _log.Warn("failed to read index information for table [" + tableName + "]");
                }
            }
            return res;
        }

        private List<ViewSchema> GetViews(SqlConversionProgressReportingHandler progressReportingHandler = null)
        {
            var views = new List<ViewSchema>();
            
            Regex removedbo = new Regex(@"dbo\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"SELECT TABLE_NAME, VIEW_DEFINITION from INFORMATION_SCHEMA.VIEWS", conn);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var vs = new ViewSchema();

                        if (reader["TABLE_NAME"] == DBNull.Value) { continue; }
                        if (reader["VIEW_DEFINITION"] == DBNull.Value) { continue; }
                        vs.ViewName = (string)reader["TABLE_NAME"];
                        vs.ViewSQL = (string)reader["VIEW_DEFINITION"];

                        // Remove all ".dbo" strings from the view definition
                        vs.ViewSQL = removedbo.Replace(vs.ViewSQL, String.Empty);

                        views.Add(vs);
                    }
                }
            }

            Object stateLocker = new Object();
            int count = 0;
            int totalViews = views.Count;
            var parallelResult = Parallel.For(0, totalViews, i =>
            {
                var vs = views[i];
                count++;
                if (progressReportingHandler != null)
                {
                    progressReportingHandler(false, true, 50 + (int)(count * 50.0 / totalViews), "Parsed view " + vs.ViewName);
                }

                int viewsProcessed;
                lock (stateLocker)
                {
                    //count++;
                    viewsProcessed = count; // Copy the current number of processed views to a local for future usage.
                }
                SqlServerToSQLite.CheckCancelled();
                if (progressReportingHandler != null)
                {
                    progressReportingHandler(false, true, (int)(count * 50.0 / totalViews), "Parsed table " + vs.ViewName);
                }
                _log.Debug("parsed view schema for [" + vs.ViewName + "]");

                int remaining = totalViews - viewsProcessed;
                OnViewSchemaReaderProgressChanged(vs, viewsProcessed, remaining);
            });

            while (!parallelResult.IsCompleted)
            {
                Thread.Sleep(1000);
            }
            _log.Debug("finished parsing all views in SQL Server schema");
            return views;
        }

        /// <summary>
        /// Add foreign key schema object from the specified components (Read from SQL Server).
        /// </summary>
        /// <param name="ts">The table schema to whom foreign key schema should be added to</param>
        private void CreateForeignKeySchema(TableSchema ts)
        {
            ts.ForeignKeys = new List<ForeignKeySchema>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(
                    @"SELECT " +
                    @"  ColumnName = CU.COLUMN_NAME, " +
                    @"  ForeignTableName  = PK.TABLE_NAME, " +
                    @"  ForeignColumnName = PT.COLUMN_NAME, " +
                    @"  DeleteRule = C.DELETE_RULE, " +
                    @"  IsNullable = COL.IS_NULLABLE " +
                    @"FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS C " +
                    @"INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS FK ON C.CONSTRAINT_NAME = FK.CONSTRAINT_NAME " +
                    @"INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS PK ON C.UNIQUE_CONSTRAINT_NAME = PK.CONSTRAINT_NAME " +
                    @"INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE CU ON C.CONSTRAINT_NAME = CU.CONSTRAINT_NAME " +
                    @"INNER JOIN " +
                    @"  ( " +
                    @"    SELECT i1.TABLE_NAME, i2.COLUMN_NAME " +
                    @"    FROM  INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1 " +
                    @"    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2 ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME " +
                    @"    WHERE i1.CONSTRAINT_TYPE = 'PRIMARY KEY' " +
                    @"  ) " +
                    @"PT ON PT.TABLE_NAME = PK.TABLE_NAME " +
                    @"INNER JOIN INFORMATION_SCHEMA.COLUMNS AS COL ON CU.COLUMN_NAME = COL.COLUMN_NAME AND FK.TABLE_NAME = COL.TABLE_NAME " +
                    @"WHERE FK.Table_NAME='" + ts.TableName + "'", conn);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ForeignKeySchema fkc = new ForeignKeySchema();
                        fkc.ColumnName = (string)reader["ColumnName"];
                        fkc.ForeignTableName = (string)reader["ForeignTableName"];
                        fkc.ForeignColumnName = (string)reader["ForeignColumnName"];
                        fkc.CascadeOnDelete = (string)reader["DeleteRule"] == "CASCADE";
                        fkc.IsNullable = (string)reader["IsNullable"] == "YES";
                        fkc.TableName = ts.TableName;
                        ts.ForeignKeys.Add(fkc);
                    }
                }
            }
        }

        /// <summary>
        /// Builds an index schema object from the specified components (Read from SQL Server).
        /// </summary>
        /// <param name="indexName">The name of the index</param>
        /// <param name="desc">The description of the index</param>
        /// <param name="keys">Key columns that are part of the index.</param>
        /// <returns>An index schema object that represents our knowledge of the index</returns>
        private IndexSchema BuildIndexSchema(string indexName, string desc, string keys)
        {
            IndexSchema res = new IndexSchema();
            res.IndexName = indexName;

            // Determine if this is a unique index or not.
            string[] descParts = desc.Split(',');
            foreach (string p in descParts)
            {
                if (p.Trim().Contains("unique"))
                {
                    res.IsUnique = true;
                    break;
                }
            }

            // Get all key names and check if they are ASCENDING or DESCENDING
            res.Columns = new List<IndexColumn>();
            string[] keysParts = keys.Split(',');
            foreach (string p in keysParts)
            {
                Match m = _keyRx.Match(p.Trim());
                if (!m.Success)
                {
                    throw new ApplicationException("Illegal key name [" + p + "] in index [" + indexName + "]");
                }

                string key = m.Groups[1].Value;
                IndexColumn ic = new IndexColumn();
                ic.ColumnName = key;
                ic.IsAscending = !m.Groups[2].Success;

                res.Columns.Add(ic);
            }
            return res;
        }
    }
}
