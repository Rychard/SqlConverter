using System;
using CommandLine;
using CommandLine.Text;

namespace Converter.Standalone
{
    public class Options
    {
        [Option('c', "config", Required = true, HelpText = "Path to configuration file.")]
        public String ConfigFile { get; set; }

        [Option('d', "database", Required = false, HelpText = "Optional database name.  This overrides the value present in the configuration file.")]
        public String DatabaseName { get; set; }

        [Option('l', "log", Required = false, HelpText = "When provided, status messages are written to this file.")]
        public String LogFile { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "When provided, status messages are output to the console.")]
        public Boolean Verbose { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = new HelpText
            {
                Heading = new HeadingInfo("SQL Converter Standalone", "Version 1.0.0.0"),
                Copyright = new CopyrightInfo("Joshua Shearer", 2015),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };
            help.AddPreOptionsLine("Usage: Converter.Standalone -c [path_to_configuration_file]");
            help.AddOptions(this);
            return help;
        }
    }
}
