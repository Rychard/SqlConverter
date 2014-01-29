using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace Converter.Logic
{
    public class DatabaseHelper
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
                return new List<String>();
            }
        }

    }
}
