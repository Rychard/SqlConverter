using System;

namespace Converter.Logic
{
    public class ConfigurationManager
    {
        private ConversionConfiguration _currentConfiguration;
        public ConversionConfiguration CurrentConfiguration
        {
            get { return this._currentConfiguration; }
            set
            {
                this._currentConfiguration = value;
                
                this._currentConfiguration.PropertyChanged += delegate { this.OnConfigurationChanged(); };
                
                // We should raise this event manually.
                // Assigning a new value to this property, by definition, is a configuration change.
                this.OnConfigurationChanged();
            }
        }

        public event EventHandler ConfigurationChanged;
        protected virtual void OnConfigurationChanged()
        {
            EventHandler handler = this.ConfigurationChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public ConfigurationManager()
        {
            this.CurrentConfiguration = new ConversionConfiguration();
        }
        
    }
}
