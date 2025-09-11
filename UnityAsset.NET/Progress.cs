namespace UnityAsset.NET;

public class LoadProgress
{
    public string StatusText { get; }
    public int Total { get; }
    public int Processed { get; }
    public double Percentage => Total == 0 ? 0 : (double)Processed / Total * 100;
    
    public LoadProgress(string statusText, int total, int processed)
    {
        StatusText = statusText;
        Total = total;
        Processed = processed;
    }
}

public class ThrottledProgress : IProgress<LoadProgress>
{
    private readonly IProgress<LoadProgress> _innerProgress;
    private readonly TimeSpan _throttleInterval;
    private DateTime _lastReportTime = DateTime.MinValue;
    private LoadProgress? _latestValue;
    
    public ThrottledProgress(IProgress<LoadProgress> innerProgress, TimeSpan throttleInterval)
    {
        _innerProgress = innerProgress;
        _throttleInterval = throttleInterval;
    }

    public void Report(LoadProgress value)
    {
        _latestValue = value;
        var now = DateTime.UtcNow;

        if (now - _lastReportTime >= _throttleInterval)
        {
            _innerProgress.Report(value);
            _lastReportTime = now;
        }
    }
    
    public void Flush()
    {
        if (_latestValue == null)
            throw new NullReferenceException();
        _innerProgress.Report(_latestValue);
    }
}