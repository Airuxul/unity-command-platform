using System.Collections.Generic;

namespace Air.UcpAgent.Protocol
{
    public sealed class UcpCommand
    {
        public string id;
        public string type;
        public int timeout;
        public Dictionary<string, object> args = new Dictionary<string, object>();
    }

    public sealed class UcpResult
    {
        public string id;
        public bool success;
        public int duration;
        public string message;
        public Dictionary<string, object> data;
        public string error;
    }

    public sealed class UcpSession
    {
        public string id;
        public string name;
        public string path;
        public string type;
        public string status;
        public List<string> capabilities = new List<string>();
        public string updatedAt;
        public int runtimePort;
    }
}
