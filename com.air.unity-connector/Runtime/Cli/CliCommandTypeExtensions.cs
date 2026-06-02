using System;

namespace Air.UnityConnector.Cli
{
    static class CliCommandTypeExtensions
    {
        static readonly Type DescriptorProvider = typeof(ICliInvokeDescriptorProvider);

        public static bool IsCliCommand(this Type type) =>
            type != null
            && type.IsClass
            && !type.IsAbstract
            && !type.ContainsGenericParameters
            && typeof(CliCommand).IsAssignableFrom(type)
            && DescriptorProvider.IsAssignableFrom(type);
    }
}
