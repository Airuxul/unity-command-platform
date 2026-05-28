using System;
using UnityCliConnector.Network;

namespace UnityCliConnector.Http
{
    public static class ConnectorHttpLifecycle
    {
        public static bool TryStart(
            ref HttpServer server,
            ref ConnectorListenConfig listen,
            ConnectorRequestDispatcher dispatcher,
            int port,
            string label,
            Action<string> log,
            Action<string> logError,
            Action onStarted = null)
        {
            try
            {
                listen = ConnectorNetwork.BuildListenConfig(port);
                server?.Dispose();
                server = new HttpServer(dispatcher, log, logError);
                server.Start(listen.BindPrefixes);
                onStarted?.Invoke();
                log?.Invoke(
                    $"[unity-connector] {label} ({listen.BindMode}) " +
                    $"http://{listen.AdvertiseHost}:{listen.Port}/");
                return true;
            }
            catch (Exception ex)
            {
                logError?.Invoke($"[unity-connector] {label} failed: {ex.Message}");
                return false;
            }
        }

        public static void Stop(ref HttpServer server) => server = Dispose(server);

        private static HttpServer Dispose(HttpServer server)
        {
            server?.Dispose();
            return null;
        }
    }
}
