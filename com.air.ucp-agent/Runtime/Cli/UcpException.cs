using System;

namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// The machine-readable reasons a command fails, sent as <c>error</c> in the result. A caller
    /// branches on these; the text in <c>message</c> is for whoever reads it.
    /// </summary>
    public static class UcpErrorCodes
    {
        /// <summary>Bad or missing parameter; also the code of any <see cref="ArgumentException"/>.</summary>
        public const string InvalidArgument = "invalid_argument";

        /// <summary>Anything else a command threw.</summary>
        public const string CommandFailed = "command_failed";

        public const string UnknownCommand = "unknown_command";
        public const string NodeNotFound = "node_not_found";
        public const string TypeNotFound = "type_not_found";
        public const string ComponentNotFound = "component_not_found";
        public const string PlayModeRequired = "play_mode_required";
        public const string EditModeRequired = "edit_mode_required";

        /// <summary>The node is inactive, or its Selectable or a CanvasGroup above it blocks input.</summary>
        public const string NotInteractable = "not_interactable";

        public const string CompileError = "compile_error";

        /// <summary>BuildPipeline reported a failed or cancelled build; data carries the report summary.</summary>
        public const string BuildFailed = "build_failed";

        /// <summary>test_run ran, and at least one test failed; data carries the summary.</summary>
        public const string TestsFailed = "tests_failed";

        /// <summary>Tests are running, and the command could disturb them.</summary>
        public const string Busy = "busy";

        /// <summary>A batch op's <c>expect</c> did not hold, so the batch was rolled back.</summary>
        public const string ExpectationFailed = "expectation_failed";

        /// <summary>A deferred command passed its deadline.</summary>
        public const string Timeout = "timeout";

        /// <summary>A deferred command whose completion was lost in a domain reload.</summary>
        public const string Orphaned = "orphaned";

        /// <summary>Every code with what it means, for the <c>help</c> card.</summary>
        public static readonly (string code, string meaning)[] All =
        {
            (InvalidArgument, "a parameter is missing, has the wrong type, or is not one of the allowed values"),
            (UnknownCommand, "no command by that name or alias on this host"),
            (NodeNotFound, "no node at that path or instance id; hint lists the closest names"),
            (TypeNotFound, "no component or object type by that name; hint lists similar names"),
            (ComponentNotFound, "the node exists but has no component of that type"),
            (PlayModeRequired, "the command only runs in Play Mode; run play first"),
            (EditModeRequired, "the command cannot run in Play Mode; run stop first"),
            (NotInteractable, "the node is inactive or does not take input right now; ui_wait for it, then retry"),
            (CompileError, "scripts do not compile; data.errors lists the compiler errors"),
            (BuildFailed, "the player build failed or was cancelled; data.messages has its errors"),
            (TestsFailed, "tests ran and some failed; data.failures lists them"),
            (Busy, "tests are running; only ping, state, console, help and echo run until they finish"),
            (ExpectationFailed, "a batch op's expect did not hold; nothing in the batch was kept"),
            (Timeout, "the command passed its deadline before it finished"),
            (Orphaned, "a domain reload lost the command before it finished"),
            (CommandFailed, "any other failure; message says why"),
        };

        /// <summary>The code for <paramref name="ex"/>: its own if it carries one, else by exception type.</summary>
        public static string Of(Exception ex) =>
            ex switch
            {
                UcpException ucp => ucp.Code,
                ArgumentException => InvalidArgument,
                _ => CommandFailed,
            };
    }

    /// <summary>An exception that says which <see cref="UcpErrorCodes"/> it is and how to fix it.</summary>
    public sealed class UcpException : Exception
    {
        public UcpException(string code, string message, string hint = null, Exception inner = null)
            : base(message, inner)
        {
            Code = string.IsNullOrEmpty(code) ? UcpErrorCodes.CommandFailed : code;
            Hint = hint;
        }

        public string Code { get; }

        /// <summary>What to try next, kept out of the message so a caller can show it apart.</summary>
        public string Hint { get; }

        /// <summary>
        /// Rewords <paramref name="inner"/> while keeping its code and hint, for a wrapper that adds
        /// context (which batch op failed) without hiding why.
        /// </summary>
        public static UcpException Wrap(Exception inner, string message) =>
            new(UcpErrorCodes.Of(inner), message, (inner as UcpException)?.Hint, inner);
    }

    /// <summary>How a command failed: what the caller gets as error, message, hint and data.</summary>
    public sealed class CommandFailure
    {
        public CommandFailure(string code, string message, string hint = null, object data = null)
        {
            Code = string.IsNullOrEmpty(code) ? UcpErrorCodes.CommandFailed : code;
            Message = string.IsNullOrEmpty(message) ? Code : message;
            Hint = hint;
            Data = data;
        }

        public string Code { get; }
        public string Message { get; }
        public string Hint { get; }

        /// <summary>Detail a caller may act on, e.g. the compiler errors of a failed compile.</summary>
        public object Data { get; }

        public static CommandFailure From(Exception ex) =>
            new(UcpErrorCodes.Of(ex), ex.Message, (ex as UcpException)?.Hint);
    }
}
