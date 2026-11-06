using Air.UcpAgent.Cli;
using Air.UcpAgent.Job;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Editor.Group
{
    /// <summary>
    /// Finishes a group whose step deferred (compile, play, test_run). Each frame it checks that
    /// step's job; once the job is done the group records it and runs the steps after it. The
    /// group's state is on disk, so this works across the domain reload the step caused.
    /// </summary>
    public sealed class GroupPolicy : ICompletionPolicy, IExpiringPolicy
    {
        public bool TryComplete(InvokeJobRecord job, EditorStateSnapshot state, out object result, out CommandFailure failure)
        {
            result = null;
            failure = null;
            if (!state.ReadyForTools)
                return false;

            var progress = GroupRun.Load(job.Id);
            if (progress == null)
            {
                failure = new CommandFailure(UcpErrorCodes.Orphaned, "The group's state on disk is gone, so it cannot go on.",
                    $"Look for its folder under {GroupRun.Root}.");
                return true;
            }

            GroupOutcome outcome;
            if (string.IsNullOrEmpty(progress.waiting))
            {
                outcome = GroupRun.Continue(progress, EditorJobStateManager.Jobs);
            }
            else
            {
                var child = EditorJobStateManager.Get(progress.waiting);
                if (child != null && !EditorJobStateManager.IsFinished(child))
                    return false;

                var view = child == null
                    ? new InvokeJobRecordView
                    {
                        Succeeded = false,
                        ErrorCode = UcpErrorCodes.Orphaned,
                        Message = "The step's job is gone; it may have passed its deadline.",
                    }
                    : View(child);
                if (child != null)
                    EditorJobStateManager.Remove(child.Id);
                outcome = GroupRun.Resume(progress, view, EditorJobStateManager.Jobs);
            }

            if (outcome.Waiting)
                return false;

            if (outcome.Failure != null)
                failure = outcome.Failure;
            else
                result = new PolicyOutcome { Message = outcome.Message, Data = outcome.Data };
            return true;
        }

        /// <summary>The caller has given up; take back what the group did, as its rollback says.</summary>
        public void Expired(InvokeJobRecord job)
        {
            var progress = GroupRun.Load(job.Id);
            if (progress == null)
                return;

            if (!string.IsNullOrEmpty(progress.waiting) && EditorJobStateManager.Get(progress.waiting) != null)
                EditorJobStateManager.Remove(progress.waiting);
            GroupRun.Expire(progress);
        }

        static InvokeJobRecordView View(InvokeJobRecord job) =>
            new()
            {
                Succeeded = job.Status == InvokeJobStatus.Succeeded,
                Message = job.Status == InvokeJobStatus.Succeeded ? job.Message : job.Error,
                ErrorCode = string.IsNullOrEmpty(job.ErrorCode) ? null : job.ErrorCode,
                Hint = string.IsNullOrEmpty(job.Hint) ? null : job.Hint,
                Data = job.Result != null ? JToken.FromObject(job.Result)
                    : string.IsNullOrEmpty(job.ResultJson) ? null : JToken.Parse(job.ResultJson),
            };
    }
}
