namespace Air.UcpAgent.Job
{
    /// <summary>
    /// A deferred command's job, persisted in SessionState and ~/.ucp/jobs across domain reloads.
    /// </summary>
    /// <remarks>
    /// Serialised with JsonUtility, and the record written by the previous package version is
    /// read back after the reload that loads a new one. Keep field names, add new fields only at
    /// the end, and treat their zero value as "written by an older version".
    /// </remarks>
    [System.Serializable]
    public sealed class InvokeJobRecord
    {
        public string Id;
        public string Command;
        public string RequestId;
        public InvokeJobStatus Status = InvokeJobStatus.Pending;
        public string CompletionKind;
        public string ResultJson;
        public string Error;
        public long CreatedAtUtcMs;
        public long UpdatedAtUtcMs;
        public object Result;

        /// <summary>When the job gives up. 0 on records from versions before 0.5.</summary>
        public long DeadlineUtcMs;

        /// <summary>The failure's code from <see cref="Cli.UcpErrorCodes"/>. Empty before 0.5.</summary>
        public string ErrorCode;

        public string Hint;

        /// <summary>What the completion policy needs to finish this job, e.g. the condition ui_wait waits for. Empty before 0.5.</summary>
        public string CompletionArgs;
    }
}
