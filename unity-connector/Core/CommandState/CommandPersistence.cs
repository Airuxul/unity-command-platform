using System;
using System.Collections.Generic;

namespace UnityCliConnector
{
    [Serializable]
    public sealed class CommandListWrapper
    {
        public List<CommandRecord> Items = new();
    }
}
