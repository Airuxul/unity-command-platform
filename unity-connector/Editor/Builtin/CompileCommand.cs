namespace UnityCliConnector.Builtin
{
    [CliCommand(
        "compile",
        Scope = CommandScope.Editor,
        Description = "Request script compilation (async job)",
        IsJob = true,
        Completion = CommandJobCatalog.CompletionCompilation,
        Aliases = "recompile,reload,reload-scripts,editor.recompile",
        DefaultTimeoutMs = 120000)]
    public static class CompileCommand
    {
        public static CommandResult Run(CliParams p) =>
            CommandResult.Success(new { started = true });
    }
}
