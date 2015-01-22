using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Converter.Logic;
using Converter.Logic.Configuration;
using Converter.Logic.Helpers;
using Converter.Logic.Schema;

namespace Converter.Standalone
{
    class Program
    {
        private static ConversionConfiguration _config;
        private static Options _options;
        private static StreamWriter _logFileStream;

        static void Main(String[] args)
        {
            _options = new Options();
            var result = CommandLine.Parser.Default.ParseArguments(args, _options);

            if (!result)
            {
                AddMessage("Invalid Arguments");
                return;
            }

            String logFilePath = _options.LogFile;
            if (!String.IsNullOrWhiteSpace(logFilePath))
            {
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }
                _logFileStream = new StreamWriter(File.OpenWrite(logFilePath));
            }

            String filename = _options.ConfigFile;
            Boolean success = SerializationHelper.TryXmlDeserialize(filename, out _config);

            if (!success)
            {
                AddMessage("The selected file was not a valid configuration file for this application.");
                return;
            }

            if (!String.IsNullOrWhiteSpace(_options.DatabaseName))
            {
                // Allow user to override database name.
                AddMessage(String.Format("A database name was supplied as an argument.  Configured database will not be used."));
                _config.DatabaseName = _options.DatabaseName;
            }

            AddMessage(String.Format("Converting database: {0}", _config.DatabaseName));

            String sqlConnString = _config.ConnectionString;

            SqlConversionProgressReportingHandler progressReportingHandler = OnSqlConversionProgressReportingHandler;
            SqlTableSelectionHandler selectionHandlerDefinition = OnSqlTableDefinitionSelectionHandler;
            SqlTableSelectionHandler selectionHandlerRecords = OnSqlTableRecordSelectionHandler;
            FailedViewDefinitionHandler viewFailureHandler = OnFailedViewDefinitionHandler;

            var filePathWithReplacedEnvironmentValues = Environment.ExpandEnvironmentVariables(_config.SqLiteDatabaseFilePath);
            var task = SqlServerToSQLite.ConvertSqlServerToSQLiteDatabase(sqlConnString, filePathWithReplacedEnvironmentValues, _config.EncryptionPassword, progressReportingHandler, selectionHandlerDefinition, selectionHandlerRecords, viewFailureHandler, _config.CreateTriggersEnforcingForeignKeys, _config.TryToCreateViews);

            task.Wait();

            if (task.Exception != null)
            {
                AddMessage("An error has occurred.  Details:");
                var exception = task.Exception;

                AddMessage(exception.ToString(), false);

                foreach (var innerException in exception.InnerExceptions)
                {
                    AddMessage(innerException.ToString(), false);
                }
            }

            if (_logFileStream != null)
            {
                _logFileStream.Dispose();    
            }
        }

        private static void OnSqlConversionProgressReportingHandler(bool done, bool success, int percent, string msg)
        {
            SqlConversionHandler(done, success, percent, msg);
        }

        private static List<TableSchema> OnSqlTableDefinitionSelectionHandler(List<TableSchema> schema)
        {
            Boolean hasExcludedTableDefinitions = (_config.ExcludedTableDefinitions.Count > 0);

            if (hasExcludedTableDefinitions)
            {
                return schema.Where(tableSchema => !_config.ExcludedTableDefinitions.Contains(tableSchema.TableName)).ToList();
            }
            return schema;
        }

        private static List<TableSchema> OnSqlTableRecordSelectionHandler(List<TableSchema> schema)
        {
            Boolean hasExcludedTableRecords = (_config.ExcludedTableRecords.Count > 0);

            if (hasExcludedTableRecords)
            {
                return schema.Where(tableSchema => !_config.ExcludedTableRecords.Contains(tableSchema.TableName)).ToList();
            }
            return schema;
        }

        private static string OnFailedViewDefinitionHandler(ViewSchema vs)
        {
            throw new NotImplementedException();
        }

        private static void SqlConversionHandler(bool done, bool success, int percent, string msg)
        {
            AddMessage(String.Format("{0}", msg));

            if (!done)
            {
                return;
            }

            if (success)
            {
                AddMessage("Conversion Finished.");
            }
        }

        private static Boolean EnsureSaveLocationExists()
        {
            try
            {
                String filePath = _config.SqLiteDatabaseFilePath;
                var filePathWithReplacedEnvironmentValues = Environment.ExpandEnvironmentVariables(filePath);
                String directory = Path.GetDirectoryName(filePathWithReplacedEnvironmentValues);

                // If the location is empty, it can't possibly exist.
                if (String.IsNullOrWhiteSpace(directory)) { return false; }

                if (!Directory.Exists(directory))
                {
                    //SaveLocationDoesNotExist();
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private static void AddMessage(String msg, Boolean showTimestamp = true)
        {
            Boolean writeToConsole = _options.Verbose;
            Boolean writeToFile = (_logFileStream != null);
            
            String line;
            if (showTimestamp)
            {
                String time = DateTime.Now.ToLongTimeString();
                line = String.Format("[{0}] {1}", time, msg);
            }
            else
            {
                line = msg;
            }

            if (writeToConsole) { Console.WriteLine(line); }
            if (writeToFile) { _logFileStream.WriteLine(line); }
        }
    }
}
