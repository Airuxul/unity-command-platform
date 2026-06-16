using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Air.UcpAgent.Invoke
{
    public static class DefaultInvokeParameterBinder
    {
        public static (object result, string error) Bind(Type paramType, Dictionary<string, object> values)
        {
            if (paramType == null)
                return (null, "Parameter type is required.");

            try
            {
                var instance = Activator.CreateInstance(paramType);
                if (values == null || values.Count == 0)
                    return (instance, null);

                foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite)
                        continue;

                    foreach (var pair in values)
                    {
                        if (!string.Equals(pair.Key, prop.Name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (TryConvert(pair.Value, prop.PropertyType, out var converted))
                            prop.SetValue(instance, converted);
                        break;
                    }
                }

                return (instance, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public static void EnsureRegistered()
        {
            InvokeParameterBinding.Bind ??= Bind;
            InvokeParameterBinding.Describe ??= static _ => Array.Empty<string>();
        }

        static bool TryConvert(object value, Type targetType, out object converted)
        {
            converted = null;
            if (value == null)
            {
                converted = null;
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
            try
            {
                if (nonNullable.IsInstanceOfType(value))
                {
                    converted = value;
                    return true;
                }

                if (nonNullable.IsEnum && value is string s)
                {
                    converted = Enum.Parse(nonNullable, s, ignoreCase: true);
                    return true;
                }

                converted = Convert.ChangeType(value, nonNullable, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
