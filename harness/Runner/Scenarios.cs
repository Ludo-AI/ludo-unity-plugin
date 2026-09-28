using System.Collections;
using System.Text.Json.Nodes;
using UnityStubs;

namespace LudoHarness;

public class ScenarioFailed : Exception
{
    public ScenarioFailed(string message) : base(message) { }
}

public class Ctx
{
    public FakeLudoApi Api;          // null in live mode
    public Contract Contract;
    public string ApiUrl, ApiKey;
    public string OutDir;
    public bool Live => Api == null;
    public readonly List<string> Failures = new();
    public readonly List<string> Notes = new();

    public PluginDriver NewPlugin(FakeLudoApi.Behavior behavior = null)
    {
        Api?.Reset(behavior);
        Directory.CreateDirectory(OutDir);
        // Save dialogs answer with a fresh path in the output folder, named as the plugin
        // proposed (so the extension the plugin chose is what lands on disk).
        Harness.SaveFilePanel = (title, name, ext) => Path.Combine(OutDir, name);
        Harness.SaveFolderPanel = title => { var d = Path.Combine(OutDir, "folder-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(d); return d; };
        Harness.DataPath = Path.Combine(OutDir, "Assets");
        Directory.CreateDirectory(Harness.DataPath);
        return new PluginDriver(ApiUrl, ApiKey);
    }

    public void Check(bool ok, string message) { if (!ok) Failures.Add(message); }
    public void Require(bool ok, string message) { if (!ok) throw new ScenarioFailed(message); }
    public void Note(string message) => Notes.Add(message);

    // No contract violations, no dropped/deprecated fields, no error dialogs.
    public void CheckClean(PluginDriver p, string path = null)
    {
        foreach (var d in p.ErrorDialogs) Failures.Add("error dialog: " + d);
        if (Api == null) return;
        lock (Api.Violations) foreach (var v in Api.Violations) Failures.Add("contract: " + v);
        foreach (var r in Api.Posts(path == null ? null : "/api" + path))
        {
            if (r.Findings == null) continue;
            foreach (var e in r.Findings.Errors) Failures.Add($"{r.Path}: {e}");
            foreach (var d in r.Findings.Dropped) Failures.Add($"{r.Path}: sends '{d}', which the API silently ignores");
            foreach (var d in r.Findings.Deprecated) Failures.Add($"{r.Path}: sends deprecated field '{d}'");
        }
    }
}

public record Scenario(string Group, string Name, Action<Ctx> Run, bool LiveToo = false);

public static class Scenarios
{
    static string FakeFile(Ctx c, string name) => c.Api == null ? null : $"{c.Api.Root}/files/{name}";

    // Magic bytes vs file extension: a mismatch is a file Unity will import wrongly or not at all.
    static string Sniff(byte[] b)
    {
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "webp";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'A' && b[10] == 'V' && b[11] == 'E') return "wav";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G') return "png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8) return "jpg";
        if (b.Length >= 3 && b[0] == 'I' && b[1] == 'D' && b[2] == '3') return "mp3";
        if (b.Length >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0) return "mp3";
        if (b.Length >= 4 && b[0] == 'g' && b[1] == 'l' && b[2] == 'T' && b[3] == 'F') return "glb";
        if (b.Length >= 3 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return "gif";
        if (b.Length >= 8 && b[4] == 'f' && b[5] == 't' && b[6] == 'y' && b[7] == 'p') return "mp4";
        if (b.Length >= 4 && b[0] == 'O' && b[1] == 'g' && b[2] == 'g' && b[3] == 'S') return "ogg";
        return "unknown";
    }

    static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["jpg"] = new[] { "jpg", "jpeg" },
    };

    // Clicks a save button and checks exactly one new file landed, non-empty, with an
    // extension that matches its content.
    static string ClickSave(Ctx c, PluginDriver p, string label, string expectKind)
    {
        var before = new HashSet<string>(Directory.GetFiles(c.OutDir, "*", SearchOption.AllDirectories));
        int dialogsBefore = Harness.Dialogs.Count;
        p.Click(label);
        c.Require(p.RunUntilIdle(60), $"'{label}' never finished");
        var added = Directory.GetFiles(c.OutDir, "*", SearchOption.AllDirectories).Where(f => !before.Contains(f)).ToList();
        var dialogs = Harness.Dialogs.Skip(dialogsBefore).ToList();
        foreach (var d in dialogs.Where(d => d.Title.Contains("Error") || d.Title.Contains("Failed"))) c.Failures.Add($"'{label}': dialog {d}");
        if (added.Count == 0) { c.Failures.Add($"'{label}' saved no file (dialogs: {string.Join(" / ", dialogs)})"); return null; }
        string file = added[0];
        var bytes = File.ReadAllBytes(file);
        string ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
        string kind = Sniff(bytes);
        c.Check(bytes.Length > 0, $"'{label}' wrote an empty file");
        bool extOk = ext == kind || (Aliases.TryGetValue(kind, out var al) && al.Contains(ext));
        c.Check(extOk, $"'{label}' saved {kind} bytes as .{ext} ({Path.GetFileName(file)})");
        if (expectKind != null) c.Check(kind == expectKind || (expectKind == "image" && (kind == "png" || kind == "jpg")), $"'{label}' saved {kind}, expected {expectKind}");
        return file;
    }

    static void Tab(PluginDriver p, string tab, string subField = null, string sub = null)
    {
        p.Set("currentTab", tab);
        if (subField != null) p.Set(subField, sub);
        p.Gui();
    }

    static JsonObject LastBody(Ctx c, string path) => c.Api?.Posts("/api" + path).LastOrDefault()?.Body;

    // ---------------------------------------------------------------- generators

    static void Images(Ctx c)
    {
        var p = c.NewPlugin();
        Tab(p, "Sprites", "currentSpriteTab", "Image");
        p.Set("imagePrompt", "a knight with a blue shield, side view");
        p.Set("imageCount", 2);
        p.Set("imageType", "sprite");
        p.Click("Generate Image(s)");
        c.Require(p.RunUntilIdle(c.Live ? 300 : 30), "image generation never finished: " + p.Status);
        var images = p.Get("generatedImages");
        c.Require(PluginDriver.Count(images) == 2, $"expected 2 images, got {PluginDriver.Count(images)}; status: {p.Status}; dialogs: {string.Join(" / ", p.Dialogs)}");
        foreach (var img in (IEnumerable)images)
            c.Check(!string.IsNullOrEmpty((string)PluginDriver.Prop(img, "Url")), "image without url");
        if (c.Live) LiveState.ImageUrl = (string)PluginDriver.Prop(((IList)images)[0], "Url");
        c.Check(PluginDriver.Count(p.Get("imagePreviewCache")) == 2, $"previews loaded: {PluginDriver.Count(p.Get("imagePreviewCache"))}/2");
        c.CheckClean(p, "/assets/image");
        ClickSave(c, p, "Save to File", "image");
        p.Click("Select");
        c.Check(p.Get("selectedSprite") != null, "'Select' did not select the image for animation");
    }

    static void AnimateSprite(Ctx c)
    {
        var p = c.NewPlugin();
        Tab(p, "Sprites", "currentSpriteTab", "ImageToSpritesheet");
        p.Set("spritesheetInitialImageUrl", c.Live ? LiveState.ImageUrl : FakeFile(c, "input-knight.webp"));
        c.Require(!string.IsNullOrEmpty(p.Get<string>("spritesheetInitialImageUrl")), "no input image (live: run images first)");
        p.Set("spritesheetMotionHint", "walking");
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(c.Live ? 900 : 30), "animation never finished: " + p.Status);
        var sheet = p.Get("currentSpritesheet");
        c.Require(sheet != null, $"no spritesheet; status: {p.Status}; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.Check(!string.IsNullOrEmpty((string)PluginDriver.Prop(sheet, "SpriteSheetB64")), $"spritesheet has no url; status: {p.Status}");
        c.Check(p.Get("spritesheetPreviewTexture") != null, "spritesheet preview not loaded");
        var body = LastBody(c, "/assets/sprite/animate");
        if (body != null)
        {
            var legacy = c.Contract.LegacyModels();
            string model = (string)body["model"];
            c.Check(model != null && !legacy.Contains(model), $"animates with legacy model '{model}'");
        }
        c.CheckClean(p, "/assets/sprite/animate");
        ClickSave(c, p, "Save Spritesheet", "image");
        foreach (var label in new[] { "Save GIF Preview" })
            if (ButtonShown(p, label)) ClickSave(c, p, label, "gif");
    }

    static bool ButtonShown(PluginDriver p, string label) { p.Gui(); return Harness.Buttons.Contains(label); }

    static void Model3DChain(Ctx c)
    {
        var p = c.NewPlugin();
        Tab(p, "Models", "currentModelTab", "Create");
        p.Set("model3DImageUrl", c.Live ? LiveState.ImageUrl : FakeFile(c, "input-crate.png"));
        p.Click("Create 3D Model");
        c.Require(p.RunUntilIdle(c.Live ? 1200 : 30), "3D creation never finished: " + p.Status);
        var model = p.Get("current3DModel");
        c.Require(model != null && !string.IsNullOrEmpty((string)PluginDriver.Prop(model, "ModelUrl")), $"no 3D model; status: {p.Status}; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.Check(PluginDriver.Count(p.Get("model3DSnapshotTextures")) >= 1, "3D snapshots not loaded");
        c.CheckClean(p, "/assets/3d-model");
        ClickSave(c, p, "Download 3D Model", "glb");

        p.Click("Use for Rigging");
        c.Require(p.Get("currentModelTab").ToString() == "Rig", "'Use for Rigging' did not switch to the Rig tab");
        p.Click("Rig Model");
        c.Require(p.RunUntilIdle(c.Live ? 1200 : 30), "rigging never finished: " + p.Status);
        var rigged = p.Get("currentRigged3DModel");
        c.Require(rigged != null && !string.IsNullOrEmpty((string)PluginDriver.Prop(rigged, "ModelUrl")), $"no rigged model; status: {p.Status}; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.CheckClean(p, "/assets/3d-model/rig");
        ClickSave(c, p, "Download Rigged Model", "glb");

        p.Click("Use for Animation");
        c.Require(p.Get("currentModelTab").ToString() == "Animate", "'Use for Animation' did not switch to the Animate tab");
        p.Set("model3DAnimateNumVariants", 2);
        p.Click("Animate 3D Model");
        c.Require(p.RunUntilIdle(c.Live ? 1200 : 30), "3D animation never finished: " + p.Status);
        var anims = p.Get("current3DAnimations");
        c.Require(PluginDriver.Count(anims) == 2, $"expected 2 animation candidates, got {PluginDriver.Count(anims)}; status: {p.Status}");
        c.CheckClean(p, "/assets/3d-model/animate");
        ClickSave(c, p, "Download Animation GLB", "glb");
        var folderFiles = Directory.GetDirectories(c.OutDir, "folder-*").SelectMany(d => Directory.GetFiles(d)).Count();
        p.Click("Download All Animation GLBs");
        c.Require(p.RunUntilIdle(60), "download-all never finished");
        var after = Directory.GetDirectories(c.OutDir, "folder-*").SelectMany(d => Directory.GetFiles(d)).ToList();
        c.Check(after.Count - folderFiles == 2, $"'Download All Animation GLBs' saved {after.Count - folderFiles} files, expected 2");
        foreach (var f in after) c.Check(Sniff(File.ReadAllBytes(f)) == "glb", $"{Path.GetFileName(f)} is not a GLB");
    }

    static void Audio(Ctx c, string sub, string path, Action<PluginDriver> fill, string generate, string resultField, string save)
    {
        var p = c.NewPlugin();
        Tab(p, "Audio", "currentAudioTab", sub);
        fill(p);
        p.Click(generate);
        c.Require(p.RunUntilIdle(c.Live ? 600 : 30), $"{sub} never finished: {p.Status}");
        var audio = p.Get(resultField);
        string url = (string)PluginDriver.Prop(audio, "Url");
        c.Require(!string.IsNullOrEmpty(url), $"{sub}: no audio url; status: '{p.Status}'; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.CheckClean(p, path);
        ClickSave(c, p, save, "mp3");
    }

    // ---------------------------------------------------------------- failure paths

    static void JobFails(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { FailWith = new JsonObject { ["status"] = 400, ["message"] = "The prompt was flagged by the content filter" } });
        Tab(p, "Audio", "currentAudioTab", "SoundEffect");
        p.Set("soundEffectDescription", "sword clash");
        p.Click("Generate Sound Effect");
        c.Require(p.RunUntilIdle(30), "stuck after a failed job: " + p.Status);
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("content filter")), $"failure reason not shown; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.Check(p.Status.Contains("content filter"), $"status doesn't carry the reason: '{p.Status}'");
        var r = p.Get("currentSoundEffect");
        c.Check(r == null || string.IsNullOrEmpty((string)PluginDriver.Prop(r, "Url")), "a failed job left a result behind");
        c.Check(!Harness.Dialogs.Any(d => d.Message.Contains("successfully")) && !p.Status.Contains("success"), $"failed job reported as success: '{p.Status}'");
    }

    static void QueueFull(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior
        {
            SubmitStatus = 429,
            SubmitCode = "PENDING_JOBS_LIMIT",
            SubmitMessage = "Your generation queue is full: up to 50 generations can be waiting or running at once.",
        });
        Tab(p, "Sprites", "currentSpriteTab", "Image");
        p.Set("imagePrompt", "a potion");
        p.Click("Generate Image(s)");
        c.Require(p.RunUntilIdle(30), "stuck after 429: " + p.Status);
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("queue is full")), $"429 reason not shown; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.Check(PluginDriver.Count(p.Get("generatedImages")) == 0, "results appeared after a 429");
    }

    static void BadApiKey(Ctx c)
    {
        var p = c.NewPlugin();
        p.Set("apiKey", "wrong-key");
        Tab(p, "Audio", "currentAudioTab", "Music");
        p.Set("musicDescription", "calm village theme");
        p.Click("Generate Music");
        c.Require(p.RunUntilIdle(30), "stuck after 401: " + p.Status);
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("Invalid API key")), $"401 reason not shown; dialogs: {string.Join(" / ", p.Dialogs)}");
    }

    static void ValidationError(Ctx c)
    {
        var p = c.NewPlugin();
        Tab(p, "Audio", "currentAudioTab", "Voice");
        p.Set("voiceDescription", "old wizard");
        p.Set("voiceText", "You shall not pass");
        p.Set("voiceType", "robot"); // not in the enum
        p.Set("voiceTypeOptions", new[] { "human", "non-human", "robot" });
        p.Click("Generate Voice");
        c.Require(p.RunUntilIdle(30), "stuck after 400: " + p.Status);
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("is not one of")), $"400 reason not shown; dialogs: {string.Join(" / ", p.Dialogs)}");
    }

    static void PollRateLimited(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollRateLimitOnce = 1, PollsUntilDone = 2 });
        Tab(p, "Audio", "currentAudioTab", "SoundEffect");
        p.Set("soundEffectDescription", "coin pickup");
        p.Click("Generate Sound Effect");
        c.Require(p.RunUntilIdle(30), "stuck after a 429 on the poll: " + p.Status);
        c.Check(!string.IsNullOrEmpty((string)PluginDriver.Prop(p.Get("currentSoundEffect"), "Url")), $"a 429 on a status poll lost the result; status: '{p.Status}'; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.CheckClean(p);
    }

    static void PollConnectionDrop(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { CloseSocketOnFirstPoll = true, PollsUntilDone = 2 });
        Tab(p, "Audio", "currentAudioTab", "SoundEffect");
        p.Set("soundEffectDescription", "door creak");
        p.Click("Generate Sound Effect");
        c.Require(p.RunUntilIdle(30), "stuck after a dropped poll: " + p.Status);
        c.Check(!string.IsNullOrEmpty((string)PluginDriver.Prop(p.Get("currentSoundEffect"), "Url")), $"a dropped status poll lost the result; status: '{p.Status}'; dialogs: {string.Join(" / ", p.Dialogs)}");
    }

    static void LongJobShowsProgress(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { NeverFinish = true, PollAfterMs = 100 });
        Tab(p, "Models", "currentModelTab", "Create");
        p.Set("model3DImageUrl", FakeFile(c, "input-crate.png"));
        c.Require(p.Has("jobTimeoutSeconds"), "no client-side job timeout (field jobTimeoutSeconds)");
        p.Set("jobTimeoutSeconds", 3f);
        p.Click("Create 3D Model");
        p.RunFor(1.5);
        c.Check(p.Processing, "gave up on a running job too early");
        c.Check(p.Status.Contains("running") || p.Status.Contains("queued") || p.Status.Contains("s)"), $"no progress in status while running: '{p.Status}'");
        c.Require(p.RunUntilIdle(15), "never gave up on a job that doesn't finish");
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("job_")), $"timeout message doesn't name the job; dialogs: {string.Join(" / ", p.Dialogs)}");
        int polls = c.Api.Polls().Count;
        c.Check(polls >= 2 && polls < 60, $"{polls} polls in ~3s");
        lock (c.Api.Violations) foreach (var v in c.Api.Violations) c.Failures.Add("contract: " + v);
    }

    // ---------------------------------------------------------------- UI / contract sweep

    static void EveryScreenRenders(Ctx c)
    {
        var p = c.NewPlugin();
        var screens = new (string tab, string field, string[] subs)[]
        {
            ("Sprites", "currentSpriteTab", new[] { "Image", "ImageToSpritesheet" }),
            ("Models", "currentModelTab", new[] { "Create", "Rig", "Animate" }),
            ("Audio", "currentAudioTab", new[] { "SoundEffect", "Music", "Voice", "Speech", "SpeechPreset" }),
            ("Settings", null, new string[] { null }),
        };
        foreach (var (tab, field, subs) in screens)
            foreach (var sub in subs)
            {
                try { Tab(p, tab, field, sub); p.Gui(); }
                catch (Exception e) { c.Failures.Add($"{tab}/{sub}: OnGUI threw {e.GetType().Name}: {e.Message}"); }
            }
        // No API key: the key screen must render and nothing else.
        var q = c.NewPlugin();
        q.Set("apiKey", "");
        q.Gui();
        c.Check(Harness.Buttons.Contains("Save API Key"), "key screen missing without an API key");
    }

    // Every dropdown that feeds an API field may only offer values the API accepts.
    static readonly (string field, string path, string param)[] OptionArrays =
    {
        ("imageTypeOptions", "/assets/image", "image_type"),
        ("artStyleOptions", "/assets/image", "art_style"),
        ("perspectiveOptions", "/assets/image", "perspective"),
        ("aspectRatioOptions", "/assets/image", "aspect_ratio"),
        ("spritesheetModelOptions", "/assets/sprite/animate", "model"),
        ("spritesheetImageTypeOptions", "/assets/sprite/animate", "image_type"),
        ("frameOptions", "/assets/sprite/animate", "frames"),
        ("frameSizeOptions", "/assets/sprite/animate", "frame_size"),
        ("marginModeOptions", "/assets/sprite/animate", "margin_ratio_mode"),
        ("textureSizeOptions", "/assets/3d-model", "texture_size"),
        ("textureTypeOptions", "/assets/3d-model", "texture_type"),
        ("model3DRigTypeOptions", "/assets/3d-model/rig", "rig_type"),
        ("model3DJointNamingOptions", "/assets/3d-model/rig", "joint_naming"),
        ("model3DAnimateModeOptions", "/assets/3d-model/animate", "mode"),
        ("voiceTypeOptions", "/audio/voice", "type"),
        ("voicePresetOptions", "/audio/speech-preset", "voice_preset_id"),
        ("emotionOptions", "/audio/speech-preset", "emotion"),
        ("languageOptions", "/audio/speech-preset", "language"),
    };

    static readonly HashSet<string> UiOnlyValues = new() { "default", "Any style", "Any perspective" };

    static void DropdownsMatchContract(Ctx c)
    {
        var p = c.NewPlugin();
        var legacy = c.Contract.LegacyModels();
        foreach (var (field, path, param) in OptionArrays)
        {
            if (!p.Has(field)) { c.Note($"{field}: not in this build"); continue; }
            var allowed = c.Contract.Enum("POST", path, param);
            c.Require(allowed != null, $"contract has no enum for {path} {param}");
            foreach (var v in (IEnumerable)p.Get(field))
            {
                string s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
                if (UiOnlyValues.Contains(s) && !allowed.Contains(s)) continue;
                c.Check(allowed.Contains(s), $"{field} offers '{s}', not accepted by {path} {param}");
                if (param == "model") c.Check(!legacy.Contains(s), $"{field} offers legacy model '{s}'");
            }
            var dups = ((IEnumerable)p.Get(field)).Cast<object>().GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            c.Check(dups.Count == 0, $"{field} lists duplicates: {string.Join(", ", dups)}");
        }
    }

    // Every model the animate tab offers, with every duration it offers for that model,
    // must produce a payload the API accepts.
    static void AnimateModelDurationSweep(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        c.Require(p.Has("spritesheetModelDurations"), "no per-model duration table (field spritesheetModelDurations)");
        Tab(p, "Sprites", "currentSpriteTab", "ImageToSpritesheet");
        p.Set("spritesheetInitialImageUrl", FakeFile(c, "input.webp"));
        var table = (IDictionary)p.Get("spritesheetModelDurations");
        int runs = 0;
        foreach (var model in (IEnumerable)p.Get("spritesheetModelOptions"))
        {
            c.Check(table.Contains(model), $"no durations listed for model '{model}'");
            if (!table.Contains(model)) continue;
            foreach (var d in (IEnumerable)table[model]!)
            {
                p.Set("spritesheetModel", model);
                p.Set("spritesheetDuration", d);
                p.Click("Animate Sprite");
                c.Require(p.RunUntilIdle(20), $"{model}@{d}s never finished");
                var body = LastBody(c, "/assets/sprite/animate");
                c.Check(body != null && (string)body["model"] == (string)model && Math.Abs(body["duration"]!.GetValue<double>() - Convert.ToDouble(d)) < 1e-6,
                    $"{model}@{d}: UI changed the selection to {body?["model"]}@{body?["duration"]}");
                runs++;
            }
        }
        c.CheckClean(p, "/assets/sprite/animate");
        c.Note($"{runs} model/duration combinations sent");
    }

    // Every art style, perspective and aspect ratio the image tab offers round-trips.
    static void ImageOptionSweep(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        Tab(p, "Sprites", "currentSpriteTab", "Image");
        p.Set("imagePrompt", "a chest");
        int runs = 0;
        foreach (var (field, target) in new[] { ("artStyleOptions", "imageArtStyle"), ("perspectiveOptions", "imagePerspective"), ("imageTypeOptions", "imageType"), ("aspectRatioOptions", "imageAspectRatio") })
            foreach (var v in (IEnumerable)p.Get(field))
            {
                p.Set(target, v);
                p.Click("Generate Image(s)");
                c.Require(p.RunUntilIdle(20), $"{target}={v} never finished");
                runs++;
            }
        c.CheckClean(p, "/assets/image");
        c.Note($"{runs} image option payloads sent");
    }

    // ---------------------------------------------------------------- registry

    public static class LiveState
    {
        public static string ImageUrl;
    }

    public static List<Scenario> All() => new()
    {
        new("generate", "images: generate 2, preview, save, select", Images, LiveToo: true),
        new("generate", "sprite animation: animate, preview, save", AnimateSprite, LiveToo: true),
        new("generate", "3D: create -> rig -> animate, with downloads", Model3DChain, LiveToo: true),
        new("generate", "audio: sound effect", c => Audio(c, "SoundEffect", "/audio/sound-effect", p => { p.Set("soundEffectDescription", "sword clash"); p.Set("soundEffectDuration", 2f); }, "Generate Sound Effect", "currentSoundEffect", "Save Sound Effect"), LiveToo: true),
        new("generate", "audio: music", c => Audio(c, "Music", "/audio/music", p => p.Set("musicDescription", "calm village theme"), "Generate Music", "currentMusic", "Save Music"), LiveToo: true),
        new("generate", "audio: voice", c => Audio(c, "Voice", "/audio/voice", p => { p.Set("voiceDescription", "old wizard"); p.Set("voiceText", "You shall not pass"); }, "Generate Voice", "currentVoice", "Save Voice"), LiveToo: true),
        new("generate", "audio: speech (clone)", c => Audio(c, "Speech", "/audio/speech", p => { p.Set("speechText", "Welcome, traveler"); p.Set("speechSample", FakeFile(c, "sample.mp3")); }, "Generate Speech", "currentSpeech", "Save Speech")),
        new("generate", "audio: speech (preset)", c => Audio(c, "SpeechPreset", "/audio/speech-preset", p => p.Set("speechPresetText", "Welcome, traveler"), "Generate Speech", "currentSpeechPreset", "Save Speech"), LiveToo: true),
        new("failure", "job fails: reason shown, no result", JobFails),
        new("failure", "submit 429 queue full: reason shown", QueueFull),
        new("failure", "bad API key: reason shown", BadApiKey),
        new("failure", "400 validation: reason shown", ValidationError),
        new("failure", "poll 429: honours Retry-After, recovers", PollRateLimited),
        new("failure", "poll connection drop: recovers", PollConnectionDrop),
        new("failure", "job never finishes: progress, then timeout", LongJobShowsProgress),
        new("ui", "every screen renders", EveryScreenRenders),
        new("ui", "dropdowns only offer values the API accepts", DropdownsMatchContract),
        new("ui", "animate: every model x duration is accepted", AnimateModelDurationSweep),
        new("ui", "image: every style/perspective/type/ratio is accepted", ImageOptionSweep),
    };
}
