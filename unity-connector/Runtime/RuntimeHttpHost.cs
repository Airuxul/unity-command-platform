using System;
using UnityEngine;
using UnityCliConnector.Http;

namespace UnityCliConnector
{
    public static class RuntimeHttpHost
    {
        private static HttpServer _server;
        private static readonly RuntimeRequestDispatcher Dispatcher = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
#if UNITY_EDITOR
            return;
#else
            if (!Debug.isDebugBuild)
                return;
            Start();
#endif
        }

        public static void Start()
        {
            try
            {
                var port = ResolvePort();
                _server?.Dispose();
                _server = new HttpServer(Dispatcher, Debug.Log, Debug.LogWarning);
                _server.Start("127.0.0.1", port);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[unity-connector] runtime HTTP failed: {ex.Message}");
            }
        }

        public static void Stop()
        {
            _server?.Dispose();
            _server = null;
        }

        private static int ResolvePort()
        {
            var env = Environment.GetEnvironmentVariable("UNITY_CMD_RUNTIME_PORT");
            if (int.TryParse(env, out var p) && p > 0)
                return p;
            return 6500 + Math.Abs(Application.dataPath.GetHashCode() % 800);
        }
    }
}
