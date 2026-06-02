using System;
using System.Collections.Generic;

namespace Air.UnityConnector.Http
{
    /// <summary>Runs POST /command handling (Editor: main thread queue; Runtime: player main thread).</summary>
    public interface ICommandScheduler
    {
        void Schedule(string body, Action<int, Dictionary<string, object>> writeJson);
    }
}
