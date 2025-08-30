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