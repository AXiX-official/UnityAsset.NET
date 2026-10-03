namespace UnityAsset.NET.TypeTreeHelper.Schema;

/// <summary>
/// The interfaces that live in the generated namespace but are written by hand, so generation cannot describe them.
/// They hold no optional members: only generation knows which versions a member is missing from.
/// </summary>
public static class HandWrittenInterfaceSchema
{
    public static readonly IReadOnlyDictionary<string, InterfaceSchema> Entries =
        new Dictionary<string, InterfaceSchema>(StringComparer.Ordinal)
    {
        ["IAnimationCurve`1"] = new("IAnimationCurve`1", false, new InterfaceMemberSchema[]
        {
            new("m_Curve", "IKeyframe<T>[]", false),
            new("m_PostInfinity", "int", false),
            new("m_PreInfinity", "int", false),
            new("m_RotationOrder", "int", false),
        }),
        ["IAnimatorController"] = new("IAnimatorController", true, new InterfaceMemberSchema[]
        {
            new("m_AnimationClips", "PPtr<IAnimationClip>[]", false),
            new("m_TOS", "ValueTuple<uint, string>[]", false),
        }),
        ["IAnimatorOverrideController"] = new("IAnimatorOverrideController", true, new InterfaceMemberSchema[]
        {
            new("m_Clips", "IAnimationClipOverride[]", false),
            new("m_Controller", "PPtr<IRuntimeAnimatorController>", false),
        }),
        ["IKeyframe`1"] = new("IKeyframe`1", false, new InterfaceMemberSchema[]
        {
            new("inSlope", "T", false),
            new("inWeight", "T?", true),
            new("outSlope", "T", false),
            new("outWeight", "T?", true),
            new("time", "float", false),
            new("value", "T", false),
            new("weightedMode", "int?", true),
        }),
        ["IMonoBehaviour"] = new("IMonoBehaviour", true, new InterfaceMemberSchema[]
        {
            new("m_Enabled", "byte", false),
            new("m_GameObject", "PPtr<GameObject>", false),
            new("m_Script", "PPtr<IMonoScript>", false),
        }),
        ["IRuntimeAnimatorController"] = new("IRuntimeAnimatorController", true, Array.Empty<InterfaceMemberSchema>()),
        ["Renderer"] = new("Renderer", false, new InterfaceMemberSchema[]
        {
            new("m_Materials", "PPtr<IMaterial>[]", false),
            new("m_StaticBatchInfo", "IStaticBatchInfo", false),
        }),
    };
}
