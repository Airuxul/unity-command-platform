#if UNITY_EDITOR
using UnityEditor;

namespace Air.UnityConnector
{
    [InitializeOnLoad]
    static class ConnectorEditorBootstrap
    {
        static ConnectorEditorBootstrap() => ConnectorSerialization.EnsureRegistered();
    }
}
#endif
