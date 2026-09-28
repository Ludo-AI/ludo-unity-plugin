using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace UnityStubs
{
    // What the fake editor recorded, and the knobs a test turns. Everything the plugin
    // would show a human (dialogs, logs, opened URLs, drawn popups) lands here.
    public static class Harness
    {
        public class LogEntry
        {
            public UnityEngine.LogType Type;
            public string Message;
            public override string ToString() { return Type + ": " + Message; }
        }

        public class Dialog
        {
            public string Title;
            public string Message;
            public override string ToString() { return "[" + Title + "] " + Message; }
        }

        public static readonly List<LogEntry> Logs = new List<LogEntry>();
        public static readonly List<Dialog> Dialogs = new List<Dialog>();
        public static readonly List<string> OpenedUrls = new List<string>();
        public static readonly List<string> HttpLog = new List<string>();

        // Popups drawn during the last OnGUI pass: label (or "") -> displayed options.
        public static readonly List<KeyValuePair<string, string[]>> Popups = new List<KeyValuePair<string, string[]>>();
        public static readonly List<string> Buttons = new List<string>();
        public static readonly List<string> Labels = new List<string>();

        // A button whose text equals this returns true once from GUILayout.Button.
        public static string ClickButton;

        public static bool EchoLogs = false;
        public static bool EchoHttp = false;

        public static string DataPath = "";
        public static string UnityVersion = "2022.3.20f1";

        // Answers for EditorUtility.SaveFilePanel / OpenFilePanel / SaveFolderPanel.
        public static Func<string, string, string, string> SaveFilePanel = (title, name, ext) => "";
        public static Func<string, string, string> OpenFilePanel = (title, ext) => "";
        public static Func<string, string> SaveFolderPanel = title => "";

        static readonly Stopwatch clock = Stopwatch.StartNew();
        public static double Now { get { return clock.Elapsed.TotalSeconds; } }

        public static void Reset()
        {
            Logs.Clear();
            Dialogs.Clear();
            OpenedUrls.Clear();
            HttpLog.Clear();
            ClickButton = null;
            UnityEditor.EditorPrefs.Clear();
        }

        public static void BeginGuiPass()
        {
            Popups.Clear();
            Buttons.Clear();
            Labels.Clear();
            UnityEngine.GUI.enabled = true;
        }

        // One editor frame: what Unity does between repaints.
        public static void Tick()
        {
            var update = UnityEditor.EditorApplication.update;
            if (update != null) update();
        }

        internal static void Log(UnityEngine.LogType type, object message)
        {
            var entry = new LogEntry { Type = type, Message = message == null ? "Null" : message.ToString() };
            lock (Logs) Logs.Add(entry);
            if (EchoLogs) Console.WriteLine("  [unity " + type + "] " + entry.Message);
            UnityEngine.Application.RaiseLog(entry.Message, "", type);
        }
    }
}
