// @tag cli-command
namespace Air.UcpAgent.Commands
{
    /// <summary>
    /// Names other C# mentions. Not a registry: a new command puts its name on its descriptor.
    /// Add a constant here only when other code has to mention that name.
    /// </summary>
    public static class CommandNames
    {
        // --- Agent / editor lifecycle -------------------------------------------------
        public const string Build = "build";
        public const string Compile = "compile";
        public const string Console = "console";
        public const string Echo = "echo";
        public const string Call = "call";
        public const string Exec = "exec";
        public const string Help = "help";
        public const string Menu = "menu";
        public const string Pause = "pause";
        public const string Ping = "ping";
        public const string Play = "play";
        public const string Profiler = "profiler";
        public const string Refresh = "refresh";
        public const string Reserialize = "reserialize";
        public const string Screenshot = "screenshot";
        public const string State = "state";
        public const string Stop = "stop";
        public const string TestRun = "test_run";

        // --- Project settings: tags / layers / tools -----------------------------------
        public const string TagList = "tag_list";
        public const string TagAdd = "tag_add";
        public const string TagRemove = "tag_remove";
        public const string LayerList = "layer_list";
        public const string LayerAdd = "layer_add";
        public const string LayerRemove = "layer_remove";
        public const string ToolSet = "tool_set";

        // --- Prefab asset --------------------------------------------------------------
        public const string PrefabCreate = "prefab_create";
        public const string PrefabCreateEmpty = "prefab_create_empty";
        public const string PrefabCreateVariant = "prefab_create_variant";
        public const string PrefabDelete = "prefab_delete";
        public const string PrefabDuplicate = "prefab_duplicate";
        public const string PrefabMove = "prefab_move";
        public const string PrefabList = "prefab_list";
        public const string PrefabInfo = "prefab_info";
        public const string PrefabTree = "prefab_tree";

        // --- Prefab instance (scene) ---------------------------------------------------
        public const string PrefabInstantiate = "prefab_instantiate";
        public const string PrefabApply = "prefab_apply";
        public const string PrefabRevert = "prefab_revert";
        public const string PrefabOverrides = "prefab_overrides";
        public const string PrefabUnpack = "prefab_unpack";
        public const string PrefabReplace = "prefab_replace";
        public const string PrefabInstanceInfo = "prefab_instance_info";

        // --- Prefab stage (Prefab Mode) ------------------------------------------------
        public const string PrefabOpen = "prefab_open";
        public const string PrefabSave = "prefab_save";
        public const string PrefabClose = "prefab_close";
        public const string PrefabStageInfo = "prefab_stage_info";

        // --- Group: several commands run as one, taken back together or one by one ------
        public const string Group = "group";

        // --- GameObject (scene, or inside a prefab asset via the `prefab` arg) ---------
        public const string GameObjectCreate = "gameobject_create";
        public const string GameObjectDelete = "gameobject_delete";
        public const string GameObjectDuplicate = "gameobject_duplicate";
        public const string GameObjectRename = "gameobject_rename";
        public const string GameObjectReparent = "gameobject_reparent";
        public const string GameObjectTransform = "gameobject_transform";
        public const string GameObjectActivate = "gameobject_activate";
        public const string GameObjectTag = "gameobject_tag";
        public const string GameObjectLayer = "gameobject_layer";
        public const string GameObjectInfo = "gameobject_info";
        public const string GameObjectTree = "gameobject_tree";
        public const string GameObjectFind = "gameobject_find";

        // --- Components ----------------------------------------------------------------
        public const string ComponentAdd = "component_add";
        public const string ComponentRemove = "component_remove";
        public const string ComponentList = "component_list";
        public const string ComponentGet = "component_get";
        public const string ComponentSet = "component_set";

        // --- Scene ---------------------------------------------------------------------
        public const string SceneOpen = "scene_open";
        public const string SceneNew = "scene_new";
        public const string SceneSave = "scene_save";
        public const string SceneSaveAs = "scene_save_as";
        public const string SceneClose = "scene_close";
        public const string SceneList = "scene_list";
        public const string SceneInfo = "scene_info";

        // --- Assets --------------------------------------------------------------------
        public const string AssetList = "asset_list";
        public const string AssetFind = "asset_find";
        public const string AssetInfo = "asset_info";
        public const string AssetCreateFolder = "asset_create_folder";
        public const string AssetDelete = "asset_delete";
        public const string AssetMove = "asset_move";
        public const string AssetDuplicate = "asset_duplicate";
        public const string AssetDependencies = "asset_dependencies";
        public const string AssetGuid = "asset_guid";

        // --- Packages (UPM) ------------------------------------------------------------
        public const string PackageList = "package_list";
        public const string PackageAdd = "package_add";
        public const string PackageRemove = "package_remove";

        // --- Editor selection ----------------------------------------------------------
        public const string SelectionGet = "selection_get";
        public const string SelectionSet = "selection_set";
        public const string SelectionClear = "selection_clear";

        // --- Play Mode UI --------------------------------------------------------------
        public const string UiSnapshot = "ui_snapshot";
        public const string UiClick = "ui_click";
        public const string UiInput = "ui_input";
        public const string UiWait = "ui_wait";
    }
}
