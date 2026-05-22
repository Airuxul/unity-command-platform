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

            if (command == "refresh" && GetBoolParam(parameters, "compile"))
                return CompletionCompilation;

            var handler = CommandDiscovery.Find(command);
            if (handler != null && handler.IsJob && !string.IsNullOrEmpty(handler.Completion))
                return handler.Completion;

            return null;
        }

        public static bool IsJobCommand(string command, Dictionary<string, object> parameters) =>
            !string.IsNullOrEmpty(GetCompletionKind(command, parameters));

        private static bool GetBoolParam(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var raw) || raw == null)
                return false;
            if (raw is bool b)
                return b;
            return bool.TryParse(raw.ToString(), out var parsed) && parsed;
        }
    }
}
