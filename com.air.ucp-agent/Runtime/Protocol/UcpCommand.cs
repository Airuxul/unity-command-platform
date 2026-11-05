using System.Collections.Generic;

namespace Air.UcpAgent.Protocol
{
    public sealed class UcpCommand
    {
        public string id;
        public string type;
        public int timeout;

        /// <summary>
        /// When ucp-host gives up, Unix ms; 0 from hosts before 0.5. A command past it is not
        /// started, and a result that comes in later is not written: nobody would read it.
        /// </summary>
        public long deadline;

        public Dictionary<string, object> args = new Dictionary<string, object>();

        public bool IsPastDeadline(long nowUtcMs) => deadline > 0 && nowUtcMs > deadline;
    }

    public sealed class UcpResult
    {
        public string id;
        public bool success;
        public int duration;
        public string message;
        public Dictionary<string, object> data;

        /// <summary>On failure, a code from <see cref="Cli.UcpErrorCodes"/>; the text is in <see cref="message"/>.</summary>
        public string error;

        /// <summary>On failure, what to try next.</summary>
        public string hint;
    }

    public sealed class UcpSession
    {
        public string id;
        public string name;
        public string path;
        public string type;
        public string status;
        public List<string> capabilities = new List<string>();
        public string updatedAt;
        public int runtimePort;

        /// <summary>Lets ucp-host tell a busy Editor (wait for it) from one that has exited (fail).</summary>
        public int pid;

        /// <summary>Unity ticks about 10 times a second in the background; ucp-host says so on a timeout.</summary>
        public bool focused;

        /// <summary>Commands that declare a default timeout, by name and alias; used when the caller sets none.</summary>
        public Dictionary<string, int> defaultTimeouts;

        /// <summary>
        /// Commands that change nothing; ucp-host does not hold them behind a command still running
        /// on this Editor (a test run, a build, Play Mode entry).
        /// </summary>
        public List<string> readOnlyCommands;
    }
}
