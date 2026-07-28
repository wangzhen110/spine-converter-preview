namespace SpineConverter.Core;

public sealed record Spine38BinaryInfo(
    string? Hash,
    SpineVersion Version,
    float X,
    float Y,
    float Width,
    float Height,
    bool HasNonessentialData,
    float? Fps,
    string? ImagesPath,
    string? AudioPath,
    int SharedStringCount,
    int HeaderByteCount,
    int FileByteCount);

public static class Spine38BinaryInspector
{
    private const int MaximumSharedStrings = 1_000_000;

    public static Spine38BinaryInfo Inspect(ReadOnlyMemory<byte> data)
    {
        var input = new SpineBinaryInput(data);
        var hash = input.ReadString();
        var versionText = input.ReadString()
            ?? throw new ConversionException("Spine binary is missing its version string.");
        var version = SpineVersion.Parse(versionText);
        if (!version.IsLine(3, 8))
            throw new ConversionException($"Expected a Spine 3.8 binary, got {version}.");

        var x = input.ReadSingle();
        var y = input.ReadSingle();
        var width = input.ReadSingle();
        var height = input.ReadSingle();
        var nonessential = input.ReadBoolean();
        float? fps = null;
        string? images = null;
        string? audio = null;
        if (nonessential)
        {
            fps = input.ReadSingle();
            images = input.ReadString();
            audio = input.ReadString();
        }

        var stringCount = input.ReadVarInt(true);
        if (stringCount is < 0 or > MaximumSharedStrings)
            throw new ConversionException($"Invalid shared string count: {stringCount}.");
        for (var index = 0; index < stringCount; index++)
            _ = input.ReadString() ?? throw new ConversionException($"Shared string {index} is null.");

        return new Spine38BinaryInfo(
            hash, version, x, y, width, height, nonessential, fps, images, audio,
            stringCount, input.Position, data.Length);
    }
}
