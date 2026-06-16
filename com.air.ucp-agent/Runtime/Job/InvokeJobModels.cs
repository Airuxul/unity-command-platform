using System.Collections.Generic;
using System.Threading.Tasks;

namespace Air.UcpAgent.Job
{
    public interface IInvokeJobNotifier
    {
        void MarkRunning(string commandId);
        void Succeed(string commandId, object result);
        void Fail(string commandId, string error);
    }

    public sealed class InvokeContext
    {
        public string CommandId { get; set; }
        public string RequestId { get; set; }
        public string Command { get; set; }
        public string HostName { get; set; }
        public IInvokeJobNotifier Notifier { get; set; }
        public bool IsCompleted { get; private set; }
        public object CompletedResult { get; private set; }
        public string CompletedError { get; private set; }

        public void MarkRunning() => Notifier?.MarkRunning(CommandId);

        public void Succeed(object result)
        {
            if (IsCompleted) return;
            IsCompleted = true;
            CompletedResult = result;
            CompletedError = null;
            Notifier?.Succeed(CommandId, result);
        }

        public void Fail(string error)
        {
            if (IsCompleted) return;
            IsCompleted = true;
            CompletedResult = null;
            CompletedError = error;
            Notifier?.Fail(CommandId, error);
        }

        public void CompleteSuccess(object result) => Succeed(result);

        public void CompleteFail(string error) => Fail(error);

        public Task RunBackground(System.Func<object> work, System.Func<object, object> mapResult = null)
        {
            MarkRunning();
            return Task.Run(() =>
            {
                try
                {
                    var raw = work != null ? work() : null;
                    Succeed(mapResult != null ? mapResult(raw) : raw);
                }
                catch (System.Exception ex)
                {
                    Fail(ex.Message);
                }
            });
        }
    }

    public sealed class InvokeRequest
    {
        public string Command { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string RequestId { get; set; }
        public string Endpoint { get; set; } = "editor";
    }

    public sealed class InvokeAccepted
    {
        public string CommandId { get; set; }
        public string Completion { get; set; }
        public string RequestId { get; set; }
    }

    [System.Serializable]
    public sealed class InvokeJobRecord
    {
        public string Id;
        public string Command;
        public string RequestId;
        public InvokeJobStatus Status = InvokeJobStatus.Pending;
        public string CompletionKind;
        public string ResultJson;
        public string Error;
        public long CreatedAtUtcMs;
        public long UpdatedAtUtcMs;
        public object Result;
    }
}
