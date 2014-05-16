using System;

namespace Converter.Logic.Schema
{
    public class ForeignKeySchema
    {
        public String TableName { get; set; }

        public String ColumnName { get; set; }

        public String ForeignTableName { get; set; }

        public String ForeignColumnName { get; set; }

        public Boolean CascadeOnDelete { get; set; }

        public Boolean IsNullable { get; set; }
    }
}
