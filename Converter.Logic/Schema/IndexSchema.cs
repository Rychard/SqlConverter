using System;
using System.Collections.Generic;

namespace Converter.Logic.Schema
{
    public class IndexSchema
    {
        public String IndexName { get; set; }

        public Boolean IsUnique { get; set; }

        public List<IndexColumn> Columns { get; set; }
    }
}
