using System;
using System.Collections.Generic;
using System.ComponentModel;
using Converter.Logic.Annotations;
using Converter.Logic.Helpers;

namespace Converter.Logic.Configuration
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
        private String _sqLiteDatabaseFilePathCompressed;
        private String _encryptionPassword;
        private Boolean _createTriggersEnforcingForeignKeys;
        private Boolean _tryToCreateViews;
        private List<String> _excludedTableDefinitions;
        private List<String> _excludedTableRecords;

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

        public String SqLiteDatabaseFilePathCompressed
        {
            get { return this._sqLiteDatabaseFilePathCompressed; }
            set
            {
                if (value == this._sqLiteDatabaseFilePathCompressed) return;
                this._sqLiteDatabaseFilePathCompressed = value;
                this.OnPropertyChanged("SqLiteDatabaseFilePathCompressed");
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

        public List<String> ExcludedTableDefinitions
        {
            get { return _excludedTableDefinitions; }
            set
            {
                if (Equals(value, _excludedTableDefinitions)) return;
                _excludedTableDefinitions = value;
                OnPropertyChanged("ExcludedTableDefinitions");
            }
        }

        public List<String> ExcludedTableRecords
        {
            get { return _excludedTableRecords; }
            set
            {
                if (Equals(value, _excludedTableRecords)) return;
                _excludedTableRecords = value;
                OnPropertyChanged("ExcludedTableRecords");
            }
        }

        #endregion

        #region Non-Serialized Public Properties

        public String ConnectionString
        {
            get
            {
                if (IntegratedSecurity)
                {
                    return GetSqlServerConnectionString(SqlServerAddress, DatabaseName);
                }
                return GetSqlServerConnectionString(SqlServerAddress, DatabaseName, User, Password);
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
                return null;
            }

        }

        #endregion

        public ConversionConfiguration()
        {
            _sqlServerAddress = "";
            _databaseName = "";
            _integratedSecurity = true;
            _user = "";
            _password = "";
            _sqLiteDatabaseFilePath = "";
            _sqLiteDatabaseFilePathCompressed = "";
            _encryptionPassword = "";
            _createTriggersEnforcingForeignKeys = false;
            _tryToCreateViews = false;
            _excludedTableDefinitions = new List<String>();
            _excludedTableRecords = new List<String>();
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
