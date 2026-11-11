using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace LudoHarness;

// An in-process stand-in for api.ludo.ai that behaves like the real public API since
// the 2026-09-10 async flip (src/api/controllers/common/public-api-jobs.js):
//   POST <generation>        -> 202 PublicJob, or 200 result when async:false
//   GET  /assets/jobs/{id}   -> PublicJob; ?wait=N long-polls; poll_after_ms while live
// Request bodies are validated against the contract snapshot. Result URLs point back
// at this server and serve bytes with the real formats (webp, mp3, glb, mp4).
public class FakeLudoApi : IDisposable
{
    public class Behavior
    {
        public int PollsUntilDone = 2;          // polls answered queued/running before terminal
        public int PollAfterMs = 60;            // advertised hint (real: 2000 queued / 5000 running)
        public JsonObject FailWith;             // job ends failed with this error {status, message}
        public bool NeverFinish;                // job never reaches a terminal state
        public int SubmitStatus;                // non-zero: reject the POST with this status
        public string SubmitMessage;
        public string SubmitCode;
        public int PollRateLimitOnce;           // 429 + Retry-After on the Nth poll (1-based), once
        public bool CloseSocketOnFirstPoll;     // drop the connection on the first poll
        public int ResultFileStatus = 200;      // status for downloaded result files
        public int SubmitGatewayErrors;         // first N submits: load balancer 502, nothing created
        public int LoseSubmitResponses;         // next N submits: job IS created, response lost as a 502
        public bool PreviewSuperseded;          // the Directional Sprites preview answers 204 (a newer one won)
        public int StylesStatus = 200;          // status for GET directional-styles
        public int AnimationsStatus = 200;      // status for GET /assets/animations (My Animations)
        public List<JsonObject> MyAnimations;   // the account's animations at the start (newest first)
        public int AccessoryStatus;             // non-zero: the accessory fit / attach answer this
    }

    public class Request
    {
        public string Method, Path, Query;
        public JsonObject Body;
        public Contract.Findings Findings;
        public int Status;
        public double At;
    }

    public class Job
    {
        public string Id, Path, TaskType;
        public JsonObject Body;
        public int Polls;
        public string Status = "queued";
        public long CreatedAt;
        public double LastAnswerAt;
        public int LastPollAfterMs;
        public bool LastWas429;
        public JsonArray Made;  // createAnimation: the animations this job made
    }

    static readonly string[] GenerationPaths =
    {
        "/assets/image", "/assets/sprite/animate", "/assets/3d-model", "/assets/3d-model/rig",
        "/assets/3d-model/animate", "/audio/sound-effect", "/audio/music", "/audio/voice",
        "/audio/speech", "/audio/speech-preset",
        "/assets/sprite/directional", "/assets/sprite/directional-stills", "/assets/image/upscale",
        "/assets/animation", "/assets/3d-model/animate-preset", "/assets/sprite/transfer-motion",
        "/assets/image/edit", "/assets/image/normal-map", "/assets/3d-model/image",
    };

    // Free synchronous POSTs: validated like the rest, answered at once, never a job.
    static readonly string[] SyncPaths = { "/assets/sprite/directional-preview" };

    readonly HttpListener listener = new();
    readonly Contract contract;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly ConcurrentDictionary<string, Job> jobs = new();
    int nextJob;
    int pollCount;
    int gatewayErrors, lostResponses;
    public int JobsCreated => jobs.Count;
    bool socketDropped;

    public readonly string ApiKey = "test-key-123";
    // Builds from before 1.0.4 (run.sh --ref) have no PluginVersion and send no version header.
    public bool RequireToolVersion;
    public Behavior B = new();
    public readonly List<Request> Requests = new();
    public readonly List<string> Violations = new();   // client broke the contract
    // The account's own animations (Motion documents), newest first, as listMyAnimations
    // answers them; createAnimation adds to it when its job succeeds.
    public readonly List<JsonObject> Animations = new();
    int nextAnimation;
    public string BaseUrl { get; }                      // what the plugin's apiUrl is set to
    public string Root { get; }

    public FakeLudoApi(Contract contract)
    {
        this.contract = contract;
        int port = FreePort();
        Root = $"http://127.0.0.1:{port}";
        BaseUrl = Root + "/api";
        listener.Prefixes.Add(Root + "/");
        listener.Start();
        _ = Task.Run(Loop);
    }

    public void Reset(Behavior b = null)
    {
        B = b ?? new Behavior();
        lock (Requests) Requests.Clear();
        lock (Violations) Violations.Clear();
        jobs.Clear();
        lock (Animations)
        {
            Animations.Clear();
            if (B.MyAnimations != null) Animations.AddRange(B.MyAnimations.Select(a => a.DeepClone().AsObject()));
        }
        pollCount = 0;
        gatewayErrors = 0;
        lostResponses = 0;
        socketDropped = false;
    }

    public List<Request> Posts(string path = null)
    {
        lock (Requests) return Requests.Where(r => r.Method == "POST" && (path == null || r.Path == path)).ToList();
    }

    public List<Request> Polls()
    {
        lock (Requests) return Requests.Where(r => r.Method == "GET" && r.Path.StartsWith("/api/assets/jobs/")).ToList();
    }

    static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    async Task Loop()
    {
        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch { return; }
            _ = Task.Run(() => Handle(ctx));
        }
    }

    double Now => clock.Elapsed.TotalMilliseconds;

    async Task Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        string path = req.Url!.AbsolutePath;
        var rec = new Request { Method = req.HttpMethod, Path = path, Query = req.Url.Query, At = Now };
        try
        {
            if (path.StartsWith("/files/")) { await ServeFile(ctx, path.Substring(7)); return; }

            lock (Requests) Requests.Add(rec);

            string auth = req.Headers["Authorization"];
            if (auth != "ApiKey " + ApiKey)
            {
                await Json(ctx, rec, 401, new JsonObject { ["message"] = "Invalid API key" });
                return;
            }
            if (req.Headers["x-ludo-tool"] != "unity")
                lock (Violations) Violations.Add($"{req.HttpMethod} {path} without x-ludo-tool: unity");
            if (RequireToolVersion && string.IsNullOrEmpty(req.Headers["x-ludo-tool-version"]))
                lock (Violations) Violations.Add($"{req.HttpMethod} {path} without x-ludo-tool-version");

            string api = path.StartsWith("/api/") ? path.Substring(4) : path;

            if (req.HttpMethod == "GET" && api == "/assets/sprite/directional-styles")
            {
                if (B.StylesStatus != 200) { await Json(ctx, rec, B.StylesStatus, new JsonObject { ["message"] = "Styles are unavailable" }); return; }
                // A snapshot of the live list (contract/directional-styles.json), thumbnails served here.
                var styles = JsonNode.Parse(File.ReadAllText(Path.Combine(contract.Dir, "directional-styles.json")))!.AsObject();
                foreach (var st in styles["styles"]!.AsArray()) st!["thumbnail_url"] = F($"thumb-{st["id"]}.webp");
                await Json(ctx, rec, 200, styles);
                return;
            }

            if (req.HttpMethod == "GET" && api == "/assets/sprite/animation-presets")
            {
                var anims = new JsonArray();
                foreach (var (id, name, cat) in new[] { ("walk_relaxed", "Walk Forward (Relaxed)", "Movement: Walking"), ("run_fast", "Run Forward (Fast)", "Movement: Running & Sprinting"), ("idle_breathing", "Idle (Breathing)", "Poses: Idles & Fidgets") })
                    anims.Add(new JsonObject { ["id"] = id, ["name"] = name, ["category"] = cat, ["description"] = name + ".", ["duration"] = 3, ["preview_url"] = F($"preset-{id}.mp4") });
                await Json(ctx, rec, 200, new JsonObject { ["animations"] = anims, ["perspectives"] = new JsonArray("side"), ["directions"] = new JsonArray("N", "NE", "E", "SE", "S", "SW", "W", "NW") });
                return;
            }

            if (req.HttpMethod == "GET" && api == "/assets/animations")
            {
                if (B.AnimationsStatus != 200) { await Json(ctx, rec, B.AnimationsStatus, new JsonObject { ["message"] = "My Animations are unavailable" }); return; }
                var q = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                int limit = int.TryParse(q["limit"], out var l) ? l : 50;
                if (limit < 1 || limit > 200) { await Json(ctx, rec, 400, new JsonObject { ["message"] = "limit must be 1 to 200" }); return; }
                var list = new JsonArray();
                lock (Animations) foreach (var a in Animations.Take(limit)) list.Add(a.DeepClone());
                await Json(ctx, rec, 200, new JsonObject { ["animations"] = list });
                return;
            }

            if (req.HttpMethod == "POST" && (api == "/assets/sprite/directional-accessory-fit" || api == "/assets/sprite/directional-accessories"))
            {
                // Free and synchronous, like the preview.
                string text = await new StreamReader(req.InputStream, Encoding.UTF8).ReadToEndAsync();
                var body = JsonNode.Parse(text)!.AsObject();
                rec.Body = body;
                var findings = contract.Validate("POST", api, body);
                rec.Findings = findings;
                if (!findings.Ok) { await Json(ctx, rec, 400, new JsonObject { ["message"] = "Request validation failed: " + string.Join("; ", findings.Errors) }); return; }
                if (B.AccessoryStatus != 0) { await Json(ctx, rec, B.AccessoryStatus, new JsonObject { ["message"] = "This model has no skeleton. Rig it first." }); return; }
                if (api.EndsWith("-fit"))
                {
                    string kind = (string)body["kind"] ?? (((string)body["name"] ?? "").Contains("sword") ? "blade" : "other");
                    string point = (string)body["point"] ?? (kind is "bow" or "shield" ? "left_hand" : kind is "helmet" or "hat" ? "head" : kind == "back" ? "back" : "right_hand");
                    string bone = (string)body["bone"] ?? (point == "right_hand" ? "mixamorig:RightHand" : point == "left_hand" ? "mixamorig:LeftHand" : "mixamorig:Head");
                    double size = body["size"]?.GetValue<double>() ?? 100;
                    var fit = new JsonObject
                    {
                        ["kind"] = kind, ["point"] = point, ["bone"] = bone, ["size"] = size,
                        ["move"] = body["move"]?.DeepClone() ?? new JsonObject { ["forward"] = 0, ["up"] = 0, ["out"] = 0 },
                        ["turn"] = body["turn"]?.DeepClone() ?? new JsonObject { ["forward"] = 0, ["up"] = 0, ["out"] = 0 },
                        ["flip"] = body["flip"]?.GetValue<bool>() ?? false, ["grip"] = body["grip"]?.GetValue<bool>() ?? true,
                        ["placement"] = new JsonObject { ["bone"] = bone, ["point"] = point, ["kind"] = kind, ["flipped"] = false, ["grip"] = true, ["base_scale"] = 0.42, ["position"] = new JsonArray(0.01, 0.08, 0.0), ["quaternion"] = new JsonArray(0, 0, 0, 1), ["scale"] = new JsonArray(0.42 * size / 100, 0.42 * size / 100, 0.42 * size / 100), ["fit_size"] = size },
                        ["checks"] = new JsonObject { ["sink"] = 0.4, ["inside"] = 1.2, ["grip_gap"] = 0.8 },
                        ["points"] = new JsonArray(new JsonObject { ["point"] = "right_hand", ["bone"] = "mixamorig:RightHand" }, new JsonObject { ["point"] = "left_hand", ["bone"] = "mixamorig:LeftHand" }, new JsonObject { ["point"] = "head", ["bone"] = "mixamorig:Head" }),
                    };
                    if (body["preview"]?.GetValue<bool>() != false)
                    {
                        fit["model_url"] = F($"fit-{Guid.NewGuid():N}.glb");
                        fit["previews"] = new JsonArray(
                            new JsonObject { ["direction"] = "SW", ["num_frames"] = 4, ["fps"] = 8, ["frame_urls"] = new JsonArray(F("fit-SW-0.webp"), F("fit-SW-1.webp"), F("fit-SW-2.webp"), F("fit-SW-3.webp")) },
                            new JsonObject { ["direction"] = "SE", ["num_frames"] = 4, ["fps"] = 8, ["frame_urls"] = new JsonArray(F("fit-SE-0.webp"), F("fit-SE-1.webp"), F("fit-SE-2.webp"), F("fit-SE-3.webp")) });
                    }
                    await Json(ctx, rec, 200, fit);
                }
                else
                {
                    var items = new JsonArray();
                    foreach (var it in body["items"]!.AsArray())
                        items.Add(new JsonObject { ["name"] = (string)it!["name"] ?? "item", ["placement"] = it["placement"]?.DeepClone() ?? new JsonObject { ["bone"] = "mixamorig:RightHand" } });
                    bool ours = ((string)body["model"] ?? "").StartsWith(Root);
                    await Json(ctx, rec, 200, new JsonObject { ["model_url"] = F($"with-items-{Guid.NewGuid():N}.glb"), ["saved"] = ours && body["save_on_model"]?.GetValue<bool>() != false, ["items"] = items, ["kept"] = new JsonArray() });
                }
                return;
            }

            if (req.HttpMethod == "POST" && api == "/assets/image/pixel-art")
            {
                // Free and synchronous; no detectable grid on our fake images unless pixel_size is given.
                string text = await new StreamReader(req.InputStream, Encoding.UTF8).ReadToEndAsync();
                var body = JsonNode.Parse(text)!.AsObject();
                rec.Body = body;
                var findings = contract.Validate("POST", api, body);
                rec.Findings = findings;
                if (!findings.Ok) { await Json(ctx, rec, 400, new JsonObject { ["message"] = "Request validation failed: " + string.Join("; ", findings.Errors) }); return; }
                if (body["pixel_size"] == null) { findings.Errors.Add("no pixel grid detected"); await Json(ctx, rec, 400, new JsonObject { ["message"] = "No pixel grid was detected in this image: pass a pixel_size" }); return; }
                double size = body["pixel_size"]!.GetValue<double>();
                await Json(ctx, rec, 200, new JsonObject { ["url"] = F($"pixelart-{Guid.NewGuid():N}.png"), ["pixel_size"] = size, ["columns"] = (int)(512 / size), ["rows"] = (int)(384 / size) });
                return;
            }

            if (req.HttpMethod == "POST" && SyncPaths.Contains(api))
            {
                string text = await new StreamReader(req.InputStream, Encoding.UTF8).ReadToEndAsync();
                var body = JsonNode.Parse(text)!.AsObject();
                rec.Body = body;
                var findings = contract.Validate("POST", api, body);
                CheckStyleParams(body, findings);
                rec.Findings = findings;
                if (!findings.Ok) { await Json(ctx, rec, 400, new JsonObject { ["message"] = "Request validation failed: " + string.Join("; ", findings.Errors) }); return; }
                var rule = AnimationRules(api, body);
                if (rule != null) { findings.Errors.Add(rule.Value.message); await Json(ctx, rec, rule.Value.status, new JsonObject { ["message"] = rule.Value.message }); return; }
                if (body.ContainsKey("request_id")) lock (Violations) Violations.Add($"POST {api} sends request_id: the preview is not a job");
                if (B.PreviewSuperseded) { rec.Status = 204; ctx.Response.StatusCode = 204; ctx.Response.Close(); return; }
                var previews = new JsonArray();
                foreach (var d in body["directions"]?.AsArray() ?? new JsonArray(JsonValue.Create("SW")))
                    previews.Add(new JsonObject
                    {
                        ["direction"] = (string)d, ["width"] = 256, ["height"] = 256, ["num_frames"] = 3, ["fps"] = 12,
                        ["frame_urls"] = new JsonArray(F($"preview-{d}-0.webp"), F($"preview-{d}-1.webp"), F($"preview-{d}-2.webp")),
                        ["render_3d"] = new JsonObject { ["style_id"] = (string)body["style_id"] ?? "render_3d", ["style_version"] = 1, ["direction"] = (string)d },
                    });
                await Json(ctx, rec, 200, new JsonObject { ["previews"] = previews });
                return;
            }

            if (req.HttpMethod == "POST" && GenerationPaths.Contains(api))
            {
                string text = await new StreamReader(req.InputStream, Encoding.UTF8).ReadToEndAsync();
                JsonObject body;
                try { body = JsonNode.Parse(text)!.AsObject(); }
                catch { await Json(ctx, rec, 400, new JsonObject { ["message"] = "Body is not a JSON object" }); return; }
                rec.Body = body;
                if (req.ContentType == null || !req.ContentType.StartsWith("application/json"))
                    lock (Violations) Violations.Add($"POST {api} Content-Type '{req.ContentType}'");
                await Submit(ctx, rec, api, body);
                return;
            }

            if (req.HttpMethod == "GET" && api.StartsWith("/assets/jobs/"))
            {
                await Poll(ctx, rec, api.Substring("/assets/jobs/".Length));
                return;
            }

            await Json(ctx, rec, 404, new JsonObject { ["message"] = $"Cannot {req.HttpMethod} {api}" });
        }
        catch (Exception e)
        {
            lock (Violations) Violations.Add("fake server crashed: " + e);
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
        }
    }

    // Like the renderer: style_params must name the style's settings, each with one of its
    // option values (same JSON type), a bool for a bool, or #rrggbb for a color.
    void CheckStyleParams(JsonObject body, Contract.Findings findings)
    {
        if (!body.ContainsKey("style_id") && !body.ContainsKey("style_params")) return;
        var styles = JsonNode.Parse(File.ReadAllText(Path.Combine(contract.Dir, "directional-styles.json")))!["styles"]!.AsArray();
        string id = (string)body["style_id"] ?? "render_3d";
        var style = styles.FirstOrDefault(st => (string)st!["id"] == id);
        if (style == null) { findings.Errors.Add($"style_id '{id}' is not a style"); return; }
        if (body["style_params"] is not JsonObject given) return;
        var settings = style["settings"]!.AsArray().Concat(style["advanced_settings"]?.AsArray() ?? new JsonArray()).ToList();
        foreach (var (name, value) in given)
        {
            var setting = settings.FirstOrDefault(x => (string)x!["name"] == name);
            if (setting == null) { findings.Errors.Add($"style_params.{name}: '{id}' has no such setting"); continue; }
            string kind = (string)setting["kind"];
            if (kind == "bool") { if (value?.GetValueKind() is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)) findings.Errors.Add($"style_params.{name} must be true/false"); continue; }
            bool listed = setting["options"]!.AsArray().Any(o => o!["value"]!.ToJsonString() == value!.ToJsonString());
            bool hex = kind == "color" && value?.GetValueKind() == System.Text.Json.JsonValueKind.String && System.Text.RegularExpressions.Regex.IsMatch((string)value!, "^#[0-9a-fA-F]{6}$");
            if (!listed && !hex) findings.Errors.Add($"style_params.{name} = {value?.ToJsonString()} is not one of its options");
        }
    }

    // What the public handlers check beyond the schema (prometheus-server master,
    // src/api/controllers/public-api.js): one animation at most, a mirror only on an
    // Animation Clip (preset_id) or an account animation (animation_id), and an
    // animation_id that is the account's own (404 otherwise). Checked before a job exists.
    (int status, string message)? AnimationRules(string api, JsonObject body)
    {
        bool has(string k) => body[k] is JsonValue v && !(v.GetValueKind() == System.Text.Json.JsonValueKind.String && (string)v == "");
        bool mirror = body["mirror"]?.GetValueKind() == System.Text.Json.JsonValueKind.True;
        switch (api)
        {
            case "/assets/sprite/directional":
            case "/assets/sprite/directional-preview":
                var sources = new[] { "animation_url", "preset_id", "animation_id", "model_animation" }.Where(has).ToList();
                if (sources.Count > 1) return (400, "Provide at most one animation: " + string.Join(", ", sources));
                if (mirror && !has("preset_id") && !has("animation_id")) return (400, "'mirror' needs a 'preset_id' or an 'animation_id'");
                break;
            case "/assets/3d-model/animate-preset":
                if (has("preset_id") == has("animation_id")) return (400, "Provide either 'preset_id' or 'animation_id'");
                break;
            case "/assets/sprite/transfer-motion":
                bool byView = has("preset_id") || has("animation_id");
                if (!has("video") && !(byView && has("perspective") && has("direction")))
                    return (400, "Either 'video', or 'preset_id' or 'animation_id' with 'perspective' and 'direction', must be provided");
                if (has("preset_id") && has("animation_id")) return (400, "Provide only one of: preset_id, animation_id");
                if (!byView && mirror) return (400, "'mirror' needs a 'preset_id' or an 'animation_id'");
                break;
            case "/assets/3d-model/image":
                // MediaGeneration.assert3DModelImageInput: a prompt, an image, or both.
                if (string.IsNullOrWhiteSpace((string)body["prompt"]) && !has("image")) return (400, "Provide a 'prompt', an 'image', or both");
                return null;
            case "/assets/image/edit":
                // editImage's prepare: mask_mode needs a mask, a prompt unless erasing,
                // no reference when erasing, and a mask the size of the image.
                bool erase = (string)body["mask_mode"] == "erase";
                if (has("mask_mode") && !has("mask")) return (400, "Error: mask_mode needs a mask");
                if (!erase && string.IsNullOrWhiteSpace((string)body["prompt"])) return (400, "Error: prompt is required unless mask_mode is erase");
                if (erase && has("reference_image")) return (400, "Error: reference_image is not used with mask_mode erase");
                if (has("mask"))
                {
                    var imageSize = PngSize((string)body["image"]);
                    var maskSize = PngSize((string)body["mask"]);
                    if (maskSize == null) return (400, "Error: the mask must be a PNG");
                    if (imageSize != null && imageSize != maskSize) return (400, $"Error: the mask is {maskSize.Value.w}x{maskSize.Value.h} but the image is {imageSize.Value.w}x{imageSize.Value.h}; they must be the same size");
                }
                return null;
            case "/assets/animation":
                if (body["augment_prompt"]?.GetValueKind() == System.Text.Json.JsonValueKind.False && !has("duration"))
                    return (400, "'duration' is required when 'augment_prompt' is false");
                return null;
            default:
                return null;
        }
        if (has("animation_id"))
        {
            string id = (string)body["animation_id"];
            lock (Animations) if (!Animations.Any(a => (string)a["id"] == id)) return (404, "Animation not found");
        }
        return null;
    }

    // Width and height of a PNG data URI (its IHDR), or null for anything else.
    static (int w, int h)? PngSize(string dataUri)
    {
        const string prefix = "data:image/png;base64,";
        if (dataUri == null || !dataUri.StartsWith(prefix)) return null;
        try
        {
            var b = Convert.FromBase64String(dataUri.Substring(prefix.Length));
            if (b.Length < 24 || b[0] != 0x89 || b[1] != 'P') return null;
            return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
        }
        catch { return null; }
    }

    async Task Submit(HttpListenerContext ctx, Request rec, string api, JsonObject body)
    {
        var findings = contract.Validate("POST", api, body);
        CheckStyleParams(body, findings);
        rec.Findings = findings;
        if (!findings.Ok)
        {
            await Json(ctx, rec, 400, new JsonObject { ["message"] = "Request validation failed: " + string.Join("; ", findings.Errors) });
            return;
        }
        var rule = AnimationRules(api, body);
        if (rule != null)
        {
            findings.Errors.Add(rule.Value.message);
            await Json(ctx, rec, rule.Value.status, new JsonObject { ["message"] = rule.Value.message });
            return;
        }
        if (gatewayErrors < B.SubmitGatewayErrors)
        {
            gatewayErrors++;
            await Html(ctx, rec, 502);
            return;
        }

        // Like jobService.enqueue: (user, request_id) maps to one job; reuse for another operation is a 409.
        string requestId = (string)body["request_id"];
        var existing = requestId == null ? null : jobs.Values.FirstOrDefault(j => (string)j.Body["request_id"] == requestId);
        if (existing != null)
        {
            if (existing.Path != api)
            {
                await Json(ctx, rec, 409, new JsonObject { ["message"] = $"request_id '{requestId}' was already used for a different operation.", ["code"] = "REQUEST_ID_IN_USE" });
                return;
            }
            ctx.Response.AddHeader("X-Ludo-Job-Id", existing.Id);
            await Json(ctx, rec, 202, Project(existing));
            return;
        }

        if (B.SubmitStatus != 0)
        {
            var err = new JsonObject { ["message"] = B.SubmitMessage ?? "Rejected" };
            if (B.SubmitCode != null) err["code"] = B.SubmitCode;
            await Json(ctx, rec, B.SubmitStatus, err);
            return;
        }

        var job = new Job
        {
            Id = "job_" + Interlocked.Increment(ref nextJob).ToString("D4"),
            Path = api,
            TaskType = contract.OperationId("POST", api),
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        jobs[job.Id] = job;
        if (lostResponses < B.LoseSubmitResponses)
        {
            lostResponses++;
            await Html(ctx, rec, 502);
            return;
        }
        ctx.Response.AddHeader("X-Ludo-Job-Id", job.Id);

        bool isAsync = body["async"] == null || (bool)body["async"];
        if (isAsync)
        {
            await Json(ctx, rec, 202, Project(job));
            return;
        }
        // The deprecated sync facade: block until terminal, return the bare result.
        job.Status = B.FailWith != null ? "failed" : (B.NeverFinish ? "running" : "succeeded");
        if (job.Status == "succeeded") await Json(ctx, rec, 200, Result(job));
        else if (job.Status == "failed") await Json(ctx, rec, (int)B.FailWith["status"]!, new JsonObject { ["message"] = (string)B.FailWith["message"] });
        else await Json(ctx, rec, 202, Project(job));
    }

    async Task Poll(HttpListenerContext ctx, Request rec, string id)
    {
        int n = Interlocked.Increment(ref pollCount);
        if (!jobs.TryGetValue(id, out var job))
        {
            await Json(ctx, rec, 404, new JsonObject { ["message"] = "No such job for this API key" });
            return;
        }

        // Contract check: a client must wait poll_after_ms between polls unless it long-polls.
        int wait = 0;
        var q = System.Web.HttpUtility.ParseQueryString(ctx.Request.Url!.Query);
        int.TryParse(q["wait"], out wait);
        if (wait > 60) lock (Violations) Violations.Add($"poll with wait={wait} (max 60)");
        // A client must wait poll_after_ms between polls (long-polling makes that cheap, not
        // optional), and after a 429 it must wait Retry-After whatever it sends.
        if (job.LastAnswerAt > 0 && Now - job.LastAnswerAt < job.LastPollAfterMs * 0.9)
            lock (Violations) Violations.Add($"polled {id} {Now - job.LastAnswerAt:F0}ms after the last answer; {(job.LastWas429 ? "Retry-After" : "poll_after_ms")} was {job.LastPollAfterMs}ms");
        job.LastWas429 = false;

        if (B.CloseSocketOnFirstPoll && !socketDropped)
        {
            socketDropped = true;
            rec.Status = -1;
            ctx.Response.Abort();
            return;
        }
        if (B.PollRateLimitOnce > 0 && n == B.PollRateLimitOnce)
        {
            ctx.Response.AddHeader("Retry-After", "1");
            await Json(ctx, rec, 429, new JsonObject { ["message"] = "Too many requests, please try again later." });
            job.LastAnswerAt = Now;
            job.LastPollAfterMs = 1000;
            job.LastWas429 = true;
            return;
        }

        job.Polls++;
        if (job.Status == "queued" || job.Status == "running")
        {
            if (B.NeverFinish) job.Status = "running";
            else if (job.Polls >= B.PollsUntilDone) job.Status = B.FailWith != null ? "failed" : "succeeded";
            else job.Status = job.Polls == 1 ? "queued" : "running";
        }
        if (wait > 0 && (job.Status == "queued" || job.Status == "running"))
            await Task.Delay(Math.Min(wait * 1000, 150)); // a short hold stands in for the long-poll

        await Json(ctx, rec, 200, Project(job));
        job.LastAnswerAt = Now;
        job.LastPollAfterMs = job.Status == "queued" || job.Status == "running" ? B.PollAfterMs : 0;
    }

    JsonObject Project(Job job)
    {
        var o = new JsonObject
        {
            ["id"] = job.Id,
            ["task_type"] = job.TaskType,
            ["status"] = job.Status,
            ["request_id"] = null,
            ["created_at"] = job.CreatedAt,
            ["finished_at"] = job.Status is "succeeded" or "failed" ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : null,
            ["credits_charged"] = job.Status == "failed" ? 0 : 1,
        };
        if (job.Status == "succeeded") o["result"] = Result(job);
        if (job.Status == "failed") o["error"] = B.FailWith!.DeepClone();
        if (job.Status is "queued" or "running") o["poll_after_ms"] = B.PollAfterMs;
        return o;
    }

    string F(string name) => $"{Root}/files/{name}";

    // Result bodies shaped like the contract's 200 responses (ImageResult[], SpriteResult,
    // Model3DResult, Rig3DModelResult, AnimationCandidates, AudioResult).
    JsonNode Result(Job job)
    {
        var b = job.Body;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool normals = b["save_normals"] != null && (bool)b["save_normals"];
        switch (job.Path)
        {
            case "/assets/image":
            case "/assets/image/edit":
                int n = b["n"] == null ? 1 : (int)b["n"]!.GetValue<double>();
                var arr = new JsonArray();
                for (int i = 0; i < n; i++)
                {
                    var img = new JsonObject { ["url"] = F($"{job.Id}-{i}.webp"), ["request_id"] = null, ["created_at"] = now };
                    if (normals) img["normal_map_url"] = F($"{job.Id}-{i}-normal.webp");
                    arr.Add(img);
                }
                return arr;
            case "/assets/animation":
                // Made once per job (polls re-project the result), then on the account's list.
                if (job.Made == null)
                {
                    int count = b["num_variants"] == null ? 1 : (int)b["num_variants"]!.GetValue<double>();
                    job.Made = new JsonArray();
                    for (int i = 0; i < count; i++)
                    {
                        string id = "6a1f" + Interlocked.Increment(ref nextAnimation).ToString("D20");
                        job.Made.Add(new JsonObject
                        {
                            ["id"] = id, ["name"] = "generated-move-" + id.Substring(id.Length - 3), ["prompt"] = (string)b["prompt"],
                            ["caption"] = "A person " + (string)b["prompt"], ["seed"] = 7 + i, ["duration"] = 2.5, ["fps"] = 30, ["loop"] = false,
                            ["preview_url"] = F($"{id}-preview.mp4"), ["created_at"] = now,
                        });
                    }
                    lock (Animations) Animations.InsertRange(0, job.Made.Select(a => a!.DeepClone().AsObject()));
                }
                return new JsonObject { ["animations"] = job.Made.DeepClone() };
            case "/assets/3d-model/animate-preset":
                var retargeted = new JsonObject
                {
                    ["clip_name"] = (string)b["preset_id"] ?? "my-animation", ["mode"] = "retarget",
                    ["glb_url"] = F($"{job.Id}-retarget.glb"), ["preview_url"] = F($"{job.Id}-retarget.mp4"),
                };
                if (b["preset_id"] != null) retargeted["preset_id"] = (string)b["preset_id"];
                if (b["animation_id"] != null) retargeted["animation_id"] = (string)b["animation_id"];
                if (b["mirror"] != null && (bool)b["mirror"]) retargeted["mirror"] = true;
                return new JsonObject { ["animations"] = new JsonArray(retargeted) };
            case "/assets/sprite/animate":
            case "/assets/sprite/transfer-motion":
                var s = new JsonObject
                {
                    ["spritesheet_url"] = F($"{job.Id}-sheet.webp"),
                    ["video_url"] = F($"{job.Id}-preview.mp4"),
                    ["num_frames"] = b["frames"] == null ? 36 : b["frames"]!.GetValue<double>(),
                    ["num_cols"] = 6,
                    ["num_rows"] = 6,
                    ["duration"] = b["duration"] == null ? 3 : b["duration"]!.GetValue<double>(),
                    ["request_id"] = null,
                    ["created_at"] = now,
                };
                if (b["gif"] != null && (bool)b["gif"]) s["gif_url"] = F($"{job.Id}-preview.gif");
                if ((string)b["model"] is null or "hydra") s["audio_url"] = F($"{job.Id}-sfx.mp3");
                if (normals) s["normal_map_url"] = F($"{job.Id}-sheet-normal.webp");
                return s;
            case "/assets/3d-model":
                var m3 = new JsonObject { ["model_url"] = F($"{job.Id}-model.glb"), ["snapshots"] = new JsonArray(F($"{job.Id}-snap0.webp"), F($"{job.Id}-snap1.webp")) };
                if (b["rig"] != null && (bool)b["rig"]) m3["rigged"] = true;
                return m3;
            case "/assets/sprite/directional":
                var sheets = new JsonArray();
                foreach (var d in b["directions"]?.AsArray() ?? new JsonArray(JsonValue.Create("SW")))
                {
                    var sheet = new JsonObject
                    {
                        ["direction"] = (string)d, ["spritesheet_url"] = F($"{job.Id}-sheet-{d}.webp"), ["video_url"] = F($"{job.Id}-{d}.mp4"),
                        ["num_frames"] = 36, ["num_cols"] = 6, ["num_rows"] = 6, ["fps"] = 12,
                        ["render_3d"] = new JsonObject { ["style_id"] = (string)b["style_id"] ?? "render_3d", ["style_version"] = 1, ["direction"] = (string)d },
                    };
                    if (b["gif"] != null && (bool)b["gif"]) sheet["gif_url"] = F($"{job.Id}-{d}.gif");
                    if (normals) sheet["normal_map_url"] = F($"{job.Id}-sheet-{d}-normal.webp");
                    sheets.Add(sheet);
                }
                return new JsonObject { ["sheets"] = sheets };
            case "/assets/sprite/directional-stills":
                var stills = new JsonArray();
                foreach (var d in b["directions"]?.AsArray() ?? new JsonArray(JsonValue.Create("SW")))
                {
                    var still = new JsonObject { ["direction"] = (string)d, ["url"] = F($"{job.Id}-still-{d}.webp"), ["width"] = 1024, ["height"] = 1024 };
                    if (normals) still["normal_map_url"] = F($"{job.Id}-still-{d}-normal.webp");
                    stills.Add(still);
                }
                return new JsonObject { ["images"] = stills };
            case "/assets/3d-model/image":
                return new JsonObject { ["url"] = F($"{job.Id}-render3d.webp"), ["request_id"] = (string)b["request_id"], ["created_at"] = now };
            case "/assets/image/normal-map":
                return new JsonObject { ["url"] = (string)b["image"] is string src && src.StartsWith("http") ? src : F($"{job.Id}-source.png"), ["normal_map_url"] = F($"{job.Id}-normal.webp") };
            case "/assets/image/upscale":
                return new JsonObject { ["url"] = F($"{job.Id}-upscaled.png"), ["request_id"] = (string)b["request_id"], ["created_at"] = now };
            case "/assets/3d-model/rig":
                return new JsonObject { ["model_url"] = F($"{job.Id}-rigged.glb"), ["rigged"] = true };
            case "/assets/3d-model/animate":
                int variants = b["num_variants"] == null ? 4 : (int)b["num_variants"]!.GetValue<double>();
                var anims = new JsonArray();
                for (int i = 0; i < variants; i++)
                    anims.Add(new JsonObject
                    {
                        ["clip_name"] = $"clip_{i + 1}", ["prompt"] = (string)b["prompt"], ["mode"] = (string)b["mode"] ?? "rot_trans",
                        ["seed"] = 1000 + i, ["glb_url"] = F($"{job.Id}-anim{i}.glb"), ["preview_url"] = F($"{job.Id}-anim{i}.mp4"),
                        ["motion"] = 0.42, ["fit_rmse"] = 0.01,
                    });
                return new JsonObject { ["animations"] = anims };
            default: // audio
                return new JsonObject { ["url"] = F($"{job.Id}-audio.mp3"), ["type"] = "sfx", ["duration"] = 2.5, ["request_id"] = null, ["created_at"] = now };
        }
    }

    public static byte[] FileBytes(string name)
    {
        string ext = Path.GetExtension(name).ToLowerInvariant();
        var ms = new MemoryStream();
        switch (ext)
        {
            case ".webp":
                ms.Write("RIFF"u8); ms.Write(new byte[] { 0x24, 0, 0, 0 }); ms.Write("WEBPVP8 "u8); ms.Write(new byte[24]);
                break;
            case ".mp3":
                ms.Write("ID3"u8); ms.Write(new byte[] { 4, 0, 0, 0, 0, 0, 0 }); ms.Write(new byte[] { 0xFF, 0xFB, 0x90, 0x64 }); ms.Write(new byte[64]);
                break;
            case ".glb":
                ms.Write("glTF"u8); ms.Write(new byte[] { 2, 0, 0, 0, 20, 0, 0, 0 }); ms.Write(new byte[8]);
                break;
            case ".mp4":
                ms.Write(new byte[] { 0, 0, 0, 0x18 }); ms.Write("ftypisom"u8); ms.Write(new byte[12]);
                break;
            case ".gif":
                ms.Write("GIF89a"u8); ms.Write(new byte[16]);
                break;
            default:
                ms.Write(UnityEngine.FakeImage.Png(64, 64));
                break;
        }
        return ms.ToArray();
    }

    async Task ServeFile(HttpListenerContext ctx, string name)
    {
        if (B.ResultFileStatus != 200)
        {
            ctx.Response.StatusCode = B.ResultFileStatus;
            ctx.Response.Close();
            return;
        }
        var bytes = FileBytes(name);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = Path.GetExtension(name) switch
        {
            ".webp" => "image/webp", ".mp3" => "audio/mpeg", ".glb" => "model/gltf-binary", ".mp4" => "video/mp4", ".gif" => "image/gif", _ => "image/png",
        };
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    // What Google's load balancer answers when the backend is unreachable.
    async Task Html(HttpListenerContext ctx, Request rec, int status)
    {
        rec.Status = status;
        var bytes = Encoding.UTF8.GetBytes("<html><head><title>502 Server Error</title></head><body><h1>Error: Server Error</h1><h2>The server encountered a temporary error and could not complete your request.<p>Please try again in 30 seconds.</h2></body></html>");
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "text/html; charset=UTF-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    async Task Json(HttpListenerContext ctx, Request rec, int status, JsonNode body)
    {
        rec.Status = status;
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        try { listener.Stop(); listener.Close(); } catch { }
    }
}
