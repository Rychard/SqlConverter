using System;
using System.ComponentModel;
using Converter.Annotations;

namespace Converter
{
    public class ConversionConfiguration : INotifyPropertyChanged
    {
        private DateTime _dateCreated;
        private String _displayName;
        private String _sqlServerAddress;
        private String _databaseName;
        private Boolean _integratedSecurity;
        private String _user;
        private String _password;
        private String _sqLiteDatabaseFilePath;
        private String _encryptionPassword;
        private Boolean _createTriggersEnforcingForeignKeys;
        private Boolean _tryToCreateViews;

        public DateTime DateCreated
        {
            get { return _dateCreated; }
            set
            {
                if (value.Equals(_dateCreated)) return;
                _dateCreated = value;
                OnPropertyChanged("DateCreated");
            }
        }

        public String DisplayName
        {
            get { return _displayName; }
            set
            {
                if (value == _displayName) return;
                _displayName = value;
                OnPropertyChanged("DisplayName");
            }
        }

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
