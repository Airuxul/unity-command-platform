namespace Air.UnityConnector.Invoke
{
    public static class InvokeCompletionCatalog
    {
        public const string CompletionDeferred = "started";
        public const string CompletionCompilation = "compilation";
        public const string CompletionEnterPlay = "enter_play";
        public const string CompletionExitPlay = "exit_play";

        public static string GetCompletionKind(string command)
        {
            if (string.IsNullOrEmpty(command))
                return null;

            var handler = InvokeRegistry.Require().Find(command, hostKind: null);
            if (handler != null && !string.IsNullOrEmpty(handler.Completion))
                return handler.Completion;

            return null;
        }

        public static bool IsDeferredCommand(string command) =>
            !string.IsNullOrEmpty(GetCompletionKind(command));
    }
}
