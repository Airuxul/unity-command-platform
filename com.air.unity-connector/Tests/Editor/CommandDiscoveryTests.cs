using Air.UnityConnector.Invoke;
using NUnit.Framework;
using Air.UnityConnector.Commands;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Tests
{
    public class CommandDiscoveryTests
    {
        [Test]
        public void DiscoversBuiltinPing()
        {
            CliCommandDiscovery.Invalidate();
            var handler = CliCommandDiscovery.Find(CommandNames.Ping);
            Assert.IsNotNull(handler);
        }

        [Test]
        public void DiscoversBuiltinConsole()
        {
            CliCommandDiscovery.Invalidate();
            var handler = CliCommandDiscovery.Find(CommandNames.Console);
            Assert.IsNotNull(handler);
            Assert.IsEmpty(handler.Completion);
        }

        [Test]
        public void DiscoversCompileAliases()
        {
            CliCommandDiscovery.Invalidate();
            var handler = CliCommandDiscovery.Find(CommandNames.Compile);
            CollectionAssert.Contains(handler.Aliases, "recompile");
        }

        [Test]
        public void DiscoversCompileAsDeferred()
        {
            CliCommandDiscovery.Invalidate();
            var handler = CliCommandDiscovery.Find(CommandNames.Compile);
            Assert.IsNotNull(handler);
            Assert.AreEqual(InvokeCompletionCatalog.CompletionCompilation, handler.Completion);
            Assert.IsTrue(InvokeCompletionCatalog.IsDeferredCommand(CommandNames.Compile));
        }

        [Test]
        public void ScreenshotScopeIsEditor()
        {
            CliCommandDiscovery.Invalidate();
            var handler = CliCommandDiscovery.Find(CommandNames.Screenshot);
            Assert.IsNotNull(handler);
            Assert.AreEqual(CommandHostScope.Editor, handler.Scope);
        }

        [Test]
        public void ConsoleCommandExposesParamDescriptions()
        {
            CliCommandDiscovery.Invalidate();
            var handler = CliCommandDiscovery.Find(CommandNames.Console);
            Assert.IsNotNull(handler);
            Assert.Greater(handler.ParamDescriptions.Length, 0);
            Assert.That(handler.ParamDescriptions[0], Does.StartWith("--"));
        }
    }
}
