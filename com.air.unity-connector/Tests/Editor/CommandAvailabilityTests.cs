using NUnit.Framework;
using Air.UnityConnector.Host;
using Air.UnityConnector.Invoke;

namespace Air.UnityConnector.Tests
{
    public class InvokeAvailabilityTests
    {
        [Test]
        public void EditorHost_AllowsEditorScopeOnly()
        {
            Assert.IsFalse(
                InvokeAvailability.IsAvailableForHost(
                    CommandHostScope.Runtime,
                    HostKind.Editor));
            Assert.IsTrue(
                InvokeAvailability.IsAvailableForHost(
                    CommandHostScope.Editor,
                    HostKind.Editor));
            Assert.IsTrue(
                InvokeAvailability.IsAvailableForHost(
                    CommandHostScope.Any,
                    HostKind.Editor));
        }

        [Test]
        public void EditorPlayHost_AllowsRuntimeScopeOnly()
        {
            Assert.IsFalse(
                InvokeAvailability.IsAvailableForHost(
                    CommandHostScope.Editor,
                    HostKind.EditorPlay));
            Assert.IsTrue(
                InvokeAvailability.IsAvailableForHost(
                    CommandHostScope.Runtime,
                    HostKind.EditorPlay));
        }

        [Test]
        public void PlayerHost_AllowsRuntimeScopeOnly()
        {
            Assert.IsFalse(
                InvokeAvailability.IsAvailableForHost(
                    CommandHostScope.Editor,
                    HostKind.Player));
            Assert.IsTrue(
                InvokeAvailability.IsAvailableForHost(
                    CommandHostScope.Runtime,
                    HostKind.Player));
        }
    }
}
