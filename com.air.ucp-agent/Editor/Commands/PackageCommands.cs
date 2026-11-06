using System;
using Air.UcpAgent.Completion;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using UnityEngine;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class PackageListCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PackageListParams>(
            CommandNames.PackageList,
            CommandHostScope.Editor,
            "UPM packages in the project: name, version, source, and whether the manifest lists it.");

        public static object Execute(PackageListParams p, CommandContext ctx) => PackageService.List(p);
    }

    [UcpCommand]
    public static class PackageAddCommand
    {
        public static readonly Type CompletionPolicy = typeof(PackagePolicy);

        public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor<PackageAddParams>(
            CommandNames.PackageAdd,
            CommandHostScope.Editor,
            "Add a UPM package and wait until it is resolved, compiled and loaded.",
            CompletionPolicy,
            defaultTimeoutMs: 5 * 60_000);

        public static object Execute(PackageAddParams p, CommandContext ctx)
        {
            var operation = PackageService.StartAdd(ctx.CommandId, p.Package);
            ctx.Defer(CompletionPolicy, UcpJson.Serialize(operation));
            return null;
        }
    }

    [UcpCommand]
    public static class PackageRemoveCommand
    {
        public static readonly Type CompletionPolicy = typeof(PackagePolicy);

        public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor<PackageRemoveParams>(
            CommandNames.PackageRemove,
            CommandHostScope.Editor,
            "Remove a package the manifest lists and wait until the project has reloaded without it. " +
            "Refuses ucp-agent, its dependencies, and embedded packages.",
            CompletionPolicy,
            defaultTimeoutMs: 5 * 60_000);

        public static object Execute(PackageRemoveParams p, CommandContext ctx)
        {
            var operation = PackageService.StartRemove(ctx.CommandId, p.Package);
            ctx.Defer(CompletionPolicy, UcpJson.Serialize(operation));
            return null;
        }
    }
}
