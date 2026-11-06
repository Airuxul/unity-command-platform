using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;

namespace Air.UcpAgent.Editor.Dispatch
{
    public enum GateDecisionKind
    {
        Proceed,
        Fail,
    }

    public readonly struct GateDecision
    {
        public GateDecisionKind Kind { get; }
        public CommandFailure Failure { get; }

        GateDecision(GateDecisionKind kind, CommandFailure failure)
        {
            Kind = kind;
            Failure = failure;
        }

        public static GateDecision Proceed() => new(GateDecisionKind.Proceed, null);

        public static GateDecision Fail(CommandFailure failure) => new(GateDecisionKind.Fail, failure);
    }

    /// <summary>Whether a claimed command may run.</summary>
    public static class InboundPipeline
    {
        public static GateDecision Evaluate(CommandEntry entry, string commandType)
        {
            var name = entry?.Name ?? commandType;
            if (TestRunState.BlocksCommand(name))
            {
                return GateDecision.Fail(new CommandFailure(
                    UcpErrorCodes.Busy,
                    $"Tests are running; '{commandType}' waits until they finish.",
                    "Retry once state reports tests_running=false; ping, state, console, help and echo run meanwhile."));
            }

            return GateDecision.Proceed();
        }
    }
}
