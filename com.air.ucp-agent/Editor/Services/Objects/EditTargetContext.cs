using System;
using Air.UcpAgent.Cli;
using Newtonsoft.Json;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// The context of a command that edits a scene or prefab. <see cref="EditorExecutionHost"/>
    /// fills it in before Execute runs; the command reads the open target from <see cref="Scope"/>.
    /// </summary>
    public class EditTargetContext : CommandContext, IEditTargetContext
    {
        /// <summary>The open target. Live only while the command runs.</summary>
        [JsonIgnore]
        public EditTargetScope Scope { get; set; }

        /// <summary>Checked once the target is open, before Execute. Set by a group step's <c>expect</c>.</summary>
        [JsonIgnore]
        public Action<EditTargetScope> Precondition { get; set; }

        /// <summary><c>scene</c>, <c>prefab_asset</c> or <c>prefab_stage</c>.</summary>
        public string TargetKind;

        /// <summary>The Unity undo group this step recorded into; -1 in Play Mode, where nothing is recorded.</summary>
        public int UndoGroup = -1;

        /// <summary>The prefab file this step may save. Set for a prefab target inside a group.</summary>
        public string PrefabPath;

        /// <summary>A copy of <see cref="PrefabPath"/> from before the group first opened it; null when this step did not open it.</summary>
        public string PrefabBackup;
    }
}
