using System;

namespace Air.UcpAgent.Invoke
{
    public static class InvokeRegistry
    {
        public static IInvokeRegistry Instance { get; set; }

        internal static IInvokeRegistry Require()
        {
            if (Instance == null)
            {
                throw new InvalidOperationException(
                    "InvokeRegistry.Instance is not set. Register from com.air.ucp-agent at startup.");
            }

            return Instance;
        }
    }
}
