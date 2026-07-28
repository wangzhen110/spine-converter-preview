namespace SpineConverter.Core;

public sealed record Spine38SkinInfo(
    int SkinCount,
    int AttachmentCount,
    IReadOnlyDictionary<string, int> AttachmentTypes,
    int WeightedAttachmentCount,
    int EventCount,
    int AnimationCount,
    IReadOnlyList<bool> EventHasAudio,
    int FirstAnimationOffset,
    int FileByteCount);

public static class Spine38BinarySkinInspector
{
    private static readonly string[] AttachmentTypeNames =
        ["region", "boundingbox", "mesh", "linkedmesh", "path", "point", "clipping"];
    private const int MaximumCollectionCount = 1_000_000;

    public static Spine38SkinInfo Inspect(ReadOnlyMemory<byte> data)
    {
        var setup = Spine38BinarySetupInspector.Inspect(data);
        var input = new SpineBinaryInput(data);
        input.Skip(setup.SkinSectionOffset);
        var typeCounts = AttachmentTypeNames.ToDictionary(name => name, _ => 0);
        var attachments = 0;
        var weighted = 0;
        var skins = 0;

        var defaultSlotCount = ReadCount(input, "default skin slots");
        if (defaultSlotCount > 0)
        {
            skins++;
            ReadSkinAttachments(input, defaultSlotCount, setup, typeCounts, ref attachments, ref weighted);
        }

        var additionalSkins = ReadCount(input, "skins");
        for (var skinIndex = 0; skinIndex < additionalSkins; skinIndex++)
        {
            skins++;
            ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, $"skin {skinIndex} name");
            ReadIndices(input, setup.BoneCount, $"skin {skinIndex} bones");
            ReadIndices(input, setup.IkConstraintCount, $"skin {skinIndex} IK constraints");
            ReadIndices(input, setup.TransformConstraintCount, $"skin {skinIndex} transform constraints");
            ReadIndices(input, setup.PathConstraintCount, $"skin {skinIndex} path constraints");
            var slotCount = ReadCount(input, $"skin {skinIndex} slots");
            ReadSkinAttachments(input, slotCount, setup, typeCounts, ref attachments, ref weighted);
        }

        var events = ReadCount(input, "events");
        var eventHasAudio = new bool[events];
        for (var eventIndex = 0; eventIndex < events; eventIndex++)
        {
            ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, $"event {eventIndex} name");
            _ = input.ReadVarInt(false);
            _ = input.ReadSingle();
            _ = input.ReadString();
            var audio = input.ReadString();
            eventHasAudio[eventIndex] = audio is not null;
            if (audio is not null) input.Skip(8);
        }

        var animations = ReadCount(input, "animations");
        return new Spine38SkinInfo(
            skins, attachments, typeCounts, weighted, events, animations, eventHasAudio, input.Position, data.Length);
    }

    private static void ReadSkinAttachments(
        SpineBinaryInput input,
        int slotCount,
        Spine38SetupInfo setup,
        Dictionary<string, int> typeCounts,
        ref int attachmentCount,
        ref int weightedCount)
    {
        for (var slot = 0; slot < slotCount; slot++)
        {
            ValidateIndex(input.ReadVarInt(true), setup.SlotCount, "skin slot");
            var count = ReadCount(input, "slot attachments");
            for (var attachment = 0; attachment < count; attachment++)
            {
                ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, "attachment placeholder");
                ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, "attachment name");
                var type = input.ReadByte();
                if (type >= AttachmentTypeNames.Length)
                    throw new ConversionException($"Unknown Spine 3.8 attachment type {type} at offset {input.Position - 1}.");
                typeCounts[AttachmentTypeNames[type]]++;
                attachmentCount++;
                ReadAttachment(input, type, setup, ref weightedCount);
            }
        }
    }

    private static void ReadAttachment(
        SpineBinaryInput input,
        int type,
        Spine38SetupInfo setup,
        ref int weightedCount)
    {
        switch (type)
        {
            case 0: // region
                ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, "region path");
                input.Skip(7 * 4 + 4);
                break;
            case 1: // bounding box
            {
                var vertices = ReadCount(input, "bounding box vertices");
                ReadVertices(input, vertices, setup.BoneCount, ref weightedCount);
                if (setup.HasNonessentialData) input.Skip(4);
                break;
            }
            case 2: // mesh
            {
                ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, "mesh path");
                input.Skip(4);
                var vertices = ReadCount(input, "mesh vertices");
                input.Skip(checked(vertices * 2 * 4));
                ReadShortArray(input, "mesh triangles");
                ReadVertices(input, vertices, setup.BoneCount, ref weightedCount);
                _ = input.ReadVarInt(true);
                if (setup.HasNonessentialData)
                {
                    ReadShortArray(input, "mesh edges");
                    input.Skip(8);
                }
                break;
            }
            case 3: // linked mesh
                ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, "linked mesh path");
                input.Skip(4);
                ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, "linked mesh skin");
                ValidateRef(input.ReadVarInt(true), setup.SharedStringCount, "linked mesh parent");
                _ = input.ReadBoolean();
                if (setup.HasNonessentialData) input.Skip(8);
                break;
            case 4: // path
            {
                _ = input.ReadBoolean();
                _ = input.ReadBoolean();
                var vertices = ReadCount(input, "path vertices");
                ReadVertices(input, vertices, setup.BoneCount, ref weightedCount);
                input.Skip(checked(vertices / 3 * 4));
                if (setup.HasNonessentialData) input.Skip(4);
                break;
            }
            case 5: // point
                input.Skip(12);
                if (setup.HasNonessentialData) input.Skip(4);
                break;
            case 6: // clipping
            {
                ValidateIndex(input.ReadVarInt(true), setup.SlotCount, "clipping end slot");
                var vertices = ReadCount(input, "clipping vertices");
                ReadVertices(input, vertices, setup.BoneCount, ref weightedCount);
                if (setup.HasNonessentialData) input.Skip(4);
                break;
            }
        }
    }

    private static void ReadVertices(
        SpineBinaryInput input,
        int vertexCount,
        int boneCount,
        ref int weightedCount)
    {
        if (!input.ReadBoolean())
        {
            input.Skip(checked(vertexCount * 2 * 4));
            return;
        }
        weightedCount++;
        for (var vertex = 0; vertex < vertexCount; vertex++)
        {
            var influences = ReadCount(input, "vertex bone influences");
            if (influences == 0)
                throw new ConversionException("A weighted vertex has no bone influences.");
            for (var influence = 0; influence < influences; influence++)
            {
                ValidateIndex(input.ReadVarInt(true), boneCount, "weighted vertex bone");
                input.Skip(12);
            }
        }
    }

    private static void ReadShortArray(SpineBinaryInput input, string label)
    {
        var count = ReadCount(input, label);
        for (var i = 0; i < count; i++) _ = input.ReadUInt16();
    }

    private static void ReadIndices(SpineBinaryInput input, int targetCount, string label)
    {
        var count = ReadCount(input, label);
        for (var i = 0; i < count; i++) ValidateIndex(input.ReadVarInt(true), targetCount, label);
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

    private static void ValidateRef(int encodedIndex, int count, string label)
    {
        if (encodedIndex < 0 || encodedIndex > count)
            throw new ConversionException($"Invalid {label} string index {encodedIndex}; table size is {count}.");
    }
}
