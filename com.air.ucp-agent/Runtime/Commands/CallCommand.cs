using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Runtime.Commands
{
    [UcpCommand]
    public static class CallCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<CallParams>(
            CommandNames.Call,
            CommandHostScope.Any,
            "Run a [UcpFunction] static method by name, with its parameters from 'args'. Without 'name', list the functions on this host.");

        public static object Execute(CallParams p, CommandContext ctx)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                ctx.Message = "functions";
                var functions = FunctionRegistry.ForHost(ctx.Host).Select(f => new Dictionary<string, object>
                {
                    ["name"] = f.Name,
                    ["description"] = f.Description,
                    ["scope"] = f.Scope.ToString().ToLowerInvariant(),
                    ["params"] = f.ParamDescriptions,
                }).ToList();
                return new Dictionary<string, object>
                {
                    ["count"] = functions.Count,
                    ["functions"] = functions,
                };
            }

            var function = FunctionRegistry.Find(ctx.Host, p.Name)
                ?? throw new UcpException(
                    UcpErrorCodes.UnknownCommand,
                    $"No function '{p.Name.Trim()}' on this host.",
                    $"Run {CommandNames.Call} without 'name' to list them.");

            ctx.Message = $"called {function.Name}";
            var args = CommandExecutor.NormalizeArgs(p.Args);
            return function.Invoke(args, ctx);
        }
    }
}
