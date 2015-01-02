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
            _manager.ConfigurationChanged += delegate { UpdateUI(); };
        }

        private void UpdateUI()
        {
            try
            {
                _isLoading = true;
                ConversionConfiguration config = this._manager.CurrentConfiguration;

                txtSqlAddress.Text = config.SqlServerAddress;
                txtSQLitePath.Text = config.SqLiteDatabaseFilePath;
                txtPassword.Text = config.EncryptionPassword;
                txtUserDB.Text = config.User;
                txtPassDB.Text = config.Password;

                int cboDatabaseIndex = cboDatabases.Items.Add(config.DatabaseName);
                cboDatabases.SelectedIndex = cboDatabaseIndex;

                cbxEncrypt.Checked = !(String.IsNullOrWhiteSpace(config.EncryptionPassword));
                cbxTriggers.Checked = config.CreateTriggersEnforcingForeignKeys;
                cbxCreateViews.Checked = config.TryToCreateViews;
                cbxIntegrated.Checked = config.IntegratedSecurity;

                if (config.IntegratedSecurity)
                {
                    lblPassword.Visible = false;
                    lblUser.Visible = false;
                    txtPassDB.Visible = false;
                    txtUserDB.Visible = false;
                }
                else
                {
                    lblPassword.Visible = true;
                    lblUser.Visible = true;
                    txtPassDB.Visible = true;
                    txtUserDB.Visible = true;
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
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            var databases = DatabaseHelper.GetDatabases(_manager.CurrentConfiguration).ToArray();

            foreach (var database in databases)
            {
                cboDatabases.Items.Add(database);
            }

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
            SqlConversionHandler handler = OnSqlConversionHandler;
            SqlTableSelectionHandler selectionHandler = OnSqlTableSelectionHandler;
            FailedViewDefinitionHandler viewFailureHandler = OnFailedViewDefinitionHandler;

            SqlServerToSQLite.ConvertSqlServerToSQLiteDatabase(sqlConnString, config.SqLiteDatabaseFilePath, config.EncryptionPassword, handler, selectionHandler, viewFailureHandler, config.CreateTriggersEnforcingForeignKeys, config.TryToCreateViews);
        }

        private void SaveLocationDoesNotExist()
        {
            // TODO: Potentially allow the user to specify a different save location.  If done, we will need to pass that information back to the caller so it can act upon the new value.
        }

        private Boolean EnsureSaveLocationExists()
        {
            try
            {
                String directory = Path.GetDirectoryName(_manager.CurrentConfiguration.SqLiteDatabaseFilePath);

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
                                         var dlg = new ViewFailureDialog();
                                         dlg.View = vs;
                                         DialogResult res = dlg.ShowDialog(this);
                                         if (res == DialogResult.OK)
                                         {
                                             updated = dlg.ViewSQL;
                                         }
                                         else
                                         {
                                             updated = null;
                                         }
                                     }));
            return updated;
        }

        private List<TableSchema> OnSqlTableSelectionHandler(List<TableSchema> schema)
        {
            var config = this._manager.CurrentConfiguration;

            Boolean hasConfigurationTables = (config.SelectedTables.Count > 0);

            Boolean useSaved = false;
            if (hasConfigurationTables)
            {
                var response = MessageBox.Show("Tables are marked as selected in the configuration file.  Would you like to re-use your selection?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (response == DialogResult.Yes)
                {
                    useSaved = true;
                }
            }

            if (!useSaved)
            {
                List<TableSchema> updated = null;
                Invoke(new MethodInvoker(delegate
                                         {
                                             // Allow the user to select which tables to include by showing him the table selection dialog.
                                             var dlg = new TableSelectionDialog();
                                             DialogResult res = dlg.ShowTables(schema, this);
                                             if (res == DialogResult.OK)
                                             {
                                                 updated = dlg.IncludedTables;
                                             }
                                         }));
                
                List<String> selectedTables = updated.Select(obj => obj.TableName).ToList();
                config.SelectedTables = selectedTables;
                return updated;
            }
            return schema.Where(tableSchema => config.SelectedTables.Contains(tableSchema.TableName)).ToList();
        }

        private void OnSqlConversionHandler(bool done, bool success, int percent, string msg)
        {
            Invoke(new MethodInvoker(delegate
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
                                                     { Path.GetFileName(config.SqLiteDatabaseFilePath), config.SqLiteDatabaseFilePath }
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
                                     }));
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
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
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
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

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
            lbMessages.Items.Add(msg);
            int visibleItems = lbMessages.ClientSize.Height / lbMessages.ItemHeight;
            lbMessages.TopIndex = Math.Max(lbMessages.Items.Count - visibleItems + 1, 0);

        }
    }
}