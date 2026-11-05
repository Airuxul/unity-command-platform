namespace Air.UcpAgent.Params
{
    /// <summary>
    /// Parameter names shared by many commands, so the <see cref="CliParamAttribute"/> that declares
    /// one and the error that names it cannot drift apart. Each name has one meaning everywhere.
    /// </summary>
    public static class ParamKeys
    {
        /// <summary>The prefab asset a content command edits — never an input or an output file.</summary>
        public const string Prefab = "prefab";

        /// <summary>A loaded scene, by name or asset path.</summary>
        public const string Scene = "scene";

        /// <summary>The asset a command operates on, or the file it writes.</summary>
        public const string Path = "path";

        /// <summary>Where a copy, move or variant is written.</summary>
        public const string Target = "target";

        /// <summary>The prefab asset an instance is made from.</summary>
        public const string Source = "source";

        /// <summary>A hierarchy path inside the edit target.</summary>
        public const string Node = "node";

        public const string Parent = "parent";

        public const string Folder = "folder";
    }
}
