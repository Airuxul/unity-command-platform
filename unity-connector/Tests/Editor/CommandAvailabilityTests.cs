using NUnit.Framework;
using UnityCliConnector.Network;

namespace UnityCliConnector.Tests
{
    public class CommandAvailabilityTests
    {
        [Test]
        public void EditorHost_AllowsEditorScopeOnly()
        {
            Assert.IsFalse(
                CommandAvailability.IsAvailableForHost(
                    CommandScope.Runtime,
                    ConnectorHostKind.Editor));
            Assert.IsTrue(
                CommandAvailability.IsAvailableForHost(
                    CommandScope.Editor,
                    ConnectorHostKind.Editor));
            Assert.IsTrue(
                CommandAvailability.IsAvailableForHost(
                    CommandScope.Any,
                    ConnectorHostKind.Editor));
        }

        [Test]
        public void EditorPlayHost_AllowsRuntimeScopeOnly()
        {
            Assert.IsFalse(
                CommandAvailability.IsAvailableForHost(
                    CommandScope.Editor,
                    ConnectorHostKind.EditorPlay));
            Assert.IsTrue(
                CommandAvailability.IsAvailableForHost(
                    CommandScope.Runtime,
                    ConnectorHostKind.EditorPlay));
        }

        [Test]
        public void PlayerHost_AllowsRuntimeScopeOnly()
        {
            Assert.IsFalse(
                CommandAvailability.IsAvailableForHost(
                    CommandScope.Editor,
                    ConnectorHostKind.Player));
            Assert.IsTrue(
                CommandAvailability.IsAvailableForHost(
                    CommandScope.Runtime,
                    ConnectorHostKind.Player));
        }
    }
}
