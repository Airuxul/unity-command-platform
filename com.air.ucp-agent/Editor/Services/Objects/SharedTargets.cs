using Air.UcpAgent.Cli;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// The edit target a group keeps open across consecutive steps that name the same prefab or
    /// scene, so they are saved once. Any other step closes it first.
    /// </summary>
    public static class SharedTargets
    {
        sealed class OpenTarget
        {
            public string Key;
            public EditTargetScope Scope;
        }

        /// <summary>The target <paramref name="target"/> names: the one already open, or a new one after saving the old.</summary>
        public static EditTargetScope Acquire(CommandGroupInfo group, EditTargetParams target)
        {
            var key = Key(target);
            if (group.OpenTarget is OpenTarget open && open.Key == key)
                return open.Scope;

            Close(group, commit: true);
            var scope = EditTargetScope.Open(target.Prefab, target.Scene);
            group.OpenTarget = new OpenTarget { Key = key, Scope = scope };
            return scope;
        }

        /// <summary>Saves (unless <paramref name="commit"/> is false) and releases the open target.</summary>
        public static void Close(CommandGroupInfo group, bool commit)
        {
            if (group?.OpenTarget is not OpenTarget open)
                return;

            group.OpenTarget = null;
            try
            {
                if (commit)
                    open.Scope.Commit();
            }
            finally
            {
                open.Scope.Dispose();
            }
        }

        static string Key(EditTargetParams target) =>
            $"{AssetPaths.Normalize(target.Prefab)}|{target.Scene?.Trim()}".ToLowerInvariant();
    }
}
