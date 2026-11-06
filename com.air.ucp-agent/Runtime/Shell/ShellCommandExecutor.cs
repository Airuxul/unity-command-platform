using System;
using System.Text;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Protocol;
using Air.UcpAgent.Runtime;
using Newtonsoft.Json;

namespace Air.UcpAgent.Runtime.Shell
{
    public static class ShellCommandExecutor
    {
        public static (UcpResult result, string error) ExecuteLine(string line)
        {
            var parse = ShellLineParser.Parse(line);
            if (!parse.IsValid)
            {
                return (UcpResults.Failure(null, new CommandFailure(
                    UcpErrorCodes.InvalidArgument,
                    parse.Error == "empty_input" ? "Enter a command." : parse.Error ?? "Invalid input")), null);
            }

            var command = new UcpRequest
            {
                id = Guid.NewGuid().ToString("N"),
                type = parse.CommandName,
                args = parse.Args,
            };

            try
            {
                return (RuntimeCommandService.Execute(command), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public static string FormatResult(UcpResult result, string executionError = null)
        {
            if (!string.IsNullOrEmpty(executionError))
                return "[error] " + executionError;

            if (result == null)
                return "(no result)";

            var sb = new StringBuilder();
            if (!result.success)
            {
                sb.Append("[error] ");
                if (!string.IsNullOrEmpty(result.error))
                    sb.Append(result.error);
                if (!string.IsNullOrEmpty(result.message))
                {
                    if (sb.Length > 8)
                        sb.Append(": ");
                    sb.Append(result.message);
                }

                if (!string.IsNullOrEmpty(result.hint))
                {
                    sb.AppendLine();
                    sb.Append(result.hint);
                }

                return sb.Length > 0 ? sb.ToString() : "[error] command failed";
            }

            if (!string.IsNullOrEmpty(result.message))
                sb.Append(result.message);

            if (result.data != null && result.data.Count > 0)
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(JsonConvert.SerializeObject(result.data));
            }

            if (sb.Length == 0)
                sb.Append("ok");

            if (result.duration > 0)
                sb.Append($" ({result.duration}ms)");

            return sb.ToString();
        }
    }
}
