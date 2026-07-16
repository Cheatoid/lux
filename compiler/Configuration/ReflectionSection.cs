namespace Lux.Configuration;

/// <summary>
/// The <c>[reflection]</c> section. Controls emission of runtime reflection metadata — a global
/// registry (<c>_G.__lux_reflect</c>) describing the program's types, populated by generated code
/// and read at runtime through the <c>reflect</c> library.
/// </summary>
public sealed class ReflectionSection
{
    /// <summary>How much metadata to emit. See <see cref="ReflectionMode"/>.</summary>
    public ReflectionMode Mode { get; set; } = ReflectionMode.None;

    internal void Merge(ReflectionSection section)
    {
        Mode = Config.MergeVal(Mode, section.Mode, ReflectionMode.None);
    }
}
