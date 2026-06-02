using System;
using Air.UnityConnector.Invoke;
using UnityEditor;
using UnityEditor.Compilation;

namespace Air.UnityConnector.Editor.Services
{
    /// <summary>
    /// Requests script compilation. Completion is owned by <see cref="EditorJobStateManager"/>
    /// (<see cref="Air.UnityConnector.Completion.CompilationPolicy"/>), including after domain reload.
    /// </summary>
    public static class ScriptCompilationService
    {
        private static string _activeCommandId;
        private static object _compilationContext;

        static ScriptCompilationService()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        public static bool OwnsActiveCommand(string commandId) =>
            !string.IsNullOrEmpty(commandId)
            && string.Equals(_activeCommandId, commandId, StringComparison.Ordinal);

        internal static void SetActiveCommandForTests(string commandId) => _activeCommandId = commandId;

        internal static void ClearActiveCommandForTests()
        {
            _activeCommandId = null;
            _compilationContext = null;
        }

        public static void RequestWithCompletion(
            string commandId,
            Action<object> completeSuccess,
            Action<string> completeFail = null)
        {
            if (string.IsNullOrEmpty(commandId))
                throw new ArgumentException("commandId is required", nameof(commandId));
            if (completeSuccess == null)
                throw new ArgumentNullException(nameof(completeSuccess));

            _activeCommandId = commandId;
            _compilationContext = null;
            EditorJobStateManager.MarkRunning(commandId);
            CompilationPipeline.RequestScriptCompilation();
        }

        static void OnCompilationStarted(object context)
        {
            if (string.IsNullOrEmpty(_activeCommandId))
                return;

            _compilationContext = context;
        }

        static void OnCompilationFinished(object context)
        {
            if (string.IsNullOrEmpty(_activeCommandId))
                return;

            if (_compilationContext != null && !ReferenceEquals(context, _compilationContext))
                return;

            var commandId = _activeCommandId;
            _activeCommandId = null;
            _compilationContext = null;

            try
            {
                EditorJobStateManager.Succeed(commandId, InvokeResult.Ok(
                    "compile completed",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["compiled"] = true,
                        ["note"] = "compilation_finished",
                    }));
            }
            catch (Exception ex)
            {
                EditorJobStateManager.Fail(commandId, ex.Message);
            }
        }
    }
}
