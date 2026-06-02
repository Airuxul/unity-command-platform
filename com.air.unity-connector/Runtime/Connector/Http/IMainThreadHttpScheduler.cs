using System;
using System.Collections.Generic;

namespace Air.UnityConnector.Http
{
    /// <summary>Routes HTTP work to the host main thread (Editor update / play-mode Update).</summary>
    public interface IMainThreadHttpScheduler : ICommandScheduler
    {
        void ScheduleCatalog(Action<int, Dictionary<string, object>> writeJson);

        void ScheduleInvokeJobStatus(string commandId, Action<int, Dictionary<string, object>> writeJson);
    }
}
