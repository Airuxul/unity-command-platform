using System;
using Air.UnityConnector.Host;
using Air.UnityConnector.Http;

namespace Air.UnityConnector.Http
{
    public static class ConnectorHttpLifecycle
    {
        public static bool TryStart(
            ref HttpListenerHost server,
            ref HostListenOptions listen,
            ConnectorRequestDispatcher dispatcher,
            int port,
            string label,
            Action<string> log,
            Action<string> logError,
            Action onStarted = null,
            bool requirePortFree = true)
        {
            listen = HostNetwork.BuildListenOptions(port);
            var probeHost = ResolveProbeHost(listen);

            if (requirePortFree && PortReachability.IsPortOpen(probeHost, port))
            {
                logError?.Invoke(FormatPortInUseMessage(label, probeHost, port));
                return false;
            }

            try
            {
                server?.Dispose();
                server = new HttpListenerHost(dispatcher, log, logError);
                server.Start(listen.BindPrefixes);
                onStarted?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                server?.Dispose();
                server = null;

                if (IsAddressInUseError(ex))
                {
                    logError?.Invoke(FormatPortInUseMessage(label, probeHost, port, ex.Message));
                    return false;
                }

                logError?.Invoke($"[unity-connector] {label} failed on {probeHost}:{port}: {ex.Message}");
                return false;
            }
        }

        public static string FormatPortInUseMessage(
            string label,
            string host,
            int port,
            string detail = null)
        {
            var endpoint = $"{host}:{port}";
            var hint = " Close the other process or set a different port (e.g. UNITY_CMD_PORT / UNITY_CMD_EDITOR_PLAY_PORT / UNITY_CMD_PLAYER_PORT).";
            if (string.IsNullOrEmpty(detail))
                return $"[unity-connector] {label} cannot start: {endpoint} is already in use.{hint}";

            return $"[unity-connector] {label} cannot start: {endpoint} is already in use ({detail}).{hint}";
        }

        private static string ResolveProbeHost(HostListenOptions listen)
        {
            if (listen == null || listen.BindMode is HostBindMode.Loopback or HostBindMode.Any)
                return "127.0.0.1";

            if (!string.IsNullOrWhiteSpace(listen.AdvertiseHost))
                return listen.AdvertiseHost;

            return "127.0.0.1";
        }

        public static void Stop(ref HttpListenerHost server)
        {
            server?.Dispose();
            server = null;
        }

        private static bool IsAddressInUseError(Exception ex)
        {
            if (ex == null)
                return false;

            var msg = ex.Message ?? "";
            if (msg.IndexOf("only one usage", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (msg.IndexOf("address already in use", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // Windows 中文版 SocketException 常见文案
            if (msg.IndexOf("只允许使用一次", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (msg.IndexOf("套接字", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return IsAddressInUseError(ex.InnerException);
        }
    }
}