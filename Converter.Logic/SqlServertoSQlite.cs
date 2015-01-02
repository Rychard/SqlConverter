using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using Converter.Logic.Triggers;
using Converter.Logic.Schema;
using log4net;

namespace Converter.Logic
{
    /// <summary>
    /// This class is responsible for converting SQL Server databases into SQLite database files.
    /// </summary>
    /// <remarks>Only supports the conversion of table and index structures.</remarks>
    public class SqlServerToSQLite
    {
        private static readonly Regex _defaultValueRx = new Regex(@"\(N(\'.*\')\)");
        private static readonly ILog _log = LogManager.GetLogger(typeof(SqlServerToSQLite));

        /// <summary>
        /// Gets a reference to the log.
        /// </summary>
        public static ILog Log
        {
            get { return _log; }
        }
        
        private static Boolean _isActive;
        private static Boolean _cancelled;
        

        /// <summary>
        /// Gets a value indicating whether this instance is active.
        /// </summary>
        /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
        public static Boolean IsActive { get { return _isActive; } }

        /// <summary>
        /// Cancels the conversion.
        /// </summary>
        public static void CancelConversion()
        {
            _cancelled = true;
        }

        /// <summary>
        /// This method takes as input the connection string to an SQL Server database
        /// and creates a corresponding SQLite database file with a schema as retrieved from
        /// the SQL Server database.
        /// </summary>
        /// <param name="sqlServerConnString">The connection string to the SQL Server database.</param>
        /// <param name="sqlitePath">The path to the SQLite database file that needs to get created.</param>
        /// <param name="password">The password to use or NULL if no password should be used to encrypt the DB</param>
        /// <param name="handler">A handler delegate for progress notifications.</param>
        /// <param name="selectionHandler">The selection handler that allows the user to select which tables to convert</param>
        /// <remarks>The method continues asynchronously in the background and the caller returns immediately.</remarks>
        public static void ConvertSqlServerToSQLiteDatabase(string sqlServerConnString, string sqlitePath, string password, SqlConversionHandler handler, SqlTableSelectionHandler selectionHandler, FailedViewDefinitionHandler viewFailureHandler, Boolean createTriggers, Boolean createViews)
        {
            // Clear cancelled flag
            _cancelled = false;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    _isActive = true;
                    String sqlitePathResolved = TemplateToFilename(sqlitePath);
                    ConvertSqlServerDatabaseToSQLiteFile(sqlServerConnString, sqlitePathResolved, password, handler, selectionHandler, viewFailureHandler, createTriggers, createViews);
                    _isActive = false;
                    handler(true, true, 100, "Finished converting database");
                }
                catch (Exception ex)
                {
                    _log.Error("Failed to convert SQL Server database to SQLite database", ex);
                    _isActive = false;
                    handler(true, false, 100, ex.Message);
                }
            });
        }

        /// <summary>
        /// Do the entire process of first reading the SQL Server schema, creating a corresponding
        /// SQLite schema, and copying all rows from the SQL Server database to the SQLite database.
        /// </summary>
        /// <param name="sqlConnString">The SQL Server connection string</param>
        /// <param name="sqlitePath">The path to the generated SQLite database file</param>
        /// <param name="password">The password to use or NULL if no password should be used to encrypt the DB</param>
        /// <param name="handler">A handler to handle progress notifications.</param>
        /// <param name="selectionHandler">The selection handler which allows the user to select which tables to convert.</param>
        private static void ConvertSqlServerDatabaseToSQLiteFile(String sqlConnString, String sqlitePath, String password, SqlConversionHandler handler, SqlTableSelectionHandler selectionHandler, FailedViewDefinitionHandler viewFailureHandler, Boolean createTriggers, Boolean createViews)
        {
            // Delete the destination file (only if it exists)
            if (DeleteFile(sqlitePath))
            {
                throw new Exception("File could not be deleted!");
            }

            SqlServerSchemaReader schemaReader = new SqlServerSchemaReader(sqlConnString, Log);

            schemaReader.TableSchemaReaderProgressChanged += (sender, args) =>
            {
                int total = args.TablesProcessed + args.TablesRemaining;
                int percentage = (int) ((args.TablesProcessed/(Double) total)*100);
                String msg = String.Format("Parsed table {0}", args.LastProcessedTable.TableName);
                handler(false, false, percentage, msg);
            };

            schemaReader.ViewSchemaReaderProgressChanged += (sender, args) =>
            {
                int total = args.ViewsProcessed + args.ViewsRemaining;
                int percentage = (int) ((args.ViewsProcessed/(Double) total)*100);
                String msg = String.Format("Parsed view {0}", args.LastProcessedView.ViewName);
                handler(false, false, percentage, msg);
            };

            schemaReader.PopulateTableSchema();
            schemaReader.PopulateViewSchema();

            var includeSchema = selectionHandler(schemaReader.Tables);
            schemaReader.TablesIncludeSchema = includeSchema;

            var includeData = selectionHandler(includeSchema);
            schemaReader.TablesIncludeData = includeData;

            // Read the schema of the SQL Server database into a memory structure
            DatabaseSchema ds = schemaReader.GetDatabaseSchema();

            // Create the SQLite database and apply the schema
            CreateSQLiteDatabase(sqlitePath, ds, password, handler, viewFailureHandler, createViews);

            // Copy all rows from SQL Server tables to the newly created SQLite database
            var tablesToCopy = ds.Tables.Where(obj => includeData.Any(include => include.TableName == obj.TableName)).ToList();
            CopySqlServerRowsToSQLiteDB(sqlConnString, sqlitePath, tablesToCopy, password, handler);

            // Add triggers based on foreign key constraints
            if (createTriggers)
            {
                AddTriggersForForeignKeys(sqlitePath, ds.Tables, password, handler);
            }
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <returns>Returns a boolean value indicating whether the file still exists.</returns>
        private static Boolean DeleteFile(String filepath)
        {
            if (File.Exists(filepath))
            {
                File.Delete(filepath);
            }
            return File.Exists(filepath);
        }

        /// <summary>
        /// Copies table rows from the SQL Server database to the SQLite database.
        /// </summary>
        /// <param name="sqlConnString">The SQL Server connection string</param>
        /// <param name="sqlitePath">The path to the SQLite database file.</param>
        /// <param name="schema">The schema of the SQL Server database.</param>
        /// <param name="password">The password to use for encrypting the file</param>
        /// <param name="handler">A handler to handle progress notifications.</param>
        private static void CopySqlServerRowsToSQLiteDB(String sqlConnString, String sqlitePath, List<TableSchema> schema, String password, SqlConversionHandler handler)
        {
            CheckCancelled();
            handler(false, true, 0, "Preparing to insert tables...");
            _log.Debug("preparing to insert tables ...");

            // Connect to the SQL Server database
            using (var sqlConnection = new SqlConnection(sqlConnString))
            {
                sqlConnection.Open();

                // Connect to the SQLite database next
                string sqliteConnString = CreateSQLiteConnectionString(sqlitePath, password);
                using (var sqliteConnection = new SQLiteConnection(sqliteConnString))
                {
                    sqliteConnection.Open();

                    // Go over all tables in the schema and copy their rows
                    for (int i = 0; i < schema.Count; i++)
                    {
                        SQLiteTransaction tx = sqliteConnection.BeginTransaction();
                        try
                        {
                            String tableQuery = BuildSqlServerTableQuery(schema[i]);
                            var query = new SqlCommand(tableQuery, sqlConnection);
                            using (SqlDataReader reader = query.ExecuteReader())
                            {
                                SQLiteCommand insert = BuildSQLiteInsert(schema[i]);
                                int counter = 0;
                                while (reader.Read())
                                {
                                    insert.Connection = sqliteConnection;
                                    insert.Transaction = tx;
                                    var pnames = new List<String>();
                                    for (int j = 0; j < schema[i].Columns.Count; j++)
                                    {
                                        String pname = "@" + GetNormalizedName(schema[i].Columns[j].ColumnName, pnames);
                                        insert.Parameters[pname].Value = CastValueForColumn(reader[j], schema[i].Columns[j]);
                                        pnames.Add(pname);
                                    }
                                    insert.ExecuteNonQuery();
                                    counter++;
                                    if (counter % 1000 == 0)
                                    {
                                        CheckCancelled();
                                        tx.Commit();
                                        handler(false, true, (int)(100.0 * i / schema.Count), "Added " + counter + " rows to table " + schema[i].TableName + " so far");
                                        tx = sqliteConnection.BeginTransaction();
                                    }
                                }
                            }

                            CheckCancelled();
                            tx.Commit();

                            handler(false, true, (int)(100.0 * i / schema.Count), "Finished inserting rows for table " + schema[i].TableName);
                            _log.Debug("finished inserting all rows for table [" + schema[i].TableName + "]");
                        }
                        catch (Exception ex)
                        {
                            _log.Error("unexpected exception", ex);
                            tx.Rollback();
                            throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Used in order to adjust the value received from SQL Server for the SQLite database.
        /// </summary>
        /// <param name="val">The value object</param>
        /// <param name="columnSchema">The corresponding column schema</param>
        /// <returns>SQLite adjusted value.</returns>
        private static Object CastValueForColumn(object val, ColumnSchema columnSchema)
        {
            if (val is DBNull)
            {
                return null;
            }

            DbType dt = GetDbTypeOfColumn(columnSchema);

            switch (dt)
            {
                case DbType.Int32:
                    if (val is short) { return (int)(short)val; }
                    if (val is byte) { return (int)(byte)val; }
                    if (val is long) { return (int)(long)val; }
                    if (val is decimal) { return (int)(decimal)val; }
                    break;

                case DbType.Int16:
                    if (val is int) { return (short)(int)val; }
                    if (val is byte) { return (short)(byte)val; }
                    if (val is long) { return (short)(long)val; }
                    if (val is decimal) { return (short)(decimal)val; }
                    break;

                case DbType.Int64:
                    if (val is int) { return (long)(int)val; }
                    if (val is short) { return (long)(short)val; }
                    if (val is byte) { return (long)(byte)val; }
                    if (val is decimal) { return (long)(decimal)val; }
                    break;

                case DbType.Single:
                    if (val is double) { return (float)(double)val; }
                    if (val is decimal) { return (float)(decimal)val; }
                    break;

                case DbType.Double:
                    if (val is float) { return (double)(float)val; }
                    if (val is double) { return (double)val; }
                    if (val is decimal) { return (double)(decimal)val; }
                    break;

                case DbType.String:
                    if (val is Guid) { return ((Guid)val).ToString(); }
                    break;

                case DbType.Guid:
                    if (val is string) { return ParseStringAsGuid((string)val); }
                    if (val is byte[]) { return ParseBlobAsGuid((byte[])val); }
                    break;

                case DbType.Binary:
                case DbType.Boolean:
                case DbType.DateTime:
                    break;

                default:
                    _log.Error("argument exception - illegal database type");
                    throw new ArgumentException("Illegal database type [" + Enum.GetName(typeof(DbType), dt) + "]");
            }

            return val;
        }

        private static Guid ParseBlobAsGuid(byte[] blob)
        {
            byte[] data = blob;
            if (blob.Length > 16)
            {
                data = new byte[16];
                for (int i = 0; i < 16; i++)
                {
                    data[i] = blob[i];
                }
            }
            else if (blob.Length < 16)
            {
                data = new byte[16];
                for (int i = 0; i < blob.Length; i++)
                {
                    data[i] = blob[i];
                }
            }

            return new Guid(data);
        }

        private static Guid ParseStringAsGuid(string str)
        {
            try
            {
                return new Guid(str);
            }
            catch (Exception ex)
            {
                Log.Error("Error in \"ParseStringAsGuid\"", ex);
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Creates a command object needed to insert values into a specific SQLite table.
        /// </summary>
        /// <param name="ts">The table schema object for the table.</param>
        /// <returns>A command object with the required functionality.</returns>
        private static SQLiteCommand BuildSQLiteInsert(TableSchema ts)
        {
            var res = new SQLiteCommand();

            var sb = new StringBuilder();
            sb.Append("INSERT INTO [" + ts.TableName + "] (");
            for (int i = 0; i < ts.Columns.Count; i++)
            {
                sb.Append("[" + ts.Columns[i].ColumnName + "]");
                if (i < ts.Columns.Count - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(") VALUES (");

            var pnames = new List<String>();
            for (int i = 0; i < ts.Columns.Count; i++)
            {
                string pname = "@" + GetNormalizedName(ts.Columns[i].ColumnName, pnames);
                sb.Append(pname);
                if (i < ts.Columns.Count - 1)
                {
                    sb.Append(", ");
                }

                DbType dbType = GetDbTypeOfColumn(ts.Columns[i]);
                var prm = new SQLiteParameter(pname, dbType, ts.Columns[i].ColumnName);
                res.Parameters.Add(prm);

                // Remember the parameter name in order to avoid duplicates
                pnames.Add(pname);
            }
            sb.Append(")");
            res.CommandText = sb.ToString();
            res.CommandType = CommandType.Text;
            return res;
        }

        /// <summary>
        /// Used in order to avoid breaking naming rules (e.g., when a table has
        /// a name in SQL Server that cannot be used as a basis for a matching index
        /// name in SQLite).
        /// </summary>
        /// <param name="str">The name to change if necessary</param>
        /// <param name="names">Used to avoid duplicate names</param>
        /// <returns>A normalized name</returns>
        private static string GetNormalizedName(string str, List<string> names)
        {
            Char[] characters = str.Select(c => Char.IsLetterOrDigit(c) ? c : '_').ToArray();
            String name = new String(characters);
            
            // Avoid returning duplicate name
            if (names.Contains(name))
            {
                return GetNormalizedName(name + "_", names);
            }
            return name;
        }

        /// <summary>
        /// Matches SQL Server types to general DB types
        /// </summary>
        /// <param name="cs">The column schema to use for the match</param>
        /// <returns>The matched DB type</returns>
        private static DbType GetDbTypeOfColumn(ColumnSchema cs)
        {
            Dictionary<String, DbType> typeMapping = new Dictionary<String, DbType>
            {
                { "tinyint", DbType.Byte }, 
                { "int", DbType.Int32 }, 
                { "smallint", DbType.Int16 }, 
                { "bigint", DbType.Int64 }, 
                { "bit", DbType.Boolean }, 
                { "nvarchar", DbType.String }, 
                { "varchar", DbType.String }, 
                { "text", DbType.String }, 
                { "ntext", DbType.String }, 
                { "float", DbType.Double }, 
                { "real", DbType.Single }, 
                { "blob", DbType.Binary }, 
                { "numeric", DbType.Double }, 
                { "timestamp", DbType.DateTime }, 
                { "datetime", DbType.DateTime }, 
                { "datetime2", DbType.DateTime }, 
                { "date", DbType.DateTime }, 
                { "time", DbType.DateTime }, 
                { "nchar", DbType.String }, 
                { "char", DbType.String }, 
                { "uniqueidentifier", DbType.Guid }, 
                { "guid", DbType.Guid }, 
                { "xml", DbType.String }, 
                { "sql_variant", DbType.Object }, 
                { "integer", DbType.Int64 },
            };

            var type = cs.ColumnType;
            if (typeMapping.ContainsKey(type))
            {
                return typeMapping[type];
            }

            _log.Error("illegal db type found");
            throw new ApplicationException("Illegal DB type found (" + cs.ColumnType + ")");
        }

        /// <summary>
        /// Builds a SELECT query for a specific table. Needed in the process of copying rows
        /// from the SQL Server database to the SQLite database.
        /// </summary>
        /// <param name="ts">The table schema of the table for which we need the query.</param>
        /// <returns>The SELECT query for the table.</returns>
        private static string BuildSqlServerTableQuery(TableSchema ts)
        {
            var sb = new StringBuilder();
            sb.Append("SELECT ");
            for (int i = 0; i < ts.Columns.Count; i++)
            {
                sb.Append("[" + ts.Columns[i].ColumnName + "]");
                if (i < ts.Columns.Count - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(" FROM " + ts.TableSchemaName + "." + "[" + ts.TableName + "]");
            return sb.ToString();
        }

        /// <summary>
        /// Creates the SQLite database from the schema read from the SQL Server.
        /// </summary>
        /// <param name="sqlitePath">The path to the generated DB file.</param>
        /// <param name="schema">The schema of the SQL server database.</param>
        /// <param name="password">The password to use for encrypting the DB or null if non is needed.</param>
        /// <param name="handler">A handle for progress notifications.</param>
        private static void CreateSQLiteDatabase(string sqlitePath, DatabaseSchema schema, string password, SqlConversionHandler handler, FailedViewDefinitionHandler viewFailureHandler, bool createViews)
        {
            _log.Debug("Creating SQLite database...");

            // Create the SQLite database file
            SQLiteConnection.CreateFile(sqlitePath);

            _log.Debug("SQLite file was created successfully at [" + sqlitePath + "]");

            // Connect to the newly created database
            string sqliteConnString = CreateSQLiteConnectionString(sqlitePath, password);

            // Create all tables in the new database
            Object stateLocker = new Object();
            int tableCount = 0;

            var orderedTables = schema.Tables.OrderBy(obj => obj.TableName).AsParallel();

            Parallel.ForEach(orderedTables, dt =>
            {
                using (var conn = new SQLiteConnection(sqliteConnString))
                {
                    conn.Open();
                    try
                    {
                        AddSQLiteTable(conn, dt);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("AddSQLiteTable failed", ex);
                        throw;
                    }
                    lock (stateLocker)
                    {
                        tableCount++;    
                    }
                    CheckCancelled();
                    handler(false, true, (int)(tableCount * 50.0 / schema.Tables.Count), "Added table " + dt.TableName + " to the SQLite database");

                    _log.Debug("added schema for SQLite table [" + dt.TableName + "]");
                }
            });

            // Create all views in the new database
            int viewCount = 0;
            if (createViews)
            {
                var orderedViews = schema.Views.OrderBy(obj => obj.ViewName).AsParallel();
                Parallel.ForEach(orderedViews, vs =>
                {
                    using (var conn = new SQLiteConnection(sqliteConnString))
                    {
                        conn.Open();
                        try
                        {
                            AddSQLiteView(conn, vs, viewFailureHandler);
                        }
                        catch (Exception ex)
                        {
                            _log.Error("AddSQLiteView failed", ex);
                            throw;
                        }
                    }
                    viewCount++;
                    CheckCancelled();
                    handler(false, true, 50 + (int) (viewCount*50.0/schema.Views.Count), "Added view " + vs.ViewName + " to the SQLite database");

                    _log.Debug("added schema for SQLite view [" + vs.ViewName + "]");
                });
            }

            _log.Debug("finished adding all table/view schemas for SQLite database");
        }

        private static void AddSQLiteView(SQLiteConnection conn, ViewSchema vs, FailedViewDefinitionHandler handler)
        {
            // Prepare a CREATE VIEW DDL statement
            String stmt = vs.ViewSQL;
            _log.Info("\n\n" + stmt + "\n\n");

            // Execute the query in order to actually create the view.
            SQLiteTransaction tx = conn.BeginTransaction();
            try
            {
                var cmd = new SQLiteCommand(stmt, conn, tx);
                cmd.ExecuteNonQuery();

                tx.Commit();
            }
            catch (SQLiteException ex)
            {
                Log.Error("Error in \"AddSQLiteView\"", ex);
                tx.Rollback();

                // Rethrow the exception if it the caller didn't supply a handler.
                if (handler == null) { throw; }
                
                var updated = new ViewSchema();
                updated.ViewName = vs.ViewName;
                updated.ViewSQL = vs.ViewSQL;

                // Ask the user to supply the new view definition SQL statement
                String sql = handler(updated);

                if (sql != null)
                {
                    // Try to re-create the view with the user-supplied view definition SQL
                    updated.ViewSQL = sql;
                    AddSQLiteView(conn, updated, handler);
                }
            }
        }

        /// <summary>
        /// Creates the CREATE TABLE DDL for SQLite and a specific table.
        /// </summary>
        /// <param name="conn">The SQLite connection</param>
        /// <param name="dt">The table schema object for the table to be generated.</param>
        private static void AddSQLiteTable(SQLiteConnection conn, TableSchema dt)
        {
            // Prepare a CREATE TABLE DDL statement
            string stmt = BuildCreateTableQuery(dt);

            _log.Info("\n\n" + stmt + "\n\n");

            // Execute the query in order to actually create the table.
            var cmd = new SQLiteCommand(stmt, conn);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// returns the CREATE TABLE DDL for creating the SQLite table from the specified
        /// table schema object.
        /// </summary>
        /// <param name="ts">The table schema object from which to create the SQL statement.</param>
        /// <returns>CREATE TABLE DDL for the specified table.</returns>
        private static string BuildCreateTableQuery(TableSchema ts)
        {
            var sb = new StringBuilder();

            sb.Append("CREATE TABLE [" + ts.TableName + "] (\n");

            bool pkey = false;
            for (int i = 0; i < ts.Columns.Count; i++)
            {
                ColumnSchema col = ts.Columns[i];
                string cline = BuildColumnStatement(col, ts, ref pkey);
                sb.Append(cline);
                if (i < ts.Columns.Count - 1)
                {
                    sb.Append(",\n");
                }
            }

            // add primary keys...
            if (ts.PrimaryKey != null && ts.PrimaryKey.Count > 0 & !pkey)
            {
                sb.Append(",\n");
                sb.Append("    PRIMARY KEY (");
                for (int i = 0; i < ts.PrimaryKey.Count; i++)
                {
                    sb.Append("[" + ts.PrimaryKey[i] + "]");
                    if (i < ts.PrimaryKey.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
                sb.Append(")\n");
            }
            else
            {
                sb.Append("\n");
            }

            // add foreign keys...
            if (ts.ForeignKeys.Count > 0)
            {
                sb.Append(",\n");
                for (int i = 0; i < ts.ForeignKeys.Count; i++)
                {
                    ForeignKeySchema foreignKey = ts.ForeignKeys[i];
                    string stmt = string.Format("    FOREIGN KEY ([{0}])\n        REFERENCES [{1}]([{2}])", foreignKey.ColumnName, foreignKey.ForeignTableName, foreignKey.ForeignColumnName);

                    sb.Append(stmt);
                    if (i < ts.ForeignKeys.Count - 1)
                    {
                        sb.Append(",\n");
                    }
                }
            }

            sb.Append("\n");
            sb.Append(");\n");

            // Create any relevant indexes
            if (ts.Indexes != null)
            {
                for (int i = 0; i < ts.Indexes.Count; i++)
                {
                    string stmt = BuildCreateIndex(ts.TableName, ts.Indexes[i]);
                    sb.Append(stmt + ";\n");
                }
            }

            string query = sb.ToString();
            return query;
        }

        /// <summary>
        /// Creates a CREATE INDEX DDL for the specified table and index schema.
        /// </summary>
        /// <param name="tableName">The name of the indexed table.</param>
        /// <param name="indexSchema">The schema of the index object</param>
        /// <returns>A CREATE INDEX DDL (SQLite format).</returns>
        private static string BuildCreateIndex(string tableName, IndexSchema indexSchema)
        {
            var sb = new StringBuilder();
            sb.Append("CREATE ");
            if (indexSchema.IsUnique)
            {
                sb.Append("UNIQUE ");
            }
            sb.Append("INDEX [" + tableName + "_" + indexSchema.IndexName + "]\n");
            sb.Append("ON [" + tableName + "]\n");
            sb.Append("(");
            for (int i = 0; i < indexSchema.Columns.Count; i++)
            {
                sb.Append("[" + indexSchema.Columns[i].ColumnName + "]");
                if (!indexSchema.Columns[i].IsAscending)
                {
                    sb.Append(" DESC");
                }
                if (i < indexSchema.Columns.Count - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.Append(")");

            return sb.ToString();
        }

        /// <summary>
        /// Used when creating the CREATE TABLE DDL. Creates a single row
        /// for the specified column.
        /// </summary>
        /// <param name="columnSchema">The column schema</param>
        /// <returns>A single column line to be inserted into the general CREATE TABLE DDL statement</returns>
        private static string BuildColumnStatement(ColumnSchema columnSchema, TableSchema tableSchema, ref bool pkey)
        {
            var sb = new StringBuilder();
            sb.Append("\t[" + columnSchema.ColumnName + "]\t");

            // Special treatment for IDENTITY columns
            if (columnSchema.IsIdentity)
            {
                if (tableSchema.PrimaryKey.Count == 1 && (columnSchema.ColumnType == "tinyint" || columnSchema.ColumnType == "int" || columnSchema.ColumnType == "smallint" || columnSchema.ColumnType == "bigint" || columnSchema.ColumnType == "integer"))
                {
                    sb.Append("integer PRIMARY KEY AUTOINCREMENT");
                    pkey = true;
                }
                else
                {
                    sb.Append("integer");
                }
            }
            else
            {
                sb.Append(columnSchema.ColumnType == "int" ? "integer" : columnSchema.ColumnType);
                if (columnSchema.Length > 0)
                {
                    sb.Append("(" + columnSchema.Length + ")");
                }
            }
            if (!columnSchema.IsNullable)
            {
                sb.Append(" NOT NULL");
            }

            if (columnSchema.IsCaseSensitive.HasValue && !columnSchema.IsCaseSensitive.Value)
            {
                sb.Append(" COLLATE NOCASE");
            }

            string defval = StripParens(columnSchema.DefaultValue);
            defval = DiscardNational(defval);
            _log.Debug("DEFAULT VALUE BEFORE [" + columnSchema.DefaultValue + "] AFTER [" + defval + "]");
            if (defval != string.Empty && defval.ToUpper().Contains("GETDATE"))
            {
                _log.Debug("converted SQL Server GETDATE() to CURRENT_TIMESTAMP for column [" + columnSchema.ColumnName + "]");
                sb.Append(" DEFAULT (CURRENT_TIMESTAMP)");
            }
            else if (defval != string.Empty && IsValidDefaultValue(defval))
            {
                sb.Append(" DEFAULT " + defval);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Discards the national prefix if exists (e.g., N'sometext') which is not
        /// supported in SQLite.
        /// </summary>
        /// <param name="value">The value.</param>
        private static string DiscardNational(string value)
        {
            var rx = new Regex(@"N\'([^\']*)\'");
            Match m = rx.Match(value);
            return m.Success ? m.Groups[1].Value : value;
        }

        /// <summary>
        /// Check if the DEFAULT clause is valid by SQLite standards
        /// </summary>
        private static bool IsValidDefaultValue(string value)
        {
            if (IsSingleQuoted(value))
            {
                return true;
            }

            double testnum;
            return double.TryParse(value, out testnum);
        }

        private static bool IsSingleQuoted(string value)
        {
            value = value.Trim();
            return (value.StartsWith("'") && value.EndsWith("'"));
        }

        /// <summary>
        /// Strip any parentheses from the string.
        /// </summary>
        /// <param name="value">The string to strip</param>
        /// <returns>The stripped string</returns>
        private static string StripParens(string value)
        {
            Regex rx = new Regex(@"\(([^\)]*)\)");
            Match m = rx.Match(value);
            if (!m.Success)
            {
                return value;
            }
            return StripParens(m.Groups[1].Value);
        }
       
        /// <summary>
        /// Convenience method for checking if the conversion progress needs to be cancelled.
        /// </summary>
        public static void CheckCancelled()
        {
            if (_cancelled)
            {
                throw new ApplicationException("User cancelled the conversion");
            }
        }

        /// <summary>
        /// Small validation method to make sure we don't miss anything without getting
        /// an exception.
        /// </summary>
        /// <param name="dataType">The datatype to validate.</param>
        public static void ValidateDataType(string dataType)
        {
            List<String> validTypes = new List<String>
            {
                "bigint", "binary", "bit", "char",
                "date", "datetime", "datetime2", "decimal",
                "float", "image", "int", "money",
                "nchar", "ntext", "numeric", "nvarchar",
                "real", "smalldatetime", "smallint", "smallmoney",
                "sql_variant", "text", "time", "timestamp",
                "tinyint", "uniqueidentifier", "varbinary", "varchar",
                "xml"
            };

            // If the specified type isn't in our list of valid types, it's not valid.
            if (!validTypes.Contains(dataType))
            {
                throw new ApplicationException("Validation failed for data type [" + dataType + "]");
            }
        }

        /// <summary>
        /// Does some necessary adjustments to a value string that appears in a column DEFAULT
        /// clause.
        /// </summary>
        /// <param name="colDefault">The original default value string (as read from SQL Server).</param>
        /// <returns>Adjusted DEFAULT value string (for SQLite)</returns>
        public static string FixDefaultValueString(string colDefault)
        {
            bool replaced = false;
            string res = colDefault.Trim();

            // Find first/last indexes in which to search
            int first = -1;
            int last = -1;
            for (int i = 0; i < res.Length; i++)
            {
                if (res[i] == '\'' && first == -1)
                {
                    first = i;
                }
                if (res[i] == '\'' && first != -1 && i > last)
                {
                    last = i;
                }
            }

            if (first != -1 && last > first)
            {
                return res.Substring(first, last - first + 1);
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < res.Length; i++)
            {
                if (res[i] != '(' && res[i] != ')')
                {
                    sb.Append(res[i]);
                    replaced = true;
                }
            }
            if (replaced)
            {
                return "(" + sb + ")";
            }
            return sb.ToString();
        }

        /// <summary>
        /// More adjustments for the DEFAULT value clause.
        /// </summary>
        /// <param name="val">The value to adjust</param>
        /// <returns>Adjusted DEFAULT value string</returns>
        public static string AdjustDefaultValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val))
            {
                return val;
            }

            Match m = _defaultValueRx.Match(val);
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
            return val;
        }

        /// <summary>
        /// Creates SQLite connection string from the specified DB file path.
        /// </summary>
        /// <param name="sqlitePath">The path to the SQLite database file.</param>
        /// <returns>SQLite connection string</returns>
        private static string CreateSQLiteConnectionString(string sqlitePath, string password)
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder();
            builder.DataSource = sqlitePath;
            if (!String.IsNullOrWhiteSpace(password))
            {
                builder.Password = password;
            }
            builder.PageSize = 4096;
            builder.UseUTF16Encoding = true;
            string connectionString = builder.ConnectionString;
            return connectionString;
        }

        private static void AddTriggersForForeignKeys(string sqlitePath, IEnumerable<TableSchema> schema, string password, SqlConversionHandler handler)
        {
            // Connect to the newly created database
            string sqliteConnString = CreateSQLiteConnectionString(sqlitePath, password);
            using (SQLiteConnection conn = new SQLiteConnection(sqliteConnString))
            {
                conn.Open();
                foreach (TableSchema dt in schema)
                {
                    try
                    {
                        AddTableTriggers(conn, dt);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("AddTableTriggers failed", ex);
                        throw;
                    }
                }
            }
            _log.Debug("finished adding triggers to schema");
        }

        private static void AddTableTriggers(SQLiteConnection conn, TableSchema dt)
        {
            IList<TriggerSchema> triggers = TriggerBuilder.GetForeignKeyTriggers(dt);
            foreach (TriggerSchema trigger in triggers)
            {
                SQLiteCommand cmd = new SQLiteCommand(WriteTriggerSchema(trigger), conn);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Gets a create script for the triggerSchema in sqlite syntax
        /// </summary>
        /// <param name="ts">Trigger to script</param>
        /// <returns>Executable script</returns>
        public static string WriteTriggerSchema(TriggerSchema ts)
        {
            return @"CREATE TRIGGER [" + ts.Name + "] " + ts.Type + " " + ts.Event + " ON [" + ts.Table + "] " + "BEGIN " + ts.Body + " END;";
        }

        private static String TemplateToFilename(String template)
        {
            DateTime current = DateTime.UtcNow;
            String filename = template;

            filename = filename.Replace("%ut", DateTimeToUnixTimestamp(current).ToString(CultureInfo.InvariantCulture));
            filename = filename.Replace("%wt", current.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture));

            return filename;
        }

        public static double DateTimeToUnixTimestamp(DateTime dateTime)
        {
            return (dateTime - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds;
        }
    }

    /// <summary>
    /// This handler is called whenever a progress is made in the conversion process.
    /// </summary>
    /// <param name="done">TRUE indicates that the entire conversion process is finished.</param>
    /// <param name="success">TRUE indicates that the current step finished successfully.</param>
    /// <param name="percent">Progress percent (0-100)</param>
    /// <param name="msg">A message that accompanies the progress.</param>
    public delegate void SqlConversionHandler(bool done, bool success, int percent, string msg);

    /// <summary>
    /// This handler allows the user to change which tables get converted from SQL Server
    /// to SQLite.
    /// </summary>
    /// <param name="schema">The original SQL Server DB schema</param>
    /// <returns>The same schema minus any table we don't want to convert.</returns>
    public delegate List<TableSchema> SqlTableSelectionHandler(List<TableSchema> schema);

    /// <summary>
    /// This handler is called in order to handle the case when copying the SQL Server view SQL
    /// statement is not enough and the user needs to either update the view definition himself
    /// or discard the view definition from the generated SQLite database.
    /// </summary>
    /// <param name="vs">The problematic view definition</param>
    /// <returns>The updated view definition, or NULL in case the view should be discarded</returns>
    public delegate string FailedViewDefinitionHandler(ViewSchema vs);
}
