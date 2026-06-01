using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace UnityCliConnector.Network
{
    public enum ConnectorBindMode
    {
        Loopback,
        Lan,
        Any,
    }

    public sealed class ConnectorListenConfig
    {
        public int Port;
        public ConnectorBindMode BindMode;
        public string AdvertiseHost;
        public IReadOnlyList<string> BindPrefixes;
        public IReadOnlyList<Dictionary<string, object>> Endpoints;
    }

    /// <summary>Default HTTP ports (override per host via env vars).</summary>
    public static class ConnectorPorts
    {
        public const string EnvEditorPort = "UNITY_CMD_PORT";
        public const string EnvEditorPlayPort = "UNITY_CMD_EDITOR_PLAY_PORT";
        public const string EnvPlayerPort = "UNITY_CMD_PLAYER_PORT";

        public const int DefaultEditor = 6547;
        public const int DefaultEditorPlay = 6794;
        public const int DefaultPlayer = 6795;
    }

    public static class ConnectorNetwork
    {
        public const string EnvBind = "UNITY_CMD_BIND";
        public const string EnvLan = "UNITY_CMD_LAN";
        public const string EnvAdvertiseHost = "UNITY_CMD_ADVERTISE_HOST";
        public const string EnvAuthToken = "UNITY_CMD_TOKEN";

        public static ConnectorBindMode ResolveBindMode()
        {
            var bind = Environment.GetEnvironmentVariable(EnvBind)?.Trim().ToLowerInvariant();
            if (bind is "lan" or "any")
                return bind == "any" ? ConnectorBindMode.Any : ConnectorBindMode.Lan;
            if (bind == "loopback" || bind == "localhost" || bind == "127.0.0.1")
                return ConnectorBindMode.Loopback;
            if (Environment.GetEnvironmentVariable(EnvLan) == "1")
                return ConnectorBindMode.Lan;
            return ConnectorBindMode.Loopback;
        }

        public static int ResolveEditorPort() =>
            ResolvePortFromEnv(ConnectorPorts.EnvEditorPort, ConnectorPorts.DefaultEditor);

        public static int ResolveEditorPlayPort() =>
            ResolvePortFromEnv(ConnectorPorts.EnvEditorPlayPort, ConnectorPorts.DefaultEditorPlay);

        public static int ResolvePlayerPort() =>
            ResolvePortFromEnv(ConnectorPorts.EnvPlayerPort, ConnectorPorts.DefaultPlayer);

        private static int ResolvePortFromEnv(string envVarName, int defaultPort)
        {
            var env = Environment.GetEnvironmentVariable(envVarName);
            if (int.TryParse(env, out var p) && p > 0)
                return p;
            return defaultPort;
        }

        public static ConnectorListenConfig BuildListenConfig(int port, ConnectorBindMode? bindMode = null)
        {
            var mode = bindMode ?? ResolveBindMode();
            var prefixes = BuildBindPrefixes(port, mode);
            var advertise = ResolveAdvertiseHost(mode);
            var endpoints = BuildEndpoints(port, mode, advertise);

            return new ConnectorListenConfig
            {
                Port = port,
                BindMode = mode,
                AdvertiseHost = advertise,
                BindPrefixes = prefixes,
                Endpoints = endpoints,
            };
        }

        public static IReadOnlyList<string> BuildBindPrefixes(int port, ConnectorBindMode mode)
        {
            var list = new List<string>();
            switch (mode)
            {
                case ConnectorBindMode.Any:
                    list.Add($"http://+:{port}/");
                    break;
                case ConnectorBindMode.Lan:
                    list.Add($"http://127.0.0.1:{port}/");
                    foreach (var ip in GetPrivateIPv4Addresses())
                        list.Add($"http://{ip}:{port}/");
                    break;
                default:
                    list.Add($"http://127.0.0.1:{port}/");
                    break;
            }

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string ResolveAdvertiseHost(ConnectorBindMode mode)
        {
            var explicitHost = Environment.GetEnvironmentVariable(EnvAdvertiseHost)?.Trim();
            if (!string.IsNullOrEmpty(explicitHost))
                return explicitHost;

            if (mode == ConnectorBindMode.Lan)
            {
                var lan = GetPrivateIPv4Addresses().FirstOrDefault();
                if (!string.IsNullOrEmpty(lan))
                    return lan;
            }

            return "127.0.0.1";
        }

        public static IReadOnlyList<Dictionary<string, object>> BuildEndpoints(
            int port,
            ConnectorBindMode mode,
            string primaryAdvertiseHost)
        {
            var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryAdvertiseHost };
            if (mode == ConnectorBindMode.Lan)
            {
                hosts.Add("127.0.0.1");
                foreach (var ip in GetPrivateIPv4Addresses())
                    hosts.Add(ip);
            }

            return hosts.Select(h => new Dictionary<string, object>
            {
                ["host"] = h,
                ["port"] = port,
            }).ToList();
        }

        public static bool IsAuthRequired() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvAuthToken));

        public static bool ValidateAuthToken(string headerToken)
        {
            var expected = Environment.GetEnvironmentVariable(EnvAuthToken);
            if (string.IsNullOrEmpty(expected))
                return true;
            return string.Equals(expected, headerToken ?? "", StringComparison.Ordinal);
        }

        public static IReadOnlyList<string> GetPrivateIPv4Addresses()
        {
            var result = new List<string>();
            try
            {
                foreach (var addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (addr.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (IsPrivateIPv4(addr))
                        result.Add(addr.ToString());
                }
            }
            catch
            {
                // ignored
            }

            return result.Distinct().ToList();
        }

        private static bool IsPrivateIPv4(IPAddress addr)
        {
            var b = addr.GetAddressBytes();
            if (b[0] == 10) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            return false;
        }
    }
}
