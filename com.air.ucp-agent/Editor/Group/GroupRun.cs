using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.IO;
using Air.UcpAgent.Params;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor.Group
{
    /// <summary>One step's outcome, as the group reports it.</summary>
    public sealed class StepRecord
    {
        public int index;
        public string command;
        public bool ok;
        public string message;
        public string error_code;
        public string hint;
        public JToken data;
        public bool undone;
    }

    /// <summary>A step that may have to be taken back: enough to find, bind and undo it after a domain reload.</summary>
    public sealed class JournalEntry
    {
        public int step;
        public string command;
        public string args;
        public string context_type;
        public string context;
    }

    /// <summary>Where a group is, written to disk before and after every step.</summary>
    public sealed class GroupProgress
    {
        public string id;
        public List<GroupStep> steps = new List<GroupStep>();
        public string rollback = GroupParams.All;
        public string on_error = GroupParams.Stop;
        public bool dry_run;
        public long deadline_utc_ms;
        public int next;

        /// <summary>The deferred step's job id while the group waits for it; null otherwise.</summary>
        public string waiting;

        public List<StepRecord> results = new List<StepRecord>();
    }

    /// <summary>How a group ended.</summary>
    public sealed class GroupOutcome
    {
        /// <summary>True while a step's job is still running; the group finishes through <see cref="GroupPolicy"/>.</summary>
        public bool Waiting;

        public CommandFailure Failure;
        public Dictionary<string, object> Data;
        public string Message;
    }

    /// <summary>
    /// Runs the steps of a <c>group</c>: in order, each through <see cref="CommandExecutor.Invoke"/>,
    /// recording every step it may have to take back. Its state lives in
    /// <c>Library/ucp/undo/&lt;group id&gt;/</c> so a step that reloads the domain (compile, play)
    /// does not lose the group: <see cref="GroupPolicy"/> picks it up afterwards.
    /// </summary>
    public static class GroupRun
    {
        public const int MaxSteps = 100;

        const string ProgressFile = "progress.json";
        const string JournalFile = "journal.json";

        static readonly JsonSerializerSettings ContextJson = new()
        {
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None,
        };

        public static string Root => Path.Combine(EditorProjectPaths.GetProjectPath(), "Library", "ucp", "undo");

        public static string DirectoryOf(string groupId) => Path.Combine(Root, Sanitize(groupId));

        /// <summary>Checks every step before any runs, then runs them.</summary>
        public static GroupOutcome Start(GroupParams p, CommandContext ctx)
        {
            Validate(p);
            var progress = new GroupProgress
            {
                id = ctx.CommandId ?? Guid.NewGuid().ToString("N"),
                steps = p.Steps,
                rollback = p.Rollback ?? GroupParams.All,
                on_error = p.OnError ?? GroupParams.Stop,
                dry_run = p.DryRun,
                deadline_utc_ms = NowMs() + Math.Max(1, ctx.TimeoutMs),
            };

            var directory = DirectoryOf(progress.id);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);
            Save(progress);
            SaveJournal(progress.id, new List<JournalEntry>());
            return Continue(progress, ctx.Jobs);
        }

        /// <summary>Runs from <see cref="GroupProgress.next"/> until a step defers or the group ends.</summary>
        public static GroupOutcome Continue(GroupProgress progress, IDeferredJobs jobs)
        {
            var group = Info(progress);
            var journal = LoadJournal(progress.id);
            try
            {
                for (var i = progress.next; i < progress.steps.Count; i++)
                {
                    if (NowMs() >= progress.deadline_utc_ms)
                        return Finish(progress, group, journal, TimedOut(progress, i, "before it could start"));

                    var step = progress.steps[i];
                    var entry = Resolve(step, i, out var parameters);
                    var sharesTarget = group.SharesTargets && entry.EditsTarget && !entry.Descriptor.ReadsTargetOnly;
                    if (!sharesTarget)
                        SharedTargets.Close(group, commit: !progress.dry_run);

                    var ctx = entry.CreateContext();
                    var childId = $"{progress.id}:{i}";
                    var remaining = (int)Math.Max(1, progress.deadline_utc_ms - NowMs());
                    ctx.Bind(CommandRegistry.EditorHost, childId, entry.Name, remaining, jobs, group);
                    var expect = OpExpectation.Parse(step.Expect, Values(step), i, entry.Name);
                    if (expect != null && ctx is EditTargetContext edit)
                        edit.Precondition = scope => expect.Check(scope, i, entry.Name);

                    var payload = CommandExecutor.Invoke(entry, parameters, ctx);
                    if (!ctx.IsCompleted)
                    {
                        // A step that edits nothing is the only kind that defers; its job ends it.
                        progress.next = i;
                        progress.waiting = childId;
                        Save(progress);
                        return new GroupOutcome { Waiting = true };
                    }

                    if (jobs != null && jobs.Has(childId))
                        jobs.Remove(childId);

                    var failed = Record(progress, i, entry, ctx, payload);
                    if (!failed && Undoable(entry, ctx))
                    {
                        journal.Add(new JournalEntry
                        {
                            step = i,
                            command = entry.Name,
                            args = (step.Args ?? new JObject()).ToString(Formatting.None),
                            context_type = ctx.GetType().AssemblyQualifiedName,
                            context = JsonConvert.SerializeObject(ctx, ContextJson),
                        });
                        SaveJournal(progress.id, journal);
                    }

                    progress.next = i + 1;
                    Save(progress);

                    if (failed && (progress.rollback == GroupParams.All || progress.dry_run || progress.on_error == GroupParams.Stop))
                        return Finish(progress, group, journal, i);
                }

                return Finish(progress, group, journal, null);
            }
            catch (Exception ex)
            {
                // Something outside a step broke (the journal could not be written, a step vanished
                // from the registry after a reload): take back what ran, then report it.
                var failure = CommandFailure.From(CommandRegistry.Unwrap(ex));
                progress.results.Add(new StepRecord
                {
                    index = progress.next,
                    command = progress.next < progress.steps.Count ? progress.steps[progress.next].Command : null,
                    ok = false,
                    message = failure.Message,
                    error_code = failure.Code,
                    hint = failure.Hint,
                });
                return Finish(progress, group, journal, progress.next);
            }
        }

        /// <summary>The deferred step finished. Records it and runs the rest.</summary>
        public static GroupOutcome Resume(GroupProgress progress, InvokeJobRecordView child, IDeferredJobs jobs)
        {
            var i = progress.next;
            var step = progress.steps[i];
            var record = new StepRecord
            {
                index = i,
                command = step.Command,
                ok = child.Succeeded,
                message = child.Message,
                error_code = child.Succeeded ? null : child.ErrorCode,
                hint = child.Succeeded ? null : child.Hint,
                data = child.Data,
            };
            progress.results.Add(record);
            progress.waiting = null;
            progress.next = i + 1;
            Save(progress);

            if (!child.Succeeded && (progress.rollback == GroupParams.All || progress.dry_run || progress.on_error == GroupParams.Stop))
                return Finish(progress, Info(progress), LoadJournal(progress.id), i);

            return Continue(progress, jobs);
        }

        /// <summary>The group's deadline passed while a step was still running.</summary>
        public static GroupOutcome Expire(GroupProgress progress)
        {
            progress.waiting = null;
            return Finish(progress, Info(progress), LoadJournal(progress.id), TimedOut(progress, progress.next, "while it ran"));
        }

        public static GroupProgress Load(string groupId)
        {
            var file = Path.Combine(DirectoryOf(groupId), ProgressFile);
            return File.Exists(file) ? JsonConvert.DeserializeObject<GroupProgress>(File.ReadAllText(file)) : null;
        }

        /// <summary>
        /// Group folders whose job is gone: Unity stopped while the group ran. They are reported and
        /// kept, never undone automatically, because what happened since is unknown.
        /// </summary>
        public static void ReportLeftovers(Func<string, bool> jobExists)
        {
            if (!Directory.Exists(Root))
                return;

            foreach (var directory in Directory.GetDirectories(Root))
            {
                var progress = SafeLoad(directory);
                var id = progress?.id ?? Path.GetFileName(directory);
                if (jobExists(id))
                    continue;

                Debug.LogWarning(
                    $"[ucp-agent] group '{id}' did not finish (Unity stopped while it ran). Nothing was taken back; " +
                    $"its journal and backups are in {directory}. Delete the folder once the project is checked.");
            }
        }

        // ------------------------------------------------------------------------------------

        static void Validate(GroupParams p)
        {
            if (p.Steps == null || p.Steps.Count == 0)
                throw new ArgumentException("'steps' must list at least one {\"command\":..,\"args\":{..}}.");
            if (p.Steps.Count > MaxSteps)
                throw new ArgumentException($"A group takes at most {MaxSteps} steps, got {p.Steps.Count}.");
            if (p.Rollback == GroupParams.All && p.OnError == GroupParams.Continue)
                throw new ArgumentException("on_error=continue needs rollback=failed: with rollback=all a failed step ends the group.");
            if (p.DryRun && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new UcpException(
                    UcpErrorCodes.EditModeRequired,
                    "dry_run needs undo to take the steps back, and Play Mode records none.",
                    $"Run {CommandNames.Stop} first.");
            }

            for (var i = 0; i < p.Steps.Count; i++)
            {
                var entry = Resolve(p.Steps[i], i, out _);
                if (p.Steps[i].Expect != null && !entry.EditsTarget)
                    throw new ArgumentException($"steps[{i}] {entry.Name}: 'expect' only applies to a step that edits a scene or prefab.");

                OpExpectation.Parse(p.Steps[i].Expect, Values(p.Steps[i]), i, entry.Name);
            }
        }

        static CommandEntry Resolve(GroupStep step, int index, out object parameters)
        {
            var name = step?.Command?.Trim();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"steps[{index}] has no 'command'.");

            var entry = CommandRegistry.Find(CommandRegistry.EditorHost, name)
                ?? throw new ArgumentException($"steps[{index}] '{name}' is not a command of this Editor.");
            if (entry.CommandType == typeof(Commands.GroupCommand))
                throw new ArgumentException($"steps[{index}]: a group cannot contain another group.");

            var (bound, error) = CommandExecutor.Bind(entry, Values(step));
            if (error != null)
                throw new ArgumentException($"steps[{index}] {entry.Name}: {error.Message}");

            parameters = bound;
            return entry;
        }

        static Dictionary<string, object> Values(GroupStep step) =>
            CommandExecutor.NormalizeArgs(step?.Args?.ToObject<Dictionary<string, object>>());

        /// <summary>A step is in the journal when there is something to take back: an Undo, or an edit target it wrote.</summary>
        static bool Undoable(CommandEntry entry, CommandContext ctx) =>
            entry.CanUndo || ctx is EditTargetContext && !entry.Descriptor.ReadsTargetOnly;

        static bool Record(GroupProgress progress, int index, CommandEntry entry, CommandContext ctx, object payload)
        {
            var failed = ctx.Failure != null;
            progress.results.Add(new StepRecord
            {
                index = index,
                command = entry.Name,
                ok = !failed,
                message = failed ? ctx.Failure.Message : ctx.Message ?? entry.Name,
                error_code = ctx.Failure?.Code,
                hint = ctx.Failure?.Hint,
                data = ToToken(failed ? ctx.Failure.Data : ctx.Result ?? payload),
                // A failed step was taken back by the execution host before it returned.
                undone = failed && Undoable(entry, ctx),
            });
            return failed;
        }

        /// <param name="failedAt">The step that failed or timed out; null when every step ran.</param>
        static GroupOutcome Finish(GroupProgress progress, CommandGroupInfo group, List<JournalEntry> journal, int? failedAt)
        {
            failedAt ??= progress.results.FirstOrDefault(r => !r.ok)?.index;
            var rollAll = progress.dry_run || failedAt != null && progress.rollback == GroupParams.All;
            var errors = new List<Dictionary<string, object>>();

            try
            {
                SharedTargets.Close(group, commit: !rollAll);
            }
            catch (Exception ex)
            {
                // Saving the last target failed: the group did not succeed, so take everything back.
                errors.Add(new Dictionary<string, object> { ["step"] = progress.next - 1, ["message"] = ex.Message });
                rollAll = true;
                failedAt ??= progress.next - 1;
            }

            if (rollAll)
                RollBack(progress, group, journal, errors);
            else if (failedAt == null)
                CollapseUndo(journal);

            var data = new Dictionary<string, object>
            {
                ["steps"] = progress.results,
                ["rollback"] = new Dictionary<string, object>
                {
                    ["mode"] = progress.dry_run ? "dry_run" : progress.rollback,
                    ["ran"] = rollAll,
                    ["ok"] = errors.Count == 0,
                    ["errors"] = errors,
                },
                ["dry_run"] = progress.dry_run,
                ["failed_at"] = failedAt,
                ["not_run"] = progress.steps.Count - progress.next,
            };

            Delete(progress.id);

            var firstFailure = progress.results.FirstOrDefault(r => !r.ok);
            if (errors.Count > 0)
            {
                return new GroupOutcome
                {
                    Data = data,
                    Failure = new CommandFailure(
                        UcpErrorCodes.RollbackFailed,
                        (firstFailure != null ? $"steps[{firstFailure.index}] {firstFailure.command} failed: {firstFailure.message} " : "") +
                        $"Taking the steps back also failed for {errors.Count} of them, so the project may be half changed.",
                        "data.rollback.errors lists which steps are still applied.",
                        data),
                };
            }

            if (firstFailure != null)
            {
                var kept = rollAll ? "nothing was kept" : "the steps before it were kept";
                return new GroupOutcome
                {
                    Data = data,
                    Failure = new CommandFailure(
                        firstFailure.error_code,
                        $"steps[{firstFailure.index}] {firstFailure.command} failed, so {kept}: {firstFailure.message}",
                        firstFailure.hint,
                        data),
                };
            }

            return new GroupOutcome
            {
                Data = data,
                Message = progress.dry_run
                    ? $"{progress.steps.Count} steps ran and were taken back (dry run)"
                    : $"{progress.steps.Count} steps succeeded",
            };
        }

        static void RollBack(GroupProgress progress, CommandGroupInfo group, List<JournalEntry> journal, List<Dictionary<string, object>> errors)
        {
            for (var i = journal.Count - 1; i >= 0; i--)
            {
                var item = journal[i];
                try
                {
                    var entry = CommandRegistry.Find(CommandRegistry.EditorHost, item.command)
                        ?? throw new InvalidOperationException($"'{item.command}' is no longer a command, so it cannot be undone.");
                    var values = CommandExecutor.NormalizeArgs(JObject.Parse(item.args).ToObject<Dictionary<string, object>>());
                    var (parameters, error) = CommandExecutor.Bind(entry, values);
                    if (error != null)
                        throw new InvalidOperationException(error.Message);

                    var type = Type.GetType(item.context_type)
                        ?? throw new InvalidOperationException($"Context type '{item.context_type}' is not loaded.");
                    var ctx = (CommandContext)JsonConvert.DeserializeObject(item.context, type, ContextJson);
                    ctx.Bind(CommandRegistry.EditorHost, $"{progress.id}:{item.step}", entry.Name, 0, null, group);
                    ExecutionHost.Current.Undo(entry, parameters, ctx);
                    var record = progress.results.FirstOrDefault(r => r.index == item.step);
                    if (record != null)
                        record.undone = true;
                }
                catch (Exception ex)
                {
                    errors.Add(new Dictionary<string, object>
                    {
                        ["step"] = item.step,
                        ["command"] = item.command,
                        ["message"] = CommandRegistry.Unwrap(ex).Message,
                    });
                }
            }
        }

        /// <summary>Folds the steps' undo groups into one, so Ctrl+Z in the Editor takes the whole group back.</summary>
        static void CollapseUndo(List<JournalEntry> journal)
        {
            var first = -1;
            foreach (var item in journal)
            {
                // Present on EditTargetContext and its subclasses only.
                var group = JObject.Parse(item.context).Value<int?>(nameof(EditTargetContext.UndoGroup)) ?? -1;
                if (group >= 0 && (first < 0 || group < first))
                    first = group;
            }

            if (first >= 0)
                Undo.CollapseUndoOperations(first);
        }

        static int TimedOut(GroupProgress progress, int index, string when)
        {
            progress.results.Add(new StepRecord
            {
                index = index,
                command = index < progress.steps.Count ? progress.steps[index].Command : null,
                ok = false,
                message = $"The group's deadline passed {when}.",
                error_code = UcpErrorCodes.Timeout,
                hint = "Pass a longer --timeout; it covers every step of the group.",
            });
            progress.next = index + 1;
            return index;
        }

        static CommandGroupInfo Info(GroupProgress progress) =>
            new(progress.id, DirectoryOf(progress.id), sharesTargets: progress.rollback == GroupParams.All || progress.dry_run);

        static JToken ToToken(object value) => value == null ? null : JToken.FromObject(value);

        static void Save(GroupProgress progress) =>
            UcpPaths.WriteJsonAtomic(Path.Combine(DirectoryOf(progress.id), ProgressFile), JsonConvert.SerializeObject(progress, Formatting.Indented));

        static void SaveJournal(string groupId, List<JournalEntry> journal) =>
            UcpPaths.WriteJsonAtomic(Path.Combine(DirectoryOf(groupId), JournalFile), JsonConvert.SerializeObject(journal, Formatting.Indented));

        static List<JournalEntry> LoadJournal(string groupId)
        {
            var file = Path.Combine(DirectoryOf(groupId), JournalFile);
            return File.Exists(file)
                ? JsonConvert.DeserializeObject<List<JournalEntry>>(File.ReadAllText(file)) ?? new List<JournalEntry>()
                : new List<JournalEntry>();
        }

        static GroupProgress SafeLoad(string directory)
        {
            try
            {
                var file = Path.Combine(directory, ProgressFile);
                return File.Exists(file) ? JsonConvert.DeserializeObject<GroupProgress>(File.ReadAllText(file)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void Delete(string groupId)
        {
            try
            {
                var directory = DirectoryOf(groupId);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[ucp-agent] could not remove the folder of group '{groupId}': {ex.Message}");
            }
        }

        static string Sanitize(string id)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return id;
        }

        static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>The finished job of a group's deferred step, as the group reads it.</summary>
    public sealed class InvokeJobRecordView
    {
        public bool Succeeded;
        public string Message;
        public string ErrorCode;
        public string Hint;
        public JToken Data;
    }
}
