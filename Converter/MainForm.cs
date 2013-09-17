using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Data.SqlClient;
using DbAccess;

namespace Converter
{
    public partial class MainForm : Form
    {
        private bool _shouldExit;
        private ConfigurationManager _manager;

        public MainForm()
        {
            InitializeComponent();
            _manager = new ConfigurationManager();
            _manager.ConfigurationChanged += delegate { this.UpdateUI(); };
        }

        private void UpdateUI()
        {
            ConversionConfiguration config = _manager.CurrentConfiguration;

            txtSqlAddress.Text = config.SqlServerAddress;
            txtSQLitePath.Text = config.SqLiteDatabaseFilePath;
            txtPassword.Text = config.EncryptionPassword;
            txtUserDB.Text = config.User;
            txtPassDB.Text = config.Password;

            cboDatabases.SelectedText = config.DatabaseName;

            cbxEncrypt.Checked = !(String.IsNullOrWhiteSpace(config.EncryptionPassword));
            cbxTriggers.Checked = config.CreateTriggersEnforcingForeignKeys;
            cbxCreateViews.Checked = config.TryToCreateViews;

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
        }


        private void btnBrowseSQLitePath_Click(object sender, EventArgs e)
        {
            DialogResult res = saveFileDialog1.ShowDialog(this);
            if (res == DialogResult.Cancel)
            {
                return;
            }

            string fpath = saveFileDialog1.FileName;
            _manager.CurrentConfiguration.SqLiteDatabaseFilePath = fpath;
            pbrProgress.Value = 0;
            lbMessages.Items.Add(String.Format("Output file set: {0}", fpath));
        }

        private void cboDatabases_SelectedIndexChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.DatabaseName = cboDatabases.SelectedText;
            UpdateSensitivity();
            pbrProgress.Value = 0;
            lbMessages.Items.Add("cboDatabases - SelectedIndexChanged");
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            try
            {
                
                ConversionConfiguration config = _manager.CurrentConfiguration;
                string connectionString = config.ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Get the names of all DBs in the database server.
                    SqlCommand query = new SqlCommand(@"select distinct [name] from sysdatabases", conn);
                    using (SqlDataReader reader = query.ExecuteReader())
                    {
                        cboDatabases.Items.Clear();
                        while (reader.Read())
                        {
                            cboDatabases.Items.Add((string)reader[0]);
                        }
                        if (cboDatabases.Items.Count > 0)
                        {
                            cboDatabases.SelectedIndex = 0;
                        }
                    }
                }

                cboDatabases.Enabled = true;

                pbrProgress.Value = 0;
                lbMessages.Items.Add(String.Format("Connected to SQL Server ({0})", config.SqlServerAddress));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Failed To Connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void txtSQLitePath_TextChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.SqLiteDatabaseFilePath = txtSQLitePath.Text;
            UpdateSensitivity();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            UpdateSensitivity();

            String version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            this.Text = "SQL Server To SQLite DB Converter (" + version + ")";
        }

        private void txtSqlAddress_TextChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.SqlServerAddress = txtSqlAddress.Text;
            UpdateSensitivity();
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
            // There is no flag for SQLite encryption.
            // The presence of a value in that property implicitly defines the value.
            UpdateSensitivity();
        }

        private void txtUserDB_TextChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.User = txtUserDB.Text;
        }

        private void txtPassDB_TextChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.Password = txtPassDB.Text;
        }

        private void txtPassword_TextChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.EncryptionPassword = txtPassword.Text;
            UpdateSensitivity();
        }

        private void cbxTriggers_CheckedChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.CreateTriggersEnforcingForeignKeys = cbxTriggers.Checked;
        }

        private void cbxCreateViews_CheckedChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.TryToCreateViews = cbxCreateViews.Checked;
        }

        private void ChkIntegratedCheckedChanged(object sender, EventArgs e)
        {
            _manager.CurrentConfiguration.IntegratedSecurity = cbxIntegrated.Checked;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            ConversionConfiguration config = _manager.CurrentConfiguration;
            string sqlConnString = config.ConnectionString;

            this.Cursor = Cursors.WaitCursor;
            SqlConversionHandler handler = this.OnSqlConversionHandler;
            SqlTableSelectionHandler selectionHandler = this.OnSqlTableSelectionHandler;
            FailedViewDefinitionHandler viewFailureHandler = this.OnFailedViewDefinitionHandler;

            SqlServerToSQLite.ConvertSqlServerToSQLiteDatabase(sqlConnString, config.SqLiteDatabaseFilePath, config.EncryptionPassword, handler, selectionHandler, viewFailureHandler, config.CreateTriggersEnforcingForeignKeys, config.TryToCreateViews);
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
            return updated;
        }

        private void OnSqlConversionHandler(bool done, bool success, int percent, string msg)
        {
            Invoke(new MethodInvoker(delegate
                                     {
                                         this.UpdateSensitivity();
                                         this.lbMessages.Items.Add(String.Format("{0}", msg));
                                         this.pbrProgress.Value = percent;

                                         if (!done)
                                         {
                                             return;
                                         }

                                         this.btnStart.Enabled = true;
                                         this.Cursor = Cursors.Default;
                                         this.UpdateSensitivity();

                                         if (success)
                                         {
                                             MessageBox.Show(this, msg, "Conversion Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                             this.pbrProgress.Value = 0;
                                             this.lbMessages.Items.Add("Conversion Finished.");
                                         }
                                         else
                                         {
                                             if (!this._shouldExit)
                                             {
                                                 MessageBox.Show(this, msg, "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                 this.pbrProgress.Value = 0;
                                                 this.lbMessages.Items.Add("Conversion Failed!");
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
            if (txtSQLitePath.Text.Trim().Length > 0 && cboDatabases.Enabled && (!cbxEncrypt.Checked || txtPassword.Text.Trim().Length > 0))
            {
                btnStart.Enabled = true && !SqlServerToSQLite.IsActive;
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
            cboDatabases.Enabled = cboDatabases.Items.Count > 0 && !SqlServerToSQLite.IsActive;
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
                ConversionConfiguration config = null;
                Boolean success = SerializationHelper.TryXmlDeserialize(filename, out config);

                if (success)
                {
                    _manager.CurrentConfiguration = config;
                    this.UpdateUI();
                }
                else
                {
                    throw new Exception("File couldn't be opened.");
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
                var sw = new StreamWriter(dlg.OpenFile());
                sw.Write(config.SerializedXml);
                sw.Flush();
                sw.Close();
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
    }
}