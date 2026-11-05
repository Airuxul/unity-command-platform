using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Completion
{
    /// <summary>Finishes package_add / package_remove once the change is resolved, compiled and reloaded.</summary>
    public sealed class PackagePolicy : ICompletionPolicy
    {
        public string Kind => InvokeCompletionCatalog.CompletionPackages;

        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out CommandFailure failure)
        {
            result = null;
            var operation = string.IsNullOrEmpty(command.CompletionArgs)
                ? null
                : JsonUtility.FromJson<PackageService.Operation>(command.CompletionArgs);
            if (operation == null)
            {
                failure = new CommandFailure(UcpErrorCodes.CommandFailed, "Package job has no operation.");
                return true;
            }

            var busy = state.IsCompiling || EditorApplication.isUpdating;
            if (!PackageService.TryFinish(command.Id, operation, busy, out var payload, out failure))
                return false;

            if (failure == null)
            {
                var name = operation.Action == PackageService.Add ? CommandNames.PackageAdd : CommandNames.PackageRemove;
                result = InvokeResult.Ok(name, payload);
            }

            return true;
        }
    }
}
