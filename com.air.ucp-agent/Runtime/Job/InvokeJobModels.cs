namespace Air.UcpAgent.Job
{
    /// <summary>
    /// A deferred command's job, persisted with <see cref="UcpJson"/> in SessionState and ~/.ucp/jobs.
    /// <see cref="Result"/> is live only. What survives a domain reload is <see cref="ResultJson"/>.
    /// </summary>
    public sealed class InvokeJobRecord
    {
        public string Id;
        public string Command;
        public InvokeJobStatus Status = InvokeJobStatus.Pending;
        /// <summary>Full name of the policy class that resumes this job after a domain reload.</summary>
        public string CompletionKind;
        public string ResultJson;
        public string Error;
        public long CreatedAtUtcMs;
        public long UpdatedAtUtcMs;

        /// <summary>Not written to disk. Restored from <see cref="ResultJson"/> after a reload.</summary>
        public object Result;

        public long DeadlineUtcMs;
        public string ErrorCode;
        public string Hint;
        public string CompletionArgs;
        public string Message;

        /// <summary>
        /// The group this job is a step of. Its result goes to the group, not to the outbox; the
        /// group removes the job once it has read it.
        /// </summary>
        public string ParentId;
    }
}
