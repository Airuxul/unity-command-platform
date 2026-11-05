using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine.SceneManagement;

namespace Air.UcpAgent.Editor.TestRunner
{
    public sealed class TestRunParams
    {
        public const string Edit = "edit";
        public const string Play = "play";

        [CliParam(Description = "edit or play (default: edit)", AllowedValues = Edit + "|" + Play)]
        public string Mode { get; set; } = Edit;

        [CliParam(Description = "JSON array of full test names (Namespace.Class.Method), as data.failures prints them")]
        public string[] Tests { get; set; }

        [CliParam(Description = "regex on full test names")]
        public string Filter { get; set; }

        [CliParam(Description = "JSON array of NUnit categories")]
        public string[] Categories { get; set; }

        [CliParam(Description = "JSON array of test assembly names, without .dll")]
        public string[] Assemblies { get; set; }
    }

    /// <summary>Installed only with com.unity.test-framework; see this assembly's defineConstraints.</summary>
    public sealed class TestRunCommand : CliCommand<TestRunParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<TestRunParams>(
            CommandNames.TestRun,
            CommandHostScope.Editor,
            "Run Test Runner tests and wait for the result: counts, and each failure with its message. " +
            "Fails as tests_failed when any test fails. Other commands are refused as busy until the run ends.",
            InvokeCompletionCatalog.CompletionTests,
            defaultTimeoutMs: 10 * 60_000);

        public override void Run(TestRunParams p)
        {
            try
            {
                Start(p);
            }
            catch (Exception ex)
            {
                CompleteFail(ex);
            }
        }

        void Start(TestRunParams p)
        {
            if (EditorApplication.isPlaying)
            {
                throw new UcpException(UcpErrorCodes.EditModeRequired, $"{CommandNames.TestRun} starts from Edit Mode.",
                    $"Run {CommandNames.Stop} first; mode={TestRunParams.Play} enters Play Mode by itself.");
            }

            if (TestRunState.IsRunActive())
                throw new UcpException(UcpErrorCodes.Busy, "A test run is already in progress.");

            // The Test Runner asks whether to save them in a modal dialog, which nobody would answer.
            var dirty = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(s => s.isDirty)
                .Select(s => string.IsNullOrEmpty(s.path) ? "(untitled)" : s.path)
                .ToList();
            if (dirty.Count > 0)
            {
                throw new UcpException(UcpErrorCodes.CommandFailed,
                    $"Scenes have unsaved changes: {string.Join(", ", dirty)}.",
                    $"Save them with {CommandNames.SceneSave}, or close them with {CommandNames.SceneClose} force=true.");
            }

            var filter = new Filter
            {
                testMode = p.Mode == TestRunParams.Play ? TestMode.PlayMode : TestMode.EditMode,
                testNames = NullIfEmpty(p.Tests),
                groupNames = string.IsNullOrWhiteSpace(p.Filter) ? null : new[] { p.Filter },
                categoryNames = NullIfEmpty(p.Categories),
                assemblyNames = NullIfEmpty(p.Assemblies),
            };

            TestRunState.Begin(CommandId, p.Mode == TestRunParams.Play ? TestRunParams.Play : TestRunParams.Edit);
            try
            {
                UcpTestCallbacks.Api.Execute(new ExecutionSettings(filter));
            }
            catch (Exception ex)
            {
                TestRunState.Failed(ex.Message);
                throw;
            }
        }

        static string[] NullIfEmpty(IEnumerable<string> values)
        {
            var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray();
            return list == null || list.Length == 0 ? null : list;
        }
    }
}
