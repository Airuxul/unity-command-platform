using System.Collections.Generic;
using Air.UnityConnector.Job;
using NUnit.Framework;
using UnityEngine;

namespace Air.UnityConnector.Tests.Editor
{
    /// <summary>Verifies InvokeJobRecord survives JsonUtility roundtrip (domain-reload ledger).</summary>
    public sealed class EditorJobLedgerTests
    {
        [Test]
        public void InvokeJobRecord_JsonUtilityRoundtrip_PreservesIdentityAndStatus()
        {
            var source = new InvokeJobRecord
            {
                Id = "b0ac47191bf94e10b47894a0abb77710",
                Command = "compile",
                RequestId = "req-1",
                Status = InvokeJobStatus.Running,
                CompletionKind = "compilation",
                CreatedAtUtcMs = 1_780_000_000_000,
                UpdatedAtUtcMs = 1_780_000_000_100,
            };

            var json = JsonUtility.ToJson(new InvokeJobListWrapper
            {
                Items = new List<InvokeJobRecord> { source },
            });

            Assert.That(json, Does.Contain(source.Id));
            Assert.That(json, Does.Contain("compilation"));
            Assert.That(json, Does.Not.Equal("{}"));

            var wrapper = JsonUtility.FromJson<InvokeJobListWrapper>(json);
            Assert.IsNotNull(wrapper?.Items);
            Assert.AreEqual(1, wrapper.Items.Count);

            var restored = wrapper.Items[0];
            Assert.AreEqual(source.Id, restored.Id);
            Assert.AreEqual(source.Command, restored.Command);
            Assert.AreEqual(source.CompletionKind, restored.CompletionKind);
            Assert.AreEqual(InvokeJobStatus.Running, restored.Status);
        }
    }
}
