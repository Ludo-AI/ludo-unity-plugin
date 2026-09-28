using System.Diagnostics;
using UnityStubs;

namespace LudoHarness;

// dotnet run --project harness/Runner -- [--plugin <LudoAIPlugin.Editor.dll>] [--filter <text>] [--live] [--verbose]
//
//   default  fake API on localhost, enforcing the contract in harness/contract/
//   --live   the real API: LUDO_API_KEY (required), LUDO_API_URL (default https://api-dev.ludo.ai/api).
//            Spends credits; runs only scenarios marked LiveToo.
public static class Program
{
    public static int Main(string[] args)
    {
        string here = AppContext.BaseDirectory;
        string harnessDir = Path.GetFullPath(Path.Combine(here, "../../../.."));
        string plugin = Arg(args, "--plugin") ?? Path.Combine(harnessDir, "Plugin/bin/Debug/netstandard2.0/LudoAIPlugin.Editor.dll");
        string filter = Arg(args, "--filter");
        bool live = args.Contains("--live");
        Harness.EchoLogs = args.Contains("--verbose");
        Harness.EchoHttp = args.Contains("--verbose");

        PluginDriver.Load(plugin);
        var contract = new Contract(Path.Combine(harnessDir, "contract/openapi-public.json"));
        string outRoot = Path.Combine(Path.GetTempPath(), "ludo-unity-harness", DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        FakeLudoApi api = null;
        string apiUrl, apiKey;
        if (live)
        {
            apiKey = Environment.GetEnvironmentVariable("LUDO_API_KEY");
            apiUrl = Environment.GetEnvironmentVariable("LUDO_API_URL") ?? "https://api-dev.ludo.ai/api";
            if (string.IsNullOrEmpty(apiKey)) { Console.Error.WriteLine("--live needs LUDO_API_KEY"); return 2; }
        }
        else
        {
            api = new FakeLudoApi(contract);
            apiUrl = api.BaseUrl;
            apiKey = api.ApiKey;
        }

        Console.WriteLine($"plugin   {plugin} ({File.GetLastWriteTime(plugin):yyyy-MM-dd HH:mm})");
        Console.WriteLine($"api      {(live ? "LIVE " : "fake ")}{apiUrl}");
        Console.WriteLine($"output   {outRoot}");
        foreach (var f in contract.PendingFields) Console.WriteLine($"pending  {f} - not in the published spec yet");
        Console.WriteLine();

        int passed = 0, failed = 0;
        foreach (var s in Scenarios.All())
        {
            if (live && !s.LiveToo) continue;
            if (filter != null && !($"{s.Group} {s.Name}").Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            var ctx = new Ctx { Api = api, Contract = contract, ApiUrl = apiUrl, ApiKey = apiKey, OutDir = Path.Combine(outRoot, Slug(s.Name)) };
            var sw = Stopwatch.StartNew();
            try { s.Run(ctx); }
            catch (ScenarioFailed e) { ctx.Failures.Add(e.Message); }
            catch (Exception e) { ctx.Failures.Add($"{e.GetType().Name}: {e.Message}\n        {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); }
            finally { UnityEditor.EditorApplication.update = null; }

            bool ok = ctx.Failures.Count == 0;
            if (ok) passed++; else failed++;
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  [{s.Group}] {s.Name}  ({sw.Elapsed.TotalSeconds:F1}s)");
            foreach (var f in ctx.Failures.Distinct()) Console.WriteLine("        - " + f);
            foreach (var n in ctx.Notes) Console.WriteLine("        . " + n);
        }

        Console.WriteLine();
        Console.WriteLine($"{passed} passed, {failed} failed");
        api?.Dispose();
        return failed == 0 ? 0 : 1;
    }

    static string Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static string Slug(string s) => new string(s.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}
