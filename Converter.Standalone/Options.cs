using System;
using CommandLine;

namespace Converter.Standalone
{
    public class Options
    {
        [Option('c', "config", Required = true, HelpText = "Path to configuration file.")]
        public String ConfigFile { get; set; }
    }
}
