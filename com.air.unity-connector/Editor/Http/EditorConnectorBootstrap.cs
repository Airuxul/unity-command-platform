using System;
using UnityEditor;
using UnityEngine;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Host;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector
{
    /// <summary>
    /// Editor listener lifecycle: domain reload, watchdog, disk cache reconciliation.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorConnectorBootstrap
    {
        internal const int HealthTimeoutMs = 1500;
        internal const int HealthProbeAttempts = 5;
        internal const int HealthTimeoutStartupRetryMs = 2500;
        internal const int HealthProbeStartupRetryAttempts = 8;
        private const double WatchdogIntervalSeconds = 2.0;
        private const double WarningCooldownSeconds = 30.0;
        private const double MaxBackoffSeconds = 60.0;
        private const double ForeignPortBackoffSeconds = 60.0;
        private const int EnsureRetriesPerBurst = 3;

        private static bool _hooksInstalled;
        private static int _ensurePass;
        private static bool _ensurePending;
        private static int _failureBurst;
        private static double _nextWatchdogUtc;
        private static double _backoffUntilUtc;
        private static double _lastWarningUtc;
        private static bool _ensureUpdatePumpActive;
        private static bool _catalogWarmupPending;
        private static readonly object Gate = new();

        static EditorConnectorBootstrap()
        {
            ConnectorSerialization.EnsureRegistered();
            EditorHttpSession.BeginDomain();
            InstallHooks();
            ApplyCacheOnDomainStart();
            Stop();
            RequestEnsureRunning(0);
            EditorApplication.update += static () => EditorConnectorServer.Instance.Scheduler.Drain();
            EditorReadinessLog.Transition(
                "EditorConnectorBootstrap static ctor",
                "domain started — RequestEnsureRunning queued");
        }

        public static void RequestEnsureRunning(int delayFrames = 1)
        {
            if (IsInBackoff())
                return;

            if (delayFrames <= 0)
            {
                ScheduleHttpEnsureRunning(immediate: true);
                return;
            }

            void Chain()
            {
                if (delayFrames <= 1)
                {
                    ScheduleHttpEnsureRunning(immediate: true);
                    return;
                }

                delayFrames--;
                EditorApplication.delayCall += Chain;
            }

            EditorApplication.delayCall += Chain;
        }

        public static void Stop()
        {
            lock (Gate)
            {
                StopServerLocked();
            }
        }

        internal static void LogThrottled(string message)
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastWarningUtc < WarningCooldownSeconds)
                return;

            _lastWarningUtc = now;
            Debug.LogWarning(message);
        }

        private static void ScheduleHttpEnsureRunning(bool immediate)
        {
            if (IsInBackoff())
                return;

            _nextWatchdogUtc = 0;

            if (immediate)
            {
                _ensurePending = false;
                RunEnsurePass();
                if (EditorConnectorServer.Instance.TryDescribeRunningCache(out _))
                {
                    ReconcileRunningCacheIfNeeded("ScheduleHttpEnsureRunning");
                    return;
                }
            }

            if (!_ensurePending)
            {
                _ensurePending = true;
                if (!_ensureUpdatePumpActive)
                {
                    _ensureUpdatePumpActive = true;
                    EditorApplication.update += OnEnsureRunningUpdate;
                }
            }

            EditorApplication.QueuePlayerLoopUpdate();
            EditorApplication.delayCall += RunEnsurePass;
        }

        private static void OnEnsureRunningUpdate()
        {
            if (IsInBackoff())
                return;

            if (EditorConnectorServer.Instance.TryDescribeRunningCache(out _))
            {
                ReconcileRunningCacheIfNeeded("OnEnsureRunningUpdate");
                _ensurePending = false;
                if (_ensureUpdatePumpActive)
                {
                    _ensureUpdatePumpActive = false;
                    EditorApplication.update -= OnEnsureRunningUpdate;
                }

                return;
            }

            if (_ensurePending)
                RunEnsurePass();
        }

        private static void StopServerLocked()
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
                port);

            EditorHttpSession.SetListenerActive(false, "StopServerLocked");
            EditorInstanceFile.PublishSnapshot();

            if (wasRunning)
                Debug.Log($"[unity-connector] Editor HTTP server stopped (port {port}).");
        }

        private static void ApplyCacheOnDomainStart()
        {
            var port = HostNetwork.ResolveEditorPort();
            var action = EditorHttpLocalCache.ReconcileOnDomainStart(
                EditorHttpSession.SessionId,
                EditorHttpSession.Generation,
                port);

            switch (action)
            {
                case EditorHttpLocalCache.StartupAction.WaitForPortRelease:
                    EditorHttpLocalCache.PrepareForStart(
                        EditorHttpSession.SessionId,
                        EditorHttpSession.Generation,
                        port);
                    break;

                case EditorHttpLocalCache.StartupAction.ForeignProcessOwnsPort:
                    _backoffUntilUtc = EditorApplication.timeSinceStartup + ForeignPortBackoffSeconds;
                    LogThrottled(
                        $"[unity-connector] Port {port} is already served by another Unity process. " +
                        "Close the other Editor or change UNITY_CMD_PORT.");
                    break;
            }
        }

        private static void InstallHooks()
        {
            if (_hooksInstalled)
                return;
            _hooksInstalled = true;

            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnBeforeAssemblyReload()
        {
            EditorReadinessLog.Transition("OnBeforeAssemblyReload", "assembly reload starting — stopping HTTP");
            CliCommandDiscovery.Invalidate();
            InvokeCatalog.ClearCachedVersions();
            EditorJobStateManager.FlushToLedger();
            EditorInstanceFile.MarkReloading();
            Stop();
        }

        private static void OnAfterAssemblyReload()
        {
            EditorReadinessLog.Transition("OnAfterAssemblyReload", "assembly reload finished — scheduling HTTP ensure");
            _failureBurst = 0;
            _backoffUntilUtc = 0;
            _ensurePending = false;
            EditorHttpSession.SetDomainReloading(true, "OnAfterAssemblyReload");
            EditorInstanceFile.MarkReloading();
            EditorJobStateManager.Reload();
            ApplyCacheOnDomainStart();
            ScheduleHttpEnsureRunning(immediate: true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.EnteredEditMode)
            {
                _failureBurst = 0;
                _backoffUntilUtc = 0;
                RequestEnsureRunning(1);
            }
        }

        private static void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < _nextWatchdogUtc)
                return;

            _nextWatchdogUtc = now + WatchdogIntervalSeconds;

            if (IsInBackoff())
                return;

            if (EditorConnectorServer.Instance.TryDescribeRunningCache(out _))
            {
                ReconcileRunningCacheIfNeeded("OnEditorUpdate watchdog");
                _failureBurst = 0;
                return;
            }

            RequestEnsureRunning(0);
        }

        private static bool IsInBackoff() =>
            EditorApplication.timeSinceStartup < _backoffUntilUtc;

        private static void RunEnsurePass()
        {
            _ensurePending = false;
            if (IsInBackoff())
                return;

            _ensurePass++;
            lock (Gate)
            {
                EnsureRunningLocked();
            }

            if (EditorConnectorServer.Instance.TryDescribeRunningCache(out _))
            {
                _ensurePass = 0;
                _failureBurst = 0;
                _backoffUntilUtc = 0;
                return;
            }

            if (_ensurePass < EnsureRetriesPerBurst)
            {
                RequestEnsureRunning(2);
                return;
            }

            _ensurePass = 0;
            EditorReadinessLog.Transition("RunEnsurePass", "ensure burst exhausted — entering backoff");
            RegisterFailureBurst();
        }

        private static void RegisterFailureBurst()
        {
            _failureBurst++;
            var backoff = Math.Min(MaxBackoffSeconds, 5.0 * _failureBurst);
            _backoffUntilUtc = EditorApplication.timeSinceStartup + backoff;
            LogThrottled(
                $"[unity-connector] Editor HTTP unavailable; backing off {backoff:0}s " +
                $"(burst {_failureBurst}). See ~/.unity-cmd/editor-http.json or restart the Editor.");
        }

        private static void EnsureRunningLocked()
        {
            var server = EditorConnectorServer.Instance;
            var port = HostNetwork.ResolveEditorPort();

            if (EditorConnectorServer.Instance.TryDescribeRunningCache(out var cacheReason))
            {
                if (!EditorHttpSession.IsCommandReady)
                    ReconcileRunningCacheLocked(cacheReason, "EnsureRunningLocked(cache_hit)");
                return;
            }

            EditorReadinessLog.Transition("EnsureRunningLocked", "cache miss — starting HTTP listener");

            var prepare = EditorHttpLocalCache.PrepareForStart(
                EditorHttpSession.SessionId,
                EditorHttpSession.Generation,
                port);

            if (prepare == EditorHttpLocalCache.PrepareResult.PortOwnedByOtherProcess)
            {
                _backoffUntilUtc = EditorApplication.timeSinceStartup + MaxBackoffSeconds;
                LogThrottled(
                    $"[unity-connector] Port {port} is owned by another Unity instance (see ~/.unity-cmd/editor-http.json).");
                return;
            }

            if (server.IsListening)
                StopServerLocked();

            if (!server.TryStart())
            {
                EditorReadinessLog.Transition("EnsureRunningLocked FAILED", "TryStart failed");
                EditorHttpLocalCache.MarkStopped(
                    EditorHttpSession.SessionId,
                    EditorHttpSession.Generation,
                    port);
                return;
            }

            if (!server.TryProbeHealth(HealthTimeoutMs, HealthProbeAttempts, out var healthError))
            {
                // Startup race: listener thread may need a little extra time before first /health responds.
                if (IsTransientHealthProbeError(healthError)
                    && server.TryProbeHealth(
                        HealthTimeoutStartupRetryMs,
                        HealthProbeStartupRetryAttempts,
                        out healthError))
                {
                    EditorReadinessLog.Transition(
                        "EnsureRunningLocked",
                        "initial health probe recovered via startup retry");
                }
                else
                {
                EditorReadinessLog.Transition(
                    "EnsureRunningLocked FAILED",
                    "health probe failed after TryStart: " + (healthError ?? "unknown"));
                LogThrottled(
                    "[unity-connector] Editor HTTP started but health check failed; will retry."
                    + (string.IsNullOrEmpty(healthError) ? "" : $" ({healthError})"));
                StopServerLocked();
                return;
                }
            }

            server.SetListenerId(Guid.NewGuid().ToString("N"));
            EditorHttpSession.SetListenerActive(true, "EnsureRunningLocked(TryStart)");
            EditorHttpSession.SetDomainReloading(false, "EnsureRunningLocked(TryStart)");
            EditorHttpLocalCache.MarkRunning(
                EditorHttpSession.SessionId,
                EditorHttpSession.Generation,
                port,
                server.ListenerId);

            Debug.Log(
                $"[unity-connector] Editor HTTP server started (port {port}, host editor, build {ConnectorBuild.Id}).");
            EditorReadinessLog.Transition("EnsureRunningLocked", "TryStart + health probe succeeded");

            EditorInstanceFile.NotifyHttpServing();
            WarmCatalogIfNeeded();
        }

        static void ReconcileRunningCacheIfNeeded(string site)
        {
            if (!EditorConnectorServer.Instance.TryDescribeRunningCache(out var cacheReason))
                return;

            if (EditorHttpSession.IsCommandReady)
                return;

            lock (Gate)
            {
                ReconcileRunningCacheLocked(cacheReason, site);
            }
        }

        static void ReconcileRunningCacheLocked(string cacheReason, string site)
        {
            EditorReadinessLog.Transition(
                site,
                $"cache hit ({cacheReason}) — reconciling flags + catalog warmup");
            EditorHttpSession.SetListenerActive(true, site);
            EditorHttpSession.SetDomainReloading(false, site);
            WarmCatalogIfNeeded();
            EditorInstanceFile.NotifyHttpServing();
        }

        static void WarmCatalogIfNeeded()
        {
            if (EditorHttpSession.CatalogReady)
                return;

            if (InvokeRegistry.Instance == null)
            {
                EditorReadinessLog.Transition(
                    "WarmCatalogIfNeeded",
                    "InvokeRegistry not ready yet; postponing catalog warmup");
                ScheduleCatalogWarmupRetry();
                return;
            }

            try
            {
                EditorReadinessLog.Transition("WarmCatalogIfNeeded", "InvokeCatalog.BuildResponse starting");
                InvokeCatalog.BuildResponse(EditorInvokeHost.Instance.HostName);
                EditorHttpSession.MarkCatalogReady();
                EditorHttpSession.SetDomainReloading(false, "WarmCatalogIfNeeded(success)");
                EditorInstanceFile.NotifyHttpServing();
                _catalogWarmupPending = false;
            }
            catch (Exception ex)
            {
                EditorHttpSession.MarkCatalogNotReady();
                EditorReadinessLog.Transition(
                    "WarmCatalogIfNeeded FAILED",
                    ex.GetType().Name + ": " + ex.Message);
                LogThrottled($"[unity-connector] Catalog warmup failed: {ex.Message}");
                ScheduleCatalogWarmupRetry();
            }
        }

        static void ScheduleCatalogWarmupRetry()
        {
            if (EditorHttpSession.CatalogReady || _catalogWarmupPending)
                return;

            _catalogWarmupPending = true;
            EditorApplication.delayCall += static () =>
            {
                _catalogWarmupPending = false;
                WarmCatalogIfNeeded();
            };
        }

        static bool IsTransientHealthProbeError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;

            return error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("connection refused", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
