using System;
using System.Collections.Generic;

namespace Air.UnityConnector.Job
{
    [Serializable]
    public sealed class InvokeJobListWrapper
    {
        public List<InvokeJobRecord> Items = new();
    }
}
