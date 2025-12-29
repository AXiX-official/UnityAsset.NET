namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface IKeyframe<T>: IPreDefinedInterface
    where T : notnull
{
    public float time { get; }
    public T value { get; }
    public T inSlope { get; }
    public T outSlope { get; }

    public int? weightedMode { get => null; }

    public T? inWeight { get => default; }

    public T? outWeight { get => default; }
}