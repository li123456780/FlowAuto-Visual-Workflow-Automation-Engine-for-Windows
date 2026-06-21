namespace FlowAuto.Engine;

public class FlowContext
{
    public Dictionary<string, object> Variables { get; } = new();
    public IntPtr CurrentHwnd { get; set; }
    public CancellationTokenSource? Cts { get; set; }
    public TaskCompletionSource<bool>? PauseTcs { get; set; }
    public FlowLogger Logger { get; }

    // Execution state
    public bool IsPaused { get; set; }
    public bool IsStopping { get; set; }
    public int CurrentNodeIndex { get; set; }

    public FlowContext(FlowLogger logger)
    {
        Logger = logger;
    }

    public void SetHwnd(IntPtr hwnd)
    {
        CurrentHwnd = hwnd;
        Variables["TargetHwnd"] = hwnd;
    }

    public T? Get<T>(string key)
    {
        if (Variables.TryGetValue(key, out var val) && val is T t)
            return t;
        return default;
    }

    public void Set(string key, object value)
    {
        Variables[key] = value;
    }

    /// <summary>
    /// Check if cancellation is requested. Throws if stopped.
    /// </summary>
    public void CheckCancellation()
    {
        if (Cts?.IsCancellationRequested == true)
            throw new OperationCanceledException("Flow execution was stopped.");
    }

    /// <summary>
    /// Wait if paused. Returns when resumed or throws if stopped.
    /// </summary>
    public async Task WaitIfPausedAsync()
    {
        while (IsPaused)
        {
            CheckCancellation();
            if (PauseTcs != null)
                await PauseTcs.Task;
            else
                await Task.Delay(100);
        }
    }
}
