using System;

namespace UnityCliConnector.Commands
{
    internal static class CommandTypeExtensions
    {
        private static readonly Type DescriptorProvider = typeof(ICommandDescriptorProvider);

        public static bool IsCommand(this Type type) => DescriptorProvider.IsAssignableFrom(type);
    }
}
