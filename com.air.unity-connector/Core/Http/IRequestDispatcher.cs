using System;
using System.Collections.Generic;

namespace UnityCliConnector.Http
{
    public interface IRequestDispatcher
    {
        bool TryDispatch(
            string method,
            string path,
            string body,
            Action<int, Dictionary<string, object>> writeJson,
            string authToken = null);
    }
}
