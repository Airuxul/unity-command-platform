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

        public void Start(string host, int port)
        {
            if (_running)
                return;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{host}:{port}/");
            _listener.Start();
            _running = true;
            _thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "UnityCliConnector.Http",
            };
            _thread.Start();
            _log?.Invoke($"[unity-connector] listening on http://{host}:{port}/");
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // ignored
            }

            _listener = null;
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
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
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

                if (_dispatcher.TryDispatch(method, path, body, (status, payload) => WriteJson(ctx, status, payload)))
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

        private static void WriteJson(HttpListenerContext ctx, int status, Dictionary<string, object> payload)
        {
            var bytes = Encoding.UTF8.GetBytes(SimpleJson.Serialize(payload));
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }
    }
}
