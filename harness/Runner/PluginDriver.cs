using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using UnityStubs;

namespace LudoHarness;

// Hosts one LudoAIPlugin window the way the Unity editor does: OnEnable, then OnGUI
// passes interleaved with EditorApplication.update ticks (which is what drives the
// bundled Editor Coroutines package). Inputs are set on the plugin's own fields,
// actions are taken by clicking its real buttons.
public class PluginDriver
{
    public static Assembly PluginAssembly;
    static Type windowType;

    public static void Load(string dllPath)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(dllPath))!;
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            string candidate = Path.Combine(dir, name.Name + ".dll");
            return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
        };
        PluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
        windowType = PluginAssembly.GetType("LudoAIPlugin", throwOnError: true)!;
    }

    public readonly object Window;
    const BindingFlags All = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public PluginDriver(string apiUrl, string apiKey)
    {
        // Leftovers from a previous scenario would keep ticking otherwise.
        UnityEditor.EditorApplication.update = null;
        Harness.Reset();
        UnityEditor.EditorPrefs.SetString("LudoAI_API_Key", apiKey);
        Window = Activator.CreateInstance(windowType)!;
        Set("apiUrl", apiUrl);
        Invoke("OnEnable");
    }

    public void Close() => Invoke("OnDisable");

    public bool Has(string field) => windowType.GetField(field, All) != null;

    public T Get<T>(string field)
    {
        var f = windowType.GetField(field, All) ?? throw new MissingFieldException("LudoAIPlugin", field);
        return (T)f.GetValue(Window);
    }

    public object Get(string field) => (windowType.GetField(field, All) ?? throw new MissingFieldException("LudoAIPlugin", field)).GetValue(Window);

    public void Set(string field, object value)
    {
        var f = windowType.GetField(field, All) ?? throw new MissingFieldException("LudoAIPlugin", field);
        if (f.FieldType.IsEnum && value is string s) value = Enum.Parse(f.FieldType, s);
        f.SetValue(Window, value);
    }

    public object Invoke(string method, params object[] args)
    {
        var m = windowType.GetMethod(method, All) ?? throw new MissingMethodException("LudoAIPlugin", method);
        try { return m.Invoke(Window, args); }
        catch (TargetInvocationException e) { throw e.InnerException!; }
    }

    // Nested data classes (GeneratedImage, AnimationClip3D, ...) are read by property.
    public static object Prop(object o, string name) => o?.GetType().GetProperty(name)?.GetValue(o);
    public static int Count(object list) => list == null ? 0 : ((ICollection)list).Count;

    public void Gui()
    {
        Harness.BeginGuiPass();
        Invoke("OnGUI");
    }

    // Clicks a button by its label on the current screen. Fails if the button is not
    // drawn or is disabled (GUI.enabled false), just as a user could not click it.
    public void Click(string label)
    {
        Harness.ClickButton = label;
        Gui();
        if (Harness.ClickButton != null)
        {
            bool drawn = Harness.Buttons.Contains(label);
            Harness.ClickButton = null;
            throw new InvalidOperationException(drawn
                ? $"button '{label}' is drawn but disabled"
                : $"button '{label}' is not on screen (buttons: {string.Join(" | ", Harness.Buttons.Distinct())})");
        }
    }

    public string Status => Get<string>("statusMessage");
    public bool Processing => Get<bool>("isProcessing");

    // Runs editor frames until the plugin is idle and every coroutine has finished.
    public bool RunUntilIdle(double timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            Harness.Tick();
            Gui();
            if (!Processing && UnityEditor.EditorApplication.update == null) return true;
            Thread.Sleep(5);
        }
        return false;
    }

    // Runs frames for a while regardless of state (for "is it still polling?" checks).
    public void RunFor(double seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until) { Harness.Tick(); Gui(); Thread.Sleep(5); }
    }

    public List<Harness.Dialog> Dialogs => Harness.Dialogs.ToList();
    public List<Harness.Dialog> ErrorDialogs => Harness.Dialogs.Where(d => d.Title.Contains("Error") || d.Title.Contains("Failed")).ToList();
}
