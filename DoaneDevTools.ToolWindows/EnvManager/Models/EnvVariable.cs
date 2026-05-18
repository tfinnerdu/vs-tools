using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace DoaneDevTools.ToolWindows.EnvManager.Models
{
    /// <summary>
    /// Represents a single key/value environment variable within a profile.
    /// Sensitive keys (matching patterns like *SECRET*, *KEY*, *PASSWORD*,
    /// *TOKEN*, *CONNECTION*) display as "[encrypted]" in the UI.
    /// </summary>
    public class EnvVariable : INotifyPropertyChanged
    {
        // Matches common sensitive key patterns (case-insensitive).
        private static readonly Regex SensitivePattern = new Regex(
            @"SECRET|PASSWORD|TOKEN|CONNECTION|CONN_STR|APIKEY|API_KEY|PRIVATE|CREDENTIALS|CREDENTIAL",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private string _key = string.Empty;
        private string _value = string.Empty;
        private bool _isSecret;

        public string Key
        {
            get => _key;
            set
            {
                if (_key == value) return;
                _key = value ?? string.Empty;
                // Automatically detect whether this key looks sensitive.
                IsSecret = SensitivePattern.IsMatch(_key);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayValue));
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayValue));
            }
        }

        /// <summary>
        /// True when the key name matches a sensitive pattern. Setting this
        /// manually overrides the auto-detection so callers can force a key to
        /// be treated (or not treated) as secret.
        /// </summary>
        public bool IsSecret
        {
            get => _isSecret;
            set
            {
                if (_isSecret == value) return;
                _isSecret = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayValue));
            }
        }

        /// <summary>
        /// The value shown in the DataGrid — masked for secret keys.
        /// </summary>
        public string DisplayValue => IsSecret ? "[encrypted]" : _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public EnvVariable Clone() => new EnvVariable
        {
            Key = Key,
            Value = Value,
            IsSecret = IsSecret
        };

        public override string ToString() => $"{Key}={DisplayValue}";
    }
}
