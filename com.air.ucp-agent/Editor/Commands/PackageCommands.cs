using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using UnityEngine;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PackageListCommand : EditorActionCommand<PackageListParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PackageListParams>(
            CommandNames.PackageList,
            CommandHostScope.Editor,
            "UPM packages in the project: name, version, source, and whether the manifest lists it.");

        protected override Dictionary<string, object> Execute(PackageListParams p) => PackageService.List(p);
    }

    public sealed class PackageAddCommand : CliCommand<PackageAddParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<PackageAddParams>(
            CommandNames.PackageAdd,
            CommandHostScope.Editor,
            "Add a UPM package and wait until it is resolved, compiled and loaded.",
            InvokeCompletionCatalog.CompletionPackages,
            defaultTimeoutMs: 5 * 60_000);

        public override void Run(PackageAddParams p)
        {
            try
            {
                var operation = PackageService.StartAdd(CommandId, p.Package);
                Defer(InvokeCompletionCatalog.CompletionPackages, JsonUtility.ToJson(operation));
            }
            catch (Exception ex)
            {
                CompleteFail(ex);
            }
        }
    }

    public sealed class PackageRemoveCommand : CliCommand<PackageRemoveParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<PackageRemoveParams>(
            CommandNames.PackageRemove,
            CommandHostScope.Editor,
            "Remove a package the manifest lists and wait until the project has reloaded without it. " +
            "Refuses ucp-agent, its dependencies, and embedded packages.",
            InvokeCompletionCatalog.CompletionPackages,
            defaultTimeoutMs: 5 * 60_000);

        public override void Run(PackageRemoveParams p)
        {
            try
            {
                var operation = PackageService.StartRemove(CommandId, p.Package);
                Defer(InvokeCompletionCatalog.CompletionPackages, JsonUtility.ToJson(operation));
            }
            catch (Exception ex)
            {
                CompleteFail(ex);
            }
        }
    }
}
