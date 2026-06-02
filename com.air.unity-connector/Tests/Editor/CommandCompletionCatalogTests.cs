using Air.UnityConnector.Invoke;
using NUnit.Framework;
using Air.UnityConnector.Commands;

namespace Air.UnityConnector.Tests
{
    public class InvokeCompletionCatalogTests
    {
        [Test]
        public void Compile_IsDeferred()
        {
            Assert.AreEqual(
                InvokeCompletionCatalog.CompletionCompilation,
                InvokeCompletionCatalog.GetCompletionKind(CommandNames.Compile));
        }

        [Test]
        public void Ping_IsNotDeferred()
        {
            Assert.IsNull(InvokeCompletionCatalog.GetCompletionKind(CommandNames.Ping));
        }

        [Test]
        public void Console_IsNotDeferred()
        {
            Assert.IsNull(InvokeCompletionCatalog.GetCompletionKind(CommandNames.Console));
        }
    }
}
