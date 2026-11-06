namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// What a completion policy hands back. The job stores <see cref="Message"/> beside the payload,
    /// so the payload itself is only the command's data.
    /// </summary>
    public sealed class PolicyOutcome
    {
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
