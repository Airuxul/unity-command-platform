namespace UnityCliConnector
{
    /// <summary>
    /// Where a command may run: Editor host (edit mode), editor_play / player hosts, or both on Editor.
    /// </summary>
    public enum CommandScope
    {
        /// <summary>Editor host, not in Play Mode.</summary>
        Editor,
        /// <summary>Editor host in Play Mode, or debug Player host.</summary>
        Runtime,
        /// <summary>Editor host in both edit and Play Mode.</summary>
        Any,
    }
}
