using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityCliConnector.Http;
using UnityEngine;

namespace UnityCliConnector
{
    /// <summary>Runs POST /command on the play-mode main thread.</summary>
    public sealed class PlayModeCommandBridge : ICommandScheduler
    {
        private sealed class Pending
        {
            public string Body;
            public Action<int, Dictionary<string, object>> WriteJson;
        }

        private readonly ICommandHost _host;
        private readonly ConcurrentQueue<Pending> _queue = new();
        private GameObject _driver;

        public PlayModeCommandBridge(ICommandHost host) => _host = host;

        public void EnsureStarted()
        {
            if (_driver != null)
                return;

            CleanupStaleBridgeObjects();

            _driver = new GameObject($"UnityCliConnector.Bridge.{_host.HostName}")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            UnityEngine.Object.DontDestroyOnLoad(_driver);
            _driver.AddComponent<BridgeDriver>().Init(this);
        }

        public void Schedule(string body, Action<int, Dictionary<string, object>> writeJson)
        {
            EnsureStarted();
            _queue.Enqueue(new Pending { Body = body, WriteJson = writeJson });
        }

        private void Drain()
        {
            RuntimeCommandStateManager.Tick(_host.HostName);

            while (_queue.TryDequeue(out var pending))
            {
                try
                {
                    var request = CommandHttpHelper.ParseCommandRequest(pending.Body, _host.HostName);
                    var post = _host.HandleCommand(request);
                    pending.WriteJson(post.StatusCode, post.Body);
                }
                catch (Exception ex)
                {
                    pending.WriteJson(500, new Dictionary<string, object>
                    {
                        ["ok"] = false,
                        ["error"] = ex.Message,
                    });
                }
            }
        }

        private static void CleanupStaleBridgeObjects()
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (go == null || string.IsNullOrEmpty(go.name))
                    continue;
                if (!go.name.StartsWith("UnityCliConnector.Bridge.", StringComparison.Ordinal))
                    continue;

                // Missing script references appear as null component slots.
                var components = go.GetComponents<Component>();
                var hasMissingScript = false;
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        hasMissingScript = true;
                        break;
                    }
                }

                if (!hasMissingScript)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(go);
                else
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private sealed class BridgeDriver : MonoBehaviour
        {
            private PlayModeCommandBridge _bridge;

            public void Init(PlayModeCommandBridge bridge) => _bridge = bridge;

            private void Update() => _bridge?.Drain();
        }
    }
}
