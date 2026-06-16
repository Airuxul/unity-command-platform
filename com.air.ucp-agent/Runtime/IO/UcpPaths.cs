using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Air.UcpAgent.IO
{
    public static class UcpPaths
    {
        public static string ResolveRoot()
        {
            var env = Environment.GetEnvironmentVariable("UCP_ROOT");
            if (!string.IsNullOrWhiteSpace(env))
                return Path.GetFullPath(env);

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".ucp");
        }

        public static string SessionFile(string projectId, string root = null) =>
            Path.Combine(GetRoot(root), "sessions", projectId + ".json");

        public static string InboxDir(string projectId, string root = null) =>
            Path.Combine(GetRoot(root), "queues", projectId, "inbox");

        public static string OutboxDir(string projectId, string root = null) =>
            Path.Combine(GetRoot(root), "queues", projectId, "outbox");

        public static string OutboxFile(string projectId, string commandId, string root = null) =>
            Path.Combine(OutboxDir(projectId, root), commandId + ".json");

        public static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static void WriteJsonAtomic(string filePath, string json)
        {
            EnsureDirectory(Path.GetDirectoryName(filePath));
            var tmp = filePath + ".tmp." + Guid.NewGuid().ToString("N");
            // UTF-8 without BOM — Node JSON.parse rejects BOM-prefixed files.
            File.WriteAllText(tmp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.Move(tmp, filePath);
        }

        static string GetRoot(string root) => root ?? ResolveRoot();
    }

    public static class ProjectId
    {
        public static string FromPath(string projectPath)
        {
            var normalized = Path.GetFullPath(projectPath).Replace('\\', '/').ToLowerInvariant();
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return hex.Substring(0, 16);
        }
    }
}
