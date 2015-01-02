using System;

namespace Converter.Logic.Configuration
{
    public class ConfigurationManager
    {
        private ConversionConfiguration _currentConfiguration;
        public ConversionConfiguration CurrentConfiguration
        {
            get { return _currentConfiguration; }
            set
            {
                _currentConfiguration = value;
                _currentConfiguration.PropertyChanged += (sender, args) => OnConfigurationChanged();
                
                // We should raise this event manually.
                // Assigning a new value to this property, by definition, is a configuration change.
                this.OnConfigurationChanged();
            }
        }

        public event EventHandler ConfigurationChanged;
        protected virtual void OnConfigurationChanged()
        {
            EventHandler handler = ConfigurationChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public ConfigurationManager()
        {
            CurrentConfiguration = new ConversionConfiguration();
        }
    }
}
