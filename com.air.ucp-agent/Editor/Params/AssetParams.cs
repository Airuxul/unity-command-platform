namespace Air.UcpAgent.Params
{
    /// <summary>Commands that address one asset or folder.</summary>
    public class AssetPathParams
    {
        [UcpParam(ParamKeys.Path, Description = "asset or folder path, e.g. Assets/Prefabs/Player.prefab",
            Required = true)]
        public string Path { get; set; }
    }

    /// <summary>Commands that write an asset somewhere new from an existing one: copy, move, variant.</summary>
    public abstract class CopyParams
    {
        [UcpParam(ParamKeys.Path, Description = "existing asset to start from", Required = true)]
        public string Path { get; set; }

        [UcpParam(ParamKeys.Target, Description = "path to write", Required = true)]
        public string Target { get; set; }
    }

    /// <summary>The folder search shared by asset_list, prefab_list and scene_list.</summary>
    public abstract class FolderQueryParams
    {
        [UcpParam(ParamKeys.Folder, Description = "folder to search under (default: Assets)")]
        public string Folder { get; set; }

        [UcpParam(Description = "name substring filter")]
        public string Name { get; set; }

        [UcpParam(Description = "max results (default: 200)")]
        public int? Limit { get; set; }
    }

    public sealed class AssetListParams : FolderQueryParams
    {
        [UcpParam(Description = "Unity type filter, e.g. Prefab, Material, Texture2D, MonoScript")]
        public string Type { get; set; }

        [UcpParam(Description = "include sub-folders (default: true)")]
        public bool? Recursive { get; set; }
    }

    public sealed class AssetFindParams
    {
        [UcpParam(Description = "raw AssetDatabase filter, e.g. \"t:Material glossy\" or \"l:Boss\"",
            Required = true)]
        public string Filter { get; set; }

        [UcpParam(Description = "folders to search under (default: whole project)")]
        public string[] Folders { get; set; }

        [UcpParam(Description = "max results (default: 200)")]
        public int? Limit { get; set; }
    }

    public sealed class AssetInfoParams : AssetPathParams
    {
        [UcpParam(Description = "also list direct dependencies")]
        public bool Dependencies { get; set; }

        [UcpParam(Description = "also list sub-assets stored in the same file")]
        public bool SubAssets { get; set; }
    }

    public sealed class AssetCreateFolderParams : AssetPathParams
    {
    }

    public sealed class AssetDeleteParams : AssetPathParams
    {
    }

    public sealed class AssetMoveParams : CopyParams
    {
    }

    public sealed class AssetDependenciesParams : AssetPathParams
    {
        [UcpParam(Description = "follow dependencies transitively")]
        public bool Recursive { get; set; }
    }

    public sealed class AssetGuidParams
    {
        [UcpParam(ParamKeys.Path, Description = "asset path to convert to a GUID")]
        public string Path { get; set; }

        [UcpParam(Description = "GUID to convert to an asset path")]
        public string Guid { get; set; }
    }
}
