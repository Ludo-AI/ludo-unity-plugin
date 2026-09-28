using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LudoHarness;

// The public API contract, read from the backend's generated openapi-public.json
// (harness/contract/, refreshed by contract/sync.sh). The fake API validates every
// request body against it the way the real server does: a wrong type, enum value or
// missing required field is a 400; an unknown field is silently dropped by the real
// API, which the harness reports because a UI control that sends one does nothing.
public class Contract
{
    readonly JsonObject spec;

    public Contract(string path)
    {
        spec = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }

    public JsonObject Schema(string name) => spec["components"]!["schemas"]![name]!.AsObject();

    public JsonObject Operation(string method, string path)
    {
        var p = spec["paths"]![path];
        return p?[method.ToLowerInvariant()]?.AsObject();
    }

    public string OperationId(string method, string path) => (string)Operation(method, path)?["operationId"];

    public JsonObject RequestSchema(string method, string path)
    {
        var op = Operation(method, path);
        var s = op?["requestBody"]?["content"]?["application/json"]?["schema"];
        return s == null ? null : Resolve(s.AsObject());
    }

    public JsonObject ResponseSchema(string method, string path, string status)
    {
        var s = Operation(method, path)?["responses"]?[status]?["content"]?["application/json"]?["schema"];
        return s == null ? null : Resolve(s.AsObject());
    }

    public JsonObject Resolve(JsonObject s)
    {
        var r = (string)s["$ref"];
        if (r == null) return s;
        return Schema(r.Substring(r.LastIndexOf('/') + 1));
    }

    public JsonObject Property(string method, string path, string field) =>
        RequestSchema(method, path)?["properties"]?[field]?.AsObject();

    public List<string> Enum(string method, string path, string field)
    {
        var e = Property(method, path, field)?["enum"]?.AsArray();
        return e?.Select(v => v!.ToJsonString().Trim('"')).ToList();
    }

    public class Findings
    {
        public List<string> Errors = new();      // the real API answers 400
        public List<string> Dropped = new();     // the real API silently ignores these
        public List<string> Deprecated = new();  // accepted, but marked deprecated
        public bool Ok => Errors.Count == 0;
    }

    public Findings Validate(string method, string path, JsonObject body)
    {
        var f = new Findings();
        var schema = RequestSchema(method, path);
        if (schema == null) { f.Errors.Add($"no request schema for {method} {path}"); return f; }
        var props = schema["properties"]?.AsObject() ?? new JsonObject();
        foreach (var req in schema["required"]?.AsArray() ?? new JsonArray())
        {
            var name = (string)req;
            if (!body.ContainsKey(name) || body[name] == null) f.Errors.Add($"missing required field '{name}'");
        }
        foreach (var (name, value) in body)
        {
            var prop = props[name]?.AsObject();
            if (prop == null) { f.Dropped.Add(name); continue; }
            prop = Resolve(prop);
            if ((bool?)prop["deprecated"] == true) f.Deprecated.Add(name);
            CheckValue(name, prop, value, f.Errors);
        }
        CheckModelDuration(schema, body, f.Errors);
        CheckMargins(schema, body, f.Errors);
        return f;
    }

    static void CheckValue(string name, JsonObject prop, JsonNode value, List<string> errors)
    {
        var type = (string)prop["type"];
        var kind = value?.GetValueKind();
        bool isNumber = kind == System.Text.Json.JsonValueKind.Number;
        switch (type)
        {
            case "string":
                if (kind != System.Text.Json.JsonValueKind.String) { errors.Add($"'{name}' must be a string, got {kind}"); return; }
                break;
            case "boolean":
                if (kind != System.Text.Json.JsonValueKind.True && kind != System.Text.Json.JsonValueKind.False) { errors.Add($"'{name}' must be a boolean, got {kind}"); return; }
                break;
            case "integer":
                if (!isNumber || value!.GetValue<double>() % 1 != 0) { errors.Add($"'{name}' must be an integer, got {value?.ToJsonString()}"); return; }
                break;
            case "number":
                if (!isNumber) { errors.Add($"'{name}' must be a number, got {kind}"); return; }
                if ((string)prop["format"] == "integer" && value!.GetValue<double>() % 1 != 0) { errors.Add($"'{name}' must be a whole number, got {value.ToJsonString()}"); return; }
                break;
        }
        var e = prop["enum"]?.AsArray();
        if (e != null)
        {
            bool match = e.Any(opt => isNumber && opt!.GetValueKind() == System.Text.Json.JsonValueKind.Number
                ? opt.GetValue<double>() == value!.GetValue<double>()
                : opt!.ToJsonString() == value!.ToJsonString());
            if (!match) errors.Add($"'{name}' = {value!.ToJsonString()} is not one of {e.ToJsonString()}");
        }
        if (isNumber)
        {
            double v = value!.GetValue<double>();
            if (prop["minimum"] != null && v < prop["minimum"]!.GetValue<double>()) errors.Add($"'{name}' = {v} is below minimum {prop["minimum"]}");
            if (prop["maximum"] != null && v > prop["maximum"]!.GetValue<double>()) errors.Add($"'{name}' = {v} is above maximum {prop["maximum"]}");
        }
        if (kind == System.Text.Json.JsonValueKind.String && prop["maxLength"] != null && ((string)value!).Length > prop["maxLength"]!.GetValue<int>())
            errors.Add($"'{name}' is longer than {prop["maxLength"]} characters");
    }

    // The duration/model pairing lives in the `duration` description ("- hydra: 3, 3.5, 4").
    // The server re-checks defaulted durations against the model; the harness holds an
    // explicit one to the same list, since the web UI and pricing only know those values.
    static readonly Regex DurationLine = new(@"^\s*-\s*([a-z0-9-]+):\s*([0-9., ]+)$", RegexOptions.Multiline);

    public static Dictionary<string, List<double>> ModelDurations(JsonObject schema)
    {
        var map = new Dictionary<string, List<double>>();
        var desc = (string)schema["properties"]?["duration"]?["description"];
        if (desc == null) return map;
        foreach (Match m in DurationLine.Matches(desc))
            map[m.Groups[1].Value] = m.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToList();
        return map;
    }

    void CheckModelDuration(JsonObject schema, JsonObject body, List<string> errors)
    {
        if (!body.ContainsKey("duration") || !body.ContainsKey("model")) return;
        var map = ModelDurations(schema);
        var model = (string)body["model"];
        if (model == null || !map.TryGetValue(model, out var allowed)) return;
        double d = body["duration"]!.GetValue<double>();
        if (!allowed.Any(a => Math.Abs(a - d) < 1e-6)) errors.Add($"duration {d} is not offered for model '{model}' (allowed: {string.Join(", ", allowed)})");
    }

    // margin_ratio_mode rules (AnimateSpritePayload description, MediaGeneration.resolveMarginParams):
    // "manual" needs a margin value; "auto"/"none" together with a value is a 400.
    static void CheckMargins(JsonObject schema, JsonObject body, List<string> errors)
    {
        if (schema["properties"]?["margin_ratio_mode"] == null) return;
        string mode = (string)body["margin_ratio_mode"];
        bool hasValue = body["margin_ratio"] != null || body["margin_ratio_horizontal"] != null || body["margin_ratio_vertical"] != null;
        if (mode == "manual" && !hasValue) errors.Add("margin_ratio_mode \"manual\" without a margin value");
        if ((mode == "auto" || mode == "none") && hasValue) errors.Add($"margin_ratio_mode \"{mode}\" together with a margin value");
        foreach (var k in new[] { "margin_ratio", "margin_ratio_horizontal", "margin_ratio_vertical" })
            if (body[k] != null && (body[k]!.GetValue<double>() < 0 || body[k]!.GetValue<double>() > 1)) errors.Add($"'{k}' must be between 0 and 1");
    }

    // Models the public docs call legacy ("`blitz` (Blitz) - legacy since ...").
    public HashSet<string> LegacyModels()
    {
        var desc = (string)spec["info"]?["description"] ?? "";
        return new HashSet<string>(Regex.Matches(desc, @"`([a-z0-9-]+)` \([^)]*\) - legacy since").Select(m => m.Groups[1].Value));
    }
}
