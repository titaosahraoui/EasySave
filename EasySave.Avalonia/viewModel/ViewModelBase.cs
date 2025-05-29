using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BackupApp.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<string> AlertRequested;
        public event EventHandler<Exception> ErrorOccurred;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            ExecuteOnUI(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }

        protected void ShowAlert(string message) => AlertRequested?.Invoke(this, message);
        protected void ShowError(Exception ex) => ErrorOccurred?.Invoke(this, ex);
        protected void ShowError(string message, Exception ex = null) =>
            ErrorOccurred?.Invoke(this, ex != null ? new Exception(message, ex) : new Exception(message));

        protected void ExecuteOnUI(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        // RelayCommand (sync, no parameter)
        public class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public event EventHandler CanExecuteChanged;

            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
            public void Execute(object parameter) => _execute();
            public void RaiseCanExecuteChanged() =>
                Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }

        // RelayCommand<T> (sync, with parameter)
        public class RelayCommand<T> : ICommand
        {
            private readonly Action<T> _execute;
            private readonly Func<T, bool> _canExecute;

            public event EventHandler CanExecuteChanged;

            public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                try
                {
                    return _canExecute?.Invoke((T)parameter) ?? true;
                }
                catch
                {
                    return false;
                }
            }

            public void Execute(object parameter)
            {
                try
                {
                    if (parameter is T typedParam)
                    {
                        _execute(typedParam);
                    }
                    else if (parameter == null && default(T) == null)
                    {
                        _execute(default);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid command parameter type for {typeof(T).Name}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RelayCommand<{typeof(T).Name}> execution failed: {ex.Message}");
                }
            }

            public void RaiseCanExecuteChanged() =>
                Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }

        // AsyncRelayCommand
        public class AsyncRelayCommand : ICommand
        {
            private readonly Func<Task> _execute;
            private readonly Func<bool> _canExecute;
            private bool _isExecuting;

            public event EventHandler CanExecuteChanged;

            public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

            public async void Execute(object parameter)
            {
                if (!CanExecute(parameter)) return;

                _isExecuting = true;
                RaiseCanExecuteChanged();

                try
                {
                    await _execute();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Async command failed: {ex.Message}");
                    // Optionally trigger global error event
                    (parameter as ViewModelBase)?.ShowError(ex);
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged();
                }
            }

            public void RaiseCanExecuteChanged() =>
                Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}
