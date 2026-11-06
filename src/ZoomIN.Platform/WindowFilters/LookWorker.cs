using Microsoft.Extensions.Logging;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// Takes the brightness looks of the filter per window on its own thread (ADR-0021). Each look
/// waits for a DWM frame; on the tracker thread that delayed the events of menus and moved
/// windows by up to 75 ms. One batch at a time; when it is done, <c>onDone</c> wakes the tracker.
/// </summary>
internal sealed class LookWorker : IDisposable
{
    private readonly ILogger _log;
    private readonly Action _onDone;
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Lock _gate = new();
    private LookRequest[]? _batch;
    private LookResult[] _results = [];
    private volatile bool _stop;

    public LookWorker(ILogger log, Action onDone)
    {
        _log = log;
        _onDone = onDone;
        _thread = new Thread(Run) { IsBackground = true, Name = "ZoomIN Window Looks" };
        _thread.Start();
    }

    /// <summary>A batch is being taken; its results are not collected yet.</summary>
    public bool Busy
    {
        get
        {
            lock (_gate)
            {
                return _batch is not null;
            }
        }
    }

    /// <summary>Starts the looks; false (nothing happens) while the last batch is still busy.</summary>
    public bool TryStart(List<LookRequest> requests)
    {
        lock (_gate)
        {
            if (_batch is not null || requests.Count == 0)
            {
                return false;
            }

            _batch = [.. requests];
        }

        _wake.Set();
        return true;
    }

    /// <summary>The results of the finished batch (empty if none); the worker is free again.</summary>
    public LookResult[] TakeResults()
    {
        lock (_gate)
        {
            var results = _results;
            _results = [];
            if (results.Length > 0)
            {
                _batch = null;
            }

            return results;
        }
    }

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
        _thread.Join(TimeSpan.FromSeconds(2));
        _wake.Dispose();
    }

    private void Run()
    {
        using var sampler = CreateSampler();
        while (true)
        {
            _wake.WaitOne();
            if (_stop)
            {
                return;
            }

            LookRequest[]? batch;
            lock (_gate)
            {
                batch = _batch;
            }

            if (batch is null)
            {
                continue;
            }

            var results = new LookResult[batch.Length];
            for (var i = 0; i < batch.Length; i++)
            {
                results[i] = batch[i].Take(sampler);
            }

            lock (_gate)
            {
                _results = results;
            }

            _onDone();
        }
    }

    private ScreenSampler? CreateSampler()
    {
        try
        {
            return new ScreenSampler();
        }
        catch (InvalidOperationException ex)
        {
            _log.WindowSamplerFailed(ex);
            return null;
        }
    }
}
