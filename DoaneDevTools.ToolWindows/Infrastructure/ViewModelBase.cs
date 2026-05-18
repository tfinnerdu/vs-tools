using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DoaneDevTools.ToolWindows.Infrastructure
{
    /// <summary>
    /// Base class for all ViewModels. Provides a strongly-typed
    /// <see cref="SetProperty{T}"/> helper that only raises
    /// <see cref="PropertyChanged"/> when the value actually changes.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
