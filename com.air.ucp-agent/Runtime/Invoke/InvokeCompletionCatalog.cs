namespace Air.UcpAgent.Invoke
{
    /// <summary>
    /// Completion kinds a <see cref="DeferredInvokeDescriptor"/> can name. Persisted in job
    /// records, so the strings must not change.
    /// </summary>
    public static class InvokeCompletionCatalog
    {
        /// <summary>Completed by the command itself while the domain lives; orphaned by a reload.</summary>
        public const string CompletionDeferred = "started";
        public const string CompletionCompilation = "compilation";
        public const string CompletionEnterPlay = "enter_play";
        public const string CompletionExitPlay = "exit_play";
        public const string CompletionUiWait = "ui_wait";
        public const string CompletionTests = "tests";
        public const string CompletionPackages = "packages";
    }
}
