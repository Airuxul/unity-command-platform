using System.Collections.Generic;

namespace UnityCliConnector
{
    public static class CommandJobCatalog
    {
        public const string CompletionCompilation = "compilation";
        public const string CompletionEnterPlay = "enter_play";
        public const string CompletionExitPlay = "exit_play";

        public static string GetCompletionKind(string command, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(command))
                return null;

            if (command == "refresh" && parameters != null &&
                parameters.TryGetValue("compile", out var compile) &&
                compile is bool b && b)
                return CompletionCompilation;

            var handler = CommandDiscovery.Find(command);
            if (handler != null && handler.IsJob && !string.IsNullOrEmpty(handler.Completion))
                return handler.Completion;

            return null;
        }

        public static bool IsJobCommand(string command, Dictionary<string, object> parameters) =>
            !string.IsNullOrEmpty(GetCompletionKind(command, parameters));
    }
}
