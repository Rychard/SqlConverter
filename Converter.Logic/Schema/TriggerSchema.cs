using System;
using Converter.Logic.Triggers;

namespace Converter.Logic.Schema
{
    public class TriggerSchema
    {
        public String Name { get; set; }
        public TriggerEvent Event { get; set; }
        public TriggerType Type { get; set; }
        public String Body { get; set; }
        public String Table { get; set; }
    }
}
