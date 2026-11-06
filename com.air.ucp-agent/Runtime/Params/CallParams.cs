using System.Collections.Generic;

namespace Air.UcpAgent.Params
{
    public sealed class CallParams
    {
        [UcpParam(Description = "function name; omit to list the functions on this host")]
        public string Name { get; set; }

        [UcpParam(Description = "the function's parameters, by parameter name")]
        public Dictionary<string, object> Args { get; set; }
    }
}
