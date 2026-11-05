using System;
using Air.UcpAgent.Commands;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>
    /// Whether tests are running, and the result of the run test_run started. The test framework's
    /// callbacks write it (com.air.UcpAgent.Editor.TestRunner); it lives in this assembly, which
    /// cannot reference the framework, so the agent can refuse commands during any run. SessionState
    /// carries it across the domain reloads of a PlayMode run.
    /// </summary>
    public static class TestRunState
    {
        const string SessionKey = "Air.UcpAgent.TestRun";

        /// <summary>What may run while tests do: none of these change the project or the play state.</summary>
        static readonly string[] AllowedDuringRun =
        {
            CommandNames.Ping, CommandNames.State, CommandNames.Console, CommandNames.Help, CommandNames.Echo,
        };

        [Serializable]
        sealed class Record
        {
            /// <summary>Empty for a run started from the Test Runner window.</summary>
            public string JobId;

            /// <summary>test_run's mode parameter; the result tree's root does not carry it.</summary>
            public string Mode;

            public bool Running;
            public string SummaryJson;
            public string Error;
        }

        /// <summary>Any test run, started by test_run or from the Test Runner window.</summary>
        public static bool IsRunActive() => Load()?.Running == true;

        public static bool BlocksCommand(string command) =>
            Array.IndexOf(AllowedDuringRun, command) < 0 && IsRunActive();

        /// <summary>Called by test_run before it starts the run, so the result finds its job.</summary>
        public static void Begin(string jobId, string mode) =>
            Save(new Record { JobId = jobId, Mode = mode, Running = true });

        /// <summary>The mode test_run started the current run in; empty for a run started elsewhere.</summary>
        public static string Mode => Load()?.Mode ?? "";

        public static void Started()
        {
            var record = Load() ?? new Record();
            record.Running = true;
            Save(record);
        }

        public static void Finished(string summaryJson) => End(record => record.SummaryJson = summaryJson);

        public static void Failed(string error) => End(record => record.Error = error);

        /// <summary>
        /// The outcome of <paramref name="jobId"/>'s run once there is one: a summary, or the error
        /// that stopped the run. Cleared as it is read.
        /// </summary>
        public static bool TryTake(string jobId, out string summaryJson, out string error)
        {
            summaryJson = null;
            error = null;
            var record = Load();
            if (record == null || record.Running || record.JobId != jobId)
                return false;

            summaryJson = record.SummaryJson;
            error = record.Error;
            SessionState.EraseString(SessionKey);
            return !string.IsNullOrEmpty(summaryJson) || !string.IsNullOrEmpty(error);
        }

        static void End(Action<Record> outcome)
        {
            var record = Load();
            if (record == null || string.IsNullOrEmpty(record.JobId))
            {
                // Nobody waits for a run started elsewhere.
                SessionState.EraseString(SessionKey);
                return;
            }

            record.Running = false;
            outcome(record);
            Save(record);
        }

        static Record Load()
        {
            var json = SessionState.GetString(SessionKey, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Record>(json);
        }

        static void Save(Record record) => SessionState.SetString(SessionKey, JsonUtility.ToJson(record));
    }
}
