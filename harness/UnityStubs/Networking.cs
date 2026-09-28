using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityStubs;

namespace UnityEngine.Networking
{
    public class UploadHandler : IDisposable
    {
        public byte[] data { get; protected set; }
        public string contentType { get; set; }
        public void Dispose() { }
    }

    public class UploadHandlerRaw : UploadHandler
    {
        public UploadHandlerRaw(byte[] data) { this.data = data; }
    }

    public class DownloadHandler : IDisposable
    {
        internal byte[] bytes;
        public byte[] data { get { return bytes; } }
        public string text { get { return bytes == null ? "" : Encoding.UTF8.GetString(bytes); } }
        public bool isDone { get; internal set; }
        public void Dispose() { }
    }

    public class DownloadHandlerBuffer : DownloadHandler { }

    public class DownloadHandlerTexture : DownloadHandler
    {
        public DownloadHandlerTexture() { }
        public DownloadHandlerTexture(bool readable) { }

        public Texture2D texture
        {
            get
            {
                var tex = new Texture2D(2, 2);
                return tex.LoadImage(bytes) ? tex : null;
            }
        }

        public static Texture2D GetContent(UnityWebRequest www)
        {
            var handler = www.downloadHandler as DownloadHandlerTexture;
            if (handler == null) throw new InvalidOperationException("DownloadHandlerTexture.GetContent: request has no DownloadHandlerTexture");
            return handler.texture;
        }
    }

    public class UnityWebRequestAsyncOperation : AsyncOperation
    {
        internal Task task;
        public UnityWebRequest webRequest { get; internal set; }
        public override bool isDone { get { return task == null || task.IsCompleted; } }
    }

    public static class UnityWebRequestTexture
    {
        public static UnityWebRequest GetTexture(string uri)
        {
            return new UnityWebRequest(uri, "GET") { downloadHandler = new DownloadHandlerTexture() };
        }

        public static UnityWebRequest GetTexture(string uri, bool nonReadable) { return GetTexture(uri); }
    }

    // UnityWebRequest over HttpClient, keeping the parts of Unity's contract the plugin
    // leans on: 2xx/3xx is Result.Success (so a 202 is a "success"), >= 400 is
    // ProtocolError with error "HTTP/1.1 <code> <reason>" and the body still readable,
    // transport failures are ConnectionError with responseCode 0, redirects followed.
    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress = 0, Success = 1, ConnectionError = 2, ProtocolError = 3, DataProcessingError = 4 }

        public const string kHttpVerbGET = "GET";
        public const string kHttpVerbPOST = "POST";

        static readonly HttpClient client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        { Timeout = Timeout.InfiniteTimeSpan };

        readonly Dictionary<string, string> requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool sent;

        public string url { get; set; }
        public string method { get; set; }
        public int timeout { get; set; }
        public UploadHandler uploadHandler { get; set; }
        public DownloadHandler downloadHandler { get; set; }
        public bool disposeDownloadHandlerOnDispose { get; set; } = true;
        public bool disposeUploadHandlerOnDispose { get; set; } = true;

        public long responseCode { get; private set; }
        public string error { get; private set; }
        public Result result { get; private set; }
        public bool isDone { get; private set; }
        public float downloadProgress { get { return isDone ? 1f : 0f; } }
        public float uploadProgress { get { return isDone ? 1f : 0f; } }

        public UnityWebRequest() { method = "GET"; }
        public UnityWebRequest(string url) : this() { this.url = url; }
        public UnityWebRequest(string url, string method) { this.url = url; this.method = method; }
        public UnityWebRequest(string url, string method, DownloadHandler downloadHandler, UploadHandler uploadHandler)
        {
            this.url = url; this.method = method; this.downloadHandler = downloadHandler; this.uploadHandler = uploadHandler;
        }

        public static UnityWebRequest Get(string uri)
        {
            return new UnityWebRequest(uri, "GET", new DownloadHandlerBuffer(), null);
        }

        public void SetRequestHeader(string name, string value)
        {
            if (sent) throw new InvalidOperationException("UnityWebRequest has already been sent; cannot set headers");
            requestHeaders[name] = value;
        }

        public string GetRequestHeader(string name)
        {
            string v;
            return requestHeaders.TryGetValue(name, out v) ? v : null;
        }

        public string GetResponseHeader(string name)
        {
            string v;
            return responseHeaders.TryGetValue(name, out v) ? v : null;
        }

        public Dictionary<string, string> GetResponseHeaders() { return new Dictionary<string, string>(responseHeaders); }

        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            if (sent) throw new InvalidOperationException("UnityWebRequest has already been sent");
            sent = true;
            result = Result.InProgress;
            var op = new UnityWebRequestAsyncOperation { webRequest = this };
            op.task = Task.Run(() => Execute());
            return op;
        }

        async Task Execute()
        {
            var cts = timeout > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout)) : new CancellationTokenSource();
            try
            {
                var msg = new HttpRequestMessage(new HttpMethod(method ?? "GET"), url);
                string contentType = null;
                foreach (var kv in requestHeaders)
                {
                    if (string.Equals(kv.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)) { contentType = kv.Value; continue; }
                    msg.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
                if (uploadHandler != null && uploadHandler.data != null)
                {
                    msg.Content = new ByteArrayContent(uploadHandler.data);
                    msg.Content.Headers.TryAddWithoutValidation("Content-Type", contentType ?? uploadHandler.contentType ?? "application/octet-stream");
                }

                using (var resp = await client.SendAsync(msg, cts.Token).ConfigureAwait(false))
                {
                    responseCode = (long)resp.StatusCode;
                    foreach (var h in resp.Headers) responseHeaders[h.Key] = string.Join(", ", h.Value);
                    foreach (var h in resp.Content.Headers) responseHeaders[h.Key] = string.Join(", ", h.Value);
                    byte[] body = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (downloadHandler != null)
                    {
                        downloadHandler.bytes = body;
                        downloadHandler.isDone = true;
                    }
                    if (responseCode >= 400)
                    {
                        result = Result.ProtocolError;
                        error = "HTTP/1.1 " + responseCode + " " + resp.ReasonPhrase;
                    }
                    else
                    {
                        result = Result.Success;
                    }
                    Record(body == null ? 0 : body.Length);
                }
            }
            catch (OperationCanceledException)
            {
                responseCode = 0;
                result = Result.ConnectionError;
                error = "Request timeout";
                Record(0);
            }
            catch (Exception e)
            {
                responseCode = 0;
                result = Result.ConnectionError;
                error = "Cannot connect to destination host (" + e.GetBaseException().Message + ")";
                Record(0);
            }
            finally
            {
                isDone = true;
                cts.Dispose();
            }
        }

        void Record(int bytes)
        {
            string line = (method ?? "GET") + " " + url + " -> " + responseCode + " (" + bytes + " bytes)";
            lock (Harness.HttpLog) Harness.HttpLog.Add(line);
            if (Harness.EchoHttp) Console.WriteLine("  [uwr] " + line);
        }

        public void Abort() { }
        public void Dispose() { }
    }
}
