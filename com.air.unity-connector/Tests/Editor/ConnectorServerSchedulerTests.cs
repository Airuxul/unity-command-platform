using System.Collections.Generic;
using Air.UnityConnector.Job;
using NUnit.Framework;
using Air.UnityConnector.Host;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Commands;
using Air.UnityConnector.Http;

namespace Air.UnityConnector.Tests
{
    public class ConnectorServerSchedulerTests
    {
        [Test]
        public void Schedule_SecondCommand_ReturnsBusyBeforeDrain()
        {
            var host = new PlayModeInvokeHost(HostKind.EditorPlay);
            var responses = new List<(int status, Dictionary<string, object> body)>();
            void Capture(int status, Dictionary<string, object> body) =>
                responses.Add((status, new Dictionary<string, object>(body)));

            var scheduler = new ConnectorMainThreadScheduler(host, _ => null);
            scheduler.Schedule("{}", Capture);
            scheduler.Schedule("{}", Capture);

            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual(503, responses[0].status);
            Assert.AreEqual("busy", responses[0].body["error"]);
            Assert.AreEqual("SERVER_BUSY", responses[0].body["error_code"]);
        }

        [Test]
        public void Schedule_WhenReloading_ReturnsDomainReloading()
        {
            var host = new PlayModeInvokeHost(HostKind.EditorPlay);
            var responses = new List<(int status, Dictionary<string, object> body)>();
            void Capture(int status, Dictionary<string, object> body) =>
                responses.Add((status, new Dictionary<string, object>(body)));

            var scheduler = new ConnectorMainThreadScheduler(
                host,
                _ => null,
                canAcceptCommand: static () => false);

            scheduler.Schedule("{}", Capture);

            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual(503, responses[0].status);
            Assert.AreEqual("reloading", responses[0].body["error"]);
            Assert.AreEqual("DOMAIN_RELOADING", responses[0].body["error_code"]);
        }

        [Test]
        public void EditorJobStateManager_Reload_MergesPendingJobFromLedger()
        {
            var entry = EditorJobStateManager.Create(
                CommandNames.Compile,
                InvokeCompletionCatalog.CompletionCompilation,
                "req-ledger-reload-test");

            EditorJobStateManager.MarkRunning(entry.Id);
            EditorJobStateManager.FlushToLedger();
            EditorJobStateManager.Reload();

            var restored = EditorJobStateManager.Get(entry.Id);
            Assert.IsNotNull(restored);
            Assert.AreEqual(entry.Id, restored.Id);
            Assert.AreEqual(InvokeJobStatus.Running, restored.Status);
        }
    }
}
