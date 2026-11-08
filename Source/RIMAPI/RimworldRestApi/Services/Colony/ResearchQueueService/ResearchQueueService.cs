using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class ResearchQueueEntryDto
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public int Progress { get; set; }
        public int Cost { get; set; }
    }

    public class ResearchQueueDto
    {
        public string Provider { get; set; }            // the mod whose queue this is
        public string ActiveProject { get; set; }        // vanilla ResearchManager's current project (null = idle)
        public List<ResearchQueueEntryDto> Queue { get; set; }
        public List<string> Added { get; set; }          // append: projects that entered the queue (prerequisites included)
        public List<string> Removed { get; set; }        // remove: projects that left it (dependents included)
    }
}

namespace RIMAPI.Services
{
    public interface IResearchQueueService
    {
        ApiResult<ResearchQueueDto> GetQueue();
        ApiResult<ResearchQueueDto> Append(string name);
        ApiResult<ResearchQueueDto> Remove(string name);
    }

    /// <summary>
    /// Research queue. Vanilla RimWorld 1.6 has no queue (one active project only), so this bridges to ResearchPal
    /// Forked's own queue by reflection: the same list its research tab shows, persisted in the save, and the mod
    /// starts the next project itself when one finishes. Without ResearchPal, or if its internals changed, every
    /// call fails with a message naming what is missing; nothing is faked.
    /// </summary>
    public class ResearchQueueService : IResearchQueueService
    {
        private const string Provider = "ResearchPal";
        private const string QueueTypeName = "ResearchPal.Queue";
        private const string TreeTypeName = "ResearchPal.Tree";
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public ApiResult<ResearchQueueDto> GetQueue()
        {
            if (!TryBridge(out Bridge bridge, out string problem))
                return ApiResult<ResearchQueueDto>.ValidationFail(problem);
            return ApiResult<ResearchQueueDto>.Ok(Snapshot(bridge));
        }

        public ApiResult<ResearchQueueDto> Append(string name)
        {
            if (!TryBridge(out Bridge bridge, out string problem))
                return ApiResult<ResearchQueueDto>.ValidationFail(problem);
            ResearchProjectDef def = FindProject(name, out problem);
            if (def == null)
                return ApiResult<ResearchQueueDto>.ValidationFail(problem);
            if (def.IsFinished)
                return ApiResult<ResearchQueueDto>.ValidationFail($"{def.defName} is already finished");

            List<ResearchProjectDef> before = bridge.QueuedDefs();
            if (before.Contains(def))
                return ApiResult<ResearchQueueDto>.ValidationFail($"{def.defName} is already queued");

            // With an empty queue ResearchPal makes the queue head the active project and stops anything else,
            // so a project the player set outside the queue is queued first instead of being silently dropped.
            ResearchProjectDef active = Find.ResearchManager?.GetProject();
            if (before.Count == 0 && active != null && active != def && !active.IsFinished)
            {
                object activeNode = bridge.NodeFor(active);
                if (activeNode != null)
                    bridge.Call("AppendS", activeNode);
            }

            object node = bridge.NodeFor(def);
            if (node == null)
                return ApiResult<ResearchQueueDto>.ValidationFail($"ResearchPal has no tree node for {def.defName} (hidden or locked project)");
            if (!(bridge.Call("AppendS", node) is bool appended) || !appended)
                return ApiResult<ResearchQueueDto>.ValidationFail($"ResearchPal refused to queue {def.defName} (not researchable now: locked by tech, bench or a requirement)");

            ResearchQueueDto result = Snapshot(bridge);
            result.Added = bridge.QueuedDefs().Except(before).Select(d => d.defName).ToList();
            return ApiResult<ResearchQueueDto>.Ok(result);
        }

        public ApiResult<ResearchQueueDto> Remove(string name)
        {
            if (!TryBridge(out Bridge bridge, out string problem))
                return ApiResult<ResearchQueueDto>.ValidationFail(problem);
            ResearchProjectDef def = FindProject(name, out problem);
            if (def == null)
                return ApiResult<ResearchQueueDto>.ValidationFail(problem);
            List<ResearchProjectDef> before = bridge.QueuedDefs();
            if (!before.Contains(def))
                return ApiResult<ResearchQueueDto>.ValidationFail($"{def.defName} is not queued");

            bridge.Call("RemoveS", bridge.NodeFor(def));
            ResearchQueueDto result = Snapshot(bridge);
            result.Removed = before.Except(bridge.QueuedDefs()).Select(d => d.defName).ToList();
            return ApiResult<ResearchQueueDto>.Ok(result);
        }

        private static ResearchQueueDto Snapshot(Bridge bridge)
        {
            var manager = Find.ResearchManager;
            return new ResearchQueueDto
            {
                Provider = Provider,
                ActiveProject = manager?.GetProject()?.defName,
                Queue = bridge.QueuedDefs().Select(d => new ResearchQueueEntryDto
                {
                    Name = d.defName,
                    Label = d.label,
                    Progress = (int)(manager?.GetProgress(d) ?? 0f),
                    Cost = (int)d.CostApparent,
                }).ToList(),
            };
        }

        private static ResearchProjectDef FindProject(string name, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                problem = "name (a ResearchProjectDef defName) is required";
                return null;
            }
            ResearchProjectDef def = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name.Trim());
            if (def == null)
                problem = $"Unknown research project '{name}' (use the defName, e.g. Electricity)";
            return def;
        }

        private static bool TryBridge(out Bridge bridge, out string problem)
        {
            bridge = null;
            Type queueType = GenTypes.GetTypeInAnyAssembly(QueueTypeName);
            Type treeType = GenTypes.GetTypeInAnyAssembly(TreeTypeName);
            if (queueType == null || treeType == null)
            {
                problem = "No research queue: ResearchPal (Forked) is not loaded, and vanilla RimWorld 1.6 has only one active project (use research/target).";
                return false;
            }
            var b = new Bridge
            {
                Instance = queueType.GetField("_instance", AnyStatic)?.GetValue(null),
                QueueField = queueType.GetField("_queue", AnyInstance),
                QueueType = queueType,
                ResearchNodes = treeType.GetMethod("ResearchNodes", AnyStatic, null, Type.EmptyTypes, null),
            };
            string missing = b.Instance == null ? "Queue._instance (no game loaded?)"
                : b.QueueField == null ? "Queue._queue"
                : b.ResearchNodes == null ? "Tree.ResearchNodes()"
                : queueType.GetMethod("AppendS", AnyStatic) == null ? "Queue.AppendS"
                : queueType.GetMethod("RemoveS", AnyStatic) == null ? "Queue.RemoveS"
                : null;
            if (missing != null)
            {
                problem = $"ResearchPal is loaded but its queue cannot be bridged: {missing} not found (mod version changed?)";
                return false;
            }
            problem = null;
            bridge = b;
            return true;
        }

        private class Bridge
        {
            public object Instance;
            public FieldInfo QueueField;
            public Type QueueType;
            public MethodInfo ResearchNodes;

            public List<ResearchProjectDef> QueuedDefs()
            {
                var list = (IEnumerable)QueueField.GetValue(Instance);
                return list.Cast<object>().Select(ProjectOf).Where(d => d != null).ToList();
            }

            public object NodeFor(ResearchProjectDef def)
            {
                var nodes = (IEnumerable)ResearchNodes.Invoke(null, null);
                return nodes?.Cast<object>().FirstOrDefault(n => ProjectOf(n) == def);
            }

            public object Call(string method, object node)
            {
                return QueueType.GetMethod(method, AnyStatic).Invoke(null, new[] { node });
            }

            private static ResearchProjectDef ProjectOf(object node)
            {
                return node?.GetType().GetField("Research", AnyInstance)?.GetValue(node) as ResearchProjectDef;
            }
        }
    }
}
