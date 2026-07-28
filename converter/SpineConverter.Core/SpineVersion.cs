using System.Text.RegularExpressions;

namespace SpineConverter.Core;

public readonly record struct SpineVersion(int Major, int Minor, int Patch)
{
    private static readonly Regex Pattern = new(
        @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static SpineVersion Parse(string value)
    {
        var match = Pattern.Match(value.Trim());
        if (!match.Success)
            throw new ConversionException($"Invalid Spine version: {value}");

        return new SpineVersion(
            int.Parse(match.Groups["major"].Value),
            int.Parse(match.Groups["minor"].Value),
            match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0);
    }

    public bool IsLine(int major, int minor) => Major == major && Minor == minor;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
