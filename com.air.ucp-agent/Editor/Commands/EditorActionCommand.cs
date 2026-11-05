using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Editor.Commands
{
    /// <summary>
    /// Shell for editor commands whose work lives in a service: a returned dictionary becomes the
    /// result payload, a thrown exception becomes <c>CompleteFail(ex)</c>. Services signal bad input
    /// by throwing; a <see cref="UcpException"/> sets the error code and hint, and the message must
    /// be actionable on its own.
    /// </summary>
    public abstract class EditorActionCommand<TParams> : CliCommand<TParams>
    {
        protected abstract Dictionary<string, object> Execute(TParams p);

        protected virtual string Message(TParams p) => Descriptor.Name;

        public sealed override void Run(TParams p)
        {
            try
            {
                var payload = Execute(p);
                CompleteSuccess(InvokeResult.Ok(Message(p), payload));
            }
            catch (Exception ex)
            {
                CompleteFail(ex);
            }
        }
    }

    /// <inheritdoc cref="EditorActionCommand{TParams}"/>
    public abstract class EditorActionCommand : CliCommand
    {
        protected abstract Dictionary<string, object> Execute();

        protected virtual string Message() => Descriptor.Name;

        public sealed override void Run()
        {
            try
            {
                var payload = Execute();
                CompleteSuccess(InvokeResult.Ok(Message(), payload));
            }
            catch (Exception ex)
            {
                CompleteFail(ex);
            }
        }
    }
}
