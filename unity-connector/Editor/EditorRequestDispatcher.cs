using System;
using System.Collections.Generic;
using UnityCliConnector.Http;

namespace UnityCliConnector
{
    public sealed class EditorRequestDispatcher : IRequestDispatcher
    {
        public const int ConnectorBuild = 4;

        public bool TryDispatch(string method, string path, string body, Action<int, Dictionary<string, object>> writeJson)
        {
            if (path == "/health" && method == "GET")
            {
                writeJson(200, new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["host"] = "editor",
                    ["connector_build"] = ConnectorBuild,
                });
                return true;
            }

            if (path == "/list" && method == "POST")
            {
                writeJson(200, CommandCatalog.BuildResponse());
                return true;
            }

            if (path.StartsWith("/jobs/", StringComparison.Ordinal) && method == "GET")
            {
                var id = path.Substring("/jobs/".Length).Trim('/');
                var job = JobManager.Get(id);
                if (job == null)
                {
                    writeJson(404, new Dictionary<string, object> { ["ok"] = false, ["error"] = "job_not_found" });
                    return true;
                }

                writeJson(200, JobToResponse(job));
                return true;
            }

            if (path == "/command" && method == "POST")
            {
                var request = CommandHttpHelper.ParseCommandRequest(body, "editor");
                var post = CommandPipeline.HandlePost(
                    request,
                    CommandJobCatalog.GetCompletionKind,
                    AcceptJob,
                    EditorCommandExecutor.ExecuteSync);
                writeJson(post.StatusCode, post.Body);
                return true;
            }

            return false;
        }

        private static Dictionary<string, object> AcceptJob(CommandRequest request, string completion)
        {
            return EditorMainThread.Run(() =>
            {
                var job = JobManager.Create(request.Command, completion, request.RequestId);
                EditorCommandExecutor.StartJobSideEffect(request.Command, request.Parameters);
                return new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["job_id"] = job.Id,
                    ["completion"] = completion,
                    ["request_id"] = request.RequestId,
                };
            }, TimeSpan.FromSeconds(30));
        }

        private static Dictionary<string, object> JobToResponse(JobRecord job)
        {
            var dict = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["job_id"] = job.Id,
                ["status"] = job.Status.ToString().ToLowerInvariant(),
                ["command"] = job.Command,
                ["error"] = job.Error,
                ["request_id"] = job.RequestId,
            };
            if (job.Result != null)
                dict["result"] = job.Result;
            return dict;
        }
    }
}
