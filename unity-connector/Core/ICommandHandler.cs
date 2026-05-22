namespace UnityCliConnector
{
    public interface ICommandHandler
    {
        string Name { get; }
        CommandScope Scope { get; }
        string Description { get; }
        bool IsJob { get; }
        string Completion { get; }
        string[] Aliases { get; }
        int DefaultTimeoutMs { get; }
        bool AllowConnectionRetry { get; }
        CommandResult Execute(CliParams parameters);
    }
}
