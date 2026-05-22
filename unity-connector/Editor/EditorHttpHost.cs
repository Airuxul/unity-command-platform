using System;
using UnityEditor;
using UnityEngine;
using UnityCliConnector.Http;

namespace UnityCliConnector
{
    [InitializeOnLoad]
    public static class EditorHttpHost
    {
        private static HttpServer _server;
        private static readonly EditorRequestDispatcher Dispatcher = new();

        static EditorHttpHost()
        {
            EditorApplication.quitting += Stop;
            Stop();
            Start();
        }

        public static void Start()
        {
            try
            {
                var port = ResolvePort();
                _server?.Dispose();
                _server = new HttpServer(Dispatcher, Debug.Log, Debug.LogWarning);
                _server.Start("127.0.0.1", port);
                HeartbeatWriter.SetEndpoint("127.0.0.1", port);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[unity-connector] failed to start HTTP server: {ex.Message}");
            }
        }

        public static void Stop()
        {
            _server?.Dispose();
            _server = null;
        }

        private static int ResolvePort()
        {
            var env = Environment.GetEnvironmentVariable("UNITY_CMD_PORT");
            if (int.TryParse(env, out var p) && p > 0)
                return p;

            var hash = Application.dataPath.GetHashCode();
            return 6400 + Math.Abs(hash % 800);
        }
    }
}
