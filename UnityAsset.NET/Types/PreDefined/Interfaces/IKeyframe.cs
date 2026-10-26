namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public interface IKeyframe<T>: IPreDefinedInterface
    where T : notnull
{
    public float time { get; set; }
    public T value { get; set; }
    public T inSlope { get; set; }
    public T outSlope { get; set; }

    public int? weightedMode { get => null; }

    public T? inWeight { get => default; }

    public T? outWeight { get => default; }
}