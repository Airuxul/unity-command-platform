using System;
using UnityEditor;
using Air.UnityConnector.Invoke;

namespace Air.UnityConnector
{
    /// <summary>Per-domain Editor HTTP identity and readiness (survives Play, resets on script reload).</summary>
    internal static class EditorHttpSession
    {
        private const string SessionKey = "Air.UnityConnector.EditorHttp.SessionId";
        private const string GenerationKey = "Air.UnityConnector.EditorHttp.Generation";

        public static string SessionId { get; private set; }
        public static int Generation { get; private set; }
        public static bool CatalogReady { get; private set; }

        /// <summary>Safe to read from HTTP worker threads (health probe).</summary>
        public static volatile bool ListenerActive;

        /// <summary>True from afterAssemblyReload until HTTP listener + catalog warmup complete.</summary>
        public static volatile bool DomainReloading;

        public static void BeginDomain()
        {
            SessionId = Guid.NewGuid().ToString("N");
            Generation = SessionState.GetInt(GenerationKey, 0) + 1;
            SessionState.SetString(SessionKey, SessionId);
            SessionState.SetInt(GenerationKey, Generation);
            CatalogReady = false;
            ListenerActive = false;
            DomainReloading = true;
            InvokeCatalog.ClearCachedVersions();
            EditorReadinessLog.Transition(
                "EditorHttpSession.BeginDomain",
                $"new session generation={Generation} (DomainReloading=true until EnsureRunning+WarmCatalog)");
        }

        public static void MarkCatalogReady()
        {
            CatalogReady = true;
            EditorReadinessLog.Transition("EditorHttpSession.MarkCatalogReady", "catalog warmup complete");
        }

        public static void MarkCatalogNotReady()
        {
            CatalogReady = false;
            EditorReadinessLog.Transition("EditorHttpSession.MarkCatalogNotReady", "catalog invalidated");
        }

        public static void SetListenerActive(bool active, string site)
        {
            ListenerActive = active;
            EditorReadinessLog.Transition(site, $"ListenerActive={(active ? "true" : "false")}");
        }

        public static void SetDomainReloading(bool reloading, string site)
        {
            DomainReloading = reloading;
            EditorReadinessLog.Transition(site, $"DomainReloading={(reloading ? "true" : "false")}");
        }

        public static bool IsCommandReady =>
            ListenerActive && CatalogReady && !DomainReloading;
    }
}
