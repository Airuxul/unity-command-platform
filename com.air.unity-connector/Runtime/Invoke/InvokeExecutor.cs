using System;
using System.Collections.Generic;
using Air.UnityConnector.Job;

namespace Air.UnityConnector.Invoke
{
    public static class InvokeExecutor
    {
        public static void Execute(InvokeContext context, Dictionary<string, object> parameters)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var handler = InvokeRegistry.Require().Find(context.Command, context.HostName);
            if (handler == null)
                throw new InvalidOperationException($"Unknown command: {context.Command}");

            handler.Invoke(new RemoteInvocationContext(context), parameters);
        }
    }
}
