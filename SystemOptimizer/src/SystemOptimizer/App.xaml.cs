using System.Windows;
using System.Windows.Threading;
using SystemOptimizer.Services;

namespace SystemOptimizer;

public partial class App : Application
{
    public App()
    {
        // Last-resort handler so an unexpected error logs and shows a message
        // instead of silently killing the app mid-operation.
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error($"Unhandled UI exception: {e.Exception}");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
            $"Details were written to:\n{Logger.LogFile}",
            "System Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
