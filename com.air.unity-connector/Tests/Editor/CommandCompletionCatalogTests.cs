using NUnit.Framework;

namespace UnityCliConnector.Tests
{
    public class CommandCompletionCatalogTests
    {
        [Test]
        public void Compile_IsDeferred()
        {
            Assert.AreEqual(
                CommandCompletionCatalog.CompletionCompilation,
                CommandCompletionCatalog.GetCompletionKind(CommandNames.Compile));
        }

        [Test]
        public void Ping_IsNotDeferred()
        {
            Assert.IsNull(CommandCompletionCatalog.GetCompletionKind(CommandNames.Ping));
        }

        [Test]
        public void Console_IsNotDeferred()
        {
            Assert.IsNull(CommandCompletionCatalog.GetCompletionKind(CommandNames.Console));
        }
    }
}
