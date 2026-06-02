using System;
using System.Collections.Generic;
using System.Linq;
using Air.UnityConnector.Params;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Air.UnityConnector.Editor.Services
{
    public static class ProfilerHierarchyService
    {
        public static Dictionary<string, object> Execute(ProfilerParams p)
        {
            var action = (p.Action ?? "hierarchy").ToLowerInvariant();
            switch (action)
            {
                case "hierarchy":
                    return Hierarchy(p);
                case "enable":
                    UnityEngine.Profiling.Profiler.enabled = true;
                    ProfilerDriver.enabled = true;
                    return new Dictionary<string, object> { ["enabled"] = true };
                case "disable":
                    ProfilerDriver.enabled = false;
                    UnityEngine.Profiling.Profiler.enabled = false;
                    return new Dictionary<string, object> { ["enabled"] = false };
                case "status":
                    var first = ProfilerDriver.firstFrameIndex;
                    var last = ProfilerDriver.lastFrameIndex;
                    return new Dictionary<string, object>
                    {
                        ["enabled"] = ProfilerDriver.enabled,
                        ["first_frame"] = first,
                        ["last_frame"] = last,
                        ["frame_count"] = last >= first ? last - first + 1 : 0,
                        ["is_playing"] = Application.isPlaying,
                    };
                case "clear":
                    ProfilerDriver.ClearAllFrames();
                    return new Dictionary<string, object> { ["cleared"] = true };
                default:
                    throw new ArgumentException(
                        $"Unknown action '{action}'. Valid: hierarchy, enable, disable, status, clear.");
            }
        }

        private static Dictionary<string, object> Hierarchy(ProfilerParams p)
        {
            if (!ProfilerDriver.enabled && ProfilerDriver.lastFrameIndex < 0)
                throw new InvalidOperationException(
                    "Profiler has no captured data. Run with action=enable or enable profiling in Editor.");

            var fromFrame = p.From ?? -1;
            var toFrame = p.To ?? -1;
            var framesCount = p.Frames ?? 0;

            if (fromFrame >= 0 || toFrame >= 0)
            {
                if (fromFrame < 0) fromFrame = ProfilerDriver.firstFrameIndex;
                if (toFrame < 0) toFrame = ProfilerDriver.lastFrameIndex;
                return AveragedHierarchy(p, fromFrame, toFrame);
            }

            if (framesCount > 1)
                return AveragedHierarchy(
                    p,
                    ProfilerDriver.lastFrameIndex - framesCount + 1,
                    ProfilerDriver.lastFrameIndex);

            var frameIndex = p.Frame ?? -1;
            if (frameIndex < 0)
                frameIndex = ProfilerDriver.lastFrameIndex;

            if (frameIndex < ProfilerDriver.firstFrameIndex || frameIndex > ProfilerDriver.lastFrameIndex)
            {
                throw new ArgumentException(
                    $"Frame {frameIndex} out of range [{ProfilerDriver.firstFrameIndex}..{ProfilerDriver.lastFrameIndex}].");
            }

            var threadIndex = p.Thread ?? 0;
            var rootName = p.Root;
            var parentId = p.Parent;
            var minTime = p.Min ?? 0f;
            var sortBy = (p.Sort ?? "total").ToLowerInvariant();
            var maxItems = p.Max ?? 30;
            if (maxItems <= 0) maxItems = 30;
            var depth = p.Depth ?? 1;
            if (depth <= 0) depth = 999;

            var sortColumn = GetSortColumn(sortBy);
            using var frameData = ProfilerDriver.GetHierarchyFrameDataView(
                frameIndex,
                threadIndex,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                sortColumn,
                false);

            if (frameData == null || !frameData.valid)
            {
                throw new InvalidOperationException(
                    $"No profiler data for frame {frameIndex}, thread {threadIndex}.");
            }

            var rootId = frameData.GetRootItemID();
            var rootChildIds = new List<int>();
            frameData.GetItemChildren(rootId, rootChildIds);

            var parent = rootId;
            var parentName = "(root)";
            if (!string.IsNullOrEmpty(rootName))
            {
                var found = FindItemByName(frameData, rootId, rootName);
                if (found < 0)
                    throw new ArgumentException($"No profiler item matching '{rootName}' found.");
                parent = found;
                parentName = frameData.GetItemName(found);
            }
            else if (parentId.HasValue)
            {
                parent = parentId.Value;
                parentName = frameData.GetItemName(parent);
            }

            var children = BuildChildren(frameData, parent, minTime, maxItems, depth);
            return new Dictionary<string, object>
            {
                ["mode"] = "frame",
                ["frame"] = frameIndex,
                ["thread_index"] = threadIndex,
                ["parent"] = parent,
                ["parent_name"] = parentName,
                ["depth"] = depth >= 999 ? 0 : depth,
                ["children"] = children,
            };
        }

        private static Dictionary<string, object> AveragedHierarchy(ProfilerParams p, int fromFrame, int toFrame)
        {
            var firstAvail = ProfilerDriver.firstFrameIndex;
            var lastAvail = ProfilerDriver.lastFrameIndex;
            fromFrame = Math.Max(fromFrame, firstAvail);
            toFrame = Math.Min(toFrame, lastAvail);
            var frameCount = toFrame - fromFrame + 1;
            if (frameCount <= 0)
            {
                throw new ArgumentException(
                    $"No frames in range [{fromFrame}..{toFrame}]. Available: [{firstAvail}..{lastAvail}].");
            }

            var threadIndex = p.Thread ?? 0;
            var rootName = p.Root;
            var minTime = p.Min ?? 0f;
            var sortBy = (p.Sort ?? "total").ToLowerInvariant();
            var maxItems = p.Max ?? 30;
            if (maxItems <= 0) maxItems = 30;
            var depth = p.Depth ?? 1;
            if (depth <= 0) depth = 999;

            var sortColumn = GetSortColumn(sortBy);
            var accumulated = new Dictionary<string, ProfilerSampleAccumulator>(StringComparer.Ordinal);

            for (var frameIndex = fromFrame; frameIndex <= toFrame; frameIndex++)
            {
                using var frameData = ProfilerDriver.GetHierarchyFrameDataView(
                    frameIndex,
                    threadIndex,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    sortColumn,
                    false);

                if (frameData == null || !frameData.valid)
                    continue;

                var rootId = frameData.GetRootItemID();
                var parentId = rootId;
                if (!string.IsNullOrEmpty(rootName))
                {
                    var found = FindItemByName(frameData, rootId, rootName);
                    if (found >= 0)
                        parentId = found;
                }

                var frameSamples = new Dictionary<string, ProfilerSampleAccumulator>(StringComparer.Ordinal);
                CollectFlat(frameData, parentId, depth, frameSamples);
                MergeFrameSamples(accumulated, frameSamples);
            }

            var items = accumulated
                .Where(kv => kv.Value.FrameCount > 0)
                .Select(kv =>
                {
                    var row = kv.Value.ToRow();
                    row.Name = kv.Key;
                    return row;
                })
                .Where(row => row.AvgTotalMs >= minTime)
                .OrderByDescending(row =>
                    sortBy == "self" ? row.AvgSelfMs : sortBy == "calls" ? row.AvgCalls : row.AvgTotalMs)
                .Take(maxItems)
                .Select(row => row.ToDictionary())
                .ToList();

            return new Dictionary<string, object>
            {
                ["mode"] = "averaged",
                ["frame_count"] = frameCount,
                ["from_frame"] = fromFrame,
                ["to_frame"] = toFrame,
                ["thread_index"] = threadIndex,
                ["root"] = string.IsNullOrEmpty(rootName) ? "(root)" : rootName,
                ["items"] = items,
            };
        }

        private static void CollectFlat(
            HierarchyFrameDataView frameData,
            int parentId,
            int remainingDepth,
            Dictionary<string, ProfilerSampleAccumulator> acc)
        {
            var childIds = new List<int>();
            frameData.GetItemChildren(parentId, childIds);

            foreach (var childId in childIds)
            {
                var name = frameData.GetItemName(childId);
                if (!acc.TryGetValue(name, out var sample))
                {
                    sample = new ProfilerSampleAccumulator();
                    acc[name] = sample;
                }

                sample.Add(
                    ReadColumnMs(frameData, childId, HierarchyFrameDataView.columnTotalTime),
                    ReadColumnMs(frameData, childId, HierarchyFrameDataView.columnSelfTime),
                    ReadColumnCalls(frameData, childId));

                if (remainingDepth > 1)
                    CollectFlat(frameData, childId, remainingDepth - 1, acc);
            }
        }

        private static void MergeFrameSamples(
            Dictionary<string, ProfilerSampleAccumulator> totals,
            Dictionary<string, ProfilerSampleAccumulator> frame)
        {
            foreach (var pair in frame)
            {
                if (!totals.TryGetValue(pair.Key, out var total))
                {
                    total = new ProfilerSampleAccumulator();
                    totals[pair.Key] = total;
                }

                total.MergeFrame(pair.Value);
            }
        }

        private static float ReadColumnMs(HierarchyFrameDataView frameData, int itemId, int column)
        {
            var value = frameData.GetItemColumnDataAsFloat(itemId, column);
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static long ReadColumnCalls(HierarchyFrameDataView frameData, int itemId)
        {
            var value = frameData.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnCalls);
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0;
            return (long)Math.Max(0, Math.Round(value));
        }

        private sealed class ProfilerSampleAccumulator
        {
            float _totalMs;
            float _selfMs;
            long _calls;
            int _frameCount;

            public int FrameCount => _frameCount;

            public void Add(float totalMs, float selfMs, long calls)
            {
                _totalMs += totalMs;
                _selfMs += selfMs;
                _calls += calls;
            }

            public void MergeFrame(ProfilerSampleAccumulator frame)
            {
                _totalMs += frame._totalMs;
                _selfMs += frame._selfMs;
                _calls += frame._calls;
                _frameCount++;
            }

            public ProfilerAveragedRow ToRow()
            {
                var frames = Math.Max(1, _frameCount);
                return new ProfilerAveragedRow
                {
                    AvgTotalMs = Math.Round(_totalMs / frames, 3),
                    AvgSelfMs = Math.Round(_selfMs / frames, 3),
                    AvgCalls = Math.Round(_calls / (double)frames, 1),
                    AppearedIn = _frameCount,
                };
            }
        }

        private struct ProfilerAveragedRow
        {
            public string Name;
            public double AvgTotalMs;
            public double AvgSelfMs;
            public double AvgCalls;
            public int AppearedIn;

            public Dictionary<string, object> ToDictionary() => new()
            {
                ["name"] = Name,
                ["avg_total_ms"] = AvgTotalMs,
                ["avg_self_ms"] = AvgSelfMs,
                ["avg_calls"] = AvgCalls,
                ["appeared_in"] = AppearedIn,
            };
        }

        private static int FindItemByName(HierarchyFrameDataView frameData, int parentId, string name)
        {
            var childIds = new List<int>();
            frameData.GetItemChildren(parentId, childIds);

            foreach (var childId in childIds)
            {
                var itemName = frameData.GetItemName(childId);
                if (itemName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return childId;

                var found = FindItemByName(frameData, childId, name);
                if (found >= 0)
                    return found;
            }

            return -1;
        }

        private static int GetSortColumn(string sortBy)
        {
            switch (sortBy)
            {
                case "self":
                    return HierarchyFrameDataView.columnSelfTime;
                case "calls":
                    return HierarchyFrameDataView.columnCalls;
                default:
                    return HierarchyFrameDataView.columnTotalTime;
            }
        }

        private static List<Dictionary<string, object>> BuildChildren(
            HierarchyFrameDataView frameData,
            int parentId,
            float minTime,
            int maxItems,
            int remainingDepth)
        {
            var childIds = new List<int>();
            frameData.GetItemChildren(parentId, childIds);

            var items = new List<Dictionary<string, object>>();
            var shown = 0;
            foreach (var childId in childIds)
            {
                var totalTime = frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnTotalTime);
                if (totalTime < minTime)
                    continue;
                if (shown >= maxItems)
                    break;
                shown++;

                var selfTime = frameData.GetItemColumnDataAsFloat(childId, HierarchyFrameDataView.columnSelfTime);
                var calls = (int)Math.Min(int.MaxValue, ReadColumnCalls(frameData, childId));

                var item = new Dictionary<string, object>
                {
                    ["item_id"] = childId,
                    ["name"] = frameData.GetItemName(childId),
                    ["total_ms"] = Math.Round(totalTime, 3),
                    ["self_ms"] = Math.Round(selfTime, 3),
                    ["calls"] = calls,
                };

                if (remainingDepth > 1)
                {
                    var sub = BuildChildren(frameData, childId, minTime, maxItems, remainingDepth - 1);
                    if (sub.Count > 0)
                        item["children"] = sub;
                }

                items.Add(item);
            }

            return items;
        }
    }
}
