using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Params
{
    public sealed class GroupParams
    {
        public const string All = "all";
        public const string Failed = "failed";
        public const string Stop = "stop";
        public const string Continue = "continue";

        [UcpParam(Description = "steps run in order, each {\"command\":\"gameobject_create\",\"args\":{..}}; a step that "
            + "edits a scene or prefab may add \"expect\":{\"node\":..,\"exists\":true,\"components\":[..]}, checked just before it runs",
            Required = true)]
        public List<GroupStep> Steps { get; set; }

        [UcpParam(Description = "all: one failed step takes back every step; failed: only the failed step is taken back",
            AllowedValues = All + "|" + Failed)]
        public string Rollback { get; set; } = All;

        [UcpParam("on_error", Description = "with rollback=failed, whether the steps after a failed one still run",
            AllowedValues = Stop + "|" + Continue)]
        public string OnError { get; set; } = Stop;

        [UcpParam("dry_run", Description = "run every step and report the results, then take all of them back")]
        public bool DryRun { get; set; }
    }

    public sealed class GroupStep
    {
        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("args")]
        public JObject Args { get; set; }

        /// <summary>Optional <c>{node?, exists?, components?}</c>, checked once the step's target is open.</summary>
        [JsonProperty("expect")]
        public JObject Expect { get; set; }
    }
}
