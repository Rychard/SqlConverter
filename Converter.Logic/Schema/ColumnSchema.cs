using System;

namespace Converter.Logic.Schema
{
    /// <summary>
    /// Contains the schema of a single DB column.
    /// </summary>
    public class ColumnSchema
    {
        public String ColumnName { get; set; }

        public String ColumnType { get; set; }

        public int Length { get; set; }

        public Boolean IsNullable { get; set; }

        public String DefaultValue { get; set; }

        public Boolean IsIdentity { get; set; }

        public Boolean? IsCaseSensitive { get; set; }

        public ColumnSchema()
        {
            this.IsCaseSensitive = null;
        }
    }
}
