using System;
using UnityEditor;
using UnityEngine;
using UnityCliConnector.Http;
using UnityCliConnector.Network;

namespace UnityCliConnector
{
    [InitializeOnLoad]
    public static class EditorHttpHost
    {
        private static HttpServer _server;
        private static ConnectorListenConfig _listen;
        private static readonly ConnectorRequestDispatcher Dispatcher = new(
            EditorCommandHost.Instance,
            EditorCommandBridge.Instance,
            EditorCommandStore.Instance);

        static EditorHttpHost()
        {
            EditorApplication.quitting += Stop;
            Stop();
            Start();
        }

        public static ConnectorListenConfig ListenConfig => _listen;

        public static void Start() =>
            ConnectorHttpLifecycle.TryStart(
                ref _server,
                ref _listen,
                Dispatcher,
                ConnectorNetwork.ResolveEditorPort(),
                "Editor HTTP",
                Debug.Log,
                Debug.LogError);

        public static void Stop() => ConnectorHttpLifecycle.Stop(ref _server);
    }
}
