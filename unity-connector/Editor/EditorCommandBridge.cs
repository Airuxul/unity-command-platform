using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;
using UnityCliConnector.Http;

namespace UnityCliConnector
{
    /// <summary>Runs POST /command on the Editor main thread (HTTP workers never block).</summary>
    [InitializeOnLoad]
    internal sealed class EditorCommandBridge : ICommandScheduler
    {
        public static readonly EditorCommandBridge Instance = new();

        private sealed class Pending
        {
            public string Body;
            public Action<int, Dictionary<string, object>> WriteJson;
        }

        private readonly ConcurrentQueue<Pending> _queue = new();

        static EditorCommandBridge()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate() => Instance.Drain();

        public void Schedule(string body, Action<int, Dictionary<string, object>> writeJson) =>
            _queue.Enqueue(new Pending { Body = body, WriteJson = writeJson });

        private void Drain()
        {
            while (_queue.TryDequeue(out var pending))
            {
                try
                {
                    var request = CommandHttpHelper.ParseCommandRequest(
                        pending.Body,
                        EditorCommandHost.Instance.HostName);
                    var post = EditorCommandHost.Instance.HandleCommand(request);
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
    }
}
