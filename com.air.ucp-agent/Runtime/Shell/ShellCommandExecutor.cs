using System;
using System.Text;
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
                return (null, MapParseError(parse.Error, line));
            }

            var validationError = ShellValidationService.ValidateBeforeExecute(parse);
            if (!string.IsNullOrEmpty(validationError))
                return (null, validationError);

            var command = new UcpCommand
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

        static string MapParseError(string error, string line)
        {
            return error switch
            {
                "empty_input" => "Enter a command.",
                "unknown_command" => "Unknown runtime command: " + ExtractFirstToken(line),
                _ => error ?? "Invalid input",
            };
        }

        static string ExtractFirstToken(string line)
        {
            var tokens = ShellTokenizer.Tokenize(line ?? "");
            return tokens.Count > 0 ? tokens[0].Value : "";
        }
    }
}
