using System;

namespace Converter.Logic.Schema
{
    /// <summary>
    /// Describes a single view schema
    /// </summary>
    public class ViewSchema
    {
        /// <summary>
        /// Contains the view name
        /// </summary>
        public String ViewName { get; set; }

        /// <summary>
        /// Contains the view SQL statement
        /// </summary>
        public String ViewSQL { get; set; }
    }
}
