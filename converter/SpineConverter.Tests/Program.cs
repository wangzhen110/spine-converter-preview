using System.Text.Json.Nodes;
using SpineConverter.Core;

var failures = new List<string>();

Run("converts real 3.8 timeline structures", () =>
{
    const string input = """
    {
      "skeleton": { "spine": "3.8.75" },
      "bones": [{ "name": "root" }],
      "slots": [{ "name": "body", "bone": "root" }],
      "transform": [{ "name": "follow", "bones": ["root"], "target": "root", "rotateMix": 0, "scaleMix": 0 }],
      "skins": [{ "name": "default", "attachments": { "body": { "body": { "width": 64, "height": 96 } } } }],
      "animations": {
        "walk": {
          "bones": { "root": { "rotate": [
            { "time": 0, "angle": -120 },
            { "time": 1, "angle": 120, "curve": 0.25, "c2": 0, "c3": 0.75, "c4": 1 },
            { "time": 2, "angle": 0 }
          ] } },
          "slots": { "body": { "color": [
            { "color": "ffffff00", "curve": 0.25, "c2": 0, "c3": 0.75, "c4": 1 },
            { "time": 1, "color": "ffffffff" }
          ] } },
          "transform": { "follow": [
            { "rotateMix": 0, "translateMix": 0, "scaleMix": 0, "shearMix": 0, "curve": 0.25, "c2": 0, "c3": 0.75, "c4": 1 },
            { "time": 1, "rotateMix": 1, "translateMix": 1, "scaleMix": 1, "shearMix": 1 }
          ] },
          "path": { "road": { "position": [
            { "position": 0, "curve": 0.25, "c2": 0, "c3": 0.75, "c4": 1 },
            { "time": 2, "position": 1 }
          ] } },
          "deform": { "default": { "body": { "body": [{ "vertices": [1, 2] }] } } }
        }
      }
    }
    """;
    var root = Convert(input);
    Equal("4.2.11", root["skeleton"]!["spine"]!.GetValue<string>());
    Equal("default", root["skins"]![0]!["name"]!.GetValue<string>());
    Equal(0d, root["transform"]![0]!["mixRotate"]!.GetValue<double>());
    Equal(-240d, root["animations"]!["walk"]!["bones"]!["root"]!["rotate"]![1]!["value"]!.GetValue<double>());
    Equal(-360d, root["animations"]!["walk"]!["bones"]!["root"]!["rotate"]![2]!["value"]!.GetValue<double>());
    Equal(4, root["animations"]!["walk"]!["bones"]!["root"]!["rotate"]![1]!["curve"]!.AsArray().Count);
    Equal(16, root["animations"]!["walk"]!["slots"]!["body"]!["rgba"]![0]!["curve"]!.AsArray().Count);
    Equal(0d, root["animations"]!["walk"]!["transform"]!["follow"]![0]!["mixX"]!.GetValue<double>());
    Equal(24, root["animations"]!["walk"]!["transform"]!["follow"]![0]!["curve"]!.AsArray().Count);
    Equal(1d, root["animations"]!["walk"]!["path"]!["road"]!["position"]![1]!["value"]!.GetValue<double>());
    Equal(4, root["animations"]!["walk"]!["path"]!["road"]!["position"]![0]!["curve"]!.AsArray().Count);
    Equal(1d, root["animations"]!["walk"]!["attachments"]!["default"]!["body"]!["body"]!["deform"]![0]!["vertices"]![0]!.GetValue<double>());
});

Run("rejects pre-3.8 skin maps", () =>
{
    const string input = """{"skeleton":{"spine":"3.8.75"},"skins":{"default":{}}}""";
    Throws(() => new JsonSkeletonConverter().Convert(input, SpineVersion.Parse("4.2.11")));
});

Run("rejects unsupported source versions", () =>
{
    const string input = """{"skeleton":{"spine":"4.1.24"},"skins":[]}""";
    Throws(() => new JsonSkeletonConverter().Convert(input, SpineVersion.Parse("4.2.11")));
});

Run("rejects unknown timelines instead of corrupting them", () =>
{
    const string input = """
    {"skeleton":{"spine":"3.8.75"},"skins":[],"animations":{"x":{"bones":{"root":{"unknown":[]}}}}}
    """;
    Throws(() => new JsonSkeletonConverter().Convert(input, SpineVersion.Parse("4.2.11")));
});

Run("reads Spine 3.8 binary primitives and metadata", () =>
{
    var bytes = new List<byte>();
    String(bytes, "hash");
    String(bytes, "3.8.75");
    Float(bytes, -12.5f);
    Float(bytes, 25.25f);
    Float(bytes, 64f);
    Float(bytes, 96f);
    bytes.Add(1);
    Float(bytes, 30f);
    String(bytes, "./images/");
    String(bytes, "./audio/");
    VarInt(bytes, 2);
    String(bytes, "body");
    String(bytes, "head");
    var info = Spine38BinaryInspector.Inspect(bytes.ToArray());
    Equal(new SpineVersion(3, 8, 75), info.Version);
    Equal(-12.5f, info.X);
    Equal(2, info.SharedStringCount);
    Equal(bytes.Count, info.HeaderByteCount);
});

Run("writes deterministic Spine 4.2 binary", () =>
{
    const string input = """
    {
      "skeleton": { "spine": "3.8.75" },
      "bones": [{ "name": "root" }],
      "slots": [{ "name": "body", "bone": "root", "attachment": "body" }],
      "skins": [{ "name": "default", "attachments": {
        "body": { "body": { "type": "region", "width": 64, "height": 96 } }
      }}],
      "animations": { "idle": { "bones": { "root": { "rotate": [
        { "angle": 0 }, { "time": 1, "angle": 5 }
      ] } } } }
    }
    """;
    var root = Convert(input);
    var first = new Spine42BinaryWriter().Write(root);
    var second = new Spine42BinaryWriter().Write(root);
    Equal(true, first.Length > 32);
    Equal(System.Convert.ToHexString(first), System.Convert.ToHexString(second));
    var writer = new Spine42BinaryWriter();
    writer.Write(root);
    Throws(() => writer.Write(root));
});

foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

JsonObject Convert(string input) => JsonNode.Parse(
    new JsonSkeletonConverter().Convert(input, SpineVersion.Parse("4.2.11")))!.AsObject();

void Run(string name, Action test)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception exception) { failures.Add($"FAIL {name}: {exception.Message}"); }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected '{expected}', got '{actual}'.");
}

static void Throws(Action action)
{
    try { action(); }
    catch (ConversionException) { return; }
    throw new Exception("Expected ConversionException.");
}

static void String(List<byte> output, string? value)
{
    if (value is null) { VarInt(output, 0); return; }
    var bytes = System.Text.Encoding.UTF8.GetBytes(value);
    VarInt(output, bytes.Length + 1);
    output.AddRange(bytes);
}

static void Float(List<byte> output, float value)
{
    var bytes = BitConverter.GetBytes(value);
    if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
    output.AddRange(bytes);
}

static void VarInt(List<byte> output, int value)
{
    var remaining = (uint)value;
    while (true)
    {
        var current = (byte)(remaining & 0x7f);
        remaining >>= 7;
        if (remaining == 0) { output.Add(current); return; }
        output.Add((byte)(current | 0x80));
    }
}
