using System;
using System.Collections.Generic;
using System.ComponentModel;
using Converter.Annotations;

namespace Converter
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
            get { return _sqlServerAddress; }
            set
            {
                if (value == _sqlServerAddress) return;
                _sqlServerAddress = value;
                OnPropertyChanged("SqlServerAddress");
            }
        }

        public String DatabaseName
        {
            get { return _databaseName; }
            set
            {
                if (value == _databaseName) return;
                _databaseName = value;
                OnPropertyChanged("DatabaseName");
            }
        }

        public Boolean IntegratedSecurity
        {
            get { return _integratedSecurity; }
            set
            {
                if (value.Equals(_integratedSecurity)) return;
                _integratedSecurity = value;
                OnPropertyChanged("IntegratedSecurity");
            }
        }

        public String User
        {
            get { return _user; }
            set
            {
                if (value == _user) return;
                _user = value;
                OnPropertyChanged("User");
            }
        }

        public String Password
        {
            get { return _password; }
            set
            {
                if (value == _password) return;
                _password = value;
                OnPropertyChanged("Password");
            }
        }

        public String SqLiteDatabaseFilePath
        {
            get { return _sqLiteDatabaseFilePath; }
            set
            {
                if (value == _sqLiteDatabaseFilePath) return;
                _sqLiteDatabaseFilePath = value;
                OnPropertyChanged("SqLiteDatabaseFilePath");
            }
        }

        public String EncryptionPassword
        {
            get { return _encryptionPassword; }
            set
            {
                if (value == _encryptionPassword) return;
                _encryptionPassword = value;
                OnPropertyChanged("EncryptionPassword");
            }
        }

        public Boolean CreateTriggersEnforcingForeignKeys
        {
            get { return _createTriggersEnforcingForeignKeys; }
            set
            {
                if (value.Equals(_createTriggersEnforcingForeignKeys)) return;
                _createTriggersEnforcingForeignKeys = value;
                OnPropertyChanged("CreateTriggersEnforcingForeignKeys");
            }
        }

        public Boolean TryToCreateViews
        {
            get { return _tryToCreateViews; }
            set
            {
                if (value.Equals(_tryToCreateViews)) return;
                _tryToCreateViews = value;
                OnPropertyChanged("TryToCreateViews");
            }
        }

        public List<String> SelectedTables
        {
            get { return _selectedTables; }
            set
            {
                if (value.Equals(_selectedTables)) return;
                _selectedTables = value;
                OnPropertyChanged("SelectedTables");
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
            _sqlServerAddress = "";
            _databaseName = "";
            _integratedSecurity = true;
            _user = "";
            _password = "";
            _sqLiteDatabaseFilePath = "";
            _encryptionPassword = "";
            _createTriggersEnforcingForeignKeys = false;
            _tryToCreateViews = false;
            _selectedTables = new List<String>();
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
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
