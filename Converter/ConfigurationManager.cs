using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Converter
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
                _currentConfiguration.PropertyChanged += delegate { this.OnConfigurationChanged(); };
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
            CurrentConfiguration = new ConversionConfiguration();
        }
        
    }
}
