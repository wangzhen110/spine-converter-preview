using System.Text.Json.Nodes;

namespace SpineConverter.Core;

public sealed partial class Spine38BinaryJsonReader
{
    private SpineBinaryInput _input = null!;
    private bool _nonessential;
    private readonly List<string> _strings = [];
    private readonly List<string> _bones = [];
    private readonly List<string> _slots = [];
    private readonly List<string> _ik = [];
    private readonly List<string> _transform = [];
    private readonly List<string> _paths = [];
    private readonly List<string> _skins = [];
    private readonly List<EventSetup> _events = [];

    public JsonObject Read(ReadOnlyMemory<byte> data)
    {
        Reset(data);
        var root = new JsonObject();
        ReadMetadata(root);
        ReadBones(root);
        ReadSlots(root);
        ReadIkConstraints(root);
        ReadTransformConstraints(root);
        ReadPathConstraints(root);
        ReadSkins(root);
        ReadEvents(root);
        ReadAnimations(root);
        if (_input.Remaining != 0)
            throw new ConversionException($"Spine binary has {_input.Remaining} unread bytes.");
        return root;
    }

    private void Reset(ReadOnlyMemory<byte> data)
    {
        _input = new SpineBinaryInput(data);
        _strings.Clear();
        _bones.Clear();
        _slots.Clear();
        _ik.Clear();
        _transform.Clear();
        _paths.Clear();
        _skins.Clear();
        _events.Clear();
    }

    private void ReadMetadata(JsonObject root)
    {
        var hash = _input.ReadString();
        var version = _input.ReadString() ?? throw new ConversionException("Missing Spine version.");
        if (!SpineVersion.Parse(version).IsLine(3, 8))
            throw new ConversionException($"Expected Spine 3.8 binary, got {version}.");
        var skeleton = new JsonObject
        {
            ["hash"] = hash,
            ["spine"] = version,
            ["x"] = _input.ReadSingle(),
            ["y"] = _input.ReadSingle(),
            ["width"] = _input.ReadSingle(),
            ["height"] = _input.ReadSingle(),
        };
        _nonessential = _input.ReadBoolean();
        if (_nonessential)
        {
            skeleton["fps"] = _input.ReadSingle();
            skeleton["images"] = _input.ReadString();
            skeleton["audio"] = _input.ReadString();
        }
        root["skeleton"] = skeleton;

        var count = Count("shared strings");
        for (var i = 0; i < count; i++)
            _strings.Add(_input.ReadString() ?? throw new ConversionException($"Shared string {i} is null."));
    }

    private void ReadBones(JsonObject root)
    {
        var output = new JsonArray();
        var count = Count("bones");
        for (var i = 0; i < count; i++)
        {
            var name = RequiredString($"bone {i}");
            _bones.Add(name);
            var bone = new JsonObject { ["name"] = name };
            if (i > 0) bone["parent"] = Name(_bones, _input.ReadVarInt(true), $"bone {i} parent");
            Set(bone, "rotation", _input.ReadSingle(), 0);
            Set(bone, "x", _input.ReadSingle(), 0);
            Set(bone, "y", _input.ReadSingle(), 0);
            Set(bone, "scaleX", _input.ReadSingle(), 1);
            Set(bone, "scaleY", _input.ReadSingle(), 1);
            Set(bone, "shearX", _input.ReadSingle(), 0);
            Set(bone, "shearY", _input.ReadSingle(), 0);
            Set(bone, "length", _input.ReadSingle(), 0);
            var transform = _input.ReadVarInt(true);
            string[] transformNames = ["normal", "onlyTranslation", "noRotationOrReflection", "noScale", "noScaleOrReflection"];
            if (transform < 0 || transform >= transformNames.Length)
                throw new ConversionException($"Invalid bone transform mode {transform}.");
            if (transform != 0) bone["transform"] = transformNames[transform];
            if (_input.ReadBoolean()) bone["skin"] = true;
            if (_nonessential) bone["color"] = Rgba(_input.ReadInt32());
            output.Add(bone);
        }
        root["bones"] = output;
    }

    private void ReadSlots(JsonObject root)
    {
        var output = new JsonArray();
        var count = Count("slots");
        for (var i = 0; i < count; i++)
        {
            var name = RequiredString($"slot {i}");
            _slots.Add(name);
            var slot = new JsonObject
            {
                ["name"] = name,
                ["bone"] = Name(_bones, _input.ReadVarInt(true), $"slot {i} bone"),
            };
            var color = _input.ReadInt32();
            if (unchecked((uint)color) != 0xffffffff) slot["color"] = Rgba(color);
            var dark = _input.ReadInt32();
            if (dark != -1) slot["dark"] = Rgb(dark);
            var attachment = RefString();
            if (attachment is not null) slot["attachment"] = attachment;
            var blend = _input.ReadVarInt(true);
            string[] blendNames = ["normal", "additive", "multiply", "screen"];
            if (blend < 0 || blend >= blendNames.Length)
                throw new ConversionException($"Invalid slot blend mode {blend}.");
            if (blend != 0) slot["blend"] = blendNames[blend];
            output.Add(slot);
        }
        if (output.Count > 0) root["slots"] = output;
    }

    private void ReadIkConstraints(JsonObject root)
    {
        var output = new JsonArray();
        var count = Count("IK constraints");
        for (var i = 0; i < count; i++)
        {
            var name = RequiredString($"IK constraint {i}");
            _ik.Add(name);
            var value = new JsonObject { ["name"] = name };
            Set(value, "order", _input.ReadVarInt(true), 0);
            if (_input.ReadBoolean()) value["skin"] = true;
            value["bones"] = ReadNameArray(_bones, "IK bones");
            value["target"] = Name(_bones, _input.ReadVarInt(true), "IK target");
            Set(value, "mix", _input.ReadSingle(), 1);
            Set(value, "softness", _input.ReadSingle(), 0);
            if (unchecked((sbyte)_input.ReadByte()) < 0) value["bendPositive"] = false;
            if (_input.ReadBoolean()) value["compress"] = true;
            if (_input.ReadBoolean()) value["stretch"] = true;
            if (_input.ReadBoolean()) value["uniform"] = true;
            output.Add(value);
        }
        if (output.Count > 0) root["ik"] = output;
    }

    private void ReadTransformConstraints(JsonObject root)
    {
        var output = new JsonArray();
        var count = Count("transform constraints");
        for (var i = 0; i < count; i++)
        {
            var name = RequiredString($"transform constraint {i}");
            _transform.Add(name);
            var value = new JsonObject { ["name"] = name };
            Set(value, "order", _input.ReadVarInt(true), 0);
            if (_input.ReadBoolean()) value["skin"] = true;
            value["bones"] = ReadNameArray(_bones, "transform bones");
            value["target"] = Name(_bones, _input.ReadVarInt(true), "transform target");
            if (_input.ReadBoolean()) value["local"] = true;
            if (_input.ReadBoolean()) value["relative"] = true;
            Set(value, "rotation", _input.ReadSingle(), 0);
            Set(value, "x", _input.ReadSingle(), 0);
            Set(value, "y", _input.ReadSingle(), 0);
            Set(value, "scaleX", _input.ReadSingle(), 0);
            Set(value, "scaleY", _input.ReadSingle(), 0);
            Set(value, "shearY", _input.ReadSingle(), 0);
            Set(value, "rotateMix", _input.ReadSingle(), 1);
            Set(value, "translateMix", _input.ReadSingle(), 1);
            Set(value, "scaleMix", _input.ReadSingle(), 1);
            Set(value, "shearMix", _input.ReadSingle(), 1);
            output.Add(value);
        }
        if (output.Count > 0) root["transform"] = output;
    }

    private void ReadPathConstraints(JsonObject root)
    {
        var output = new JsonArray();
        var count = Count("path constraints");
        for (var i = 0; i < count; i++)
        {
            var name = RequiredString($"path constraint {i}");
            _paths.Add(name);
            var value = new JsonObject { ["name"] = name };
            Set(value, "order", _input.ReadVarInt(true), 0);
            if (_input.ReadBoolean()) value["skin"] = true;
            value["bones"] = ReadNameArray(_bones, "path bones");
            value["target"] = Name(_slots, _input.ReadVarInt(true), "path target");
            string[] positionNames = ["fixed", "percent"];
            string[] spacingNames = ["length", "fixed", "percent"];
            string[] rotateNames = ["tangent", "chain", "chainScale"];
            SetEnum(value, "positionMode", _input.ReadVarInt(true), positionNames, 1);
            SetEnum(value, "spacingMode", _input.ReadVarInt(true), spacingNames, 0);
            SetEnum(value, "rotateMode", _input.ReadVarInt(true), rotateNames, 0);
            Set(value, "rotation", _input.ReadSingle(), 0);
            Set(value, "position", _input.ReadSingle(), 0);
            Set(value, "spacing", _input.ReadSingle(), 0);
            Set(value, "rotateMix", _input.ReadSingle(), 1);
            Set(value, "translateMix", _input.ReadSingle(), 1);
            output.Add(value);
        }
        if (output.Count > 0) root["path"] = output;
    }

    private JsonArray ReadNameArray(List<string> names, string label)
    {
        var output = new JsonArray();
        var count = Count(label);
        for (var i = 0; i < count; i++) output.Add(Name(names, _input.ReadVarInt(true), label));
        return output;
    }

    private int Count(string label)
    {
        var value = _input.ReadVarInt(true);
        if (value is < 0 or > 10_000_000) throw new ConversionException($"Invalid {label} count: {value}.");
        return value;
    }

    private string RequiredString(string label) =>
        _input.ReadString() ?? throw new ConversionException($"Missing {label} name.");

    private string? RefString()
    {
        var encoded = _input.ReadVarInt(true);
        if (encoded == 0) return null;
        return Name(_strings, encoded - 1, "shared string");
    }

    private static string Name(List<string> values, int index, string label)
    {
        if (index < 0 || index >= values.Count)
            throw new ConversionException($"Invalid {label} index {index}; collection size is {values.Count}.");
        return values[index];
    }

    private static void Set(JsonObject output, string name, double value, double defaultValue)
    {
        if (value != defaultValue) output[name] = value;
    }

    private static void Set(JsonObject output, string name, int value, int defaultValue)
    {
        if (value != defaultValue) output[name] = value;
    }

    private static void SetEnum(JsonObject output, string name, int value, string[] names, int defaultValue)
    {
        if (value < 0 || value >= names.Length) throw new ConversionException($"Invalid {name} value {value}.");
        if (value != defaultValue) output[name] = names[value];
    }

    private static string Rgba(int value) => unchecked((uint)value).ToString("x8");
    private static string Rgb(int value) => (unchecked((uint)value) & 0x00ffffff).ToString("x6");

    private sealed record EventSetup(string Name, int Int, float Float, string? String, bool HasAudio);
}
