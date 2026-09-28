using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityStubs;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public class EditorWindow : ScriptableObject
    {
        public GUIContent titleContent { get; set; } = new GUIContent();
        public Rect position { get; set; } = new Rect(0, 0, 600, 900);
        public Vector2 minSize { get; set; }
        public Vector2 maxSize { get; set; }
        public int RepaintCount { get; private set; }

        public void Repaint() { RepaintCount++; }
        public void Show() { }
        public void Close() { }
        public void Focus() { }

        public static T GetWindow<T>() where T : EditorWindow, new() { return new T(); }
        public static T GetWindow<T>(string title) where T : EditorWindow, new() { var w = new T(); w.titleContent = new GUIContent(title); return w; }
        public static T GetWindow<T>(bool utility, string title) where T : EditorWindow, new() { return GetWindow<T>(title); }
    }

    public static class EditorApplication
    {
        public delegate void CallbackFunction();

        public static CallbackFunction update;
        public static double timeSinceStartup { get { return Harness.Now; } }
        public static bool isCompiling { get { return false; } }
        public static bool isPlaying { get { return false; } }
    }

    public static class EditorPrefs
    {
        static readonly Dictionary<string, object> store = new Dictionary<string, object>();

        internal static void Clear() { store.Clear(); }

        public static string GetString(string key) { return GetString(key, ""); }
        public static string GetString(string key, string defaultValue) { object v; return store.TryGetValue(key, out v) ? (string)v : defaultValue; }
        public static void SetString(string key, string value) { store[key] = value; }
        public static int GetInt(string key, int defaultValue) { object v; return store.TryGetValue(key, out v) ? (int)v : defaultValue; }
        public static void SetInt(string key, int value) { store[key] = value; }
        public static float GetFloat(string key, float defaultValue) { object v; return store.TryGetValue(key, out v) ? (float)v : defaultValue; }
        public static void SetFloat(string key, float value) { store[key] = value; }
        public static bool GetBool(string key, bool defaultValue) { object v; return store.TryGetValue(key, out v) ? (bool)v : defaultValue; }
        public static void SetBool(string key, bool value) { store[key] = value; }
        public static bool HasKey(string key) { return store.ContainsKey(key); }
        public static void DeleteKey(string key) { store.Remove(key); }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok)
        {
            Harness.Dialogs.Add(new Harness.Dialog { Title = title, Message = message });
            return true;
        }

        public static bool DisplayDialog(string title, string message, string ok, string cancel)
        {
            Harness.Dialogs.Add(new Harness.Dialog { Title = title, Message = message });
            return true;
        }

        public static string SaveFilePanel(string title, string directory, string defaultName, string extension) { return Harness.SaveFilePanel(title, defaultName, extension); }
        public static string OpenFilePanel(string title, string directory, string extension) { return Harness.OpenFilePanel(title, extension); }
        public static string SaveFolderPanel(string title, string folder, string defaultName) { return Harness.SaveFolderPanel(title); }
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
        public static void RevealInFinder(string path) { }
    }

    public static class AssetDatabase
    {
        static string Abs(string assetPath)
        {
            // "Assets/..." resolves against Application.dataPath (".../Assets").
            string root = Path.GetDirectoryName(Harness.DataPath.TrimEnd('/', '\\')) ?? "";
            return Path.Combine(root, assetPath);
        }

        public static void Refresh() { }
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object { return null; }
        public static bool IsValidFolder(string path) { return Directory.Exists(Abs(path)); }

        public static string CreateFolder(string parentFolder, string newFolderName)
        {
            Directory.CreateDirectory(Abs(Path.Combine(parentFolder, newFolderName)));
            return Guid.NewGuid().ToString("N");
        }

        public static void ImportAsset(string path) { }
    }

    public static class Selection
    {
        public static UnityEngine.Object activeObject { get; set; }
    }

    public static class EditorGUIUtility
    {
        public static void PingObject(UnityEngine.Object obj) { }
        public static float currentViewWidth { get { return 600f; } }
    }

    public enum MessageType { None = 0, Info = 1, Warning = 2, Error = 3 }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel = new GUIStyle();
        public static GUIStyle label = new GUIStyle();
        public static GUIStyle helpBox = new GUIStyle();
        public static GUIStyle toolbarButton = new GUIStyle();
        public static GUIStyle wordWrappedLabel = new GUIStyle();
        public static GUIStyle miniLabel = new GUIStyle();
        public static GUIStyle miniButton = new GUIStyle();
        public static GUIStyle largeLabel = new GUIStyle();
        public static GUIStyle centeredGreyMiniLabel = new GUIStyle();
        public static GUIStyle textArea = new GUIStyle();
        public static GUIStyle toolbar = new GUIStyle();
    }

    public static class EditorGUILayout
    {
        static int Popup(string label, int selectedIndex, string[] displayedOptions)
        {
            Harness.Popups.Add(new KeyValuePair<string, string[]>(label ?? "", displayedOptions));
            // Real Unity draws an out-of-range index as an empty selection; it doesn't throw.
            return selectedIndex;
        }

        public static int Popup(int selectedIndex, string[] displayedOptions, params GUILayoutOption[] options) { return Popup(null, selectedIndex, displayedOptions); }
        public static int Popup(int selectedIndex, string[] displayedOptions, GUIStyle style, params GUILayoutOption[] options) { return Popup(null, selectedIndex, displayedOptions); }
        public static int Popup(string label, int selectedIndex, string[] displayedOptions, params GUILayoutOption[] options) { return Popup(label, selectedIndex, displayedOptions); }
        public static int IntPopup(string label, int selectedValue, string[] displayedOptions, int[] optionValues, params GUILayoutOption[] options)
        {
            Harness.Popups.Add(new KeyValuePair<string, string[]>(label ?? "", displayedOptions));
            return selectedValue;
        }

        public static string TextField(string text, params GUILayoutOption[] options) { return text; }
        public static string TextField(string label, string text, params GUILayoutOption[] options) { return text; }
        public static string TextArea(string text, params GUILayoutOption[] options) { return text; }
        public static string TextArea(string text, GUIStyle style, params GUILayoutOption[] options) { return text; }
        public static bool Toggle(bool value, params GUILayoutOption[] options) { return value; }
        public static bool Toggle(string label, bool value, params GUILayoutOption[] options) { return value; }
        public static bool ToggleLeft(string label, bool value, params GUILayoutOption[] options) { return value; }
        public static float Slider(float value, float leftValue, float rightValue, params GUILayoutOption[] options) { return value; }
        public static float Slider(string label, float value, float leftValue, float rightValue, params GUILayoutOption[] options) { return value; }
        public static int IntSlider(int value, int leftValue, int rightValue, params GUILayoutOption[] options) { return value; }
        public static int IntSlider(string label, int value, int leftValue, int rightValue, params GUILayoutOption[] options) { return value; }
        public static int IntField(int value, params GUILayoutOption[] options) { return value; }
        public static int IntField(string label, int value, params GUILayoutOption[] options) { return value; }
        public static float FloatField(float value, params GUILayoutOption[] options) { return value; }
        public static float FloatField(string label, float value, params GUILayoutOption[] options) { return value; }
        public static void LabelField(string label, params GUILayoutOption[] options) { Harness.Labels.Add(label); }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { Harness.Labels.Add(label); }
        public static void LabelField(string label, string label2, params GUILayoutOption[] options) { Harness.Labels.Add(label + " " + label2); }
        public static void HelpBox(string message, MessageType type) { Harness.Labels.Add(message); }
        public static void HelpBox(string message, MessageType type, bool wide) { Harness.Labels.Add(message); }
        public static void Space() { }
        public static void Space(float width) { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static void SelectableLabel(string text, params GUILayoutOption[] options) { Harness.Labels.Add(text); }
    }
}

namespace UnityEditor.Compilation
{
    public enum CompilerMessageType { Error = 0, Warning = 1, Info = 2 }

    public struct CompilerMessage
    {
        public string message;
        public string file;
        public int line;
        public int column;
        public CompilerMessageType type;
    }

    public static class CompilationPipeline
    {
        public static event Action<string, CompilerMessage[]> assemblyCompilationFinished;
        public static void RequestScriptCompilation() { }
    }
}

namespace UnityEditor.PackageManager
{
    public enum StatusCode { InProgress = 0, Success = 1, Failure = 2 }

    public class Error
    {
        public string message { get; set; }
    }

    public class PackageInfo
    {
        public string name { get; set; }
        public string version { get; set; }
    }

    public static class Client
    {
        public static Requests.AddRequest Add(string identifier) { return new Requests.AddRequest(); }
    }
}

namespace UnityEditor.PackageManager.Requests
{
    public abstract class Request
    {
        public bool IsCompleted { get { return true; } }
        public StatusCode Status { get { return StatusCode.Success; } }
        public Error Error { get { return null; } }
    }

    public abstract class Request<T> : Request
    {
        public T Result { get { return default(T); } }
    }

    public sealed class AddRequest : Request<PackageInfo> { }
}

namespace WebP
{
    public enum Error { Success = 0, InvalidHeader = 20, DecodingError = 30 }

    public static class Texture2DExt
    {
        public delegate void ScalingFunction(ref int width, ref int height);

        // The real one is native libwebp; here a RIFF/WEBP header is enough to "decode".
        public static Texture2D CreateTexture2DFromWebP(byte[] lData, bool lMipmaps, bool lLinear, out Error lError, ScalingFunction scalingFunction = null, bool makeNoLongerReadable = true)
        {
            int w, h;
            if (FakeImage.Sniff(lData, out w, out h) != "webp") { lError = Error.InvalidHeader; return null; }
            lError = Error.Success;
            return new Texture2D(w, h);
        }
    }
}
