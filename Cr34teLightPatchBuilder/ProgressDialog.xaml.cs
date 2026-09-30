using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Cr34teLightPatchBuilder;

public partial class ProgressDialog : Window
{
    private int _currentProgress = -1;
    private bool _operationCompleted;

    private ProgressDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        OperationTitleText.Text = title;
        OperationMessageText.Text = message;
        Closing += (_, eventArgs) => eventArgs.Cancel = !_operationCompleted;
    }

    public static T RunWithProgress<T>(
        Window owner,
        string title,
        string message,
        Func<IProgress<int>, T> operation,
        Action<T>? onUiCompleted = null,
        string completionMessage = "Updating the patch view...")
    {
        var dialog = new ProgressDialog(title, message) { Owner = owner };
        T? result = default;
        Exception? failure = null;

        dialog.Loaded += async (_, _) =>
        {
            try
            {
                var progress = new Progress<int>(value => dialog.SetProgress((int)(Math.Clamp(value, 0, 100) * 0.85)));
                result = await Task.Run(() => operation(progress));

                dialog.SetStatus(completionMessage);
                dialog.SetProgress(90);
                await dialog.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                onUiCompleted?.Invoke(result!);
                await dialog.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

                dialog.SetProgress(100);
                dialog._operationCompleted = true;
                dialog.DialogResult = true;
            }
            catch (Exception exception)
            {
                failure = exception;
                dialog._operationCompleted = true;
                dialog.Close();
            }
        };

        dialog.ShowDialog();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }

    private void SetProgress(int value)
    {
        var progress = Math.Clamp(value, 0, 100);
        if (progress <= _currentProgress)
        {
            return;
        }

        _currentProgress = progress;
        OperationProgressBar.Value = progress;
        ProgressPercentText.Text = $"{progress}%";
    }

    private void SetStatus(string message)
    {
        OperationStatusText.Text = message;
    }
}