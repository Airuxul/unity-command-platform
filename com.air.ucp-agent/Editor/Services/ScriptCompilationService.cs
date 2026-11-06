using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;
using UnityEditor;
using UnityEditor.Compilation;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>
    /// Requests script compilation. Completion is owned by <see cref="EditorJobStateManager"/>
    /// (<see cref="Air.UcpAgent.Completion.CompilationPolicy"/>), including after domain reload.
    /// </summary>
    /// <remarks>
    /// Subscribes at domain load, not on first use: <c>compile</c> refreshes before it requests a
    /// compilation, and the compile that refresh starts has to be seen too.
    /// </remarks>
    [InitializeOnLoad]
    public static class ScriptCompilationService
    {
        const int MaxReportedErrors = 50;

        private static string _activeCommandId;
        private static object _compilationContext;

        /// <summary>
        /// Errors of the last compilation in this domain that rebuilt an assembly. A failed compile
        /// does not reload the domain, so the policy can still read them after the callbacks ran.
        /// </summary>
        static readonly List<Dictionary<string, object>> LastErrors = new();

        /// <summary>False until a compilation in this domain rebuilt something.</summary>
        static bool _haveResult;

        static readonly List<Dictionary<string, object>> PendingErrors = new();
        static int _pendingAssemblies;

        static ScriptCompilationService()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        public static bool OwnsActiveCommand(string commandId) =>
            !string.IsNullOrEmpty(commandId)
            && string.Equals(_activeCommandId, commandId, StringComparison.Ordinal);

        public static void RequestWithCompletion(string commandId)
        {
            if (string.IsNullOrEmpty(commandId))
                throw new ArgumentException("commandId is required", nameof(commandId));

            _activeCommandId = commandId;
            _compilationContext = null;
            EditorJobStateManager.MarkRunning(commandId);
            CompilationPipeline.RequestScriptCompilation();
        }

        /// <summary>
        /// Null if the scripts compile, judged by the last compilation in this domain that rebuilt
        /// an assembly. <see cref="EditorUtility.scriptCompilationFailed"/> keeps its old value
        /// until the domain reloads, so it only decides when this domain has seen no such
        /// compilation: right after a reload, or in an editor that opened a broken project.
        /// </summary>
        public static CommandFailure CompileFailure()
        {
            var failed = _haveResult ? LastErrors.Count > 0 : EditorUtility.scriptCompilationFailed;
            if (!failed)
                return null;

            var message = LastErrors.Count == 0
                ? "Scripts have compile errors."
                : $"Compilation failed with {LastErrors.Count} error(s). First: {LastErrors[0]["message"]}";
            return new CommandFailure(
                UcpErrorCodes.CompileError,
                message,
                LastErrors.Count == 0
                    ? $"Run {CommandNames.Console} with type=error to read them."
                    : "Fix the errors in data.errors, then compile again.",
                new Dictionary<string, object> { ["errors"] = new List<Dictionary<string, object>>(LastErrors) });
        }

        static void OnCompilationStarted(object context)
        {
            PendingErrors.Clear();
            _pendingAssemblies = 0;
            if (string.IsNullOrEmpty(_activeCommandId))
                return;

            _compilationContext = context;
        }

        static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            _pendingAssemblies++;
            foreach (var message in messages)
            {
                if (message.type != CompilerMessageType.Error || PendingErrors.Count >= MaxReportedErrors)
                    continue;

                PendingErrors.Add(new Dictionary<string, object>
                {
                    ["file"] = message.file,
                    ["line"] = message.line,
                    ["column"] = message.column,
                    ["message"] = message.message,
                });
            }
        }

        static void OnCompilationFinished(object context)
        {
            if (_pendingAssemblies > 0)
            {
                LastErrors.Clear();
                LastErrors.AddRange(PendingErrors);
                _haveResult = true;
            }

            if (string.IsNullOrEmpty(_activeCommandId))
                return;

            if (_compilationContext != null && !ReferenceEquals(context, _compilationContext))
                return;

            var commandId = _activeCommandId;
            _activeCommandId = null;
            _compilationContext = null;

            // Nothing went through the compiler: either nothing changed, or the sources match a
            // cached build (a fix that undoes a broken edit). Only the domain reload that may follow
            // tells which, so CompilationPolicy decides once Unity is idle.
            if (_pendingAssemblies == 0)
                return;

            try
            {
                var failure = CompileFailure();
                if (failure != null)
                {
                    EditorJobStateManager.Fail(commandId, failure);
                    return;
                }

                EditorJobStateManager.Succeed(commandId, new PolicyOutcome
                {
                    Message = "compile completed",
                    Data = new Dictionary<string, object>
                    {
                        ["compiled"] = true,
                        ["note"] = "compilation_finished",
                    },
                });
            }
            catch (Exception ex)
            {
                EditorJobStateManager.Fail(commandId, CommandFailure.From(ex));
            }
        }
    }
}
