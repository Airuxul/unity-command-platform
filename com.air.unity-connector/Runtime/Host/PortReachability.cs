using System;
using System.Net.Sockets;
using System.Threading;

namespace Air.UnityConnector.Host
{
    /// <summary>
    /// TCP probe helpers for loopback connector ports (domain reload / listener restart).
    /// </summary>
    public static class PortReachability
    {
        public static bool IsPortOpen(string host, int port, int connectTimeoutMs = 120)
        {
            if (port <= 0)
                return false;

            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                if (!task.Wait(connectTimeoutMs))
                    return false;
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Block until nothing accepts TCP on the port, or timeout elapses.
        /// </summary>
        public static void WaitUntilFree(string host, int port, int timeoutMs)
        {
            if (port <= 0 || timeoutMs <= 0)
                return;

            var deadline = Environment.TickCount + timeoutMs;
            while (Environment.TickCount < deadline)
            {
                if (!IsPortOpen(host, port))
                    return;
                Thread.Sleep(50);
            }
        }
    }
}
