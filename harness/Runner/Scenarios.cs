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
        // New files, or files rewritten (two saves in the same second share a name).
        var before = Directory.GetFiles(c.OutDir, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.GetLastWriteTimeUtc);
        int dialogsBefore = Harness.Dialogs.Count;
        p.Click(label);
        c.Require(p.RunUntilIdle(60), $"'{label}' never finished");
        var added = Directory.GetFiles(c.OutDir, "*", SearchOption.AllDirectories).Where(f => !before.TryGetValue(f, out var t) || File.GetLastWriteTimeUtc(f) != t).ToList();
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
        if (c.Live) LiveState.ImageUrl2 = (string)PluginDriver.Prop(((IList)images)[1], "Url");
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
        bool gif = p.Has("spritesheetGif");
        if (gif) p.Set("spritesheetGif", true);
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
        if (gif || ButtonShown(p, "Save GIF Preview")) ClickSave(c, p, "Save GIF Preview", "gif");
        bool hydra = (string)body?["model"] is null or "hydra";
        if (!c.Live && hydra) c.Check(ButtonShown(p, "Save Sound Effect"), "hydra returned a sound effect but there is no button to save it");
        if (ButtonShown(p, "Save Sound Effect")) ClickSave(c, p, "Save Sound Effect", "mp3");
    }

    static void AnimateMargins(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        c.Require(p.Has("spritesheetMarginHorizontal"), "no per-axis margins (field spritesheetMarginHorizontal)");
        Tab(p, "Sprites", "currentSpriteTab", "ImageToSpritesheet");
        p.Set("spritesheetInitialImageUrl", FakeFile(c, "input.webp"));
        foreach (var mode in new[] { "auto", "none", "manual" })
        {
            p.Set("spritesheetMarginMode", mode);
            p.Set("spritesheetMarginHorizontal", 0.25f);
            p.Set("spritesheetMarginVertical", 0.05f);
            p.Click("Animate Sprite");
            c.Require(p.RunUntilIdle(20), $"margin mode {mode} never finished");
            var body = LastBody(c, "/assets/sprite/animate");
            c.Check((string)body["margin_ratio_mode"] == mode, $"mode {mode}: sent margin_ratio_mode {body["margin_ratio_mode"]}");
            if (mode == "manual")
                c.Check(body["margin_ratio_horizontal"]?.GetValue<double>() is > 0.24 and < 0.26 && body["margin_ratio_vertical"]?.GetValue<double>() is > 0.04 and < 0.06,
                    $"manual margins not sent per axis: {body.ToJsonString()}");
        }
        c.CheckClean(p, "/assets/sprite/animate");
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

    // Back view: pasted URL, or picked from the project (normalised like the front image).
    static void Model3DBackView(Ctx c)
    {
        var p = c.NewPlugin();
        c.Require(p.Has("model3DBackImageUrl"), "no back view input (field model3DBackImageUrl)");
        Tab(p, "Models", "currentModelTab", "Create");
        p.Set("model3DImageUrl", FakeFile(c, "front.png"));

        // 1. no back view: the field is not sent at all
        p.Click("Create 3D Model");
        c.Require(p.RunUntilIdle(30), "3D creation never finished: " + p.Status);
        c.Check(LastBody(c, "/assets/3d-model")?["back_image"] == null, "back_image sent although none was chosen");

        // 2. a pasted URL goes through as-is
        p.Set("model3DBackImageUrl", FakeFile(c, "back.png"));
        p.Click("Create 3D Model");
        c.Require(p.RunUntilIdle(30), "3D creation with back view never finished: " + p.Status);
        c.Check((string)LastBody(c, "/assets/3d-model")?["back_image"] == FakeFile(c, "back.png"), "pasted back view URL not sent as back_image");

        // 3. picked from the project: becomes a PNG data URI
        string file = Path.Combine(c.OutDir, "back-view.png");
        File.WriteAllBytes(file, UnityEngine.FakeImage.Png(512, 512));
        Harness.OpenFilePanel = (title, ext) => title.Contains("Back") ? file : "";
        p.Click("Select Back View");
        c.Check(p.Get<string>("model3DBackImageUrl").StartsWith("data:image/png;base64,"), $"picked back view not loaded as a data URI: {p.Get<string>("model3DBackImageUrl")?.Substring(0, Math.Min(40, p.Get<string>("model3DBackImageUrl")?.Length ?? 0))}");
        p.Click("Create 3D Model");
        c.Require(p.RunUntilIdle(30), "3D creation with picked back view never finished: " + p.Status);
        c.Check(((string)LastBody(c, "/assets/3d-model")?["back_image"] ?? "").StartsWith("data:image/png;base64,"), "picked back view not sent as a PNG data URI");
        c.Check(!string.IsNullOrEmpty((string)PluginDriver.Prop(p.Get("current3DModel"), "ModelUrl")), "no model with a back view");

        // 4. Clear removes it
        p.Click("Clear");
        c.Check(string.IsNullOrEmpty(p.Get<string>("model3DBackImageUrl")), "'Clear' left the back view set");
        c.CheckClean(p, "/assets/3d-model");
    }

    // Live only: the API accepts back_image and the job completes. (Whether the model's back
    // follows the image needs eyes on the GLB.)
    static void Model3DBackViewLive(Ctx c)
    {
        c.Require(c.Live, "live-only scenario");
        c.Require(LiveState.ImageUrl != null && LiveState.ImageUrl2 != null, "run the images scenario first");
        var p = c.NewPlugin();
        Tab(p, "Models", "currentModelTab", "Create");
        p.Set("model3DImageUrl", LiveState.ImageUrl);
        p.Set("model3DBackImageUrl", LiveState.ImageUrl2);
        p.Click("Create 3D Model");
        c.Require(p.RunUntilIdle(1200), "3D creation with back view never finished: " + p.Status);
        var model = p.Get("current3DModel");
        c.Require(model != null && !string.IsNullOrEmpty((string)PluginDriver.Prop(model, "ModelUrl")), $"no model; status: {p.Status}; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.CheckClean(p);
        ClickSave(c, p, "Download 3D Model", "glb");
        c.Note("model url: " + PluginDriver.Prop(model, "ModelUrl"));
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

    // Audio round: descriptive preset voices take emotion (never delivery); first-name voices take
    // delivery (never a non-Default emotion). Voice design takes an optional image; clone takes delivery.
    static void SpeechPresetFamilies(Ctx c)
    {
        Audio(c, "SpeechPreset", "/audio/speech-preset", p => { p.Set("speechPresetText", "Welcome, traveler"); p.Set("speechPresetVoiceId", "Serious woman"); p.Set("speechPresetEmotion", "Happy"); p.Set("speechPresetDelivery", "ignored"); }, "Generate Speech", "currentSpeechPreset", "Save Speech");
        var body = LastBody(c, "/audio/speech-preset");
        c.Require(body != null, "no preset request recorded");
        c.Check((string)body["emotion"] == "Happy", $"descriptive voice: emotion not sent ({body})");
        c.Check(body["delivery"] == null, $"descriptive voice: delivery must not be sent ({body})");

        Audio(c, "SpeechPreset", "/audio/speech-preset", p => { p.Set("speechPresetText", "Welcome, traveler"); p.Set("speechPresetVoiceId", "Rachel"); p.Set("speechPresetEmotion", "Happy"); p.Set("speechPresetDelivery", "whispering, out of breath"); }, "Generate Speech", "currentSpeechPreset", "Save Speech");
        body = LastBody(c, "/audio/speech-preset");
        c.Check(body["emotion"] == null, $"first-name voice: emotion must not be sent ({body})");
        c.Check((string)body["delivery"] == "whispering, out of breath", $"first-name voice: delivery not sent ({body})");
    }

    static void VoiceFromImage(Ctx c)
    {
        Audio(c, "Voice", "/audio/voice", p => { p.Set("voiceImage", FakeFile(c, "input-knight.webp")); p.Set("voiceText", "You shall not pass"); }, "Generate Voice", "currentVoice", "Save Voice");
        var body = LastBody(c, "/audio/voice");
        c.Require(body != null, "no voice request recorded");
        c.Check(!string.IsNullOrEmpty((string)body["image"]), $"image not sent ({body})");
        c.Check(body["voice_description"] == null, $"empty description must be omitted ({body})");
    }

    static void SpeechCloneDelivery(Ctx c)
    {
        Audio(c, "Speech", "/audio/speech", p => { p.Set("speechText", "Welcome, traveler"); p.Set("speechSample", FakeFile(c, "sample.mp3")); p.Set("speechDelivery", "angry, shouting"); }, "Generate Speech", "currentSpeech", "Save Speech");
        var body = LastBody(c, "/audio/speech");
        c.Check((string)body?["delivery"] == "angry, shouting", $"delivery not sent ({body})");
    }


    // ---------------------------------------------------------------- 3D: hero / rig in one call

    static void Model3DHeroRig(Ctx c)
    {
        var p = c.NewPlugin();
        c.Require(p.Has("model3DModel") && p.Has("model3DRig"), "no model choice / rig option on Create 3D (fields model3DModel, model3DRig)");
        Tab(p, "Models", "currentModelTab", "Create");
        p.Set("model3DImageUrl", FakeFile(c, "front.png"));
        p.Set("model3DBackImageUrl", FakeFile(c, "back.png"));

        // prop: the back view goes; no rig fields unless asked
        p.Click("Create 3D Model");
        c.Require(p.RunUntilIdle(30), "prop creation never finished: " + p.Status);
        var body = LastBody(c, "/assets/3d-model");
        c.Check((string)body?["model"] == "prop", $"model not sent as prop ({body})");
        c.Check(body?["back_image"] != null, "prop: back view not sent");
        c.Check(body?["rig"] == null && body?["rig_type"] == null, $"rig fields sent without Also Rig ({body})");

        // hero: front image only, even with a back view still in the field
        p.Set("model3DModel", "hero");
        p.Set("model3DRig", true);
        p.Click("Create 3D Model");
        c.Require(p.RunUntilIdle(30), "hero creation never finished: " + p.Status);
        body = LastBody(c, "/assets/3d-model");
        c.Check((string)body?["model"] == "hero", $"model not sent as hero ({body})");
        c.Check(body?["back_image"] == null, "hero: back_image sent (the API answers it with a 400)");
        c.Check(body?["rig"]?.GetValue<bool>() == true && (string)body?["joint_naming"] == "mixamo", $"rig fields not sent ({body})");
        // Create rigs with the general rig: the API takes no rig type there.
        c.Check(body?["rig_type"] == null, $"rig_type sent on create, which takes none ({body})");

        // a rigged result is ready for the next steps without the Rig tab
        var rigged = p.Get("currentRigged3DModel");
        c.Check(rigged != null && (string)PluginDriver.Prop(rigged, "ModelUrl") == (string)PluginDriver.Prop(p.Get("current3DModel"), "ModelUrl"), "a model rigged on create did not become the rigged model");
        c.Check(ButtonShown(p, "Use for Animation") && !Harness.Buttons.Contains("Use for Rigging"), "rigged on create still offers Rigging instead of Animation");
        p.Click("Use for Directional Sprites");
        c.Check(p.Get("currentModelTab").ToString() == "Directional", "'Use for Directional Sprites' did not switch tabs");
        c.Check(p.Get<string>("directionalModelInput") == (string)PluginDriver.Prop(p.Get("current3DModel"), "ModelUrl"), "the created model did not reach Directional Sprites");
        c.CheckClean(p, "/assets/3d-model");
    }

    // ---------------------------------------------------------------- Directional Sprites

    static object JVal(object v) => Activator.CreateInstance(Type.GetType("Newtonsoft.Json.Linq.JValue, Newtonsoft.Json", true)!, v);

    static PluginDriver DirectionalPlugin(Ctx c, FakeLudoApi.Behavior b = null)
    {
        var p = c.NewPlugin(b);
        c.Require(p.Has("directionalModelInput"), "no Directional Sprites tab in this build");
        Tab(p, "Models", "currentModelTab", "Directional");
        c.Require(p.RunUntilIdle(20), "styles never loaded");
        p.Gui();
        return p;
    }

    static void Directional(Ctx c)
    {
        var p = DirectionalPlugin(c);
        var styles = p.Get("directionalStyles");
        c.Require(styles != null, $"styles not loaded: {p.Get("directionalStylesError")}");
        c.Check(PluginDriver.Count(PluginDriver.Prop(styles, "Styles")) >= 10, "fewer styles than the API lists");

        p.Set("directionalModelInput", FakeFile(c, "rigged.glb"));
        p.Set("directionalAnimationSource", "preset");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "presets never loaded");
        p.Gui();
        c.Require(PluginDriver.Count(p.Get("animationPresets")) == 3, $"presets: {PluginDriver.Count(p.Get("animationPresets"))}/3");
        c.Check(p.Get<string>("directionalPresetId") == "walk_relaxed", $"first preset not picked: {p.Get<string>("directionalPresetId")}");
        p.Set("directionalPresetCategory", "Poses: Idles & Fidgets");
        p.Gui();
        c.Check(p.Get<string>("directionalPresetId") == "idle_breathing", $"category change did not pick its preset: {p.Get<string>("directionalPresetId")}");

        // a pixel style, with a numeric choice, a custom colour and an advanced bool changed
        p.Set("directionalStyleId", "pixel_art");
        p.Gui();
        var prm = (IDictionary)p.Get("directionalStyleParams");
        prm["resolution"] = JVal(96L);
        prm["outline"] = JVal("#ff0000");
        prm["palette"] = JVal("standard"); // the default: must not be sent
        p.Set("directionalShowAdvanced", true);
        prm["highlights"] = JVal(false);
        p.Gui();
        p.Set("directionalDirections", new List<string> { "S", "SW", "N" });

        p.Click("Preview (free)");
        c.Require(p.RunUntilIdle(30), "preview never finished: " + p.Status);
        c.Check(PluginDriver.Count(p.Get("directionalPreviews")) == 3, $"previews: {PluginDriver.Count(p.Get("directionalPreviews"))}/3; status {p.Status}");
        var frames = (IDictionary)p.Get("directionalPreviewFrames");
        foreach (var d in new[] { "S", "SW", "N" })
        {
            var list = frames.Contains(d) ? (IList)frames[d]! : null;
            c.Check(list != null && list.Count == 3 && list.Cast<object>().All(t => t != null), $"preview {d}: frames not loaded");
        }
        var body = LastBody(c, "/assets/sprite/directional-preview");
        c.Require(body != null, "no preview request recorded");
        c.Check(body["directions"]!.ToJsonString() == "[\"S\",\"SW\",\"N\"]", $"directions {body["directions"]!.ToJsonString()}");
        c.Check(body["style_params"]?["resolution"]?.GetValueKind() == System.Text.Json.JsonValueKind.Number && body["style_params"]!["resolution"]!.GetValue<double>() == 96, $"resolution not sent as the number 96: {body["style_params"]?.ToJsonString()}");
        c.Check((string)body["style_params"]?["outline"] == "#ff0000", $"custom colour not sent: {body["style_params"]?.ToJsonString()}");
        c.Check(body["style_params"]?["palette"] == null, $"a default setting was sent: {body["style_params"]?.ToJsonString()}");
        c.Check(body["style_params"]?["highlights"]?.GetValueKind() == System.Text.Json.JsonValueKind.False, $"advanced bool not sent: {body["style_params"]?.ToJsonString()}");
        c.Check((string)body["preset_id"] == "idle_breathing", $"preset not sent ({body})");
        c.Check(body["crop_loop"] == null, $"crop_loop sent: the API no longer takes it ({body})");
        c.Check(body["num_frames"] == null && body["target_frame_size"] == null, "preview sent sheet-only fields");

        p.Click("Render Spritesheets (3 credits)");
        c.Require(p.RunUntilIdle(30), "spritesheets never finished: " + p.Status);
        c.Require(PluginDriver.Count(p.Get("directionalSheets")) == 3, $"sheets: {PluginDriver.Count(p.Get("directionalSheets"))}/3; status {p.Status}; dialogs {string.Join(" / ", p.Dialogs)}");
        body = LastBody(c, "/assets/sprite/directional");
        c.Check(body?["target_frame_size"]?.GetValue<double>() == 640 && body?["num_frames"]?.GetValue<double>() == 36, $"sheet size/frames ({body})");
        c.Check(body?["loop"] == null, "loop sent while left at the animation's default");
        ClickSave(c, p, "Save Spritesheet", "png");
        p.Click("Save All Spritesheets");
        c.Require(p.RunUntilIdle(30), "save-all never finished");
        var folder = Directory.GetDirectories(c.OutDir, "folder-*").OrderBy(Directory.GetCreationTimeUtc).Last();
        var saved = Directory.GetFiles(folder);
        c.Check(saved.Length == 3 && saved.All(f => f.EndsWith(".png")), $"save-all wrote {saved.Length} files: {string.Join(", ", saved.Select(Path.GetFileName))}");

        p.Click("Render Stills (1.5 credits)");
        c.Require(p.RunUntilIdle(30), "stills never finished: " + p.Status);
        c.Require(PluginDriver.Count(p.Get("directionalStills")) == 3, $"stills: {PluginDriver.Count(p.Get("directionalStills"))}/3");
        ClickSave(c, p, "Save Still", "png");
        p.Click("Animate This Still");
        c.Check(p.Get("currentTab").ToString() == "Sprites" && p.Get("currentSpriteTab").ToString() == "ImageToSpritesheet", "'Animate This Still' did not open Animate Sprite");
        c.Check((p.Get<string>("spritesheetInitialImageUrl") ?? "").Contains("still-S"), "the still did not reach Animate Sprite");

        // every request: contract, dropped fields (stills take no animation), style params
        c.CheckClean(p);
    }

    static void DirectionalSources(Ctx c)
    {
        var p = DirectionalPlugin(c, new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        p.Set("directionalModelInput", FakeFile(c, "rigged.glb"));

        // none: a still pose, no animation or travel fields, no frame count
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "still-pose sheet never finished");
        var body = LastBody(c, "/assets/sprite/directional");
        foreach (var f in new[] { "animation_url", "preset_id", "model_animation", "keep_horizontal_travel", "num_frames" })
            c.Check(body?[f] == null, $"no animation: '{f}' sent ({body})");

        // an animation inside the GLB
        p.Set("directionalAnimationSource", "model_animation");
        p.Gui();
        c.Check(!ButtonEnabled(p, "Render Spritesheets (1 credit)"), "can render with an empty animation name");
        p.Set("directionalModelAnimation", "Walk");
        p.Set("directionalLoop", "off");
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "model_animation sheet never finished");
        body = LastBody(c, "/assets/sprite/directional");
        c.Check((string)body?["model_animation"] == "Walk" && body?["loop"]?.GetValue<bool>() == false, $"model_animation / loop not sent ({body})");

        // from Animate 3D: model and its clip arrive together
        Tab(p, "Models", "currentModelTab", "Animate");
        p.Set("model3DAnimateModelInput", FakeFile(c, "rigged-hero.glb"));
        p.Set("model3DAnimateNumVariants", 2);
        p.Click("Animate 3D Model");
        c.Require(p.RunUntilIdle(20), "3D animation never finished");
        p.Click("Use for Directional Sprites");
        c.Check(p.Get("currentModelTab").ToString() == "Directional", "Animate's 'Use for Directional Sprites' did not switch tabs");
        c.Check(p.Get<string>("directionalModelInput") == FakeFile(c, "rigged-hero.glb"), "the animated model did not come along");
        string clip = (string)PluginDriver.Prop(((IList)p.Get("current3DAnimations"))[0], "GlbUrl");
        c.Check(p.Get<string>("directionalAnimationSource") == "animation" && p.Get<string>("directionalAnimationUrl") == clip, "the clip did not come along");
        p.Set("directionalGif", true);
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "animation_url sheet never finished");
        body = LastBody(c, "/assets/sprite/directional");
        c.Check((string)body?["animation_url"] == clip && body?["gif"]?.GetValue<bool>() == true, $"animation_url / gif not sent ({body})");
        c.Check(ButtonShown(p, "Save GIF"), "gif returned but no Save GIF button");
        ClickSave(c, p, "Save GIF", "gif");

        // directions: none chosen means nothing to render
        p.Set("directionalDirections", new List<string>());
        c.Check(!ButtonEnabled(p, "Preview (free)"), "can preview with no direction");
        p.Click("All 8");
        c.Check(PluginDriver.Count(p.Get("directionalDirections")) == 8, "'All 8' did not pick every direction");
        c.CheckClean(p);
    }

    static void DirectionalPreviewSuperseded(Ctx c)
    {
        var p = DirectionalPlugin(c, new FakeLudoApi.Behavior { PreviewSuperseded = true });
        p.Set("directionalModelInput", FakeFile(c, "model.glb"));
        p.Click("Preview (free)");
        c.Require(p.RunUntilIdle(20), "stuck after a superseded preview");
        c.Check(p.ErrorDialogs.Count == 0, $"a replaced preview is not an error: {string.Join(" / ", p.Dialogs)}");
        c.Check(p.Status.Contains("replaced"), $"status does not say the preview was replaced: '{p.Status}'");
    }

    static void DirectionalStylesUnavailable(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { StylesStatus = 503 });
        Tab(p, "Models", "currentModelTab", "Directional");
        c.Require(p.RunUntilIdle(20), "styles request never finished");
        p.Gui();
        c.Check(p.Get("directionalStylesError") != null && ButtonShown(p, "Retry Loading Styles"), "no message / retry when styles fail");
        c.Api.B.StylesStatus = 200;
        p.Click("Retry Loading Styles");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "styles retry never finished");
        c.Check(p.Get("directionalStyles") != null, "retry did not load the styles");
    }

    static bool ButtonEnabled(PluginDriver p, string label)
    {
        try { Harness.ClickButton = label; p.Gui(); bool clicked = Harness.ClickButton == null; Harness.ClickButton = null; return clicked; }
        finally { Harness.ClickButton = null; }
    }

    // Live only (~2 credits): real styles/presets/preview/sheet/still/upscale responses parse.
    // LUDO_LIVE_MODEL is a rigged GLB, LUDO_LIVE_ANIMATION a clip for it.
    static void DirectionalLive(Ctx c)
    {
        c.Require(c.Live, "live-only scenario");
        string model = Environment.GetEnvironmentVariable("LUDO_LIVE_MODEL");
        string anim = Environment.GetEnvironmentVariable("LUDO_LIVE_ANIMATION");
        c.Require(!string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(anim), "set LUDO_LIVE_MODEL and LUDO_LIVE_ANIMATION");
        var p = DirectionalPlugin(c);
        c.Require(p.Get("directionalStyles") != null, $"styles: {p.Get("directionalStylesError")}");
        p.Set("directionalModelInput", model);
        p.Set("directionalAnimationSource", "preset");
        p.Gui();
        c.Require(p.RunUntilIdle(60), "presets never loaded");
        p.Gui();
        c.Require(PluginDriver.Count(p.Get("animationPresets")) > 100, $"presets: {PluginDriver.Count(p.Get("animationPresets"))}");
        p.Set("directionalPresetCategory", "Movement: Walking");
        p.Gui();
        p.Set("directionalStyleId", "pixel_art");
        p.Gui();
        ((IDictionary)p.Get("directionalStyleParams"))["resolution"] = JVal(96L);
        p.Click("Preview (free)");
        c.Require(p.RunUntilIdle(600), "preview never finished: " + p.Status);
        var previews = p.Get("directionalPreviews");
        c.Require(PluginDriver.Count(previews) == 1, $"previews: {PluginDriver.Count(previews)}; status {p.Status}; dialogs {string.Join(" / ", p.Dialogs)}");
        var frameUrls = PluginDriver.Prop(((IList)previews!)[0], "FrameUrls");
        c.Note($"preview: {PluginDriver.Count(frameUrls)} frames, preset {p.Get<string>("directionalPresetId")}");
        c.Check(PluginDriver.Count(frameUrls) > 1, "an animated preview came back with one frame");

        p.Set("directionalAnimationSource", "animation");
        p.Set("directionalAnimationUrl", anim);
        p.Set("directionalFrameSize", 256);
        p.Set("directionalNumFrames", 16);
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(1200), "sheet never finished: " + p.Status);
        var sheets = p.Get("directionalSheets");
        c.Require(PluginDriver.Count(sheets) == 1, $"sheets: {PluginDriver.Count(sheets)}; status {p.Status}; dialogs {string.Join(" / ", p.Dialogs)}");
        var sheet = ((IList)sheets!)[0];
        c.Note($"sheet: {PluginDriver.Prop(sheet, "NumFrames")} frames, {PluginDriver.Prop(sheet, "NumCols")}x{PluginDriver.Prop(sheet, "NumRows")}, {PluginDriver.Prop(sheet, "Fps")} fps");
        c.Check((int)PluginDriver.Prop(sheet, "NumCols")! > 0 && (float)PluginDriver.Prop(sheet, "Fps")! > 0, "sheet grid / fps not read");
        ClickSave(c, p, "Save Spritesheet", null);

        p.Click("Render Stills (0.5 credits)");
        c.Require(p.RunUntilIdle(1200), "still never finished: " + p.Status);
        var stills = p.Get("directionalStills");
        c.Require(PluginDriver.Count(stills) == 1, $"stills: {PluginDriver.Count(stills)}; status {p.Status}; dialogs {string.Join(" / ", p.Dialogs)}");
        var still = ((IList)stills!)[0];
        c.Note($"still: {PluginDriver.Prop(still, "Width")}x{PluginDriver.Prop(still, "Height")}");

        // Upscale the still (under 2048px): a new image comes back.
        p.Set("upscaleImageInput", (string)PluginDriver.Prop(still, "Url"));
        Tab(p, "Sprites", "currentSpriteTab", "Upscale");
        p.Click("Upscale Image (0.5 credits)");
        c.Require(p.RunUntilIdle(600), "upscale never finished: " + p.Status);
        var up = p.Get("currentUpscaledImage");
        c.Require(up != null && !string.IsNullOrEmpty((string)PluginDriver.Prop(up, "Url")), $"no upscaled image; status {p.Status}; dialogs {string.Join(" / ", p.Dialogs)}");
        c.Check((string)PluginDriver.Prop(up, "Url") != (string)PluginDriver.Prop(still, "Url"), "upscale returned the source url");
        c.Note("upscaled: " + PluginDriver.Prop(up, "Url"));
        c.CheckClean(p);
    }

    // ---------------------------------------------------------------- My Animations, Mirror

    static JsonObject Animation(string id, string name, string prompt) => new()
    {
        ["id"] = id, ["name"] = name, ["prompt"] = prompt, ["duration"] = 3.0, ["loop"] = true,
        ["preview_url"] = $"http://127.0.0.1/files/{id}.mp4", ["created_at"] = 1762800000,
    };

    // Generate Animation from the My Animation source, then the new animation is picked and
    // goes out as animation_id; Mirror goes with it (and with a clip) but never elsewhere.
    static void MyAnimationsDirectional(Ctx c)
    {
        var p = DirectionalPlugin(c, new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1, MyAnimations = new() { Animation("6a1f00000000000000000000old1", "zombie-walk", "zombie walk") } });
        p.Set("directionalModelInput", FakeFile(c, "rigged.glb"));
        p.Set("directionalAnimationSource", "my_animation");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "My Animations never loaded");
        p.Gui();
        c.Check(PluginDriver.Count(p.Get("myAnimations")) == 1 && p.Get<string>("directionalMyAnimationId") == "6a1f00000000000000000000old1", $"the account's animation is not listed and picked ({p.Get<string>("directionalMyAnimationId")})");
        c.Check(c.Api.Requests.Any(r => r.Method == "GET" && r.Path == "/api/assets/animations"), "My Animations not read from GET /assets/animations");

        // an empty description cannot be sent
        c.Check(!ButtonEnabled(p, "Generate Animation (0.5 credits)"), "Generate Animation is enabled with no description");
        p.Set("newAnimationPrompt", "overhead sword slash");
        p.Set("newAnimationVariants", 2);
        p.Click("Generate Animations (1 credit)");
        c.Require(p.RunUntilIdle(20), "Generate Animation never finished: " + p.Status);
        var gen = LastBody(c, "/assets/animation");
        c.Check((string)gen?["prompt"] == "overhead sword slash" && gen?["num_variants"]?.GetValue<double>() == 2, $"createAnimation body ({gen})");
        var mine = (IList)p.Get("myAnimations");
        c.Require(mine.Count == 3, $"My Animations has {mine.Count}, expected the old one plus 2 new");
        string made = (string)PluginDriver.Prop(mine[0], "Id");
        c.Check(made != "6a1f00000000000000000000old1" && p.Get<string>("directionalMyAnimationId") == made, "the generated animation was not picked");
        c.Check(p.Status.Contains("My Animations"), $"status does not say where it went: '{p.Status}'");

        // without Mirror
        p.Click("Preview (free)");
        c.Require(p.RunUntilIdle(20), "preview never finished: " + p.Status);
        var body = LastBody(c, "/assets/sprite/directional-preview");
        c.Check((string)body?["animation_id"] == made && body?["preset_id"] == null && body?["mirror"] == null, $"preview: animation_id / no mirror ({body})");
        // with Mirror, on the preview and the sheets
        p.Set("directionalMirror", true);
        p.Click("Preview (free)");
        c.Require(p.RunUntilIdle(20), "mirrored preview never finished");
        c.Check(LastBody(c, "/assets/sprite/directional-preview")?["mirror"]?.GetValue<bool>() == true, "preview: mirror not sent");
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "mirrored sheet never finished");
        body = LastBody(c, "/assets/sprite/directional");
        c.Check((string)body?["animation_id"] == made && body?["mirror"]?.GetValue<bool>() == true && body?["num_frames"] != null, $"sheets: animation_id + mirror ({body})");

        // an Animation Clip takes Mirror too, and only one animation goes out
        p.Set("directionalAnimationSource", "preset");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "Animation Clips never loaded");
        p.Gui();
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "mirrored clip sheet never finished");
        body = LastBody(c, "/assets/sprite/directional");
        c.Check(body?["preset_id"] != null && body?["animation_id"] == null && body?["mirror"]?.GetValue<bool>() == true, $"clip: preset_id + mirror only ({body})");
        p.Set("directionalMirror", false);
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "clip sheet never finished");
        c.Check(LastBody(c, "/assets/sprite/directional")?["mirror"] == null, "clip without Mirror still sends mirror");

        // Mirror left on, then a source that cannot take it: not sent (the API answers 400)
        p.Set("directionalMirror", true);
        p.Set("directionalAnimationSource", "model_animation");
        p.Set("directionalModelAnimation", "Walk");
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "model_animation sheet never finished");
        body = LastBody(c, "/assets/sprite/directional");
        c.Check(body?["mirror"] == null && body?["preset_id"] == null && body?["animation_id"] == null, $"model_animation sent mirror or a second animation ({body})");
        // stills take no animation at all
        p.Click("Render Stills (0.5 credits)");
        c.Require(p.RunUntilIdle(20), "stills never finished");
        body = LastBody(c, "/assets/sprite/directional-stills");
        c.Check(body?["mirror"] == null && body?["animation_id"] == null, $"stills sent an animation ({body})");
        c.CheckClean(p);
    }

    // Animate 3D retargets an Animation Clip or one of My Animations (animate-preset).
    static void AnimationClips3D(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1, MyAnimations = new() { Animation("6a1f00000000000000000000mine", "victory-dance", "victory dance") } });
        Tab(p, "Models", "currentModelTab", "Animate");
        p.Set("model3DAnimateModelInput", FakeFile(c, "rigged-hero.glb"));
        p.Set("model3DAnimateSource", "preset");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "Animation Clips never loaded");
        p.Gui();
        p.Click("Animate 3D Model");
        c.Require(p.RunUntilIdle(20), "clip retarget never finished: " + p.Status);
        var body = LastBody(c, "/assets/3d-model/animate-preset");
        c.Check((string)body?["preset_id"] == "walk_relaxed" && body?["animation_id"] == null && body?["mirror"] == null, $"clip retarget body ({body})");
        foreach (var f in new[] { "prompt", "num_variants", "mode", "loop", "augment_prompt" }) c.Check(body?[f] == null, $"clip retarget sent '{f}'");
        c.Check(c.Api.Posts("/api/assets/3d-model/animate").Count == 0, "a clip went to the text animation endpoint");
        c.Require(PluginDriver.Count(p.Get("current3DAnimations")) == 1, $"retarget results: {PluginDriver.Count(p.Get("current3DAnimations"))}");
        ClickSave(c, p, "Download Animation GLB", "glb");

        p.Set("model3DAnimateMirror", true);
        p.Click("Animate 3D Model");
        c.Require(p.RunUntilIdle(20), "mirrored clip retarget never finished");
        c.Check(LastBody(c, "/assets/3d-model/animate-preset")?["mirror"]?.GetValue<bool>() == true, "clip retarget: mirror not sent");
        c.Check(ButtonShown(p, "Download Animation GLB") && (bool)PluginDriver.Prop(((IList)p.Get("current3DAnimations"))[0], "Mirror")!, "a mirrored result is not read as mirrored");

        p.Set("model3DAnimateSource", "my_animation");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "My Animations never loaded");
        p.Gui();
        p.Click("Animate 3D Model");
        c.Require(p.RunUntilIdle(20), "My Animation retarget never finished");
        body = LastBody(c, "/assets/3d-model/animate-preset");
        c.Check((string)body?["animation_id"] == "6a1f00000000000000000000mine" && body?["preset_id"] == null && body?["mirror"]?.GetValue<bool>() == true, $"My Animation retarget body ({body})");
        p.Set("model3DAnimateMirror", false);
        p.Click("Animate 3D Model");
        c.Require(p.RunUntilIdle(20), "unmirrored My Animation retarget never finished");
        c.Check(LastBody(c, "/assets/3d-model/animate-preset")?["mirror"] == null, "mirror sent while off");

        // the result goes on to Directional Sprites like any other clip
        p.Click("Use for Directional Sprites");
        c.Check(p.Get<string>("directionalAnimationSource") == "animation" && (p.Get<string>("directionalAnimationUrl") ?? "").EndsWith("-retarget.glb"), "the retargeted GLB did not reach Directional Sprites");

        // the text description path is unchanged
        Tab(p, "Models", "currentModelTab", "Animate");
        p.Set("model3DAnimateSource", "prompt");
        p.Set("model3DAnimateNumVariants", 1);
        p.Click("Animate 3D Model");
        c.Require(p.RunUntilIdle(20), "text animation never finished");
        c.Check(LastBody(c, "/assets/3d-model/animate")?["prompt"] != null, "text animation no longer reaches /assets/3d-model/animate");
        c.CheckClean(p);
    }

    // Animate Sprite from an Animation Clip or one of My Animations (transfer-motion).
    static void AnimateSpriteFromClip(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        Tab(p, "Sprites", "currentSpriteTab", "ImageToSpritesheet");
        p.Set("spritesheetInitialImageUrl", FakeFile(c, "input-knight.webp"));
        p.Set("spritesheetSource", "preset");
        p.Set("spritesheetFrameSize", -9); // "Match input frame" is not offered from a clip
        p.Set("spritesheetMarginMode", "auto");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "Animation Clips never loaded");
        p.Gui();
        p.Set("spritesheetClipPerspective", "isometric");
        p.Set("spritesheetClipDirection", "SW");
        p.Set("spritesheetClipPrompt", "keep the cape still");
        p.Set("spritesheetGif", true);
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "clip animation never finished: " + p.Status);
        var body = LastBody(c, "/assets/sprite/transfer-motion");
        c.Require(body != null, "Animate Sprite from a clip did not call transfer-motion");
        c.Check((string)body["preset_id"] == "walk_relaxed" && (string)body["perspective"] == "isometric" && (string)body["direction"] == "SW", $"clip / view not sent ({body})");
        c.Check((string)body["image"] == FakeFile(c, "input-knight.webp") && (string)body["prompt"] == "keep the cape still", $"image / extra instructions ({body})");
        c.Check((string)body["margin_ratio_mode"] == "manual" && body["frame_size"]!.GetValue<double>() == 256, $"clip margins / frame size ({body})");
        c.Check(body["mirror"] == null && body["animation_id"] == null, $"clip: mirror or animation_id sent ({body})");
        c.Check(c.Api.Posts("/api/assets/sprite/animate").Count == 0, "a clip went to the text animation endpoint");
        c.Require(p.Get("currentSpritesheet") != null, "no spritesheet from a clip");
        ClickSave(c, p, "Save Spritesheet", "image");
        ClickSave(c, p, "Save GIF Preview", "gif");

        p.Set("spritesheetMirror", true);
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "mirrored clip animation never finished");
        c.Check(LastBody(c, "/assets/sprite/transfer-motion")?["mirror"]?.GetValue<bool>() == true, "clip: mirror not sent");

        // My Animation: generated here, then played on the sprite
        p.Set("spritesheetSource", "my_animation");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "My Animations never loaded");
        p.Gui();
        c.Check(!ButtonEnabled(p, "Animate Sprite"), "Animate Sprite enabled with no animation of my own");
        p.Set("newAnimationPrompt", "jumps and lands in a crouch");
        p.Click("Generate Animation (0.5 credits)");
        c.Require(p.RunUntilIdle(20), "Generate Animation never finished");
        string made = p.Get<string>("spritesheetMyAnimationId");
        c.Require(!string.IsNullOrEmpty(made), "the generated animation was not picked");
        p.Set("transferModel", "forge");
        p.Set("transferDuration", 1.5f);
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "My Animation sprite never finished");
        body = LastBody(c, "/assets/sprite/transfer-motion");
        c.Check((string)body?["animation_id"] == made && body?["preset_id"] == null && body?["mirror"]?.GetValue<bool>() == true && (string)body?["model"] == "forge", $"My Animation body ({body})");
        p.Set("spritesheetMirror", false);
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "unmirrored My Animation sprite never finished");
        c.Check(LastBody(c, "/assets/sprite/transfer-motion")?["mirror"] == null, "mirror sent while off");

        // and the text description path is unchanged (its own model, frame size and margins)
        p.Set("spritesheetSource", "prompt");
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "text animation never finished");
        body = LastBody(c, "/assets/sprite/animate");
        c.Check(body != null && (string)body["margin_ratio_mode"] == "auto" && body["mirror"] == null, $"text animation changed by the clip settings ({body})");
        c.CheckClean(p);
    }

    // The validation paths: an animation deleted in the web app (404), the rules the fake
    // holds the plugin to, and My Animations that cannot be read.
    static void AnimationValidation(Ctx c)
    {
        var p = DirectionalPlugin(c, new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1, MyAnimations = new() { Animation("6a1f00000000000000000000gone", "spin", "spin") } });
        p.Set("directionalModelInput", FakeFile(c, "rigged.glb"));
        p.Set("directionalAnimationSource", "my_animation");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "My Animations never loaded");
        p.Gui();
        // both ids in the plugin's state: only the chosen source's goes out
        p.Set("directionalPresetId", "walk_relaxed");
        lock (c.Api.Animations) c.Api.Animations.Clear(); // deleted in the web app since the list loaded
        p.Click("Render Spritesheets (1 credit)");
        c.Require(p.RunUntilIdle(20), "stuck after a 404");
        var body = LastBody(c, "/assets/sprite/directional");
        c.Check(body?["preset_id"] == null && (string)body?["animation_id"] == "6a1f00000000000000000000gone", $"sent more than the chosen animation ({body})");
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("Animation not found") && d.Message.Contains("404")), $"404 not shown; dialogs: {string.Join(" / ", p.Dialogs)}");
        c.Check(p.Get("directionalSheets") == null, "a failed render left sheets behind");
        p.Click("Refresh My Animations");
        c.Require(p.RunUntilIdle(20), "refresh never finished");
        p.Gui();
        c.Check(PluginDriver.Count(p.Get("myAnimations")) == 0 && !ButtonEnabled(p, "Render Spritesheets (1 credit)"), "after a refresh the deleted animation can still be rendered");

        // the fake answers what the backend answers (so the scenarios above mean something)
        using (var http = new HttpClient())
        {
            int Post(string path, string json)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, c.ApiUrl + path) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
                req.Headers.TryAddWithoutValidation("Authorization", "ApiKey " + c.ApiKey);
                req.Headers.TryAddWithoutValidation("x-ludo-tool", "unity");
                return (int)http.Send(req).StatusCode;
            }
            lock (c.Api.Animations) c.Api.Animations.Add(Animation("6a1f00000000000000000000real", "spin", "spin"));
            c.Check(Post("/assets/sprite/directional-preview", "{\"model\":\"m.glb\",\"preset_id\":\"walk_relaxed\",\"animation_id\":\"6a1f00000000000000000000real\"}") == 400, "fake: preset_id + animation_id is not a 400");
            c.Check(Post("/assets/sprite/directional", "{\"model\":\"m.glb\",\"model_animation\":\"Walk\",\"mirror\":true}") == 400, "fake: mirror without a clip or animation is not a 400");
            c.Check(Post("/assets/3d-model/animate-preset", "{\"model\":\"m.glb\"}") == 400, "fake: animate-preset with neither id is not a 400");
            c.Check(Post("/assets/3d-model/animate-preset", "{\"model\":\"m.glb\",\"preset_id\":\"a\",\"animation_id\":\"6a1f00000000000000000000real\"}") == 400, "fake: animate-preset with both ids is not a 400");
            c.Check(Post("/assets/sprite/transfer-motion", "{\"image\":\"i.png\",\"preset_id\":\"walk_relaxed\"}") == 400, "fake: a clip without a view is not a 400");
            c.Check(Post("/assets/sprite/transfer-motion", "{\"image\":\"i.png\",\"animation_id\":\"6a1f00000000000000000000nope\",\"perspective\":\"low\",\"direction\":\"S\"}") == 404, "fake: someone else's animation is not a 404");
            c.Check(Post("/assets/animation", "{\"prompt\":\"spin\",\"augment_prompt\":false}") == 400, "fake: augment_prompt false without duration is not a 400");
        }

        // My Animations cannot be read: a message and a retry
        var q = DirectionalPlugin(c, new FakeLudoApi.Behavior { AnimationsStatus = 503 });
        q.Set("directionalAnimationSource", "my_animation");
        q.Gui();
        c.Require(q.RunUntilIdle(20), "My Animations request never finished");
        q.Gui();
        c.Check(q.Get("myAnimationsError") != null && ButtonShown(q, "Retry Loading My Animations"), "no message / retry when My Animations fail");
        c.Api.B.AnimationsStatus = 200;
        q.Click("Retry Loading My Animations");
        q.Gui();
        c.Require(q.RunUntilIdle(20), "My Animations retry never finished");
        c.Check(q.Get("myAnimations") != null, "retry did not load My Animations");
    }

    // ---------------------------------------------------------------- optional fields

    // Clicks a save button that should write a file and its normal map next to it.
    static void SaveWithNormal(Ctx c, PluginDriver p, string label)
    {
        var before = Directory.GetFiles(c.OutDir, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.GetLastWriteTimeUtc);
        int dialogsBefore = Harness.Dialogs.Count;
        p.Click(label);
        c.Require(p.RunUntilIdle(60), $"'{label}' never finished");
        foreach (var d in Harness.Dialogs.Skip(dialogsBefore).Where(d => d.Title.Contains("Error"))) c.Failures.Add($"'{label}': dialog {d}");
        var added = Directory.GetFiles(c.OutDir, "*", SearchOption.AllDirectories).Where(f => !before.TryGetValue(f, out var t) || File.GetLastWriteTimeUtc(f) != t).ToList();
        var mains = added.Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_normal")).ToList();
        c.Require(mains.Count >= 1, $"'{label}' saved nothing");
        foreach (var main in mains)
        {
            string normal = Path.Combine(Path.GetDirectoryName(main)!, Path.GetFileNameWithoutExtension(main) + "_normal.png");
            c.Check(added.Contains(normal), $"'{label}': no normal map next to {Path.GetFileName(main)} (new files: {string.Join(", ", added.Select(Path.GetFileName))})");
            if (File.Exists(normal)) c.Check(Sniff(File.ReadAllBytes(normal)) == "png", $"{Path.GetFileName(normal)} is not a PNG");
        }
    }

    static void NormalMapsAndOptions(Ctx c)
    {
        // Generate Sprite: free normal maps, saved next to each image
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        Tab(p, "Sprites", "currentSpriteTab", "Image");
        p.Set("imagePrompt", "a stone wall tile");
        p.Click("Generate Image(s)");
        c.Require(p.RunUntilIdle(20), "image never finished");
        c.Check(LastBody(c, "/assets/image")?["save_normals"] == null, "save_normals sent while off");
        p.Set("generatedImages", Activator.CreateInstance(p.Get("generatedImages").GetType()));
        p.Set("imageSaveNormals", true);
        p.Click("Generate Image(s)");
        c.Require(p.RunUntilIdle(20), "image with normal map never finished");
        c.Check(LastBody(c, "/assets/image")?["save_normals"]?.GetValue<bool>() == true, "save_normals not sent");
        c.Check(!string.IsNullOrEmpty((string)PluginDriver.Prop(((IList)p.Get("generatedImages"))[0], "NormalMapUrl")), "normal_map_url not read");
        SaveWithNormal(c, p, "Save to File");
        c.CheckClean(p, "/assets/image");

        // Animate Sprite: normal map (both sources) and Use Pixel Grid
        Tab(p, "Sprites", "currentSpriteTab", "ImageToSpritesheet");
        p.Set("spritesheetInitialImageUrl", FakeFile(c, "input.webp"));
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "animation never finished");
        var body = LastBody(c, "/assets/sprite/animate");
        c.Check(body?["use_pixel_grid"]?.GetValue<bool>() == true && body?["save_normals"] == null, $"defaults: use_pixel_grid true, no save_normals ({body})");
        p.Set("spritesheetUsePixelGrid", false);
        p.Set("spritesheetSaveNormals", true);
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "animation with normal map never finished");
        body = LastBody(c, "/assets/sprite/animate");
        c.Check(body?["use_pixel_grid"]?.GetValue<bool>() == false && body?["save_normals"]?.GetValue<bool>() == true, $"use_pixel_grid false / save_normals ({body})");
        SaveWithNormal(c, p, "Save Spritesheet");
        p.Set("spritesheetSource", "preset");
        p.Gui();
        c.Require(p.RunUntilIdle(20), "clips never loaded");
        p.Gui();
        p.Click("Animate Sprite");
        c.Require(p.RunUntilIdle(20), "clip animation with normal map never finished");
        body = LastBody(c, "/assets/sprite/transfer-motion");
        c.Check(body?["use_pixel_grid"]?.GetValue<bool>() == false && body?["save_normals"]?.GetValue<bool>() == true, $"transfer: use_pixel_grid / save_normals ({body})");
        SaveWithNormal(c, p, "Save Spritesheet");
        c.CheckClean(p);

        // Directional Sprites: 0.5 credits more per sheet, free with stills
        var d = DirectionalPlugin(c, new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        d.Set("directionalModelInput", FakeFile(c, "rigged.glb"));
        d.Set("directionalDirections", new List<string> { "S", "SW" });
        d.Set("directionalSaveNormals", true);
        d.Click("Preview (free)");
        c.Require(d.RunUntilIdle(20), "preview never finished");
        c.Check(LastBody(c, "/assets/sprite/directional-preview")?["save_normals"] == null, "preview sent save_normals, which it does not take");
        d.Click("Render Spritesheets (3 credits)");
        c.Require(d.RunUntilIdle(20), "sheets with normal maps never finished");
        c.Check(LastBody(c, "/assets/sprite/directional")?["save_normals"]?.GetValue<bool>() == true, "sheets: save_normals not sent");
        SaveWithNormal(c, d, "Save Spritesheet");
        SaveWithNormal(c, d, "Save All Spritesheets");
        d.Click("Render Stills (1 credit)");
        c.Require(d.RunUntilIdle(20), "stills with normal maps never finished");
        c.Check(LastBody(c, "/assets/sprite/directional-stills")?["save_normals"]?.GetValue<bool>() == true, "stills: save_normals not sent");
        SaveWithNormal(c, d, "Save All Stills");
        c.CheckClean(d);

        // Audio: Loop on sound effects and music
        Audio(c, "SoundEffect", "/audio/sound-effect", a => { a.Set("soundEffectDescription", "rain on a tin roof"); a.Set("soundEffectLoop", true); }, "Generate Sound Effect", "currentSoundEffect", "Save Sound Effect");
        c.Check(LastBody(c, "/audio/sound-effect")?["loop"]?.GetValue<bool>() == true, "sound effect: loop not sent");
        Audio(c, "Music", "/audio/music", a => { a.Set("musicDescription", "tavern theme"); a.Set("musicLoop", true); }, "Generate Music", "currentMusic", "Save Music");
        c.Check(LastBody(c, "/audio/music")?["loop"]?.GetValue<bool>() == true, "music: loop not sent");
    }

    // ---------------------------------------------------------------- Edit Image

    static void EditImageModes(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        c.Require(p.Has("editImageInput"), "no Edit Image in this build");
        // from a generated image's card
        Tab(p, "Sprites", "currentSpriteTab", "Image");
        p.Set("imagePrompt", "a knight");
        p.Click("Generate Image(s)");
        c.Require(p.RunUntilIdle(20), "image generation never finished");
        string url = (string)PluginDriver.Prop(((IList)p.Get("generatedImages"))[0], "Url");
        p.Click("Edit");
        c.Check(p.Get("currentSpriteTab").ToString() == "Edit" && p.Get<string>("editImageInput") == url, "the card's Edit did not open Edit Image with the image");
        c.Check(!ButtonEnabled(p, "Edit Image (0.5 credits)"), "Edit Image enabled without a description of the change");
        p.Set("editPrompt", "give the knight a red cape");
        p.Click("Edit Image (0.5 credits)");
        c.Require(p.RunUntilIdle(20), "edit never finished: " + p.Status);
        var body = LastBody(c, "/assets/image/edit");
        c.Check((string)body?["image"] == url && (string)body?["prompt"] == "give the knight a red cape" && body?["n"]?.GetValue<double>() == 1, $"edit body ({body})");
        foreach (var f in new[] { "mask", "mask_mode", "image_type", "augment_prompt", "reference_image" }) c.Check(body?[f] == null, $"edit sent '{f}'");
        c.Require(PluginDriver.Count(p.Get("editResults")) == 1, "no edited image");
        ClickSave(c, p, "Save Edited Image", "png");
        c.CheckClean(p, "/assets/image/edit");

        // Edit with Mask: image and mask from the project, same size; a type and normal maps
        string imageFile = Path.Combine(c.OutDir, "edit-source.png"), maskFile = Path.Combine(c.OutDir, "edit-mask.png");
        File.WriteAllBytes(imageFile, UnityEngine.FakeImage.Png(512, 384));
        File.WriteAllBytes(maskFile, UnityEngine.FakeImage.Png(512, 384));
        Harness.OpenFilePanel = (title, ext) => title.Contains("Mask") ? maskFile : imageFile;
        p.Click("Select from Project");
        p.Set("editMode", "mask_replace");
        p.Gui();
        c.Check(!ButtonEnabled(p, "Edit with Mask (1 credit)"), "Edit with Mask enabled without a mask");
        p.Click("Select Mask");
        p.Set("editImageType", "sprite");
        p.Set("editSaveNormals", true);
        p.Set("editPrompt", "a wooden shield");
        p.Click("Edit with Mask (1 credit)");
        c.Require(p.RunUntilIdle(20), "masked edit never finished: " + p.Status);
        body = LastBody(c, "/assets/image/edit");
        c.Check(((string)body?["mask"] ?? "").StartsWith("data:image/png;base64,") && (string)body?["mask_mode"] == "replace" && (string)body?["prompt"] == "a wooden shield", $"mask edit body (mask_mode {body?["mask_mode"]}, prompt {body?["prompt"]})");
        c.Check((string)body?["image_type"] == "sprite" && body?["save_normals"]?.GetValue<bool>() == true, "image_type / save_normals not sent");
        SaveWithNormal(c, p, "Save Edited Image");

        // Erase with Mask: no prompt and no reference go out, 1 credit each
        p.Set("editMode", "mask_erase");
        p.Set("editReferenceImage", FakeFile(c, "ref.png"));
        p.Set("editCount", 2);
        p.Click("Erase with Mask (2 credits)");
        c.Require(p.RunUntilIdle(20), "erase never finished: " + p.Status);
        body = LastBody(c, "/assets/image/edit");
        c.Check((string)body?["mask_mode"] == "erase" && body?["prompt"] == null && body?["reference_image"] == null && body?["n"]?.GetValue<double>() == 2, $"erase body ({body?["mask_mode"]}, prompt {body?["prompt"]}, ref {body?["reference_image"]})");
        c.Check(PluginDriver.Count(p.Get("editResults")) == 2, "erase results");
        c.CheckClean(p, "/assets/image/edit");

        // a mask of another size: refused before any request when both sizes are known...
        File.WriteAllBytes(maskFile, UnityEngine.FakeImage.Png(256, 256));
        p.Click("Select Mask");
        int posts = c.Api.Posts("/api/assets/image/edit").Count;
        c.Check(!ButtonEnabled(p, "Erase with Mask (2 credits)"), "a mask of the wrong size can be sent");
        c.Check(c.Api.Posts("/api/assets/image/edit").Count == posts, "a mismatched mask was sent");
        // ...and the API's 400 is shown when the image's size is not known here (a pasted image)
        p.Set("editImageInput", "data:image/png;base64," + Convert.ToBase64String(UnityEngine.FakeImage.Png(512, 384)));
        p.Set("editSourceWidth", 0);
        p.Click("Erase with Mask (2 credits)");
        c.Require(p.RunUntilIdle(20), "stuck after a 400");
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("same size")), $"mask size 400 not shown; dialogs: {string.Join(" / ", p.Dialogs)}");
    }

    static void PixelArtMode(Ctx c)
    {
        var p = c.NewPlugin();
        Tab(p, "Sprites", "currentSpriteTab", "Edit");
        string file = Path.Combine(c.OutDir, "to-pixelate.png");
        File.WriteAllBytes(file, UnityEngine.FakeImage.Png(512, 384));
        Harness.OpenFilePanel = (title, ext) => file;
        p.Click("Select from Project");
        p.Set("editMode", "pixel_art");
        p.Set("pixelArtPixelSize", 8f);
        p.Set("pixelArtKeepColors", false);
        p.Set("pixelArtPaletteSize", 16);
        p.Click("Convert to Pixel Art (free)");
        c.Require(p.RunUntilIdle(20), "pixel art never finished: " + p.Status);
        var body = LastBody(c, "/assets/image/pixel-art");
        c.Check(body?["pixel_size"]?.GetValue<double>() == 8 && body?["palette_size"]?.GetValue<double>() == 16 && ((string)body?["image"] ?? "").StartsWith("data:image/png"), $"pixel art body ({body?["pixel_size"]}, {body?["palette_size"]})");
        c.Check(c.Api.JobsCreated == 0 && c.Api.Polls().Count == 0, "pixel art is synchronous, but a job was polled");
        var art = p.Get("currentPixelArt");
        c.Require(art != null && (int)PluginDriver.Prop(art, "Columns")! == 64, $"pixel art result not read; status {p.Status}");
        c.Check(p.Status.Contains("64x48"), $"status does not give the art's size: '{p.Status}'");
        ClickSave(c, p, "Save Pixel Art", "png");
        c.CheckClean(p, "/assets/image/pixel-art");

        // all colours kept: no palette_size; Detect: no pixel_size, and the API's 400 is shown
        p.Set("pixelArtKeepColors", true);
        p.Set("pixelArtDetectSize", true);
        p.Click("Convert to Pixel Art (free)");
        c.Require(p.RunUntilIdle(20), "stuck after a 400");
        body = LastBody(c, "/assets/image/pixel-art");
        c.Check(body?["pixel_size"] == null && body?["palette_size"] == null, $"Detect / Keep All Colors still sent sizes ({body?["pixel_size"]}, {body?["palette_size"]})");
        c.Check(p.ErrorDialogs.Any(d => d.Message.Contains("pixel_size")), $"no-grid 400 not shown; dialogs: {string.Join(" / ", p.Dialogs)}");
    }

    // ---------------------------------------------------------------- Upscale Image

    static void Upscale(Ctx c)
    {
        var p = c.NewPlugin();
        c.Require(p.Has("upscaleImageInput"), "no Upscale Image in this build");
        // from an image card
        Tab(p, "Sprites", "currentSpriteTab", "Image");
        p.Set("imagePrompt", "a potion");
        p.Click("Generate Image(s)");
        c.Require(p.RunUntilIdle(30), "image generation never finished");
        var image = ((IList)p.Get("generatedImages"))[0];
        c.Check((int)PluginDriver.Prop(image, "Width")! > 0, "image size not taken from its preview");
        p.Click("Upscale");
        c.Check(p.Get("currentSpriteTab").ToString() == "Upscale", "'Upscale' did not open Upscale Image");
        c.Check(p.Get<string>("upscaleImageInput") == (string)PluginDriver.Prop(image, "Url"), "the image did not reach Upscale Image");
        p.Click("Upscale Image (0.5 credits)");
        c.Require(p.RunUntilIdle(30), "upscale never finished: " + p.Status);
        var result = p.Get("currentUpscaledImage");
        c.Require(result != null && !string.IsNullOrEmpty((string)PluginDriver.Prop(result, "Url")), $"no upscaled image; status {p.Status}; dialogs {string.Join(" / ", p.Dialogs)}");
        c.Check(PluginDriver.Count(p.Get("generatedImages")) == 1, "upscaling changed the generated images list");
        ClickSave(c, p, "Save Upscaled Image", "png");

        // from the project: the file goes unchanged (no resize, no re-encode)
        string file = Path.Combine(c.OutDir, "to-upscale.png");
        var bytes = UnityEngine.FakeImage.Png(1500, 900);
        File.WriteAllBytes(file, bytes);
        Harness.OpenFilePanel = (title, ext) => title.Contains("Upscale") ? file : "";
        p.Click("Select from Project");
        c.Check(p.Get<string>("upscaleImageInput") == "data:image/png;base64," + Convert.ToBase64String(bytes), "project image was changed before upload");
        c.Check(p.Get<int>("upscaleSourceWidth") == 1500, "project image size not read");
        p.Click("Upscale Image (0.5 credits)");
        c.Require(p.RunUntilIdle(30), "project upscale never finished");
        c.Check((string)LastBody(c, "/assets/image/upscale")?["image"] == "data:image/png;base64," + Convert.ToBase64String(bytes), "project image not sent as is");

        // 2048px or more on the longest side: refused before any request
        File.WriteAllBytes(file, UnityEngine.FakeImage.Png(2048, 1024));
        p.Click("Select from Project");
        int posts = c.Api.Posts("/api/assets/image/upscale").Count;
        c.Check(!ButtonEnabled(p, "Upscale Image (0.5 credits)"), "a 2048px image can be sent for upscaling");
        c.Check(c.Api.Posts("/api/assets/image/upscale").Count == posts, "an oversize image was sent");
        c.CheckClean(p);
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

    // poll_after_ms longer than the plugin's own tick, so ignoring it shows.
    static void HonoursPollAfter(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollAfterMs = 800, PollsUntilDone = 3 });
        Tab(p, "Audio", "currentAudioTab", "SoundEffect");
        p.Set("soundEffectDescription", "footsteps on gravel");
        p.Click("Generate Sound Effect");
        c.Require(p.RunUntilIdle(30), "never finished: " + p.Status);
        c.Check(c.Api.Polls().Count == 3, $"expected 3 polls, got {c.Api.Polls().Count}");
        c.Check(c.Api.Polls().All(r => r.Query.Contains("wait=")), "status polls don't long-poll (?wait=)");
        c.CheckClean(p);
    }

    // The gateway 502s and one response is lost after the job was created: retries must
    // reuse the request_id so exactly one job (one charge) exists.
    static void SubmitGatewayRetry(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { SubmitGatewayErrors = 1, LoseSubmitResponses = 1 });
        c.Require(p.Has("submitRetryBaseSeconds"), "submits are not retried (field submitRetryBaseSeconds)");
        p.Set("submitRetryBaseSeconds", 0.3f);
        Tab(p, "Audio", "currentAudioTab", "SoundEffect");
        p.Set("soundEffectDescription", "thunder");
        p.Click("Generate Sound Effect");
        c.Require(p.RunUntilIdle(30), "never finished after gateway errors: " + p.Status);
        c.Check(!string.IsNullOrEmpty((string)PluginDriver.Prop(p.Get("currentSoundEffect"), "Url")), $"no result after transient 502s; status: '{p.Status}'; dialogs: {string.Join(" / ", p.Dialogs)}");
        var posts = c.Api.Posts("/api/audio/sound-effect");
        c.Check(posts.Count == 3, $"expected 3 submits (502, lost 502, ok), got {posts.Count}");
        var ids = posts.Select(r => (string)r.Body?["request_id"]).Distinct().ToList();
        c.Check(ids.Count == 1 && ids[0] != null, $"retries did not reuse one request_id: {string.Join(", ", ids)}");
        c.Check(c.Api.JobsCreated == 1, $"{c.Api.JobsCreated} jobs created for one click");
        c.CheckClean(p);

        // A second click is a new generation: new request_id.
        p.Click("Generate Sound Effect");
        c.Require(p.RunUntilIdle(30), "second click never finished");
        c.Check(c.Api.Posts("/api/audio/sound-effect").Select(r => (string)r.Body?["request_id"]).Distinct().Count() == 2, "second click reused the first click's request_id");
    }

    static void GatewayDown(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { SubmitGatewayErrors = 99 });
        p.Set("submitRetryBaseSeconds", 0.2f);
        Tab(p, "Audio", "currentAudioTab", "Music");
        p.Set("musicDescription", "boss battle");
        p.Click("Generate Music");
        c.Require(p.RunUntilIdle(30), "stuck while the gateway is down: " + p.Status);
        c.Check(c.Api.Posts().Count == 4, $"expected 4 attempts, got {c.Api.Posts().Count}");
        var d = p.ErrorDialogs.FirstOrDefault();
        c.Check(d != null && d.Message.Contains("temporarily unavailable") && !d.Message.Contains("<html"), $"outage message not readable: {d}");
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
            ("Sprites", "currentSpriteTab", new[] { "Image", "ImageToSpritesheet", "Upscale", "Edit" }),
            ("Models", "currentModelTab", new[] { "Create", "Rig", "Animate", "Directional" }),
            ("Audio", "currentAudioTab", new[] { "SoundEffect", "Music", "Voice", "Speech", "SpeechPreset" }),
            ("Settings", null, new string[] { null }),
        };
        foreach (var (tab, field, subs) in screens)
            foreach (var sub in subs)
            {
                try
                {
                    if (field != null && sub != null && !Enum.GetNames(p.Get(field).GetType()).Contains(sub)) { c.Note($"{tab}/{sub}: not in this build"); continue; }
                    Tab(p, tab, field, sub); p.Gui();
                }
                catch (Exception e) { c.Failures.Add($"{tab}/{sub}: OnGUI threw {e.GetType().Name}: {e.Message}"); }
            }
        // No API key: the key screen must render and nothing else.
        var q = c.NewPlugin();
        q.Set("apiKey", "");
        q.Gui();
        c.Check(Harness.Buttons.Contains("Save API Key"), "key screen missing without an API key");
    }

    // With the window open, errors from the user's own code must not make the plugin
    // chatter in the console or touch its state.
    static void QuietOnUnrelatedErrors(Ctx c)
    {
        var p = c.NewPlugin();
        p.Gui();
        int before = Harness.Logs.Count;
        UnityEngine.Debug.LogError("Assets/Game/Player.cs(12,5): error CS0103: The name 'speed' does not exist in the current context");
        UnityEngine.Debug.LogError("NullReferenceException: Object reference not set to an instance of an object");
        var chatter = Harness.Logs.Skip(before).Where(l => l.Message.Contains("[LudoAIPlugin]")).ToList();
        c.Check(chatter.Count == 0, $"plugin reacted to unrelated console errors: {string.Join(" | ", chatter.Select(l => l.Message))}");
        p.Close();
    }

    // Every dropdown that feeds an API field may only offer values the API accepts.
    static readonly (string field, string path, string param)[] OptionArrays =
    {
        ("imageTypeOptions", "/assets/image", "image_type"),
        ("artStyleOptions", "/assets/image", "art_style"),
        ("perspectiveOptions", "/assets/image", "perspective"),
        ("aspectRatioOptions", "/assets/image", "aspect_ratio"),
        ("editImageTypeOptions", "/assets/image/edit", "image_type"),
        ("spritesheetModelOptions", "/assets/sprite/animate", "model"),
        ("spritesheetImageTypeOptions", "/assets/sprite/animate", "image_type"),
        ("frameOptions", "/assets/sprite/animate", "frames"),
        ("frameSizeOptions", "/assets/sprite/animate", "frame_size"),
        ("marginModeOptions", "/assets/sprite/animate", "margin_ratio_mode"),
        ("textureSizeOptions", "/assets/3d-model", "texture_size"),
        ("textureTypeOptions", "/assets/3d-model", "texture_type"),
        ("model3DModelOptions", "/assets/3d-model", "model"),
        ("model3DJointNamingOptions", "/assets/3d-model", "joint_naming"),
        ("directionalPerspectiveOptions", "/assets/sprite/directional", "perspective"),
        ("directionalPerspectiveOptions", "/assets/sprite/directional-stills", "perspective"),
        ("directionalPerspectiveOptions", "/assets/sprite/directional-preview", "perspective"),
        ("directionalDirectionOptions", "/assets/sprite/directional", "directions"),
        ("transferModelOptions", "/assets/sprite/transfer-motion", "model"),
        ("transferFrameSizeOptions", "/assets/sprite/transfer-motion", "frame_size"),
        ("transferMarginModeOptions", "/assets/sprite/transfer-motion", "margin_ratio_mode"),
        ("frameOptions", "/assets/sprite/transfer-motion", "frames"),
        ("directionalPerspectiveOptions", "/assets/sprite/transfer-motion", "perspective"),
        ("directionalDirectionOptions", "/assets/sprite/transfer-motion", "direction"),
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

    // Six style ids were renamed (Nov 4 2026). A style kept from an older build under an
    // old id must keep its look under the new id, never fall back to "Any style" or go out
    // as the old id. The map is the backend's (enumAliases.artStyles): by meaning, not name.
    static void ArtStyleRenamed(Ctx c)
    {
        var p = c.NewPlugin(new FakeLudoApi.Behavior { PollsUntilDone = 1, PollAfterMs = 1 });
        Tab(p, "Sprites", "currentSpriteTab", "Image");
        p.Set("imagePrompt", "a chest");
        var expected = new Dictionary<string, string>
        {
            ["8-Bit"] = "Micro Pixel", ["16-Bit"] = "8-Bit Pixel", ["32-Bit"] = "16-Bit Pixel",
            ["Hi-Bit"] = "32-Bit Pixel", ["Retro 2D"] = "Free Pixel", ["Anime/Manga"] = "Anime",
        };
        var options = ((IEnumerable)p.Get("artStyleOptions")).Cast<string>().ToList();
        var allowed = c.Contract.Enum("POST", "/assets/image", "art_style");
        foreach (var (old, now) in expected)
        {
            c.Check(!options.Contains(old), $"the dropdown still offers the old id '{old}'");
            c.Check(options.Contains(now) && allowed.Contains(now), $"'{now}' missing from the dropdown or the contract");
            // shown: the dropdown lands on the renamed style
            p.Set("imageArtStyle", old);
            p.Gui();
            c.Check(p.Get<string>("imageArtStyle") == now, $"'{old}' shown as '{p.Get<string>("imageArtStyle")}', expected '{now}'");
            // and goes out under its new id
            p.Set("imageArtStyle", old);
            p.Click("Generate Image(s)");
            c.Require(p.RunUntilIdle(20), $"{old}: generation never finished");
            c.Check((string)LastBody(c, "/assets/image")?["art_style"] == now, $"'{old}' sent as '{LastBody(c, "/assets/image")?["art_style"]}', expected '{now}'");
        }
        c.CheckClean(p, "/assets/image");
    }

    // ---------------------------------------------------------------- registry

    public static class LiveState
    {
        public static string ImageUrl;
        public static string ImageUrl2;
    }

    public static List<Scenario> All() => new()
    {
        new("generate", "images: generate 2, preview, save, select", Images, LiveToo: true),
        new("generate", "sprite animation: animate, preview, save", AnimateSprite, LiveToo: true),
        new("generate", "3D: create -> rig -> animate, with downloads", Model3DChain, LiveToo: true),
        new("generate", "3D: optional back view (URL, from project, clear)", Model3DBackView),
        new("live", "3D: back view accepted by the live API", Model3DBackViewLive, LiveToo: true),
        new("generate", "3D: prop/hero model choice, rig on create", Model3DHeroRig),
        new("generate", "directional sprites: styles, preset, preview, sheets, stills, saves", Directional),
        new("generate", "directional sprites: animation sources and hand-offs", DirectionalSources),
        new("generate", "my animations: generate, pick, mirror on directional sprites", MyAnimationsDirectional),
        new("generate", "my animations: animation clips and my animations on animate 3D", AnimationClips3D),
        new("generate", "my animations: animate sprite from a clip or my animation", AnimateSpriteFromClip),
        new("failure", "my animations: deleted animation, one source, list unavailable", AnimationValidation),
        new("generate", "normal maps saved next to the asset, use pixel grid, audio loop", NormalMapsAndOptions),
        new("failure", "directional preview replaced by a newer one (204)", DirectionalPreviewSuperseded),
        new("failure", "directional styles unavailable: message and retry", DirectionalStylesUnavailable),
        new("generate", "upscale image: from a card, from the project, size limit", Upscale),
        new("generate", "edit image: edit, edit with mask, erase with mask, mask size", EditImageModes),
        new("generate", "edit image: convert to pixel art (synchronous), detect without a grid", PixelArtMode),
        new("live", "directional sprites + upscale against the live API", DirectionalLive, LiveToo: true),
        new("generate", "audio: sound effect", c => Audio(c, "SoundEffect", "/audio/sound-effect", p => { p.Set("soundEffectDescription", "sword clash"); p.Set("soundEffectDuration", 2f); }, "Generate Sound Effect", "currentSoundEffect", "Save Sound Effect"), LiveToo: true),
        new("generate", "audio: music", c => Audio(c, "Music", "/audio/music", p => p.Set("musicDescription", "calm village theme"), "Generate Music", "currentMusic", "Save Music"), LiveToo: true),
        new("generate", "audio: voice", c => Audio(c, "Voice", "/audio/voice", p => { p.Set("voiceDescription", "old wizard"); p.Set("voiceText", "You shall not pass"); }, "Generate Voice", "currentVoice", "Save Voice"), LiveToo: true),
        new("generate", "audio: speech (clone)", c => Audio(c, "Speech", "/audio/speech", p => { p.Set("speechText", "Welcome, traveler"); p.Set("speechSample", FakeFile(c, "sample.mp3")); }, "Generate Speech", "currentSpeech", "Save Speech")),
        new("generate", "audio: speech (preset)", c => Audio(c, "SpeechPreset", "/audio/speech-preset", p => p.Set("speechPresetText", "Welcome, traveler"), "Generate Speech", "currentSpeechPreset", "Save Speech"), LiveToo: true),
        new("generate", "audio: preset voice families (emotion vs delivery)", SpeechPresetFamilies),
        new("generate", "audio: voice from character image", VoiceFromImage),
        new("generate", "audio: speech clone with delivery", SpeechCloneDelivery),
        new("failure", "job fails: reason shown, no result", JobFails),
        new("failure", "submit 429 queue full: reason shown", QueueFull),
        new("failure", "bad API key: reason shown", BadApiKey),
        new("failure", "400 validation: reason shown", ValidationError),
        new("failure", "poll 429: honours Retry-After, recovers", PollRateLimited),
        new("failure", "polling: long-polls and waits poll_after_ms", HonoursPollAfter),
        new("failure", "poll connection drop: recovers", PollConnectionDrop),
        new("failure", "submit 502 / lost response: retried with one request_id, one job", SubmitGatewayRetry),
        new("failure", "gateway down: readable message after retries", GatewayDown),
        new("failure", "job never finishes: progress, then timeout", LongJobShowsProgress),
        new("ui", "every screen renders", EveryScreenRenders),
        new("ui", "quiet when the user's own code logs errors", QuietOnUnrelatedErrors),
        new("ui", "dropdowns only offer values the API accepts", DropdownsMatchContract),
        new("ui", "animate: every model x duration is accepted", AnimateModelDurationSweep),
        new("ui", "animate: margin modes (auto / none / manual per-axis)", AnimateMargins),
        new("ui", "image: every style/perspective/type/ratio is accepted", ImageOptionSweep),
        new("ui", "image: a renamed art style keeps its look under the new id", ArtStyleRenamed),
    };
}
