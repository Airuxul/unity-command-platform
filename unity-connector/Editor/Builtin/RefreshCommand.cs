using System.Collections.Generic;
using UnityCliConnector.Editor.Services;

namespace UnityCliConnector.Builtin
{
    [CliCommand(
        "refresh",
        Scope = CommandScope.Editor,
        Description = "Refresh AssetDatabase; compile=true starts async compilation job")]
    public static class RefreshCommand
    {
        public static CommandResult Run(CliParams p)
        {
            try
            {
                var data = AssetRefreshService.Refresh(p.ToDictionary());
                return CommandResult.Success(data);
            }
            catch (System.Exception ex)
            {
                return CommandResult.Fail(ex.Message);
            }
        }
    }
}
