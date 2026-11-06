using System;

namespace Air.UcpAgent.Invoke
{
    public static class PolicyNames
    {
        public static string Of(Type policy)
        {
            if (policy == null)
                throw new ArgumentException("A completion policy class is required.", nameof(policy));
            if (!policy.IsClass || policy.IsAbstract || policy.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    $"'{policy.FullName}' is not a concrete completion policy.", nameof(policy));
            }

            return policy.FullName;
        }
    }
}
