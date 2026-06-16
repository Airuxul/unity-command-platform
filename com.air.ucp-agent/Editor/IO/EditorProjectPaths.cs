using System.IO;
using Air.UcpAgent.IO;
using UnityEngine;

namespace Air.UcpAgent
{
  internal static class EditorProjectPaths
  {
    public static string GetProjectPath() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    public static string JobsDirectory()
    {
      var projectId = ProjectId.FromPath(GetProjectPath());
      return Path.Combine(UcpPaths.ResolveRoot(), "jobs", projectId);
    }
  }
}
