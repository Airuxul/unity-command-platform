using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace UnityCliConnector.Http
{
    public sealed class HttpServer : IDisposable
    {
        private readonly IRequestDispatcher _dispatcher;
        private readonly Action<string> _log;
        private readonly Action<string> _logError;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public HttpServer(IRequestDispatcher dispatcher, Action<string> log = null, Action<string> logError = null)
        {
            _dispatcher = dispatcher;
            _log = log;
            _logError = logError;
        }

        public void Start(string host, int port) =>
            Start(new[] { $"http://{host}:{port}/" });

        public void Start(IReadOnlyList<string> prefixes)
        {
            if (_running || prefixes == null || prefixes.Count == 0)
                return;

            _listener = new HttpListener();
            foreach (var prefix in prefixes)
                _listener.Prefixes.Add(prefix);

            _listener.Start();
            _running = true;
            _thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "UnityCliConnector.Http",
            };
            _thread.Start();
            _log?.Invoke($"[unity-connector] listening on {string.Join(", ", prefixes)}");
        }

        public void Stop()
        {
            _running = false;
            var listener = _listener;
            var thread = _thread;
            _listener = null;
            _thread = null;

            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch
            {
                // ignored
            }

            if (thread != null && thread.IsAlive)
            {
                try
                {
                    thread.Join(2000);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public void Dispose() => Stop();

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
                }
                catch (Exception ex) when (IsBenignShutdownError(ex))
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        _logError?.Invoke($"[unity-connector] accept error: {ex.Message}");
                }
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            try
            {
                var path = ctx.Request.Url?.AbsolutePath ?? "/";
                var method = ctx.Request.HttpMethod?.ToUpperInvariant() ?? "GET";
                var body = ReadBody(ctx.Request);

                var authToken = ctx.Request.Headers["X-Unity-Cmd-Token"];
                if (string.IsNullOrEmpty(authToken))
                {
                    var auth = ctx.Request.Headers["Authorization"];
                    if (auth != null && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        authToken = auth.Substring("Bearer ".Length).Trim();
                }

                if (_dispatcher.TryDispatch(
                        method,
                        path,
                        body,
                        (status, payload) => WriteJson(ctx, status, payload),
                        authToken))
                    return;

                WriteJson(ctx, 404, new Dictionary<string, object> { ["ok"] = false, ["error"] = "not_found" });
            }
            catch (Exception ex)
            {
                WriteJson(ctx, 500, new Dictionary<string, object> { ["ok"] = false, ["error"] = ex.Message });
            }
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
                return "{}";
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static bool IsBenignShutdownError(Exception ex)
        {
            if (ex is HttpListenerException or ObjectDisposedException or ThreadAbortException)
                return true;

            var msg = ex.Message ?? "";
            if (msg.IndexOf("Thread was being aborted", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (msg.IndexOf("I/O operation has been aborted", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return ex.InnerException != null && IsBenignShutdownError(ex.InnerException);
        }

        private static void WriteJson(HttpListenerContext ctx, int status, Dictionary<string, object> payload)
        {
            var bytes = Encoding.UTF8.GetBytes(ConnectorJson.Serialize(payload));
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }
    }
}
