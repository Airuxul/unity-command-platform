using UnityCliConnector.Commands;

namespace UnityCliConnector.Builtin
{
    public class ReserializeCommand : CommandBase, ICommand<ReserializeParams>, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new CommandDescriptor<ReserializeParams>(
            CommandNames.Reserialize,
            CommandScope.Editor,
            "Force reserialize assets (whole project or paths)");

        public void Run(ReserializeParams p)
        {
            try
            {
                var data = Editor.Services.ReserializeService.Reserialize(p);
                CompleteSuccess(data);
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }
}
