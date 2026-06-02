using System;
using System.Collections.Generic;
using System.Linq;

namespace Air.UnityConnector.Invoke
{
    public static class InvokeParameterBinding
    {
        public static Func<Type, Dictionary<string, object>, (object result, string error)> Bind { get; set; }

        public static Func<Type, string[]> Describe { get; set; }

        public static (object result, string error) BindRequired(Type paramType, Dictionary<string, object> values)
        {
            DefaultInvokeParameterBinder.EnsureRegistered();
            var bind = Bind ?? DefaultInvokeParameterBinder.Bind;
            return bind(paramType, values ?? new Dictionary<string, object>());
        }

        public static (object result, string error) BindRequired(
            Type paramType,
            IReadOnlyDictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
                return BindRequired(paramType, new Dictionary<string, object>());

            if (values is Dictionary<string, object> dict)
                return BindRequired(paramType, dict);

            return BindRequired(paramType, values.ToDictionary(p => p.Key, p => p.Value));
        }

        public static string[] DescribeOrEmpty(Type paramType)
        {
            if (paramType == null)
                return Array.Empty<string>();
            DefaultInvokeParameterBinder.EnsureRegistered();
            return Describe?.Invoke(paramType) ?? Array.Empty<string>();
        }
    }
}
