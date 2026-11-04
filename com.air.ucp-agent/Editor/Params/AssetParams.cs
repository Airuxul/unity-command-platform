namespace Air.UcpAgent.Params
{
    /// <summary>Commands that address one asset or folder.</summary>
    public class AssetPathParams
    {
        [CliParam(Description = "asset or folder path, e.g. Assets/Prefabs/Player.prefab", Required = true,
            AlternateKeys = "asset,asset_path")]
        public string Path { get; set; }
    }

    public sealed class AssetListParams
    {
        [CliParam(Description = "folder to list (default: Assets)")]
        public string Folder { get; set; }

        [CliParam(Description = "Unity type filter, e.g. Prefab, Material, Texture2D, MonoScript")]
        public string Type { get; set; }

        [CliParam(Description = "name substring filter")]
        public string Name { get; set; }

        [CliParam(Description = "include sub-folders (default: true)")]
        public bool? Recursive { get; set; }

        [CliParam(Description = "max results (default: 200)")]
        public int? Limit { get; set; }
    }

    public sealed class AssetFindParams
    {
        [CliParam(Description = "raw AssetDatabase filter, e.g. \"t:Material glossy\" or \"l:Boss\"",
            Required = true, AlternateKeys = "query,search")]
        public string Filter { get; set; }

        [CliParam(Description = "folders to search under (default: whole project)")]
        public string[] Folders { get; set; }

        [CliParam(Description = "max results (default: 200)")]
        public int? Limit { get; set; }
    }

    public sealed class AssetInfoParams : AssetPathParams
    {
        [CliParam(Description = "also list direct dependencies")]
        public bool Dependencies { get; set; }

        [CliParam(Description = "also list sub-assets stored in the same file")]
        public bool SubAssets { get; set; }
    }

    public sealed class AssetCreateFolderParams : AssetPathParams
    {
    }

    public sealed class AssetDeleteParams : AssetPathParams
    {
    }

    public sealed class AssetMoveParams : AssetPathParams
    {
        [CliParam(Description = "destination path", Required = true, AlternateKeys = "output,destination,to")]
        public string Target { get; set; }
    }

    public sealed class AssetDependenciesParams : AssetPathParams
    {
        [CliParam(Description = "follow dependencies transitively")]
        public bool Recursive { get; set; }
    }

    public sealed class AssetGuidParams
    {
        [CliParam(Description = "asset path to convert to a GUID", AlternateKeys = "asset,asset_path")]
        public string Path { get; set; }

        [CliParam(Description = "GUID to convert to an asset path")]
        public string Guid { get; set; }
    }
}
