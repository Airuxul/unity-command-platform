using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Group;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class GroupCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GroupParams>(
            CommandNames.Group,
            CommandHostScope.Editor,
            "Run several commands in order as one. rollback=all takes every step back when one fails; " +
            "rollback=failed takes back only the failed step, and on_error says whether the rest still run. " +
            "Steps may include compile or play: the group carries on after the domain reload.",
            defaultTimeoutMs: 10 * 60_000);

        public static object Execute(GroupParams p, CommandContext ctx)
        {
            var outcome = GroupRun.Start(p, ctx);
            if (outcome.Waiting)
            {
                ctx.Defer(typeof(GroupPolicy), ctx.CommandId);
                return null;
            }

            if (outcome.Failure != null)
            {
                ctx.Fail(outcome.Failure);
                return null;
            }

            ctx.Message = outcome.Message;
            return outcome.Data;
        }
    }
}
