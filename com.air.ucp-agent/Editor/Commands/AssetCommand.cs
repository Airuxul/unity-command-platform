using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class AssetCommand : CliCommand<AssetParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetParams>(
            CommandNames.Asset,
            CommandHostScope.Editor,
            "Move, copy, delete, or create a folder with AssetDatabase so .meta stays with the asset. Paths must stay under Assets/.");

        public override void Run(AssetParams p)
        {
            try
            {
                var data = AssetService.Execute(p.Action, p.Path, p.To);
                CompleteSuccess(InvokeResult.Ok("asset completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }

    public sealed class AssetParams
    {
        [CliParam(Description = "move, copy, delete, or mkdir", AllowedValues = "move|copy|delete|mkdir", Required = true)]
        public string Action { get; set; }

        [CliParam(Description = "Asset path under Assets/. Source for move and copy.", Required = true, AlternateKeys = "from")]
        public string Path { get; set; }

        [CliParam(Description = "Destination asset path for move and copy")]
        public string To { get; set; }
    }
}
