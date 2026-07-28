using System.Text.Json.Nodes;

namespace SpineConverter.Core;

public sealed partial class Spine38BinaryJsonReader
{
    private void ReadEvents(JsonObject root)
    {
        var count = Count("events");
        if (count == 0) return;
        var output = new JsonObject();
        for (var i = 0; i < count; i++)
        {
            var name = RefString() ?? throw new ConversionException($"Event {i} has no name.");
            var intValue = _input.ReadVarInt(false);
            var floatValue = _input.ReadSingle();
            var stringValue = _input.ReadString();
            var audio = _input.ReadString();
            var value = new JsonObject();
            Set(value, "int", intValue, 0);
            Set(value, "float", floatValue, 0);
            if (stringValue is not null) value["string"] = stringValue;
            if (audio is not null)
            {
                value["audio"] = audio;
                Set(value, "volume", _input.ReadSingle(), 1);
                Set(value, "balance", _input.ReadSingle(), 0);
            }
            _events.Add(new EventSetup(name, intValue, floatValue, stringValue, audio is not null));
            output[name] = value;
        }
        root["events"] = output;
    }

    private void ReadAnimations(JsonObject root)
    {
        var count = Count("animations");
        if (count == 0) return;
        var output = new JsonObject();
        for (var i = 0; i < count; i++)
        {
            var name = RequiredString($"animation {i}");
            output[name] = ReadAnimation();
        }
        root["animations"] = output;
    }

    private JsonObject ReadAnimation()
    {
        var output = new JsonObject();
        ReadSlotAnimation(output);
        ReadBoneAnimation(output);
        ReadIkAnimation(output);
        ReadTransformAnimation(output);
        ReadPathAnimation(output);
        ReadDeformAnimation(output);
        ReadDrawOrderAnimation(output);
        ReadEventAnimation(output);
        return output;
    }

    private void ReadSlotAnimation(JsonObject animation)
    {
        var entryCount = Count("animated slots");
        if (entryCount == 0) return;
        var slots = new JsonObject();
        for (var entry = 0; entry < entryCount; entry++)
        {
            var slotName = Name(_slots, _input.ReadVarInt(true), "animated slot");
            var timelines = new JsonObject();
            var timelineCount = Count("slot timelines");
            for (var timeline = 0; timeline < timelineCount; timeline++)
            {
                var type = _input.ReadByte();
                var frameCount = Count("slot frames");
                var frames = new JsonArray();
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var frame = new JsonObject();
                    Set(frame, "time", _input.ReadSingle(), 0);
                    switch (type)
                    {
                        case 0:
                        {
                            var attachment = RefString();
                            if (attachment is not null) frame["name"] = attachment;
                            break;
                        }
                        case 1:
                            frame["color"] = Rgba(_input.ReadInt32());
                            if (frameIndex + 1 < frameCount) ReadCurve(frame);
                            break;
                        case 2:
                            frame["light"] = Rgba(_input.ReadInt32());
                            frame["dark"] = Rgb(_input.ReadInt32());
                            if (frameIndex + 1 < frameCount) ReadCurve(frame);
                            break;
                        default:
                            throw new ConversionException($"Unknown slot timeline type {type}.");
                    }
                    frames.Add(frame);
                }
                timelines[type switch { 0 => "attachment", 1 => "color", 2 => "twoColor", _ => "unknown" }] = frames;
            }
            slots[slotName] = timelines;
        }
        animation["slots"] = slots;
    }

    private void ReadBoneAnimation(JsonObject animation)
    {
        var entryCount = Count("animated bones");
        if (entryCount == 0) return;
        var bones = new JsonObject();
        for (var entry = 0; entry < entryCount; entry++)
        {
            var boneName = Name(_bones, _input.ReadVarInt(true), "animated bone");
            var timelines = new JsonObject();
            var timelineCount = Count("bone timelines");
            for (var timeline = 0; timeline < timelineCount; timeline++)
            {
                var type = _input.ReadByte();
                if (type > 3) throw new ConversionException($"Unknown bone timeline type {type}.");
                var frameCount = Count("bone frames");
                var frames = new JsonArray();
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var frame = new JsonObject();
                    Set(frame, "time", _input.ReadSingle(), 0);
                    if (type == 0)
                        Set(frame, "angle", _input.ReadSingle(), 0);
                    else
                    {
                        var defaultValue = type == 2 ? 1 : 0;
                        Set(frame, "x", _input.ReadSingle(), defaultValue);
                        Set(frame, "y", _input.ReadSingle(), defaultValue);
                    }
                    if (frameIndex + 1 < frameCount) ReadCurve(frame);
                    frames.Add(frame);
                }
                string[] names = ["rotate", "translate", "scale", "shear"];
                timelines[names[type]] = frames;
            }
            bones[boneName] = timelines;
        }
        animation["bones"] = bones;
    }

    private void ReadIkAnimation(JsonObject animation)
    {
        var count = Count("IK timelines");
        if (count == 0) return;
        var output = new JsonObject();
        for (var i = 0; i < count; i++)
        {
            var name = Name(_ik, _input.ReadVarInt(true), "IK timeline");
            var frameCount = Count("IK frames");
            var frames = new JsonArray();
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var frame = new JsonObject();
                Set(frame, "time", _input.ReadSingle(), 0);
                Set(frame, "mix", _input.ReadSingle(), 1);
                Set(frame, "softness", _input.ReadSingle(), 0);
                if (unchecked((sbyte)_input.ReadByte()) < 0) frame["bendPositive"] = false;
                if (_input.ReadBoolean()) frame["compress"] = true;
                if (_input.ReadBoolean()) frame["stretch"] = true;
                if (frameIndex + 1 < frameCount) ReadCurve(frame);
                frames.Add(frame);
            }
            output[name] = frames;
        }
        animation["ik"] = output;
    }

    private void ReadTransformAnimation(JsonObject animation)
    {
        var count = Count("transform timelines");
        if (count == 0) return;
        var output = new JsonObject();
        for (var i = 0; i < count; i++)
        {
            var name = Name(_transform, _input.ReadVarInt(true), "transform timeline");
            var frameCount = Count("transform frames");
            var frames = new JsonArray();
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var frame = new JsonObject();
                Set(frame, "time", _input.ReadSingle(), 0);
                Set(frame, "rotateMix", _input.ReadSingle(), 1);
                Set(frame, "translateMix", _input.ReadSingle(), 1);
                Set(frame, "scaleMix", _input.ReadSingle(), 1);
                Set(frame, "shearMix", _input.ReadSingle(), 1);
                if (frameIndex + 1 < frameCount) ReadCurve(frame);
                frames.Add(frame);
            }
            output[name] = frames;
        }
        animation["transform"] = output;
    }

    private void ReadPathAnimation(JsonObject animation)
    {
        var entryCount = Count("animated paths");
        if (entryCount == 0) return;
        var output = new JsonObject();
        for (var entry = 0; entry < entryCount; entry++)
        {
            var name = Name(_paths, _input.ReadVarInt(true), "path timeline");
            var timelines = new JsonObject();
            var count = Count("path timelines");
            for (var timeline = 0; timeline < count; timeline++)
            {
                var type = _input.ReadByte();
                if (type > 2) throw new ConversionException($"Unknown path timeline type {type}.");
                var frameCount = Count("path frames");
                var frames = new JsonArray();
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var frame = new JsonObject();
                    Set(frame, "time", _input.ReadSingle(), 0);
                    if (type == 0) Set(frame, "position", _input.ReadSingle(), 0);
                    else if (type == 1) Set(frame, "spacing", _input.ReadSingle(), 0);
                    else
                    {
                        Set(frame, "rotateMix", _input.ReadSingle(), 1);
                        Set(frame, "translateMix", _input.ReadSingle(), 1);
                    }
                    if (frameIndex + 1 < frameCount) ReadCurve(frame);
                    frames.Add(frame);
                }
                string[] names = ["position", "spacing", "mix"];
                timelines[names[type]] = frames;
            }
            output[name] = timelines;
        }
        animation["path"] = output;
    }

    private void ReadDeformAnimation(JsonObject animation)
    {
        var skinCount = Count("deform skins");
        if (skinCount == 0) return;
        var skins = new JsonObject();
        for (var skinEntry = 0; skinEntry < skinCount; skinEntry++)
        {
            var skinName = Name(_skins, _input.ReadVarInt(true), "deform skin");
            var slots = new JsonObject();
            var slotCount = Count("deform slots");
            for (var slotEntry = 0; slotEntry < slotCount; slotEntry++)
            {
                var slotName = Name(_slots, _input.ReadVarInt(true), "deform slot");
                var attachments = new JsonObject();
                var attachmentCount = Count("deform attachments");
                for (var attachmentEntry = 0; attachmentEntry < attachmentCount; attachmentEntry++)
                {
                    var attachmentName = RefString() ?? throw new ConversionException("Deform attachment is null.");
                    var frameCount = Count("deform frames");
                    var frames = new JsonArray();
                    for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                    {
                        var frame = new JsonObject();
                        Set(frame, "time", _input.ReadSingle(), 0);
                        var values = Count("deform values");
                        if (values > 0)
                        {
                            var offset = _input.ReadVarInt(true);
                            Set(frame, "offset", offset, 0);
                            frame["vertices"] = ReadFloatArray(values);
                        }
                        if (frameIndex + 1 < frameCount) ReadCurve(frame);
                        frames.Add(frame);
                    }
                    attachments[attachmentName] = frames;
                }
                slots[slotName] = attachments;
            }
            skins[skinName] = slots;
        }
        animation["deform"] = skins;
    }

    private void ReadDrawOrderAnimation(JsonObject animation)
    {
        var frameCount = Count("draw order frames");
        if (frameCount == 0) return;
        var frames = new JsonArray();
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frame = new JsonObject();
            Set(frame, "time", _input.ReadSingle(), 0);
            var offsetCount = Count("draw order offsets");
            if (offsetCount > 0)
            {
                var offsets = new JsonArray();
                for (var offset = 0; offset < offsetCount; offset++)
                {
                    offsets.Add(new JsonObject
                    {
                        ["slot"] = Name(_slots, _input.ReadVarInt(true), "draw order slot"),
                        ["offset"] = _input.ReadVarInt(true),
                    });
                }
                frame["offsets"] = offsets;
            }
            frames.Add(frame);
        }
        animation["drawOrder"] = frames;
    }

    private void ReadEventAnimation(JsonObject animation)
    {
        var frameCount = Count("event frames");
        if (frameCount == 0) return;
        var frames = new JsonArray();
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frame = new JsonObject();
            Set(frame, "time", _input.ReadSingle(), 0);
            var setup = _events.ElementAtOrDefault(_input.ReadVarInt(true))
                ?? throw new ConversionException("Invalid event frame index.");
            frame["name"] = setup.Name;
            Set(frame, "int", _input.ReadVarInt(false), setup.Int);
            Set(frame, "float", _input.ReadSingle(), setup.Float);
            if (_input.ReadBoolean()) frame["string"] = _input.ReadString();
            if (setup.HasAudio)
            {
                Set(frame, "volume", _input.ReadSingle(), 1);
                Set(frame, "balance", _input.ReadSingle(), 0);
            }
            frames.Add(frame);
        }
        animation["events"] = frames;
    }

    private void ReadCurve(JsonObject frame)
    {
        var type = _input.ReadByte();
        if (type == 0) return;
        if (type == 1)
        {
            frame["curve"] = "stepped";
            return;
        }
        if (type != 2) throw new ConversionException($"Unknown curve type {type}.");
        frame["curve"] = _input.ReadSingle();
        frame["c2"] = _input.ReadSingle();
        frame["c3"] = _input.ReadSingle();
        frame["c4"] = _input.ReadSingle();
    }
}
