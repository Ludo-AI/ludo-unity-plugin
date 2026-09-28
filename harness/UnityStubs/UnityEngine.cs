using System;
using System.IO;
using UnityStubs;

namespace UnityEngine
{
    public enum LogType { Error = 0, Assert = 1, Warning = 2, Log = 3, Exception = 4 }

    public class Object
    {
        public string name { get; set; }

        public static void DestroyImmediate(Object obj) { }
        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets) { }
        public static void Destroy(Object obj) { }

        public static implicit operator bool(Object exists) { return !ReferenceEquals(exists, null); }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() { return new T(); }
    }

    public static class Debug
    {
        public static void Log(object message) { Harness.Log(LogType.Log, message); }
        public static void LogWarning(object message) { Harness.Log(LogType.Warning, message); }
        public static void LogError(object message) { Harness.Log(LogType.Error, message); }
        public static void LogException(Exception exception) { Harness.Log(LogType.Exception, exception); }
        public static void LogAssertion(object message) { Harness.Log(LogType.Assert, message); }
        public static void LogFormat(string format, params object[] args) { Harness.Log(LogType.Log, string.Format(format, args)); }
        public static void LogErrorFormat(string format, params object[] args) { Harness.Log(LogType.Error, string.Format(format, args)); }
        public static void LogWarningFormat(string format, params object[] args) { Harness.Log(LogType.Warning, string.Format(format, args)); }
    }

    public static class Application
    {
        public delegate void LogCallback(string condition, string stackTrace, LogType type);

        public static event LogCallback logMessageReceived;

        public static string dataPath { get { return Harness.DataPath; } }
        public static string unityVersion { get { return Harness.UnityVersion; } }

        public static void OpenURL(string url) { Harness.OpenedUrls.Add(url); }

        internal static void RaiseLog(string condition, string stackTrace, LogType type)
        {
            var handler = logMessageReceived;
            if (handler != null) handler(condition, stackTrace, type);
        }
    }

    public static class Mathf
    {
        public static int RoundToInt(float f) { return (int)Math.Round(f, MidpointRounding.ToEven); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1); } }
        public static Color grey { get { return gray; } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color green { get { return new Color(0, 1, 0, 1); } }
        public static Color yellow { get { return new Color(1, 0.92f, 0.016f, 1); } }
        public static Color cyan { get { return new Color(0, 1, 1, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public enum TextureFormat { Alpha8 = 1, ARGB4444 = 2, RGB24 = 3, RGBA32 = 4, ARGB32 = 5 }
    public enum RenderTextureFormat { ARGB32 = 0, Default = 7 }
    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1 }
    public enum FontStyle { Normal = 0, Bold = 1, Italic = 2, BoldAndItalic = 3 }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum ScaleMode { StretchToFill, ScaleAndCrop, ScaleToFit }

    public class Texture : Object
    {
        public virtual int width { get; set; }
        public virtual int height { get; set; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }

    public class Texture2D : Texture
    {
        // The bytes the texture was loaded from, so EncodeToPNG can round-trip a PNG.
        internal byte[] SourceBytes;
        internal string SourceFormat;

        public Texture2D(int width, int height) { this.width = width; this.height = height; format = TextureFormat.RGBA32; }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) : this(width, height) { format = textureFormat; }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain, bool linear) : this(width, height) { format = textureFormat; }

        public TextureFormat format { get; private set; }

        public bool isReadable { get { return true; } }

        public Color32[] GetPixels32() { return new Color32[width * height]; }
        public void SetPixels32(Color32[] colors) { }
        public Color[] GetPixels() { return new Color[width * height]; }
        public void SetPixels(Color[] colors) { }
        public void ReadPixels(Rect source, int destX, int destY) { }
        public void ReadPixels(Rect source, int destX, int destY, bool recalculateMipMaps) { }
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }

    // Texture2D.LoadImage / EncodeToPNG / EncodeToJPG are extension methods in real Unity.
    public static class ImageConversion
    {
        public static bool LoadImage(this Texture2D tex, byte[] data) { return LoadImage(tex, data, false); }

        public static bool LoadImage(this Texture2D tex, byte[] data, bool markNonReadable)
        {
            int w, h;
            string format = FakeImage.Sniff(data, out w, out h);
            // Unity's LoadImage decodes PNG and JPG only.
            if (format != "png" && format != "jpg") return false;
            tex.width = w;
            tex.height = h;
            tex.SourceBytes = data;
            tex.SourceFormat = format;
            return true;
        }

        public static byte[] EncodeToPNG(this Texture2D tex)
        {
            if (tex.SourceFormat == "png") return tex.SourceBytes;
            return FakeImage.Png(Math.Max(1, tex.width), Math.Max(1, tex.height));
        }

        public static byte[] EncodeToJPG(this Texture2D tex) { return EncodeToPNG(tex); }
        public static byte[] EncodeToJPG(this Texture2D tex, int quality) { return EncodeToPNG(tex); }
    }

    public class RenderTexture : Texture
    {
        public static RenderTexture active { get; set; }

        public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format)
        {
            return new RenderTexture { width = width, height = height };
        }

        public static RenderTexture GetTemporary(int width, int height) { return GetTemporary(width, height, 0, RenderTextureFormat.Default); }

        public static void ReleaseTemporary(RenderTexture temp) { }
    }

    public static class Graphics
    {
        public static void Blit(Texture source, RenderTexture dest) { }
    }

    public class Sprite : Object { }

    public class AsyncOperation
    {
        public virtual bool isDone { get { return true; } }
        public float progress { get { return isDone ? 1f : 0f; } }
    }

    // ---- IMGUI ----

    public class GUIStyleState
    {
        public Color textColor { get; set; }
        public Texture2D background { get; set; }
    }

    public class RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
    }

    public class GUIStyle
    {
        public GUIStyle() { normal = new GUIStyleState(); hover = new GUIStyleState(); active = new GUIStyleState(); padding = new RectOffset(); margin = new RectOffset(); }
        public GUIStyle(GUIStyle other) : this() { if (other != null) { fontSize = other.fontSize; fontStyle = other.fontStyle; alignment = other.alignment; wordWrap = other.wordWrap; richText = other.richText; } }

        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public bool stretchWidth { get; set; }
        public float fixedHeight { get; set; }
        public float fixedWidth { get; set; }
        public GUIStyleState normal { get; set; }
        public GUIStyleState hover { get; set; }
        public GUIStyleState active { get; set; }
        public RectOffset padding { get; set; }
        public RectOffset margin { get; set; }

        public static implicit operator GUIStyle(string str) { return new GUIStyle(); }
    }

    public class GUIContent
    {
        public string text;
        public Texture image;
        public string tooltip;
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
        public GUIContent(string text, string tooltip) { this.text = text; this.tooltip = tooltip; }
        public GUIContent(Texture image) { this.image = image; }
    }

    public sealed class GUILayoutOption { }

    public static class GUI
    {
        public static bool enabled { get; set; } = true;
        public static Color backgroundColor { get; set; }
        public static Color contentColor { get; set; }
        public static Color color { get; set; }
    }

    public static class GUILayout
    {
        static bool Press(string text)
        {
            if (text != null) Harness.Buttons.Add(text);
            if (text != null && text == Harness.ClickButton && GUI.enabled)
            {
                Harness.ClickButton = null;
                return true;
            }
            return false;
        }

        public static void Label(string text, params GUILayoutOption[] options) { Harness.Labels.Add(text); }
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) { Harness.Labels.Add(text); }
        public static void Label(Texture image, params GUILayoutOption[] options) { }
        public static void Label(Texture image, GUIStyle style, params GUILayoutOption[] options) { }
        public static void Label(GUIContent content, params GUILayoutOption[] options) { Harness.Labels.Add(content == null ? null : content.text); }
        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options) { Harness.Labels.Add(content == null ? null : content.text); }

        public static bool Button(string text, params GUILayoutOption[] options) { return Press(text); }
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) { return Press(text); }
        public static bool Button(Texture image, params GUILayoutOption[] options) { return false; }
        public static bool Button(GUIContent content, params GUILayoutOption[] options) { return Press(content == null ? null : content.text); }
        public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options) { return Press(content == null ? null : content.text); }

        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) { return value; }
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) { return value; }
        public static string TextField(string text, params GUILayoutOption[] options) { return text; }
        public static string TextArea(string text, params GUILayoutOption[] options) { return text; }

        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) { }
        public static void BeginVertical(string style, params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) { return scrollPosition; }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, GUIStyle style, params GUILayoutOption[] options) { return scrollPosition; }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, params GUILayoutOption[] options) { return scrollPosition; }
        public static void EndScrollView() { }

        public static GUILayoutOption Width(float width) { return new GUILayoutOption(); }
        public static GUILayoutOption Height(float height) { return new GUILayoutOption(); }
        public static GUILayoutOption MinWidth(float minWidth) { return new GUILayoutOption(); }
        public static GUILayoutOption MaxWidth(float maxWidth) { return new GUILayoutOption(); }
        public static GUILayoutOption MinHeight(float minHeight) { return new GUILayoutOption(); }
        public static GUILayoutOption MaxHeight(float maxHeight) { return new GUILayoutOption(); }
        public static GUILayoutOption ExpandWidth(bool expand) { return new GUILayoutOption(); }
        public static GUILayoutOption ExpandHeight(bool expand) { return new GUILayoutOption(); }
    }

    public class MonoBehaviour : Object { }

    // ---- fake image bytes ----

    public static class FakeImage
    {
        // "png" | "jpg" | "webp" | "gif" | null, with dimensions where the header gives them.
        public static string Sniff(byte[] d, out int w, out int h)
        {
            w = 0; h = 0;
            if (d == null || d.Length < 12) return null;
            if (d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 && d.Length >= 24)
            {
                w = (d[16] << 24) | (d[17] << 16) | (d[18] << 8) | d[19];
                h = (d[20] << 24) | (d[21] << 16) | (d[22] << 8) | d[23];
                return "png";
            }
            if (d[0] == 0xFF && d[1] == 0xD8) { w = 64; h = 64; return "jpg"; }
            if (d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') { w = 64; h = 64; return "webp"; }
            if (d[0] == 'G' && d[1] == 'I' && d[2] == 'F') { w = 64; h = 64; return "gif"; }
            return null;
        }

        // A structurally valid PNG header (signature + IHDR) of the given size; enough
        // for anything that sniffs dimensions. Not a decodable image.
        public static byte[] Png(int width, int height)
        {
            var ms = new MemoryStream();
            ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            ms.Write(new byte[] { 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }, 0, 8);
            ms.Write(BE(width), 0, 4);
            ms.Write(BE(height), 0, 4);
            ms.Write(new byte[] { 8, 6, 0, 0, 0, 0, 0, 0, 0 }, 0, 9);
            return ms.ToArray();
        }

        static byte[] BE(int v) { return new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }; }
    }
}
