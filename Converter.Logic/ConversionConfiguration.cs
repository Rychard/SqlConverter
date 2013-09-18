using System;
using System.Collections.Generic;
using System.ComponentModel;
using Converter.Logic.Annotations;

namespace Converter.Logic
{
    public class ConversionConfiguration : INotifyPropertyChanged
    {
        #region Private Fields
        
        private String _sqlServerAddress;
        private String _databaseName;
        private Boolean _integratedSecurity;
        private String _user;
        private String _password;
        private String _sqLiteDatabaseFilePath;
        private String _encryptionPassword;
        private Boolean _createTriggersEnforcingForeignKeys;
        private Boolean _tryToCreateViews;
        private List<String> _selectedTables;

        #endregion

        #region Serialized Public Properties

        public String SqlServerAddress
        {
            get { return this._sqlServerAddress; }
            set
            {
                if (value == this._sqlServerAddress) return;
                this._sqlServerAddress = value;
                this.OnPropertyChanged("SqlServerAddress");
            }
        }

        public String DatabaseName
        {
            get { return this._databaseName; }
            set
            {
                if (value == this._databaseName) return;
                this._databaseName = value;
                this.OnPropertyChanged("DatabaseName");
            }
        }

        public Boolean IntegratedSecurity
        {
            get { return this._integratedSecurity; }
            set
            {
                if (value.Equals(this._integratedSecurity)) return;
                this._integratedSecurity = value;
                this.OnPropertyChanged("IntegratedSecurity");
            }
        }

        public String User
        {
            get { return this._user; }
            set
            {
                if (value == this._user) return;
                this._user = value;
                this.OnPropertyChanged("User");
            }
        }

        public String Password
        {
            get { return this._password; }
            set
            {
                if (value == this._password) return;
                this._password = value;
                this.OnPropertyChanged("Password");
            }
        }

        public String SqLiteDatabaseFilePath
        {
            get { return this._sqLiteDatabaseFilePath; }
            set
            {
                if (value == this._sqLiteDatabaseFilePath) return;
                this._sqLiteDatabaseFilePath = value;
                this.OnPropertyChanged("SqLiteDatabaseFilePath");
            }
        }

        public String EncryptionPassword
        {
            get { return this._encryptionPassword; }
            set
            {
                if (value == this._encryptionPassword) return;
                this._encryptionPassword = value;
                this.OnPropertyChanged("EncryptionPassword");
            }
        }

        public Boolean CreateTriggersEnforcingForeignKeys
        {
            get { return this._createTriggersEnforcingForeignKeys; }
            set
            {
                if (value.Equals(this._createTriggersEnforcingForeignKeys)) return;
                this._createTriggersEnforcingForeignKeys = value;
                this.OnPropertyChanged("CreateTriggersEnforcingForeignKeys");
            }
        }

        public Boolean TryToCreateViews
        {
            get { return this._tryToCreateViews; }
            set
            {
                if (value.Equals(this._tryToCreateViews)) return;
                this._tryToCreateViews = value;
                this.OnPropertyChanged("TryToCreateViews");
            }
        }

        public List<String> SelectedTables
        {
            get { return this._selectedTables; }
            set
            {
                if (value.Equals(this._selectedTables)) return;
                this._selectedTables = value;
                this.OnPropertyChanged("SelectedTables");
            }
        }

        #endregion

        #region Non-Serialized Public Properties

        public String ConnectionString
        {
            get
            {
                if (this.IntegratedSecurity)
                {
                    return GetSqlServerConnectionString(this.SqlServerAddress, this.DatabaseName);
                }
                else
                {
                    return GetSqlServerConnectionString(this.SqlServerAddress, this.DatabaseName, this.User, this.Password);
                }
            }
        }

        public String SerializedXml
        {
            get
            {
                Boolean success;
                String serializedXml = SerializationHelper.TryXmlSerialize(this, out success);
                if (success)
                {
                    return serializedXml;
                }
                else
                {
                    return null;
                }
            }

        }

        #endregion

        public ConversionConfiguration()
        {
            this._sqlServerAddress = "";
            this._databaseName = "";
            this._integratedSecurity = true;
            this._user = "";
            this._password = "";
            this._sqLiteDatabaseFilePath = "";
            this._encryptionPassword = "";
            this._createTriggersEnforcingForeignKeys = false;
            this._tryToCreateViews = false;
            this._selectedTables = new List<String>();
        }

        private static string GetSqlServerConnectionString(string address, string db)
        {
            string res = @"Data Source=" + address.Trim() + ";Initial Catalog=" + db.Trim() + ";Integrated Security=SSPI;";
            return res;
        }
        private static string GetSqlServerConnectionString(string address, string db, string user, string pass)
        {
            string res = @"Data Source=" + address.Trim() + ";Initial Catalog=" + db.Trim() + ";User ID=" + user.Trim() + ";Password=" + pass.Trim();
            return res;
        }


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
