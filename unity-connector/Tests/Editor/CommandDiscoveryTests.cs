using NUnit.Framework;

namespace UnityCliConnector.Tests
{
    public class CommandDiscoveryTests
    {
        [Test]
        public void DiscoversBuiltinPing()
        {
            CommandDiscovery.Invalidate();
            var handler = CommandDiscovery.Find(CommandNames.Ping);
            Assert.IsNotNull(handler);
        }

        [Test]
        public void DiscoversBuiltinConsole()
        {
            CommandDiscovery.Invalidate();
            var handler = CommandDiscovery.Find(CommandNames.Console);
            Assert.IsNotNull(handler);
            Assert.IsEmpty(handler.Completion);
        }

        [Test]
        public void DiscoversCompileAliases()
        {
            CommandDiscovery.Invalidate();
            var handler = CommandDiscovery.Find(CommandNames.Compile);
            CollectionAssert.Contains(handler.Aliases, "recompile");
        }

        [Test]
        public void DiscoversCompileAsDeferred()
        {
            CommandDiscovery.Invalidate();
            var handler = CommandDiscovery.Find(CommandNames.Compile);
            Assert.IsNotNull(handler);
            Assert.AreEqual(CommandCompletionCatalog.CompletionCompilation, handler.Completion);
            Assert.IsTrue(CommandCompletionCatalog.IsDeferredCommand(CommandNames.Compile));
        }

        [Test]
        public void ScreenshotScopeIsEditor()
        {
            CommandDiscovery.Invalidate();
            var handler = CommandDiscovery.Find(CommandNames.Screenshot);
            Assert.IsNotNull(handler);
            Assert.AreEqual(CommandScope.Editor, handler.Scope);
        }

        [Test]
        public void ConsoleCommandExposesParamDescriptions()
        {
            CommandDiscovery.Invalidate();
            var handler = CommandDiscovery.Find(CommandNames.Console);
            Assert.IsNotNull(handler);
            Assert.Greater(handler.ParamDescriptions.Length, 0);
            Assert.That(handler.ParamDescriptions[0], Does.StartWith("--"));
        }
    }
}
