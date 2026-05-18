using System;
using System.Windows.Input;

namespace DoaneDevTools.ToolWindows.Shared
{
    /// <summary>Standard ICommand implementation for MVVM command binding.</summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);
    }

    /// <summary>Async variant — fire-and-forget with exception surfacing.</summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, System.Threading.Tasks.Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(
            Func<object?, System.Threading.Tasks.Task> execute,
            Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) =>
            !_isExecuting && (_canExecute == null || _canExecute(parameter));

        public async void Execute(object? parameter)
        {
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
