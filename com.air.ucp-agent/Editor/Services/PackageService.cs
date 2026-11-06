using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>
    /// UPM packages. Adding or removing one resolves the project, and a package with scripts
    /// recompiles and reloads the domain, which loses the in-memory request. So a job finishes on
    /// what the registered packages say once the Editor has gone quiet, not on the request alone.
    /// </summary>
    public static class PackageService
    {
        public const string Add = "add";
        public const string Remove = "remove";

        /// <summary>
        /// Quiet this long before the job answers: a resolve that brings in scripts starts compiling
        /// a few frames after its request completes, and the caller should get the Editor after that.
        /// </summary>
        const double SettleSeconds = 1.0;

        [Serializable]
        public sealed class Operation
        {
            public string Action;

            /// <summary>What package_add was given, or the name package_remove was given.</summary>
            public string Identifier;

            /// <summary>The package name when known up front; a git URL only names it once resolved.</summary>
            public string Name;
        }

        /// <summary>The request of the job in flight. Lost in a domain reload; the job then goes by the registered packages.</summary>
        static Request _request;

        static string _requestJobId;
        static double _quietSince;

        public static Dictionary<string, object> List(PackageListParams p)
        {
            var packages = PackageInfo.GetAllRegisteredPackages()
                .Where(info => p.Indirect || info.isDirectDependency)
                .OrderBy(info => info.name, StringComparer.Ordinal)
                .Select(Describe)
                .ToList();
            return new Dictionary<string, object> { ["packages"] = packages, ["count"] = packages.Count };
        }

        public static Operation StartAdd(string jobId, string identifier)
        {
            RequireIdle(CommandNames.PackageAdd);
            identifier = identifier.Trim();
            var operation = new Operation { Action = Add, Identifier = identifier, Name = NameOf(identifier) };
            Begin(jobId, Client.Add(identifier));
            return operation;
        }

        public static Operation StartRemove(string jobId, string name)
        {
            RequireIdle(CommandNames.PackageRemove);
            name = name.Trim();
            var registered = PackageInfo.GetAllRegisteredPackages();
            var info = registered.FirstOrDefault(i => i.name == name);
            if (info == null || !info.isDirectDependency)
            {
                throw new UcpException(UcpErrorCodes.InvalidArgument, $"'{name}' is not in the project manifest.",
                    $"Run {CommandNames.PackageList} for the direct dependencies.");
            }

            var self = PackageInfo.FindForAssembly(typeof(PackageService).Assembly);
            if (self != null && (name == self.name || self.dependencies.Any(d => d.name == name)))
                throw new InvalidOperationException($"'{name}' is {(name == self.name ? "ucp-agent itself" : "a dependency of ucp-agent")}; removing it would cut this connection.");
            if (info.source == PackageSource.Embedded)
                throw new InvalidOperationException($"'{name}' is embedded in Packages/{Path.GetFileName(info.resolvedPath)}; it goes when that folder does.");

            Begin(jobId, Client.Remove(name));
            return new Operation { Action = Remove, Identifier = name, Name = name };
        }

        /// <returns>True once the job is done, with <paramref name="result"/> or <paramref name="failure"/>.</returns>
        public static bool TryFinish(string jobId, Operation operation, bool editorBusy, out Dictionary<string, object> result,
            out CommandFailure failure)
        {
            result = null;
            failure = null;

            var request = _requestJobId == jobId ? _request : null;
            if (request != null)
            {
                if (!request.IsCompleted)
                    return false;

                if (request.Status == StatusCode.Failure)
                {
                    Forget();
                    failure = Failure(operation, request.Error);
                    return true;
                }

                if (request is AddRequest add && add.Result != null)
                    operation.Name = add.Result.name;
            }

            var now = EditorApplication.timeSinceStartup;
            if (editorBusy || _quietSince <= 0)
            {
                _quietSince = editorBusy ? 0 : now;
                return false;
            }

            if (now - _quietSince < SettleSeconds)
                return false;

            Forget();
            var info = Find(operation);
            if (operation.Action == Remove)
            {
                if (info != null)
                {
                    failure = new CommandFailure(UcpErrorCodes.CommandFailed, $"'{operation.Name}' is still installed.",
                        "console may say why the Package Manager kept it.");
                    return true;
                }

                result = new Dictionary<string, object> { ["package"] = operation.Name, ["removed"] = true };
                return true;
            }

            if (info == null)
            {
                failure = new CommandFailure(UcpErrorCodes.CommandFailed, $"'{operation.Identifier}' is not installed after resolving.",
                    "console has the Package Manager's error.");
                return true;
            }

            result = Describe(info);
            return true;
        }

        static void Begin(string jobId, Request request)
        {
            _request = request;
            _requestJobId = jobId;
            _quietSince = 0;
        }

        static void Forget()
        {
            _request = null;
            _requestJobId = null;
            _quietSince = 0;
        }

        static void RequireIdle(string command)
        {
            if (EditorApplication.isPlaying)
                throw new UcpException(UcpErrorCodes.EditModeRequired, $"{command} runs in Edit Mode.", $"Run {CommandNames.Stop} first.");
            if (_request != null && !_request.IsCompleted)
                throw new InvalidOperationException("Another package change is still resolving.");
        }

        /// <summary>A name, name@version, or a local folder's package.json; null for a git URL or tarball.</summary>
        static string NameOf(string identifier)
        {
            if (identifier.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = Path.Combine(Path.GetFullPath(Path.Combine(EditorProjectPaths.GetProjectPath(), "Packages",
                    identifier.Substring("file:".Length))), "package.json");
                return File.Exists(manifest) ? UcpJson.Deserialize<PackageName>(File.ReadAllText(manifest))?.name : null;
            }

            if (identifier.Contains("://") || identifier.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                return null;

            var at = identifier.IndexOf('@');
            return at > 0 ? identifier.Substring(0, at) : identifier;
        }

        [Serializable]
        sealed class PackageName
        {
            public string name;
        }

        static PackageInfo Find(Operation operation)
        {
            var registered = PackageInfo.GetAllRegisteredPackages();
            if (!string.IsNullOrEmpty(operation.Name))
                return registered.FirstOrDefault(i => i.name == operation.Name);

            // Git and tarball packages are registered as name@<what was added>.
            return registered.FirstOrDefault(i => i.packageId.EndsWith("@" + operation.Identifier, StringComparison.Ordinal));
        }

        static CommandFailure Failure(Operation operation, Error error)
        {
            var code = error?.errorCode is ErrorCode.NotFound or ErrorCode.InvalidParameter
                ? UcpErrorCodes.InvalidArgument
                : UcpErrorCodes.CommandFailed;
            return new CommandFailure(code, $"package_{operation.Action} '{operation.Identifier}' failed: {error?.message ?? "unknown error"}");
        }

        static Dictionary<string, object> Describe(PackageInfo info) =>
            new()
            {
                ["package"] = info.name,
                ["version"] = info.version,
                ["display_name"] = info.displayName,
                ["source"] = info.source.ToString().ToLowerInvariant(),
                ["direct"] = info.isDirectDependency,
            };
    }
}
