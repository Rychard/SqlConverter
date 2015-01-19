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
        private static Boolean _running;

        static void Main(String[] args)
        {
            var options = new Options();
            var result = CommandLine.Parser.Default.ParseArguments(args, options);

            if (!result)
            {
                AddMessage("Invalid Configuration File");
                return;
            }

            String filename = options.ConfigFile;
            Boolean success = SerializationHelper.TryXmlDeserialize(filename, out _config);

            if (!success)
            {
                AddMessage("The selected file was not a valid configuration file for this application.");
                return;
            }

            _running = true;

            String sqlConnString = _config.ConnectionString;

            SqlConversionProgressReportingHandler progressReportingHandler = OnSqlConversionProgressReportingHandler;
            SqlTableSelectionHandler selectionHandlerDefinition = OnSqlTableDefinitionSelectionHandler;
            SqlTableSelectionHandler selectionHandlerRecords = OnSqlTableRecordSelectionHandler;
            FailedViewDefinitionHandler viewFailureHandler = OnFailedViewDefinitionHandler;

            var filePathWithReplacedEnvironmentValues = Environment.ExpandEnvironmentVariables(_config.SqLiteDatabaseFilePath);
            var task = SqlServerToSQLite.ConvertSqlServerToSQLiteDatabase(sqlConnString, filePathWithReplacedEnvironmentValues, _config.EncryptionPassword, progressReportingHandler, selectionHandlerDefinition, selectionHandlerRecords, viewFailureHandler, _config.CreateTriggersEnforcingForeignKeys, _config.TryToCreateViews);

            task.Wait();
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
                return schema.Where(tableSchema => !_config.ExcludedTableDefinitions.Contains(tableSchema.TableName)).ToList();
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
                _running = false;
                return;
            }

            if (success)
            {
                AddMessage("Conversion Finished.");

                // TODO: implement archive generation.
                //// If a filename is set for an archive, then compress the database.
                //var config = _manager.CurrentConfiguration;
                //if (!String.IsNullOrWhiteSpace(config.SqLiteDatabaseFilePathCompressed))
                //{
                //    var contents = new Dictionary<String, String>
                //    {
                //        {Path.GetFileName(config.SqLiteDatabaseFilePath), config.SqLiteDatabaseFilePath}
                //    };

                //    ZipHelper.CreateZip(config.SqLiteDatabaseFilePathCompressed, contents);
                //}

                //MessageBox.Show(this, msg, "Conversion Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            //else
            //{
            //    if (!_shouldExit)
            //    {
            //        MessageBox.Show(this, msg, "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        pbrProgress.Value = 0;
            //        AddMessage("Conversion Failed!");
            //    }
            //    else
            //    {
            //        Application.Exit();
            //    }
            //}
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

        private static void AddMessage(String msg)
        {
            String time = DateTime.Now.ToLongTimeString();
            String line = String.Format("[{0}] {1}", time, msg);
            Console.WriteLine(line);
        }
    }
}
