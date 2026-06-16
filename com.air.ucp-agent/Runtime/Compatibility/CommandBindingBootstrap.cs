// @tag cli-param
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Air.UcpAgent
{
    static class CommandBindingBootstrap
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void RegisterEditor() => Register();
#endif

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterPlayer() => Register();

        static void Register()
        {
            UcpSerialization.EnsureRegistered();
            InvokeRegistry.Instance = DiscoveryInvokeRegistry.Instance;
            InvokeParameterBinding.Bind = CliParamBinder.Bind;
            InvokeParameterBinding.Describe = CliParamBinder.Describe;
            CliCommandDiscovery.LogWarning = UcpLog.LogWarning;
        }
    }
}
