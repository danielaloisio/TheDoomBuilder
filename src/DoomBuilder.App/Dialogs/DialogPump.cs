using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// UDB's code asks for dialogs synchronously (it was written for WinForms' modal ShowDialog), but Avalonia dialogs are
/// asynchronous. This runs a nested dispatcher loop until the dialog's task completes, so the caller gets the answer on the
/// same stack and everything else on the UI thread (painting, input) keeps working in the meantime.
/// </summary>
public static class DialogPump
{
    public static T Run<T>(Func<Task<T>> show)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            throw new InvalidOperationException("Dialogs must be shown from the UI thread.");

        var frame = new DispatcherFrame();
        T result = default;
        Exception error = null;

        Task<T> task = show();
        task.ContinueWith(t =>
        {
            if (t.IsFaulted) error = t.Exception.GetBaseException();
            else if (t.IsCompletedSuccessfully) result = t.Result;
            frame.Continue = false;
        }, TaskScheduler.FromCurrentSynchronizationContext());

        Dispatcher.UIThread.PushFrame(frame);

        if (error != null) throw new InvalidOperationException("The dialog failed: " + error.Message, error);
        return result;
    }
}
