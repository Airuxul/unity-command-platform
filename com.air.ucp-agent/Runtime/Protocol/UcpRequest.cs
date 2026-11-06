using System.Collections.Generic;

namespace Air.UcpAgent.Protocol
{
    public sealed class UcpRequest
    {
        public string id;
        public string type;
        public int timeout;

        /// <summary>
        /// When ucp-host gives up, Unix ms. The editor agent rejects 0. A command past it is not
        /// started, and a result that comes in later is not written.
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
        /// Commands whose descriptor sets <c>RunsWhileBusy</c>. ucp-host does not hold them behind
        /// another command on this Editor. An empty list waits for everything.
        /// </summary>
        public List<string> runsWhileBusy;

        /// <summary>
        /// Commands that name other commands to run before or after themselves. Absent when
        /// none do. Keyed by command name and alias.
        /// </summary>
        public Dictionary<string, CommandChain> commandChains;
    }

    public sealed class CommandChain
    {
        public List<string> before = new List<string>();
        public List<string> after = new List<string>();
    }
}
