namespace UnityCliConnector.Builtin
{
    [CliCommand(
        "editor.play",
        Scope = CommandScope.Editor,
        Description = "Enter Play Mode (async job)",
        IsJob = true,
        Completion = CommandJobCatalog.CompletionEnterPlay,
        DefaultTimeoutMs = 60000)]
    public static class EditorPlayCommand
    {
        public static CommandResult Run(CliParams p) =>
            CommandResult.Success(new { started = true });
    }

    [CliCommand(
        "editor.stop",
        Scope = CommandScope.Editor,
        Description = "Exit Play Mode (async job)",
        IsJob = true,
        Completion = CommandJobCatalog.CompletionExitPlay,
        DefaultTimeoutMs = 60000)]
    public static class EditorStopCommand
    {
        public static CommandResult Run(CliParams p) =>
            CommandResult.Success(new { started = true });
    }
}
