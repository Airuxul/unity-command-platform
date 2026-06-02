using System.Collections.Generic;

namespace Air.UnityConnector.Invoke
{
    public interface IInvokeRegistry
    {
        IReadOnlyList<IInvokeHandler> Handlers { get; }
        IInvokeHandler Find(string command, string hostKind);
    }
}
