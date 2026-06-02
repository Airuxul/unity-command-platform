using Air.UnityConnector.Job;
using NUnit.Framework;
using Air.UnityConnector;
using Air.UnityConnector.Completion;
using Air.UnityConnector.Editor.Services;

namespace Air.UnityConnector.Tests.Editor
{
    /// <summary>Unit tests for post-reload compilation completion policy.</summary>
    public sealed class CompilationPolicyTests
    {
        private readonly CompilationPolicy _policy = new();

        [Test]
        public void PendingWhileNotCompiling_CompletesImmediately()
        {
            var command = new InvokeJobRecord { Status = InvokeJobStatus.Pending };
            var state = new EditorStateSnapshot { IsCompiling = false };

            Assert.IsTrue(_policy.TryComplete(command, state, out var result, out var error));
            Assert.IsNull(error);
            Assert.IsNotNull(result);
        }

        [Test]
        public void RunningWhileNotCompiling_CompletesWhenServiceDoesNotOwnCommand()
        {
            var command = new InvokeJobRecord { Id = "job-after-reload", Status = InvokeJobStatus.Running };
            var state = new EditorStateSnapshot { IsCompiling = false };

            Assert.IsTrue(_policy.TryComplete(command, state, out _, out _));
        }

        [Test]
        public void RunningWhileNotCompiling_WaitsWhenServiceOwnsCommand()
        {
            ScriptCompilationService.SetActiveCommandForTests("job-active");
            try
            {
                var command = new InvokeJobRecord { Id = "job-active", Status = InvokeJobStatus.Running };
                var state = new EditorStateSnapshot { IsCompiling = false };

                Assert.IsFalse(_policy.TryComplete(command, state, out _, out _));
            }
            finally
            {
                ScriptCompilationService.ClearActiveCommandForTests();
            }
        }

        [Test]
        public void PendingWhileCompiling_Waits()
        {
            var command = new InvokeJobRecord { Status = InvokeJobStatus.Pending };
            var state = new EditorStateSnapshot { IsCompiling = true };

            Assert.IsFalse(_policy.TryComplete(command, state, out _, out _));
            Assert.AreEqual(InvokeJobStatus.Running, command.Status);
        }
    }
}
