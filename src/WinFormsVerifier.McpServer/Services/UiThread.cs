using System.Collections.Concurrent;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Services;

public sealed class UiThread : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private int _consecutiveTimeouts;
    private bool _disposed;

    public UiThread()
    {
        _thread = new Thread(Pump)
        {
            IsBackground = true,
            Name = "UIA-STA"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Pump()
    {
        try
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                try
                {
                    work();
                }
                catch
                {
                    // Exception is captured by TaskCompletionSource inside RunAsync
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Thread shutting down
        }
    }

    /// <summary>
    /// Executes a UIA operation on the dedicated STA thread with a hard timeout and cancellation support.
    /// </summary>
    public async Task<T> RunAsync<T>(Func<T> work, TimeSpan timeout, CancellationToken ct = default)
    {
        if (_disposed)
        {
            throw new ToolException(ErrorCode.Internal, "UiThread đã bị dispose.");
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        void WorkItem()
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(ct);
                    return;
                }

                var result = work();
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        try
        {
            _queue.Add(WorkItem, ct);
        }
        catch (OperationCanceledException)
        {
            throw new ToolException(ErrorCode.Timeout, "Thao tác bị hủy trước khi đưa vào hàng đợi UI.");
        }
        catch (InvalidOperationException)
        {
            throw new ToolException(ErrorCode.Internal, "Hàng đợi UiThread đã dừng tiếp nhận công việc.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var delayTask = Task.Delay(Timeout.Infinite, cts.Token);
        var completedTask = await Task.WhenAny(tcs.Task, delayTask);

        if (completedTask != tcs.Task)
        {
            var timeouts = Interlocked.Increment(ref _consecutiveTimeouts);
            if (timeouts >= 2)
            {
                throw new ToolException(
                    ErrorCode.Timeout,
                    $"Thao tác vượt quá thời gian chờ {timeout.TotalSeconds:0.#}s (lần thứ {timeouts} liên tiếp). " +
                    "Session UI có thể đã bị tắc nghẽn hoặc hỏng COM. Hãy gọi 'wf_close_app' rồi attach lại.",
                    "Gọi wf_close_app hoặc kill process đích nếu ứng dụng bị treo.");
            }

            throw new ToolException(
                ErrorCode.Timeout,
                $"Thao tác trên UI vượt quá thời gian chờ {timeout.TotalSeconds:0.#}s.",
                "Ứng dụng có thể đang bận xử lý hoặc có hộp thoại chặn. Thử gọi wf_wait_idle hoặc kiểm tra modal dialog.");
        }

        // Reset poison counter on success
        Interlocked.Exchange(ref _consecutiveTimeouts, 0);
        return await tcs.Task;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _queue.CompleteAdding();
            _thread.Join(2000);
            _queue.Dispose();
        }
        catch
        {
            // Ignore during shutdown
        }
    }
}
