namespace UnityAsset.NET.TypeTreeHelper.Diagnostics;

/// <summary>What a diagnostic is about, as far as generation can say.</summary>
public sealed record DiagnosticLocation(string? ClassName = null, string? UnityVersion = null, string? MemberPath = null)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (ClassName is not null)
            parts.Add($"class {ClassName}");
        if (UnityVersion is not null)
            parts.Add($"version {UnityVersion}");
        if (MemberPath is not null)
            parts.Add($"member {MemberPath}");

        return parts.Count == 0 ? "unknown location" : string.Join(", ", parts);
    }
}

/// <summary>One thing generation found wrong, with a stable code so a caller never has to parse the message.</summary>
public sealed record GenerationDiagnostic(string Code, string Message, DiagnosticLocation Location)
{
    public override string ToString() => $"{Code}: {Message} ({Location})";
}

/// <summary>The codes generation reports; stable, so a caller can act on them.</summary>
public static class DiagnosticCodes
{
    /// <summary>A member named m_Name is not a string, so the class cannot carry a name.</summary>
    public const string NamedMemberIsNotString = "CG0001";

    /// <summary>An interface is referenced but generation did not emit it and it is not hand-written.</summary>
    public const string InterfaceNotGenerated = "CG0002";

    /// <summary>An interface has no version to project its members from.</summary>
    public const string EmptySchema = "CG0003";

    /// <summary>Two member names collapse to one C# identifier.</summary>
    public const string MemberNameCollision = "CG0004";

    /// <summary>An interface's declared members are not the union of the members its versions carry.</summary>
    public const string MemberUnionMismatch = "CG0005";

    /// <summary>A member is declared optional but every version carries it, or the other way round.</summary>
    public const string OptionalityMismatch = "CG0006";

    /// <summary>One version's tree carries the same member name more than once.</summary>
    public const string MemberRepeatedInVersion = "CG0007";
}

/// <summary>Collects diagnostics so a run reports everything it found instead of stopping at the first failure.</summary>
public sealed class DiagnosticBag
{
    private readonly List<GenerationDiagnostic> _diagnostics = new();

    public IReadOnlyList<GenerationDiagnostic> Diagnostics => _diagnostics;

    public void Report(GenerationDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public void Report(string code, string message, DiagnosticLocation location)
        => Report(new GenerationDiagnostic(code, message, location));

    /// <summary>Throws one exception carrying everything reported, or returns when there is nothing to report.</summary>
    public void ThrowIfAny()
    {
        if (_diagnostics.Count > 0)
            throw new GenerationDiagnosticsException(_diagnostics);
    }
}

/// <summary>The failures one generation run collected, so a caller sees all of them at once.</summary>
public sealed class GenerationDiagnosticsException : Exception
{
    public GenerationDiagnosticsException(IReadOnlyList<GenerationDiagnostic> diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())))
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<GenerationDiagnostic> Diagnostics { get; }
}
