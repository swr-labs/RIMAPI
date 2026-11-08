using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class AreaRunDto
    {
        public int Z { get; set; }
        public int X1 { get; set; }
        public int X2 { get; set; }
    }

    public class AreaDetailDto
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }        // e.g. Area_Home, Area_Allowed, Area_NoRoof, Area_BuildRoof
        public bool IsHome { get; set; }        // the Home area (gates auto-repair/auto-haul zones)
        public bool AssignableToPawns { get; set; } // can be used as a pawn/animal allowed area
        public int CellsCount { get; set; }
        public int? MinX { get; set; }
        public int? MinZ { get; set; }
        public int? MaxX { get; set; }
        public int? MaxZ { get; set; }
        public List<AreaRunDto> Runs { get; set; }   // only with include_cells: horizontal runs of active cells
    }

    public class AreasDto
    {
        public List<AreaDetailDto> Areas { get; set; } = new List<AreaDetailDto>();
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyInsightService
    {
        ApiResult<AreasDto> GetAreas(int mapId, bool includeCells);
    }

    public partial class ColonyInsightService
    {
        // Largest number of horizontal runs one area may return; a bigger area fails instead of being cut short.
        private const int MaxAreaRuns = 5000;

        public ApiResult<AreasDto> GetAreas(int mapId, bool includeCells)
        {
            Map map = FindMap(mapId);
            if (map == null)
            {
                return ApiResult<AreasDto>.Fail(MapNotFound(mapId));
            }

            var dto = new AreasDto();
            foreach (Area area in map.areaManager.AllAreas)
            {
                var cells = area.ActiveCells.ToList();
                var entry = new AreaDetailDto
                {
                    Id = area.ID,
                    Label = area.Label,
                    Type = area.GetType().Name,
                    IsHome = area == map.areaManager.Home,
                    AssignableToPawns = area.AssignableAsAllowed(),
                    CellsCount = cells.Count,
                };
                if (cells.Count > 0)
                {
                    entry.MinX = cells.Min(c => c.x);
                    entry.MaxX = cells.Max(c => c.x);
                    entry.MinZ = cells.Min(c => c.z);
                    entry.MaxZ = cells.Max(c => c.z);
                }
                if (includeCells)
                {
                    List<AreaRunDto> runs = ToRuns(cells);
                    if (runs.Count > MaxAreaRuns)
                    {
                        return ApiResult<AreasDto>.ValidationFail(
                            $"area {area.ID} '{area.Label}' has {runs.Count} runs; include_cells is limited to {MaxAreaRuns} per area. Use the bounding box."
                        );
                    }
                    entry.Runs = runs;
                }
                dto.Areas.Add(entry);
            }
            return ApiResult<AreasDto>.Ok(dto);
        }

        private static List<AreaRunDto> ToRuns(List<IntVec3> cells)
        {
            var runs = new List<AreaRunDto>();
            foreach (var row in cells.GroupBy(c => c.z).OrderBy(g => g.Key))
            {
                int start = int.MinValue, prev = int.MinValue;
                foreach (int x in row.Select(c => c.x).OrderBy(x => x))
                {
                    if (start == int.MinValue) { start = prev = x; continue; }
                    if (x == prev + 1) { prev = x; continue; }
                    runs.Add(new AreaRunDto { Z = row.Key, X1 = start, X2 = prev });
                    start = prev = x;
                }
                runs.Add(new AreaRunDto { Z = row.Key, X1 = start, X2 = prev });
            }
            return runs;
        }
    }
}
