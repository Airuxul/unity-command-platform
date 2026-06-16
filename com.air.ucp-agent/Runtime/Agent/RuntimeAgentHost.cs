using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json;
using UnityEngine;

namespace Air.UcpAgent.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RuntimeAgentHost : MonoBehaviour
    {
        const int DefaultPort = 6620;
        const int PortMin = 6620;
        const int PortMax = 6699;

        static RuntimeAgentHost _instance;

        HttpListener _listener;
        Thread _listenerThread;
        volatile bool _running;
        string _projectPath;
        int _port;
        string _bindHost;
        List<string> _capabilities = new List<string>();
        volatile string _healthJson = "{\"ok\":false,\"type\":\"runtime\",\"capabilities\":[]}";

        readonly ConcurrentQueue<PendingRequest> _pending = new();

        sealed class PendingRequest
        {
            public HttpListenerContext Context;
            public UcpCommand Command;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Application.isPlaying || _instance != null)
                return;

            var go = new GameObject("UcpRuntimeAgent");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RuntimeAgentHost>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            _bindHost = ResolveBindHost();
            _capabilities = RuntimeCommandService.BuildCapabilities();

            try
            {
                _port = PickPort();
                StartListener();
                UcpLog.Log($"[ucp-agent] Runtime HTTP listening on http://{_bindHost}:{_port}/");
            }
            catch (Exception ex)
            {
                UcpLog.LogWarning("[ucp-agent] Runtime agent failed to start: " + ex.Message);
            }
        }

        void OnDestroy()
        {
            Shutdown();
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
                Shutdown();
        }

        public static void ShutdownForPlayModeExit()
        {
            if (_instance == null)
                return;

            _instance.Shutdown();
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        void Shutdown()
        {
            StopListener();
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            RefreshHealthCache();

            while (_pending.TryDequeue(out var pending))
            {
                UcpResult result;
                try
                {
                    result = RuntimeCommandService.Execute(pending.Command);
                }
                catch (Exception ex)
                {
                    result = new UcpResult
                    {
                        id = pending.Command?.id,
                        success = false,
                        duration = 0,
                        error = "handler_exception",
                        message = ex.Message,
                    };
                }

                WriteJsonResponse(pending.Context, 200, result);
            }
        }

        void StartListener()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{_bindHost}:{_port}/");
            _listener.Start();
            _running = true;

            _listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "UcpRuntimeHttp",
            };
            _listenerThread.Start();
        }

        void StopListener()
        {
            _running = false;
            _healthJson = "{\"ok\":false,\"type\":\"runtime\",\"capabilities\":[]}";

            try
            {
                _listener?.Stop();
            }
            catch
            {
                // ignored
            }

            try
            {
                _listener?.Close();
            }
            catch
            {
                // ignored
            }

            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                try
                {
                    _listenerThread.Join(2000);
                }
                catch
                {
                    // ignored
                }
            }

            _listenerThread = null;
            _listener = null;
        }

        void ListenLoop()
        {
            while (_running && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context = null;
                try
                {
                    context = _listener.GetContext();
                }
                catch
                {
                    if (!_running)
                        break;
                    continue;
                }

                if (context == null)
                    continue;

                try
                {
                    HandleContext(context);
                }
                catch (Exception ex)
                {
                    try
                    {
                        WriteJsonResponse(
                            context,
                            500,
                            new UcpResult
                            {
                                id = null,
                                success = false,
                                duration = 0,
                                error = "runtime_http_error",
                                message = ex.Message,
                            });
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        void HandleContext(HttpListenerContext context)
        {
            var request = context.Request;
            if (request.Url == null)
            {
                WriteNotFound(context);
                return;
            }

            var path = request.Url.AbsolutePath.TrimEnd('/');
            if (string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                WriteHealthResponse(context);
                return;
            }

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                || !path.EndsWith("/command", StringComparison.OrdinalIgnoreCase))
            {
                WriteNotFound(context);
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                body = reader.ReadToEnd();

            UcpCommand command;
            try
            {
                command = JsonConvert.DeserializeObject<UcpCommand>(body);
            }
            catch (Exception ex)
            {
                WriteJsonResponse(
                    context,
                    400,
                    new UcpResult
                    {
                        id = null,
                        success = false,
                        duration = 0,
                        error = "invalid_json",
                        message = ex.Message,
                    });
                return;
            }

            if (command == null || string.IsNullOrEmpty(command.id))
            {
                WriteJsonResponse(
                    context,
                    400,
                    new UcpResult
                    {
                        id = command?.id,
                        success = false,
                        duration = 0,
                        error = "invalid_command",
                        message = "Command id is required",
                    });
                return;
            }

            _pending.Enqueue(new PendingRequest { Context = context, Command = command });
        }

        void WriteHealthResponse(HttpListenerContext context)
        {
            var json = !_running
                ? "{\"ok\":false,\"type\":\"runtime\",\"capabilities\":[]}"
                : (_healthJson ?? "{\"ok\":false,\"type\":\"runtime\",\"capabilities\":[]}");
            var bytes = Encoding.UTF8.GetBytes(json);
            var response = context.Response;
            response.StatusCode = 200;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        void RefreshHealthCache()
        {
            var payload = new Dictionary<string, object>
            {
                ["ok"] = Application.isPlaying,
                ["type"] = "runtime",
                ["capabilities"] = _capabilities,
                ["path"] = _projectPath,
                ["product"] = Application.productName,
                ["port"] = _port,
            };
            _healthJson = JsonConvert.SerializeObject(payload);
        }

        static void WriteNotFound(HttpListenerContext context)
        {
            WriteJsonResponse(
                context,
                404,
                new UcpResult
                {
                    id = null,
                    success = false,
                    duration = 0,
                    error = "not_found",
                    message = "Use GET /health or POST /command",
                });
        }

        static void WriteJsonResponse(HttpListenerContext context, int statusCode, UcpResult result)
        {
            WriteRawJson(context, statusCode, result);
        }

        static void WriteRawJson(HttpListenerContext context, int statusCode, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            var response = context.Response;
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        static string ResolveBindHost()
        {
            var env = Environment.GetEnvironmentVariable("UCP_RUNTIME_BIND");
            if (string.IsNullOrWhiteSpace(env) || env == "+" || env == "*")
                return "127.0.0.1";
            return env.Trim();
        }

        static int PickPort()
        {
            var envPort = Environment.GetEnvironmentVariable("UCP_RUNTIME_PORT");
            if (int.TryParse(envPort, out var preferred) && preferred > 0 && IsPortAvailable(preferred))
                return preferred;

            if (IsPortAvailable(DefaultPort))
                return DefaultPort;

            for (var port = PortMin; port <= PortMax; port++)
            {
                if (port == DefaultPort)
                    continue;
                if (IsPortAvailable(port))
                    return port;
            }

            throw new InvalidOperationException($"No free runtime port in range {PortMin}-{PortMax}");
        }

        static bool IsPortAvailable(int port)
        {
            try
            {
                var probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                probe.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
