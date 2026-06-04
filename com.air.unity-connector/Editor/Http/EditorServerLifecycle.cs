using System;
using System.IO;
using Air.UnityConnector.Cli;
using Air.UnityConnector.Host;
using Air.UnityConnector.Http;
using Air.UnityConnector.Invoke;
using UnityEditor;
using UnityEngine;

namespace Air.UnityConnector.Server
{
    /// <summary>Shared stop/start/catalog operations for <see cref="EditorServerSupervisor"/> (single write path).</summary>
    internal static class EditorServerLifecycle
    {
        internal const int HealthTimeoutMs = 2500;
        internal const int HealthProbeAttempts = 8;
        internal const double MaxBackoffSeconds = 60.0;
        internal const double ForeignPortBackoffSeconds = 60.0;

        internal enum StartAttemptResult
        {
            Running,
            CacheReconciled,
            ForeignPort,
            Failed,
        }

        internal static void PerformStop(string site)
        {
            var server = EditorConnectorServer.Instance;
            var wasRunning = server.IsListening;
            var port = server.Port;

            EditorHttpSession.MarkCatalogNotReady();
            server.Stop();
            server.ClearListenerId();

            EditorHttpLocalCache.MarkStopped(
                EditorHttpSession.SessionId,
                EditorHttpSession.Generation,
                port,
                EditorServerSupervisorPhase.Stopped);

            EditorHttpSession.SetListenerActive(false, site);
            EditorInstanceFile.PublishSnapshot();

            if (wasRunning)
                Debug.Log($"[unity-connector] Editor HTTP server stopped (port {port}).");
        }

        internal static EditorHttpLocalCache.StartupAction ApplyCacheOnDomainStart()
        {
            var port = HostNetwork.ResolveEditorPort();
            return EditorHttpLocalCache.ReconcileOnDomainStart(
                EditorHttpSession.SessionId,
                EditorHttpSession.Generation,
                port);
        }

        internal static StartAttemptResult TryStartListening()
        {
            var server = EditorConnectorServer.Instance;
            var port = HostNetwork.ResolveEditorPort();

            if (server.TryDescribeRunningCache(out var cacheReason))
            {
                if (!EditorHttpSession.IsCommandReady)
                    ReconcileRunningCache(cacheReason, "TryStartListening(cache_hit)");
                if (!EditorHttpSession.IsCommandReady)
                {
                    EditorConnectorStartupLog.Record(
                        "TryStartListening(cache_hit)",
                        "cache hit but commands_ready=false (catalog not warmed)");
                    return StartAttemptResult.Failed;
                }

                return StartAttemptResult.CacheReconciled;
            }

            var prepare = EditorHttpLocalCache.PrepareForStart(
                EditorHttpSession.SessionId,
                EditorHttpSession.Generation,
                port);

            if (prepare == EditorHttpLocalCache.PrepareResult.PortOwnedByOtherProcess)
            {
                var portMsg = ConnectorHttpLifecycle.FormatPortInUseMessage(
                    "Editor HTTP server",
                    "127.0.0.1",
                    port,
                    "another Unity instance or stale listener");
                if (EditorServerSupervisor.Instance.IsHttpTransitionUnstable())
                    EditorServerSupervisor.LogThrottled(portMsg);
                else
                    EditorServerSupervisor.LogConnectorError(portMsg);
                return StartAttemptResult.ForeignPort;
            }

            PerformStop("TryStartListening(prebind)");

            // Skip TCP pre-check after local Stop: TIME_WAIT can look "open" without a listener.
            if (!server.TryStart(requirePortFree: false))
            {
                EditorConnectorStartupLog.Record(
                    "TryStartListening",
                    "TryStart returned false (see prior LogError from HttpListener bind)");
                EditorHttpLocalCache.MarkStopped(
                    EditorHttpSession.SessionId,
                    EditorHttpSession.Generation,
                    port,
                    EditorServerSupervisorPhase.Stopped);
                return StartAttemptResult.Failed;
            }

            if (!server.TryProbeHealth(HealthTimeoutMs, HealthProbeAttempts, out var healthError))
            {
                var healthMsg =
                    "health probe failed after bind"
                    + (string.IsNullOrEmpty(healthError) ? "" : ": " + healthError);
                if (EditorServerSupervisor.Instance.IsHttpTransitionUnstable())
                    EditorServerSupervisor.LogThrottled("[unity-connector] " + healthMsg);
                else
                    EditorConnectorStartupLog.Record("TryStartListening(health)", healthMsg);
                PerformStop("TryStartListening(health_failed)");
                return StartAttemptResult.Failed;
            }

            server.SetListenerId(Guid.NewGuid().ToString("N"));
            EditorHttpSession.SetListenerActive(true, "TryStartListening");
            EditorHttpSession.SetDomainReloading(false, "TryStartListening");
            EditorHttpLocalCache.MarkRunning(
                EditorHttpSession.SessionId,
                EditorHttpSession.Generation,
                port,
                server.ListenerId,
                EditorServerSupervisorPhase.Running);

            Debug.Log(
                $"[unity-connector] Editor HTTP server started (port {port}, host editor, build {ConnectorBuild.Id}).");
            EditorConnectorStartupLog.Clear();

            EditorInstanceFile.NotifyHttpServing();
            WarmCatalogIfNeeded();
            return StartAttemptResult.Running;
        }

        internal static void ReconcileRunningCache(string cacheReason, string site)
        {
            EditorHttpSession.SetListenerActive(true, site);
            EditorHttpSession.SetDomainReloading(false, site);
            WarmCatalogIfNeeded();
            EditorInstanceFile.NotifyHttpServing();
        }

        internal static void WarmCatalogIfNeeded()
        {
            if (EditorHttpSession.CatalogReady)
                return;

            if (InvokeRegistry.Instance == null)
            {
                EditorApplication.delayCall += WarmCatalogIfNeeded;
                return;
            }

            try
            {
                InvokeCatalog.BuildResponse(EditorInvokeHost.Instance.HostName);
                EditorHttpSession.MarkCatalogReady();
                EditorHttpSession.SetDomainReloading(false, "WarmCatalogIfNeeded(success)");
                EditorInstanceFile.NotifyHttpServing();
            }
            catch (Exception ex)
            {
                EditorHttpSession.MarkCatalogNotReady();
                EditorServerSupervisor.LogThrottled($"[unity-connector] Catalog warmup failed: {ex.Message}");
                EditorApplication.delayCall += WarmCatalogIfNeeded;
            }
        }
    }

    /// <summary>
    /// Last Editor HTTP start failure — always Console Error + ~/.unity-cmd/last-editor-startup-failure.txt.
    /// </summary>
    internal static class EditorConnectorStartupLog
    {
        private const string Prefix = "[unity-connector][startup-failure]";
        private const double RepeatCooldownSeconds = 15.0;
        private static readonly object Gate = new();
        private static double _lastRecordUtc;
        private static string _lastRecordKey;

        public static string LastSite { get; private set; }
        public static string LastMessage { get; private set; }
        public static string LastUtc { get; private set; }

        public static void Record(string site, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var key = (site ?? "") + "|" + message;
            var now = EditorApplication.timeSinceStartup;
            lock (Gate)
            {
                if (key == _lastRecordKey && now - _lastRecordUtc < RepeatCooldownSeconds)
                    return;

                _lastRecordKey = key;
                _lastRecordUtc = now;
                LastSite = site ?? "";
                LastMessage = message;
                LastUtc = DateTime.UtcNow.ToString("o");
                Debug.LogError($"{Prefix} {LastSite}: {LastMessage}");
                WriteDiskUnsafe();
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                LastSite = "";
                LastMessage = "";
                LastUtc = "";
            }
        }

        private static void WriteDiskUnsafe()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-cmd");
                Directory.CreateDirectory(dir);
                File.WriteAllText(
                    Path.Combine(dir, "last-editor-startup-failure.txt"),
                    $"utc={LastUtc}\nsite={LastSite}\nmessage={LastMessage}\n");
            }
            catch
            {
                // ignored
            }
        }
    }
}
