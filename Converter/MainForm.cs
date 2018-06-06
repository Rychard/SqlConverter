using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Converter.Logic;
using Converter.Logic.Configuration;
using Converter.Logic.Helpers;
using Converter.Logic.Schema;

namespace Converter.WinForms
{
    public partial class MainForm : Form
    {
        private bool _shouldExit;
        private bool _isLoading;
        private readonly ConfigurationManager _manager;

        public MainForm()
        {
            InitializeComponent();
            _manager = new ConfigurationManager();
            _manager.ConfigurationChanged += (sender, args) => UpdateUI();
        }

        private void UpdateUI()
        {
            try
            {
                _isLoading = true;
                ConversionConfiguration config = this._manager.CurrentConfiguration;
                
                var filePathWithReplacedEnvironmentValues = Environment.ExpandEnvironmentVariables(config.SqLiteDatabaseFilePath);

                txtSqlAddress.Text = config.SqlServerAddress;
                txtSQLitePath.Text = filePathWithReplacedEnvironmentValues;
                txtPassword.Text = config.EncryptionPassword;
                txtUserDB.Text = config.User;
                txtPassDB.Text = config.Password;

                if (!String.IsNullOrWhiteSpace(config.DatabaseName))
                {
                    int cboDatabaseIndex;
                    if (cboDatabases.Items.Contains(config.DatabaseName))
                    {
                        cboDatabaseIndex = cboDatabases.Items.IndexOf(config.DatabaseName);
                    }
                    else
                    {
                        cboDatabaseIndex = cboDatabases.Items.Add(config.DatabaseName);
                    }
                    cboDatabases.SelectedIndex = cboDatabaseIndex;
                }
                

                cbxEncrypt.Checked = !(String.IsNullOrWhiteSpace(config.EncryptionPassword));
                cbxTriggers.Checked = config.CreateTriggersEnforcingForeignKeys;
                cbxCreateViews.Checked = config.TryToCreateViews;
                cbxIntegrated.Checked = config.IntegratedSecurity;

                if (config.IntegratedSecurity)
                {
                    lblPassword.Enabled = false;
                    lblUser.Enabled = false;
                    txtPassDB.Enabled = false;
                    txtUserDB.Enabled = false;
                }
                else
                {
                    lblPassword.Enabled = true;
                    lblUser.Enabled = true;
                    txtPassDB.Enabled = true;
                    txtUserDB.Enabled = true;
                }
                _isLoading = false;

                UpdateSensitivity();
            }
            catch (Exception ex)
            {
                SqlServerToSQLite.Log.Error("Error in \"UpdateUI\"", ex);
                // Do nothing.
            }
        }


        private void btnBrowseSQLitePath_Click(object sender, EventArgs e)
        {
            DialogResult result = saveFileDialog1.ShowDialog(this);
            if (result == DialogResult.Cancel) { return; }

            String filePath = saveFileDialog1.FileName;
            _manager.CurrentConfiguration.SqLiteDatabaseFilePath = filePath;
            pbrProgress.Value = 0;
            AddMessage(String.Format("Output file set: {0}", filePath));
        }

        private void cboDatabases_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox control = (ComboBox) sender;

            if (!_isLoading)
            {
                _manager.CurrentConfiguration.DatabaseName = control.SelectedText;
                UpdateSensitivity();
                pbrProgress.Value = 0;
                AddMessage("cboDatabases - SelectedIndexChanged");
            }

            _manager.CurrentConfiguration.DatabaseName = control.Text;
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            var databaseList = DatabaseHelper.GetDatabases(_manager.CurrentConfiguration);
            if (databaseList == null)
            {
                MessageBox.Show("An error occurred while connecting to SQL Server.  Please ensure that you have entered the correct credentials, and try again.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            // Put them in the correct order.
            var databases = databaseList.OrderBy(obj => obj).ToArray();

            // Remove existing items.
            cboDatabases.Items.Clear();

            // Add records, so long as they don't already appear in the list.
            foreach (var database in databases)
            {
                if (!cboDatabases.Items.Contains(database))
                {
                    cboDatabases.Items.Add(database);    
                }
            }

            // If there's at least one entry in the list, select the first item.
            if (cboDatabases.Items.Count > 0)
            {
                cboDatabases.SelectedIndex = 0;
            }
        }

        private void txtSQLitePath_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.SqLiteDatabaseFilePath = txtSQLitePath.Text;
                UpdateSensitivity();
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            UpdateSensitivity();

            String version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Text = "SQL Server To SQLite DB Converter (" + version + ")";
        }

        private void txtSqlAddress_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.SqlServerAddress = txtSqlAddress.Text;
                UpdateSensitivity();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            SqlServerToSQLite.CancelConversion();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (SqlServerToSQLite.IsActive)
            {
                SqlServerToSQLite.CancelConversion();
                _shouldExit = true;
                e.Cancel = true;
            }
            else
            {
                e.Cancel = false;
            }
        }

        private void cbxEncrypt_CheckedChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                // There is no flag for SQLite encryption.
                // The presence of a value in that property implicitly defines the value.
                UpdateSensitivity();
            }
        }

        private void txtUserDB_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.User = txtUserDB.Text;
            }
        }

        private void txtPassDB_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.Password = txtPassDB.Text;
            }
        }

        private void txtPassword_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.EncryptionPassword = txtPassword.Text;
                UpdateSensitivity();
            }
        }

        private void cbxTriggers_CheckedChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.CreateTriggersEnforcingForeignKeys = cbxTriggers.Checked;
            }
        }

        private void cbxCreateViews_CheckedChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.TryToCreateViews = cbxCreateViews.Checked;
            }
        }

        private void ChkIntegratedCheckedChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                _manager.CurrentConfiguration.IntegratedSecurity = cbxIntegrated.Checked;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!EnsureSaveLocationExists())
            {
                MessageBox.Show("Specified save location is in a directory that does not exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ConversionConfiguration config = _manager.CurrentConfiguration;
            string sqlConnString = config.ConnectionString;

            Cursor = Cursors.WaitCursor;
            SqlConversionProgressReportingHandler progressReportingHandler = OnSqlConversionProgressReportingHandler;
            SqlTableSelectionHandler selectionHandlerDefinition = OnSqlTableDefinitionSelectionHandler;
            SqlTableSelectionHandler selectionHandlerRecords = OnSqlTableRecordSelectionHandler;
            FailedViewDefinitionHandler viewFailureHandler = OnFailedViewDefinitionHandler;

            var filePathWithReplacedEnvironmentValues = Environment.ExpandEnvironmentVariables(config.SqLiteDatabaseFilePath);
            SqlServerToSQLite.ConvertSqlServerToSQLiteDatabase(sqlConnString, filePathWithReplacedEnvironmentValues, config.EncryptionPassword, progressReportingHandler, selectionHandlerDefinition, selectionHandlerRecords, viewFailureHandler, config.CreateTriggersEnforcingForeignKeys, config.TryToCreateViews);
        }

        private void SaveLocationDoesNotExist()
        {
            // TODO: Potentially allow the user to specify a different save location.  If done, we will need to pass that information back to the caller so it can act upon the new value.
        }

        private Boolean EnsureSaveLocationExists()
        {
            try
            {
                String filePath = _manager.CurrentConfiguration.SqLiteDatabaseFilePath;
                var filePathWithReplacedEnvironmentValues = Environment.ExpandEnvironmentVariables(filePath);
                String directory = Path.GetDirectoryName(filePathWithReplacedEnvironmentValues);

                // If the location is empty, it can't possibly exist.
                if (String.IsNullOrWhiteSpace(directory)) { return false; }

                if (!Directory.Exists(directory))
                {
                    SaveLocationDoesNotExist();
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private string OnFailedViewDefinitionHandler(ViewSchema vs)
        {
            string updated = null;
            Invoke(new MethodInvoker(() =>
            {
                updated = FailedViewDefinitionHandler(vs);
            }));
            return updated;
        }

        private string FailedViewDefinitionHandler(ViewSchema vs)
        {
            String updated = null;
            var dialog = new ViewFailureDialog();
            dialog.View = vs;
            DialogResult res = dialog.ShowDialog(this);
            if (res == DialogResult.OK)
            {
                updated = dialog.ViewSQL;
            }
            return updated;
        }

        private List<TableSchema> OnSqlTableDefinitionSelectionHandler(List<TableSchema> schema)
        {
            var config = this._manager.CurrentConfiguration;
            Boolean hasExcludedTableDefinitions = (config.ExcludedTableDefinitions.Count > 0);

            if (hasExcludedTableDefinitions)
            {
                return schema.Where(tableSchema => !config.ExcludedTableDefinitions.Contains(tableSchema.TableName)).ToList();
            }

            var updated = new List<TableSchema>();
            Invoke(new MethodInvoker(() =>
            {
                // Allow the user to select which tables to include by showing him the table selection dialog.
                var dlg = new TableSelectionDialog("Select Table Definitions To Include");
                DialogResult res = dlg.ShowTables(schema, this);
                if (res == DialogResult.OK)
                {
                    updated = dlg.IncludedTables;
                }
            }));
                
            List<String> selectedTables = updated.Select(obj => obj.TableName).ToList();
            var excludedTables = schema.Select(obj => obj.TableName).Except(selectedTables).ToList();
            config.ExcludedTableDefinitions = excludedTables;
            return updated;
        }

        private List<TableSchema> OnSqlTableRecordSelectionHandler(List<TableSchema> schema)
        {
            var config = this._manager.CurrentConfiguration;
            Boolean hasExcludedTableRecords = (config.ExcludedTableRecords.Count > 0);

            if (hasExcludedTableRecords)
            {
                return schema.Where(tableSchema => !config.ExcludedTableDefinitions.Contains(tableSchema.TableName)).ToList();
            }

            var updated = new List<TableSchema>();
            Invoke(new MethodInvoker(() =>
            {
                // Allow the user to select which tables to include by showing him the table selection dialog.
                var dlg = new TableSelectionDialog("Select Table Data To Include");
                DialogResult res = dlg.ShowTables(schema, this);
                if (res == DialogResult.OK)
                {
                    updated = dlg.IncludedTables;
                }
            }));

            List<String> selectedTables = updated.Select(obj => obj.TableName).ToList();
            var excludedTables = schema.Select(obj => obj.TableName).Except(selectedTables).ToList();
            config.ExcludedTableRecords = excludedTables;
            return updated;
        }

        private void OnSqlConversionProgressReportingHandler(bool done, bool success, int percent, string msg)
        {
            Invoke(new MethodInvoker(() => SqlConversionHandler(done, success, percent, msg)));
        }

        private void SqlConversionHandler(bool done, bool success, int percent, string msg)
        {
            UpdateSensitivity();
            AddMessage(String.Format("{0}", msg));
            pbrProgress.Value = percent;

            if (!done) { return; }

            btnStart.Enabled = true;
            Cursor = Cursors.Default;
            UpdateSensitivity();

            if (success)
            {
                pbrProgress.Value = 0;
                AddMessage("Conversion Finished.");

                // If a filename is set for an archive, then compress the database.
                var config = _manager.CurrentConfiguration;
                if (!String.IsNullOrWhiteSpace(config.SqLiteDatabaseFilePathCompressed))
                {
                    var contents = new Dictionary<String, String>
                    {
                        {Path.GetFileName(config.SqLiteDatabaseFilePath), config.SqLiteDatabaseFilePath}
                    };

                    ZipHelper.CreateZip(config.SqLiteDatabaseFilePathCompressed, contents);
                }
                MessageBox.Show(this, msg, "Conversion Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                if (!_shouldExit)
                {
                    MessageBox.Show(this, msg, "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    pbrProgress.Value = 0;
                    AddMessage("Conversion Failed!");
                }
                else
                {
                    Application.Exit();
                }
            }
        }

        #region Private Methods
        private void UpdateSensitivity()
        {
            Invoke(new MethodInvoker(UpdateSensitivitySafe));
        }

        private void UpdateSensitivitySafe()
        {
            if (txtSQLitePath.Text.Trim().Length > 0 && (!cbxEncrypt.Checked || txtPassword.Text.Trim().Length > 0))
            {
                btnStart.Enabled = !SqlServerToSQLite.IsActive;
            }
            else
            {
                btnStart.Enabled = false;
            }

            btnSet.Enabled = _manager.CurrentConfiguration.SqlServerAddress.Trim().Length > 0 && !SqlServerToSQLite.IsActive;
            btnCancel.Visible = SqlServerToSQLite.IsActive;
            txtSqlAddress.Enabled = !SqlServerToSQLite.IsActive;
            txtSQLitePath.Enabled = !SqlServerToSQLite.IsActive;
            btnBrowseSQLitePath.Enabled = !SqlServerToSQLite.IsActive;
            cbxEncrypt.Enabled = !SqlServerToSQLite.IsActive;
            cboDatabases.Enabled = !SqlServerToSQLite.IsActive;
            txtPassword.Enabled = cbxEncrypt.Checked && cbxEncrypt.Enabled;
            cbxIntegrated.Enabled = !SqlServerToSQLite.IsActive;
            cbxCreateViews.Enabled = !SqlServerToSQLite.IsActive;
            cbxTriggers.Enabled = !SqlServerToSQLite.IsActive;
            txtPassDB.Enabled = !SqlServerToSQLite.IsActive;
            txtUserDB.Enabled = !SqlServerToSQLite.IsActive;
        }
        #endregion

        private void ToolStripMenuItemNew(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration = new ConversionConfiguration();
        }

        private void ToolStripMenuItemOpen(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            dlg.RestoreDirectory = true; //this opens the last used directory location instead of defaulting to desktop
            dlg.Multiselect = false;

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                String filename = dlg.FileName;
                ConversionConfiguration config;
                Boolean success = SerializationHelper.TryXmlDeserialize(filename, out config);

                if (success)
                {
                    _manager.CurrentConfiguration = config;
                }
                else
                {
                    MessageBox.Show("The selected file was not a valid configuration file for this application.", "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ToolStripMenuItemSave(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.AddExtension = true;
            dlg.DefaultExt = "xml";
            dlg.FileName = "SqlConverter.Configuration.xml";
            dlg.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
            dlg.RestoreDirectory = true; //this opens the last used directory location instead of defaulting to desktop

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                ConversionConfiguration config = _manager.CurrentConfiguration;
                using (var sw = new StreamWriter(dlg.OpenFile()))
                {
                    sw.Write(config.SerializedXml);
                }
            }
        }

        private void ToolStripMenuItemExit(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to exit?", "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        private void AddMessage(String msg)
        {
            String time = DateTime.Now.ToLongTimeString();
            String line = String.Format("[{0}] {1}", time, msg);
            lbMessages.Items.Add(line);
            int visibleItems = lbMessages.ClientSize.Height / lbMessages.ItemHeight;
            lbMessages.TopIndex = Math.Max(lbMessages.Items.Count - visibleItems + 1, 0);
        }
    }
}