using System.Collections.Generic;

namespace Air.UnityConnector.Host
{
    public sealed class HostListenOptions
    {
        public int Port;
        public HostBindMode BindMode;
        public string AdvertiseHost;
        public IReadOnlyList<string> BindPrefixes;
        public IReadOnlyList<Dictionary<string, object>> Endpoints;
    }
}
