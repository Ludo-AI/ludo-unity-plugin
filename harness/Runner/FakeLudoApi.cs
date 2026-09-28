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
    }

    static readonly string[] GenerationPaths =
    {
        "/assets/image", "/assets/sprite/animate", "/assets/3d-model", "/assets/3d-model/rig",
        "/assets/3d-model/animate", "/audio/sound-effect", "/audio/music", "/audio/voice",
        "/audio/speech", "/audio/speech-preset",
    };

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
    public Behavior B = new();
    public readonly List<Request> Requests = new();
    public readonly List<string> Violations = new();   // client broke the contract
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

            string api = path.StartsWith("/api/") ? path.Substring(4) : path;

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

    async Task Submit(HttpListenerContext ctx, Request rec, string api, JsonObject body)
    {
        var findings = contract.Validate("POST", api, body);
        rec.Findings = findings;
        if (!findings.Ok)
        {
            await Json(ctx, rec, 400, new JsonObject { ["message"] = "Request validation failed: " + string.Join("; ", findings.Errors) });
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
        switch (job.Path)
        {
            case "/assets/image":
                int n = b["n"] == null ? 1 : (int)b["n"]!.GetValue<double>();
                var arr = new JsonArray();
                for (int i = 0; i < n; i++) arr.Add(new JsonObject { ["url"] = F($"{job.Id}-{i}.webp"), ["request_id"] = null, ["created_at"] = now });
                return arr;
            case "/assets/sprite/animate":
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
                return s;
            case "/assets/3d-model":
                return new JsonObject { ["model_url"] = F($"{job.Id}-model.glb"), ["snapshots"] = new JsonArray(F($"{job.Id}-snap0.webp"), F($"{job.Id}-snap1.webp")) };
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
