using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Converter.Logic
{
    public class DatabaseHelper
    {
        public static List<String> GetDatabases(ConversionConfiguration config)
        {
            try
            {
                string connectionString = config.ConnectionString;
                List<String> databases = new List<String>();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Get the names of all DBs in the database server.
                    SqlCommand query = new SqlCommand(@"select distinct [name] from sysdatabases", conn);
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
