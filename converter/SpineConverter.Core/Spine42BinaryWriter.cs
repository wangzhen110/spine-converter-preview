using System.Globalization;
using System.Text.Json.Nodes;

namespace SpineConverter.Core;

public sealed partial class Spine42BinaryWriter
{
    private readonly SpineBinaryOutput _output = new();
    private readonly List<string> _strings = [];
    private readonly Dictionary<string, int> _stringIndices = new(StringComparer.Ordinal);
    private readonly List<string> _bones = [];
    private readonly List<string> _slots = [];
    private readonly List<string> _ik = [];
    private readonly List<string> _transform = [];
    private readonly List<string> _paths = [];
    private readonly List<string> _skins = [];
    private readonly List<EventInfo> _events = [];
    private bool _written;

    public byte[] Write(JsonObject root)
    {
        if (_written)
            throw new ConversionException("A Spine42BinaryWriter instance can only write one skeleton.");
        _written = true;
        ValidateVersion(root);
        CollectReferences(root);
        WriteHeader(root);
        WriteStringTable();
        WriteBones(root);
        WriteSlots(root);
        WriteIk(root);
        WriteTransform(root);
        WritePath(root);
        _output.WriteVarInt(0, true); // Physics constraints are not present in Spine 3.8 input.
        WriteSkins(root);
        WriteEvents(root);
        WriteAnimations(root);
        return _output.ToArray();
    }

    private static void ValidateVersion(JsonObject root)
    {
        var version = root["skeleton"]?["spine"]?.GetValue<string>()
            ?? throw new ConversionException("Missing skeleton.spine version.");
        if (!SpineVersion.Parse(version).IsLine(4, 2))
            throw new ConversionException($"Spine 4.2 binary writer cannot write version {version}.");
    }

    private void CollectReferences(JsonObject root)
    {
        if (root["slots"] is JsonArray slots)
            foreach (var node in slots)
                AddString(node?["attachment"]?.GetValue<string>());
        if (root["skins"] is JsonArray skins)
        {
            foreach (var skinNode in skins)
            {
                if (skinNode?["attachments"] is not JsonObject slotMap) continue;
                foreach (var (_, slotNode) in slotMap)
                foreach (var (placeholder, attachmentNode) in slotNode!.AsObject())
                {
                    AddString(placeholder);
                    if (attachmentNode is not JsonObject attachment) continue;
                    AddString(TextOrNull(attachment, "name"));
                    AddString(TextOrNull(attachment, "path"));
                    AddString(TextOrNull(attachment, "parent"));
                }
            }
        }
        if (root["animations"] is not JsonObject animations) return;
        foreach (var (_, animationNode) in animations)
        {
            var animation = animationNode!.AsObject();
            if (animation["slots"] is JsonObject animatedSlots)
                foreach (var (_, slotNode) in animatedSlots)
                    if (slotNode?["attachment"] is JsonArray frames)
                        foreach (var frame in frames) AddString(frame?["name"]?.GetValue<string>());
            if (animation["attachments"] is JsonObject animatedSkins)
                foreach (var (_, skinNode) in animatedSkins)
                foreach (var (_, slotNode) in skinNode!.AsObject())
                foreach (var (attachmentName, _) in slotNode!.AsObject()) AddString(attachmentName);
        }
    }

    private void AddString(string? value)
    {
        if (value is null || _stringIndices.ContainsKey(value)) return;
        _stringIndices[value] = _strings.Count;
        _strings.Add(value);
    }

    private void WriteHeader(JsonObject root)
    {
        var skeleton = Object(root, "skeleton");
        _output.WriteInt64(0);
        _output.WriteString(Text(skeleton, "spine"));
        _output.WriteSingle(Float(skeleton, "x"));
        _output.WriteSingle(Float(skeleton, "y"));
        _output.WriteSingle(Float(skeleton, "width"));
        _output.WriteSingle(Float(skeleton, "height"));
        _output.WriteSingle(Float(skeleton, "referenceScale", 100));
        _output.WriteBoolean(false);
    }

    private void WriteStringTable()
    {
        _output.WriteVarInt(_strings.Count, true);
        foreach (var value in _strings) _output.WriteString(value);
    }

    private void WriteBones(JsonObject root)
    {
        var bones = Array(root, "bones");
        _output.WriteVarInt(bones.Count, true);
        for (var index = 0; index < bones.Count; index++)
        {
            var bone = bones[index]!.AsObject();
            var name = Text(bone, "name");
            _bones.Add(name);
            _output.WriteString(name);
            if (index > 0) _output.WriteVarInt(Index(_bones, Text(bone, "parent"), "bone parent"), true);
            _output.WriteSingle(Float(bone, "rotation"));
            _output.WriteSingle(Float(bone, "x"));
            _output.WriteSingle(Float(bone, "y"));
            _output.WriteSingle(Float(bone, "scaleX", 1));
            _output.WriteSingle(Float(bone, "scaleY", 1));
            _output.WriteSingle(Float(bone, "shearX"));
            _output.WriteSingle(Float(bone, "shearY"));
            _output.WriteSingle(Float(bone, "length"));
            string[] modes = ["normal", "onlyTranslation", "noRotationOrReflection", "noScale", "noScaleOrReflection"];
            var mode = TextOrNull(bone, "inherit") ?? TextOrNull(bone, "transform") ?? "normal";
            _output.WriteVarInt(Index(modes, mode, "bone inherit"), true);
            _output.WriteBoolean(Bool(bone, "skin"));
        }
    }

    private void WriteSlots(JsonObject root)
    {
        var slots = root["slots"] as JsonArray ?? [];
        _output.WriteVarInt(slots.Count, true);
        foreach (var node in slots)
        {
            var slot = node!.AsObject();
            var name = Text(slot, "name");
            _slots.Add(name);
            _output.WriteString(name);
            _output.WriteVarInt(Index(_bones, Text(slot, "bone"), "slot bone"), true);
            _output.WriteInt32(Color(TextOrNull(slot, "color") ?? "ffffffff", true));
            _output.WriteInt32(slot["dark"] is null ? -1 : Color(Text(slot, "dark"), false));
            WriteRef(TextOrNull(slot, "attachment"));
            string[] blends = ["normal", "additive", "multiply", "screen"];
            _output.WriteVarInt(Index(blends, TextOrNull(slot, "blend") ?? "normal", "slot blend"), true);
        }
    }

    private void WriteIk(JsonObject root)
    {
        var constraints = root["ik"] as JsonArray ?? [];
        _output.WriteVarInt(constraints.Count, true);
        foreach (var node in constraints)
        {
            var value = node!.AsObject();
            var name = Text(value, "name");
            _ik.Add(name);
            _output.WriteString(name);
            _output.WriteVarInt(Int(value, "order"), true);
            WriteNameIndices(Array(value, "bones"), _bones, "IK bones");
            _output.WriteVarInt(Index(_bones, Text(value, "target"), "IK target"), true);
            var mix = Float(value, "mix", 1);
            var softness = Float(value, "softness");
            var flags = 0;
            if (Bool(value, "skin")) flags |= 1;
            if (Bool(value, "bendPositive", true)) flags |= 2;
            if (Bool(value, "compress")) flags |= 4;
            if (Bool(value, "stretch")) flags |= 8;
            if (Bool(value, "uniform")) flags |= 16;
            if (mix != 0) { flags |= 32; if (mix != 1) flags |= 64; }
            if (softness != 0) flags |= 128;
            _output.WriteByte(flags);
            if ((flags & 64) != 0) _output.WriteSingle(mix);
            if ((flags & 128) != 0) _output.WriteSingle(softness);
        }
    }

    private void WriteTransform(JsonObject root)
    {
        var constraints = root["transform"] as JsonArray ?? [];
        _output.WriteVarInt(constraints.Count, true);
        foreach (var node in constraints)
        {
            var value = node!.AsObject();
            var name = Text(value, "name");
            _transform.Add(name);
            _output.WriteString(name);
            _output.WriteVarInt(Int(value, "order"), true);
            WriteNameIndices(Array(value, "bones"), _bones, "transform bones");
            _output.WriteVarInt(Index(_bones, Text(value, "target"), "transform target"), true);
            var first = 0;
            if (Bool(value, "skin")) first |= 1;
            if (Bool(value, "local")) first |= 2;
            if (Bool(value, "relative")) first |= 4;
            first |= Flag(value, "rotation", 8);
            first |= Flag(value, "x", 16);
            first |= Flag(value, "y", 32);
            first |= Flag(value, "scaleX", 64);
            first |= Flag(value, "scaleY", 128);
            _output.WriteByte(first);
            WriteFlagFloat(value, "rotation", first, 8);
            WriteFlagFloat(value, "x", first, 16);
            WriteFlagFloat(value, "y", first, 32);
            WriteFlagFloat(value, "scaleX", first, 64);
            WriteFlagFloat(value, "scaleY", first, 128);
            var second = Flag(value, "shearY", 1)
                | Flag(value, "mixRotate", 2, 1)
                | Flag(value, "mixX", 4, 1)
                | Flag(value, "mixY", 8, Float(value, "mixX", 1))
                | Flag(value, "mixScaleX", 16, 1)
                | Flag(value, "mixScaleY", 32, Float(value, "mixScaleX", 1))
                | Flag(value, "mixShearY", 64, 1);
            _output.WriteByte(second);
            WriteFlagFloat(value, "shearY", second, 1);
            WriteFlagFloat(value, "mixRotate", second, 2, 1);
            WriteFlagFloat(value, "mixX", second, 4, 1);
            WriteFlagFloat(value, "mixY", second, 8, Float(value, "mixX", 1));
            WriteFlagFloat(value, "mixScaleX", second, 16, 1);
            WriteFlagFloat(value, "mixScaleY", second, 32, Float(value, "mixScaleX", 1));
            WriteFlagFloat(value, "mixShearY", second, 64, 1);
        }
    }

    private void WritePath(JsonObject root)
    {
        var constraints = root["path"] as JsonArray ?? [];
        _output.WriteVarInt(constraints.Count, true);
        foreach (var node in constraints)
        {
            var value = node!.AsObject();
            var name = Text(value, "name");
            _paths.Add(name);
            _output.WriteString(name);
            _output.WriteVarInt(Int(value, "order"), true);
            _output.WriteBoolean(Bool(value, "skin"));
            WriteNameIndices(Array(value, "bones"), _bones, "path bones");
            _output.WriteVarInt(Index(_slots, Text(value, "target"), "path target"), true);
            string[] positions = ["fixed", "percent"];
            string[] spacings = ["length", "fixed", "percent", "proportional"];
            string[] rotations = ["tangent", "chain", "chainScale"];
            var flags = Index(positions, TextOrNull(value, "positionMode") ?? "percent", "position mode")
                | (Index(spacings, TextOrNull(value, "spacingMode") ?? "length", "spacing mode") << 1)
                | (Index(rotations, TextOrNull(value, "rotateMode") ?? "tangent", "rotate mode") << 3);
            if (Float(value, "rotation") != 0) flags |= 128;
            _output.WriteByte(flags);
            if ((flags & 128) != 0) _output.WriteSingle(Float(value, "rotation"));
            _output.WriteSingle(Float(value, "position"));
            _output.WriteSingle(Float(value, "spacing"));
            _output.WriteSingle(Float(value, "mixRotate", 1));
            _output.WriteSingle(Float(value, "mixX", 1));
            _output.WriteSingle(Float(value, "mixY", Float(value, "mixX", 1)));
        }
    }

    private void WriteNameIndices(JsonArray values, List<string> names, string label)
    {
        _output.WriteVarInt(values.Count, true);
        foreach (var node in values) _output.WriteVarInt(Index(names, node!.GetValue<string>(), label), true);
    }

    private void WriteRef(string? value)
    {
        _output.WriteVarInt(value is null ? 0 : _stringIndices[value] + 1, true);
    }

    private static JsonObject Object(JsonObject parent, string key) =>
        parent[key] as JsonObject ?? throw new ConversionException($"Missing object '{key}'.");
    private static JsonArray Array(JsonObject parent, string key) =>
        parent[key] as JsonArray ?? throw new ConversionException($"Missing array '{key}'.");
    private static string Text(JsonObject value, string key) =>
        TextOrNull(value, key) ?? throw new ConversionException($"Missing string '{key}'.");
    private static string? TextOrNull(JsonObject value, string key) => value[key]?.GetValue<string>();
    private static float Float(JsonObject value, string key, float fallback = 0) =>
        value[key] is JsonValue node && node.TryGetValue<float>(out var result) ? result : fallback;
    private static int Int(JsonObject value, string key, int fallback = 0) =>
        value[key] is JsonValue node && node.TryGetValue<int>(out var result) ? result : fallback;
    private static bool Bool(JsonObject value, string key, bool fallback = false) =>
        value[key] is JsonValue node && node.TryGetValue<bool>(out var result) ? result : fallback;
    private static int Index(IReadOnlyList<string> values, string value, string label)
    {
        for (var i = 0; i < values.Count; i++) if (values[i] == value) return i;
        throw new ConversionException($"Unknown {label}: {value}");
    }
    private static int Flag(JsonObject value, string key, int bit, float fallback = 0) =>
        Float(value, key, fallback) != fallback ? bit : 0;
    private void WriteFlagFloat(JsonObject value, string key, int flags, int bit, float fallback = 0)
    {
        if ((flags & bit) != 0) _output.WriteSingle(Float(value, key, fallback));
    }
    private static int Color(string value, bool alpha)
    {
        var expected = alpha ? 8 : 6;
        if (value.Length != expected) throw new ConversionException($"Invalid color '{value}'.");
        return unchecked((int)uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private sealed record EventInfo(string Name, int Int, float Float, string? String, bool HasAudio);
}
