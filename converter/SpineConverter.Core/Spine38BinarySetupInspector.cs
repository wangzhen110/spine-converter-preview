namespace SpineConverter.Core;

public sealed record Spine38SetupInfo(
    SpineVersion Version,
    int SharedStringCount,
    int BoneCount,
    int SlotCount,
    int IkConstraintCount,
    int TransformConstraintCount,
    int PathConstraintCount,
    bool HasNonessentialData,
    int SkinSectionOffset,
    int FileByteCount);

public static class Spine38BinarySetupInspector
{
    private const int MaximumCollectionCount = 1_000_000;

    public static Spine38SetupInfo Inspect(ReadOnlyMemory<byte> data)
    {
        var input = new SpineBinaryInput(data);
        _ = input.ReadString();
        var version = SpineVersion.Parse(input.ReadString()
            ?? throw new ConversionException("Spine binary is missing its version string."));
        if (!version.IsLine(3, 8))
            throw new ConversionException($"Expected a Spine 3.8 binary, got {version}.");

        input.Skip(16);
        var nonessential = input.ReadBoolean();
        if (nonessential)
        {
            input.Skip(4);
            _ = input.ReadString();
            _ = input.ReadString();
        }

        var sharedStrings = ReadCount(input, "shared strings");
        for (var i = 0; i < sharedStrings; i++)
            _ = input.ReadString() ?? throw new ConversionException($"Shared string {i} is null.");

        var bones = ReadCount(input, "bones");
        for (var i = 0; i < bones; i++)
        {
            _ = input.ReadString() ?? throw new ConversionException($"Bone {i} has no name.");
            if (i > 0) ValidateIndex(input.ReadVarInt(true), i, $"bone {i} parent");
            input.Skip(8 * 4);
            _ = input.ReadVarInt(true); // transform mode
            _ = input.ReadBoolean();
            if (nonessential) input.Skip(4);
        }

        var slots = ReadCount(input, "slots");
        for (var i = 0; i < slots; i++)
        {
            _ = input.ReadString() ?? throw new ConversionException($"Slot {i} has no name.");
            ValidateIndex(input.ReadVarInt(true), bones, $"slot {i} bone");
            input.Skip(8); // light and dark colors
            ValidateRefString(input.ReadVarInt(true), sharedStrings, $"slot {i} attachment");
            _ = input.ReadVarInt(true); // blend mode
        }

        var ik = ReadCount(input, "IK constraints");
        for (var i = 0; i < ik; i++)
        {
            _ = input.ReadString() ?? throw new ConversionException($"IK constraint {i} has no name.");
            _ = input.ReadVarInt(true);
            _ = input.ReadBoolean();
            var constrainedBones = ReadCount(input, $"IK constraint {i} bones");
            for (var n = 0; n < constrainedBones; n++)
                ValidateIndex(input.ReadVarInt(true), bones, $"IK constraint {i} bone");
            ValidateIndex(input.ReadVarInt(true), bones, $"IK constraint {i} target");
            input.Skip(8);
            _ = input.ReadByte();
            _ = input.ReadBoolean();
            _ = input.ReadBoolean();
            _ = input.ReadBoolean();
        }

        var transform = ReadCount(input, "transform constraints");
        for (var i = 0; i < transform; i++)
        {
            _ = input.ReadString() ?? throw new ConversionException($"Transform constraint {i} has no name.");
            _ = input.ReadVarInt(true);
            _ = input.ReadBoolean();
            var constrainedBones = ReadCount(input, $"transform constraint {i} bones");
            for (var n = 0; n < constrainedBones; n++)
                ValidateIndex(input.ReadVarInt(true), bones, $"transform constraint {i} bone");
            ValidateIndex(input.ReadVarInt(true), bones, $"transform constraint {i} target");
            _ = input.ReadBoolean();
            _ = input.ReadBoolean();
            input.Skip(10 * 4);
        }

        var path = ReadCount(input, "path constraints");
        for (var i = 0; i < path; i++)
        {
            _ = input.ReadString() ?? throw new ConversionException($"Path constraint {i} has no name.");
            _ = input.ReadVarInt(true);
            _ = input.ReadBoolean();
            var constrainedBones = ReadCount(input, $"path constraint {i} bones");
            for (var n = 0; n < constrainedBones; n++)
                ValidateIndex(input.ReadVarInt(true), bones, $"path constraint {i} bone");
            ValidateIndex(input.ReadVarInt(true), slots, $"path constraint {i} target");
            _ = input.ReadVarInt(true);
            _ = input.ReadVarInt(true);
            _ = input.ReadVarInt(true);
            input.Skip(5 * 4);
        }

        return new Spine38SetupInfo(
            version, sharedStrings, bones, slots, ik, transform, path, nonessential, input.Position, data.Length);
    }

    private static int ReadCount(SpineBinaryInput input, string label)
    {
        var count = input.ReadVarInt(true);
        if (count is < 0 or > MaximumCollectionCount)
            throw new ConversionException($"Invalid {label} count: {count}.");
        return count;
    }

    private static void ValidateIndex(int index, int count, string label)
    {
        if (index < 0 || index >= count)
            throw new ConversionException($"Invalid {label} index {index}; collection size is {count}.");
    }

    private static void ValidateRefString(int encodedIndex, int count, string label)
    {
        if (encodedIndex < 0 || encodedIndex > count)
            throw new ConversionException($"Invalid {label} string index {encodedIndex}; table size is {count}.");
    }
}
