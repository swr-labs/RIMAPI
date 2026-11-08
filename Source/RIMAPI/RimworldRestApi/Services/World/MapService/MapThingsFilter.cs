using System;
using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Http;
using RimWorld;
using Verse;

namespace RIMAPI.Services
{
    /// <summary>
    /// Server-side filters for map/things and map/plants, applied to the game's Things before they become DTOs.
    /// A colony map lists ~1500 haulables (most of them stone chunks) and ~5 MB of plants, so callers that
    /// only need one def, one category or one area should not pay for the whole map.
    /// Query: def=A,B (defNames), category=A,B (ThingCategoryDefs; matches any), and EITHER x,z,radius (cells
    /// within the circle) OR rect=x1,z1,x2,z2 (inclusive corners). Every given filter must match.
    /// </summary>
    public class MapThingsFilter
    {
        public HashSet<string> Defs;
        public HashSet<string> Categories;
        public IntVec3? Center;
        public int Radius;
        public CellRect? Rect;

        public bool IsEmpty => Defs == null && Categories == null && Center == null && Rect == null;

        /// <summary>Parses the query; returns null with a message when a filter is malformed.</summary>
        public static MapThingsFilter Parse(RimApiContext context, out string problem)
        {
            problem = null;
            var filter = new MapThingsFilter
            {
                Defs = ParseList(RequestParser.GetStringParameter(context, "def", false)),
                Categories = ParseList(RequestParser.GetStringParameter(context, "category", false)),
            };

            bool hasX = RequestParser.HasParameter(context, "x");
            bool hasZ = RequestParser.HasParameter(context, "z");
            bool hasRadius = RequestParser.HasParameter(context, "radius");
            bool hasRect = RequestParser.HasParameter(context, "rect");
            if (hasX || hasZ || hasRadius)
            {
                if (!(hasX && hasZ && hasRadius))
                {
                    problem = "x, z and radius go together (all three or none)";
                    return null;
                }
                if (!int.TryParse(RequestParser.GetStringParameter(context, "x"), out int x) ||
                    !int.TryParse(RequestParser.GetStringParameter(context, "z"), out int z) ||
                    !int.TryParse(RequestParser.GetStringParameter(context, "radius"), out int radius) || radius < 0)
                {
                    problem = "x, z must be integers and radius a non-negative integer";
                    return null;
                }
                filter.Center = new IntVec3(x, 0, z);
                filter.Radius = radius;
            }
            if (hasRect)
            {
                if (filter.Center != null)
                {
                    problem = "use either x,z,radius or rect, not both";
                    return null;
                }
                string[] parts = RequestParser.GetStringParameter(context, "rect").Split(',');
                var n = new int[4];
                if (parts.Length != 4 || parts.Where((p, i) => !int.TryParse(p.Trim(), out n[i])).Any())
                {
                    problem = "rect must be four integers x1,z1,x2,z2";
                    return null;
                }
                filter.Rect = CellRect.FromLimits(Math.Min(n[0], n[2]), Math.Min(n[1], n[3]), Math.Max(n[0], n[2]), Math.Max(n[1], n[3]));
            }
            return filter;
        }

        public bool Keeps(Thing thing)
        {
            if (Defs != null && !Defs.Contains(thing.def.defName))
                return false;
            if (Categories != null && (thing.def.thingCategories == null || !thing.def.thingCategories.Any(InCategories)))
                return false;
            if (Center != null && thing.Position.DistanceToSquared(Center.Value) > Radius * Radius)
                return false;
            if (Rect != null && !Rect.Value.Contains(thing.Position))
                return false;
            return true;
        }

        /// <summary>
        /// A category matches itself and every ancestor: stone chunks are in StoneChunks (child of Chunks), so
        /// category=Chunks must include them (live 2026-11-08: direct-only matching found 15 of ~900 chunks).
        /// </summary>
        private bool InCategories(ThingCategoryDef category)
        {
            for (ThingCategoryDef c = category; c != null; c = c.parent)
            {
                if (Categories.Contains(c.defName))
                    return true;
            }
            return false;
        }

        private static HashSet<string> ParseList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var items = raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
            return new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
        }
    }
}
