using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace LiveSplit.Services;

/// <summary>Helpers to run async Avalonia work from the synchronous APIs of LiveSplit.Core.</summary>
public static class UiThread
{
    public static bool IsUiThread => Dispatcher.UIThread.CheckAccess();

    /// <summary>
    /// Runs <paramref name="work"/> on the UI thread and waits for it, pumping a nested dispatcher loop while the task
    /// is pending (the UI stays responsive, like a WinForms ShowDialog). Safe to call from any thread.
    /// </summary>
    public static T RunSync<T>(Func<Task<T>> work)
    {
        if (!IsUiThread)
        {
            return Dispatcher.UIThread.Invoke(() => RunSync(work));
        }

        Task<T> task = work();
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(
                _ => Dispatcher.UIThread.Post(() => frame.Continue = false),
                TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame(frame);
        }

        return task.GetAwaiter().GetResult();
    }

    public static void RunSync(Func<Task> work)
    {
        RunSync(async () =>
        {
            await work();
            return 0;
        });
    }

    /// <summary>Runs an action on the UI thread, inline when already there.</summary>
    public static void Invoke(Action action)
    {
        if (IsUiThread)
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Invoke(action);
        }
    }

    public static void Post(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }
}
