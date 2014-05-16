using System.Collections.Generic;

namespace Converter.Logic.Schema
{
    /// <summary>
    /// Contains the entire database schema
    /// </summary>
    public class DatabaseSchema
    {
        public List<TableSchema> Tables = new List<TableSchema>();
        public List<ViewSchema> Views = new List<ViewSchema>();
    }
}
