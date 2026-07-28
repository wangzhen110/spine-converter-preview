namespace SpineConverter.Core;

public sealed record Spine38AnimationInfo(
    int AnimationCount,
    int TimelineCount,
    int FrameCount,
    int FinalOffset,
    int FileByteCount);

public static class Spine38BinaryAnimationInspector
{
    private const int MaximumCollectionCount = 10_000_000;

    public static Spine38AnimationInfo Inspect(ReadOnlyMemory<byte> data)
    {
        var setup = Spine38BinarySetupInspector.Inspect(data);
        var skins = Spine38BinarySkinInspector.Inspect(data);
        var input = new SpineBinaryInput(data);
        input.Skip(skins.FirstAnimationOffset);
        var timelines = 0;
        var frames = 0;

        for (var animation = 0; animation < skins.AnimationCount; animation++)
        {
            _ = input.ReadString() ?? throw new ConversionException($"Animation {animation} has no name.");
            ReadSlotTimelines(input, setup, ref timelines, ref frames);
            ReadBoneTimelines(input, setup, ref timelines, ref frames);
            ReadIkTimelines(input, setup, ref timelines, ref frames);
            ReadTransformTimelines(input, setup, ref timelines, ref frames);
            ReadPathTimelines(input, setup, ref timelines, ref frames);
            ReadDeformTimelines(input, setup, skins, ref timelines, ref frames);
            ReadDrawOrder(input, setup, ref timelines, ref frames);
            ReadEvents(input, skins, ref timelines, ref frames);
        }

        if (input.Remaining != 0)
            throw new ConversionException(
                $"Spine binary has {input.Remaining} unread bytes after {skins.AnimationCount} animations.");
        return new Spine38AnimationInfo(skins.AnimationCount, timelines, frames, input.Position, data.Length);
    }

    private static void ReadSlotTimelines(
        SpineBinaryInput input, Spine38SetupInfo setup, ref int timelines, ref int frames)
    {
        var slots = Count(input, "animated slots");
        for (var i = 0; i < slots; i++)
        {
            Index(input.ReadVarInt(true), setup.SlotCount, "animated slot");
            var count = Count(input, "slot timelines");
            for (var timeline = 0; timeline < count; timeline++)
            {
                var type = input.ReadByte();
                var frameCount = Count(input, "slot timeline frames");
                timelines++;
                frames += frameCount;
                for (var frame = 0; frame < frameCount; frame++)
                {
                    switch (type)
                    {
                        case 0:
                            input.Skip(4);
                            Ref(input.ReadVarInt(true), setup.SharedStringCount, "slot attachment frame");
                            break;
                        case 1:
                            input.Skip(8);
                            if (frame + 1 < frameCount) Curve(input);
                            break;
                        case 2:
                            input.Skip(12);
                            if (frame + 1 < frameCount) Curve(input);
                            break;
                        default:
                            throw new ConversionException($"Unknown slot timeline type {type}.");
                    }
                }
            }
        }
    }

    private static void ReadBoneTimelines(
        SpineBinaryInput input, Spine38SetupInfo setup, ref int timelines, ref int frames)
    {
        var bones = Count(input, "animated bones");
        for (var i = 0; i < bones; i++)
        {
            Index(input.ReadVarInt(true), setup.BoneCount, "animated bone");
            var count = Count(input, "bone timelines");
            for (var timeline = 0; timeline < count; timeline++)
            {
                var type = input.ReadByte();
                if (type > 3) throw new ConversionException($"Unknown bone timeline type {type}.");
                var frameCount = Count(input, "bone timeline frames");
                timelines++;
                frames += frameCount;
                for (var frame = 0; frame < frameCount; frame++)
                {
                    input.Skip(type == 0 ? 8 : 12);
                    if (frame + 1 < frameCount) Curve(input);
                }
            }
        }
    }

    private static void ReadIkTimelines(
        SpineBinaryInput input, Spine38SetupInfo setup, ref int timelines, ref int frames)
    {
        var count = Count(input, "IK timelines");
        for (var timeline = 0; timeline < count; timeline++)
        {
            Index(input.ReadVarInt(true), setup.IkConstraintCount, "IK timeline");
            var frameCount = Count(input, "IK frames");
            timelines++;
            frames += frameCount;
            for (var frame = 0; frame < frameCount; frame++)
            {
                input.Skip(15);
                if (frame + 1 < frameCount) Curve(input);
            }
        }
    }

    private static void ReadTransformTimelines(
        SpineBinaryInput input, Spine38SetupInfo setup, ref int timelines, ref int frames)
    {
        var count = Count(input, "transform timelines");
        for (var timeline = 0; timeline < count; timeline++)
        {
            Index(input.ReadVarInt(true), setup.TransformConstraintCount, "transform timeline");
            var frameCount = Count(input, "transform frames");
            timelines++;
            frames += frameCount;
            for (var frame = 0; frame < frameCount; frame++)
            {
                input.Skip(20);
                if (frame + 1 < frameCount) Curve(input);
            }
        }
    }

    private static void ReadPathTimelines(
        SpineBinaryInput input, Spine38SetupInfo setup, ref int timelines, ref int frames)
    {
        var paths = Count(input, "animated path constraints");
        for (var path = 0; path < paths; path++)
        {
            Index(input.ReadVarInt(true), setup.PathConstraintCount, "path timeline");
            var count = Count(input, "path timelines");
            for (var timeline = 0; timeline < count; timeline++)
            {
                var type = input.ReadByte();
                if (type > 2) throw new ConversionException($"Unknown path timeline type {type}.");
                var frameCount = Count(input, "path frames");
                timelines++;
                frames += frameCount;
                for (var frame = 0; frame < frameCount; frame++)
                {
                    input.Skip(type == 2 ? 12 : 8);
                    if (frame + 1 < frameCount) Curve(input);
                }
            }
        }
    }

    private static void ReadDeformTimelines(
        SpineBinaryInput input,
        Spine38SetupInfo setup,
        Spine38SkinInfo skins,
        ref int timelines,
        ref int frames)
    {
        var skinCount = Count(input, "animated deform skins");
        for (var skin = 0; skin < skinCount; skin++)
        {
            Index(input.ReadVarInt(true), skins.SkinCount, "deform skin");
            var slotCount = Count(input, "deform slots");
            for (var slot = 0; slot < slotCount; slot++)
            {
                Index(input.ReadVarInt(true), setup.SlotCount, "deform slot");
                var attachmentCount = Count(input, "deform attachments");
                for (var attachment = 0; attachment < attachmentCount; attachment++)
                {
                    Ref(input.ReadVarInt(true), setup.SharedStringCount, "deform attachment");
                    var frameCount = Count(input, "deform frames");
                    timelines++;
                    frames += frameCount;
                    for (var frame = 0; frame < frameCount; frame++)
                    {
                        input.Skip(4);
                        var values = Count(input, "deform values");
                        if (values > 0)
                        {
                            _ = input.ReadVarInt(true);
                            input.Skip(checked(values * 4));
                        }
                        if (frame + 1 < frameCount) Curve(input);
                    }
                }
            }
        }
    }

    private static void ReadDrawOrder(
        SpineBinaryInput input, Spine38SetupInfo setup, ref int timelines, ref int frames)
    {
        var frameCount = Count(input, "draw order frames");
        if (frameCount > 0) timelines++;
        frames += frameCount;
        for (var frame = 0; frame < frameCount; frame++)
        {
            input.Skip(4);
            var offsets = Count(input, "draw order offsets");
            for (var offset = 0; offset < offsets; offset++)
            {
                Index(input.ReadVarInt(true), setup.SlotCount, "draw order slot");
                _ = input.ReadVarInt(true);
            }
        }
    }

    private static void ReadEvents(
        SpineBinaryInput input, Spine38SkinInfo skins, ref int timelines, ref int frames)
    {
        var frameCount = Count(input, "event frames");
        if (frameCount > 0) timelines++;
        frames += frameCount;
        for (var frame = 0; frame < frameCount; frame++)
        {
            input.Skip(4);
            var eventIndex = input.ReadVarInt(true);
            Index(eventIndex, skins.EventCount, "event frame");
            _ = input.ReadVarInt(false);
            input.Skip(4);
            if (input.ReadBoolean()) _ = input.ReadString();
            if (skins.EventHasAudio[eventIndex]) input.Skip(8);
        }
    }

    private static void Curve(SpineBinaryInput input)
    {
        var type = input.ReadByte();
        if (type == 2) input.Skip(16);
        else if (type > 2) throw new ConversionException($"Unknown curve type {type}.");
    }

    private static int Count(SpineBinaryInput input, string label)
    {
        var count = input.ReadVarInt(true);
        if (count is < 0 or > MaximumCollectionCount)
            throw new ConversionException($"Invalid {label} count: {count}.");
        return count;
    }

    private static void Index(int index, int count, string label)
    {
        if (index < 0 || index >= count)
            throw new ConversionException($"Invalid {label} index {index}; collection size is {count}.");
    }

    private static void Ref(int encodedIndex, int count, string label)
    {
        if (encodedIndex < 0 || encodedIndex > count)
            throw new ConversionException($"Invalid {label} string index {encodedIndex}; table size is {count}.");
    }
}
