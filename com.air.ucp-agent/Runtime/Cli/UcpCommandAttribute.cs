using System;

namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// Marks a static class as a command. <see cref="CommandRegistry"/> finds it by reflection and
    /// reads three members by name:
    /// <list type="bullet">
    /// <item><c>public static readonly InvokeDescriptor Descriptor</c></item>
    /// <item><c>public static object Execute(TParams p, TContext ctx)</c>, or <c>Execute(TContext ctx)</c> without parameters.
    /// <c>TContext</c> is <see cref="CommandContext"/> or a subclass that records what <c>Undo</c> needs.</item>
    /// <item>Optional <c>public static void Undo(TParams p, TContext ctx)</c>, same parameters as <c>Execute</c>.
    /// A command without one has nothing to take back when a group rolls back.</item>
    /// </list>
    /// Returning ends a synchronous command as success. <see cref="CommandContext.Fail(CommandFailure)"/> and
    /// <see cref="CommandContext.Defer(System.Type,string)"/> are the other two endings. The class keeps no
    /// state: a static field that is not readonly is a load error.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UcpCommandAttribute : Attribute
    {
    }
}
