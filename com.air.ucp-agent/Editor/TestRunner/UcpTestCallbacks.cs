using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Editor.Services;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Air.UcpAgent.Editor.TestRunner
{
    /// <summary>
    /// Feeds every test run's progress into <see cref="TestRunState"/>. The framework drops its
    /// callbacks with each domain, so they are registered again in every one, and only once per
    /// domain or each result would arrive twice.
    /// </summary>
    [InitializeOnLoad]
    static class UcpTestCallbacks
    {
        const int MaxFailures = 50;
        const int MaxStackLines = 8;

        public static readonly TestRunnerApi Api;

        static UcpTestCallbacks()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.hideFlags = HideFlags.HideAndDontSave;
            Api.RegisterCallbacks(new Callbacks());
        }

        sealed class Callbacks : ICallbacks, IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) => TestRunState.Started();

            public void RunFinished(ITestResultAdaptor result) => TestRunState.Finished(UcpJson.Serialize(Summarize(result)));

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            public void OnError(string message) => TestRunState.Failed(message);
        }

        static Dictionary<string, object> Summarize(ITestResultAdaptor run)
        {
            var failures = Leaves(run)
                .Where(r => r.TestStatus == TestStatus.Failed)
                .Take(MaxFailures)
                .Select(r => new Dictionary<string, object>
                {
                    ["name"] = r.Test.FullName,
                    ["message"] = r.Message?.Trim() ?? "",
                    ["stack_trace"] = string.Join("\n", (r.StackTrace ?? "").Split('\n').Take(MaxStackLines)).Trim(),
                })
                .ToList();

            return new Dictionary<string, object>
            {
                ["mode"] = TestRunState.Mode,
                ["total"] = run.PassCount + run.FailCount + run.SkipCount + run.InconclusiveCount,
                ["passed"] = run.PassCount,
                ["failed"] = run.FailCount,
                ["skipped"] = run.SkipCount,
                ["inconclusive"] = run.InconclusiveCount,
                ["duration_s"] = Math.Round(run.Duration, 3),
                // At most MaxFailures; failed has the full count.
                ["failures"] = failures,
            };
        }

        static IEnumerable<ITestResultAdaptor> Leaves(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                yield return result;
                yield break;
            }

            foreach (var child in result.Children)
            {
                foreach (var leaf in Leaves(child))
                    yield return leaf;
            }
        }
    }
}
