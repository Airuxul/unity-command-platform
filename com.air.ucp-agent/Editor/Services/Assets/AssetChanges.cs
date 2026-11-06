using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Editor.IO;
using UnityEditor;

namespace Air.UcpAgent.Editor.Services.Assets
{
    /// <summary>
    /// What a command did to asset files, for its Undo. Unity's undo does not record AssetDatabase
    /// moves, deletes or new folders, so these commands take them back themselves.
    /// </summary>
    /// <remarks>Plain JSON values only: a group writes this to disk with the step's context.</remarks>
    public sealed class AssetChanges
    {
        /// <summary>Folders this command created, outermost first.</summary>
        public List<string> CreatedFolders = new List<string>();

        /// <summary>The asset this command created; deleted by Undo.</summary>
        public string CreatedAsset;

        public string MovedFrom;
        public string MovedTo;

        /// <summary>The asset this command deleted, and the copy it took first.</summary>
        public string DeletedAsset;
        public string Backup;

        /// <summary>
        /// Runs <paramref name="body"/>, which creates <paramref name="assetPath"/> and any folder
        /// above it, and records what it created, also when it throws halfway.
        /// </summary>
        public T Creating<T>(string assetPath, Func<T> body, bool isFolder = false)
        {
            var folders = MissingFolders(isFolder ? assetPath : AssetPaths.ParentFolder(assetPath));
            var existed = AssetPaths.Exists(assetPath);
            try
            {
                return body();
            }
            finally
            {
                CreatedFolders = folders.Where(AssetDatabase.IsValidFolder).ToList();
                if (!isFolder && !existed && AssetPaths.Exists(assetPath))
                    CreatedAsset = assetPath;
            }
        }

        /// <summary>Runs <paramref name="body"/>, which moves <paramref name="from"/> to <paramref name="to"/>.</summary>
        public T Moving<T>(string from, string to, Func<T> body)
        {
            var folders = MissingFolders(AssetPaths.ParentFolder(to));
            try
            {
                return body();
            }
            finally
            {
                CreatedFolders = folders.Where(AssetDatabase.IsValidFolder).ToList();
                if (!AssetPaths.Exists(from) && AssetPaths.Exists(to))
                {
                    MovedFrom = from;
                    MovedTo = to;
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="body"/>, which deletes <paramref name="path"/>. Inside a group the file,
        /// its .meta and (for a folder) everything under it are copied to the group's folder first,
        /// so Undo can put them back with the same GUIDs.
        /// </summary>
        public T Deleting<T>(string path, CommandGroupInfo group, Func<T> body)
        {
            if (group != null)
            {
                Backup = Path.Combine(group.Directory, "deleted-" + Guid.NewGuid().ToString("N"));
                CopyAsset(AssetPaths.ToFullPath(path), Backup);
            }

            try
            {
                return body();
            }
            finally
            {
                if (!AssetPaths.Exists(path))
                    DeletedAsset = path;
            }
        }

        /// <summary>Takes back what was recorded, newest change first. Each part is skipped when it did not happen.</summary>
        public void Undo()
        {
            if (!string.IsNullOrEmpty(DeletedAsset))
                Restore();

            if (!string.IsNullOrEmpty(MovedTo) && AssetPaths.Exists(MovedTo))
            {
                var error = AssetDatabase.MoveAsset(MovedTo, MovedFrom);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException($"Could not move '{MovedTo}' back to '{MovedFrom}': {error}");
            }

            if (!string.IsNullOrEmpty(CreatedAsset) && AssetPaths.Exists(CreatedAsset) && !AssetDatabase.DeleteAsset(CreatedAsset))
                throw new InvalidOperationException($"Could not delete '{CreatedAsset}'.");

            for (var i = CreatedFolders.Count - 1; i >= 0; i--)
            {
                var folder = CreatedFolders[i];
                if (AssetDatabase.IsValidFolder(folder) && IsEmpty(folder))
                    AssetDatabase.DeleteAsset(folder);
            }
        }

        void Restore()
        {
            if (AssetPaths.Exists(DeletedAsset))
                return;
            if (string.IsNullOrEmpty(Backup) || !(File.Exists(Backup) || Directory.Exists(Backup)))
                throw new IOException($"No copy of '{DeletedAsset}' was kept, so it cannot be restored.");

            var target = AssetPaths.ToFullPath(DeletedAsset);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
            CopyAsset(Backup, target);
            AssetDatabase.ImportAsset(DeletedAsset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
        }

        /// <summary>Copies a file or folder and its .meta.</summary>
        static void CopyAsset(string source, string target)
        {
            if (Directory.Exists(source))
                CopyDirectory(source, target);
            else
                File.Copy(source, target, overwrite: true);

            if (File.Exists(source + ".meta"))
                File.Copy(source + ".meta", target + ".meta", overwrite: true);
        }

        static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            foreach (var directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }

        static bool IsEmpty(string folder)
        {
            var full = AssetPaths.ToFullPath(folder);
            return !Directory.Exists(full) || !Directory.EnumerateFileSystemEntries(full).Any();
        }

        /// <summary>The folders <see cref="AssetPaths.EnsureFolder"/> would create for <paramref name="folder"/>, outermost first.</summary>
        static List<string> MissingFolders(string folder)
        {
            var missing = new List<string>();
            var normalized = AssetPaths.Normalize(folder);
            if (string.IsNullOrEmpty(normalized))
                return missing;

            var segments = normalized.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                current += "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(current))
                    missing.Add(current);
            }

            return missing;
        }
    }

    /// <summary>The context of an asset command that can be taken back.</summary>
    public sealed class AssetFileContext : CommandContext
    {
        public AssetChanges Changes = new AssetChanges();
    }
}
