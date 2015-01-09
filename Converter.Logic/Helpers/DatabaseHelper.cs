using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Converter.Logic.Configuration;

namespace Converter.Logic.Helpers
{
    public static class DatabaseHelper
    {
        public static List<String> GetDatabases(ConversionConfiguration config)
        {
            try
            {
                String connectionString = config.ConnectionString;
                var databases = new List<String>();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Get the names of all DBs in the database server.
                    var query = new SqlCommand(@"SELECT DISTINCT [name] FROM sysdatabases", conn);
                    using (SqlDataReader reader = query.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            databases.Add((String)reader[0]);
                        }
                    }
                }
                return databases;
            }
            catch (Exception ex)
            {
                SqlServerToSQLite.Log.Error("Error in \"GetDatabases\"", ex);
                return null;
            }
        }

    }
}
