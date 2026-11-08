using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class StorageFilterDto
    {
        public string Kind { get; set; }          // "stockpile_zone" | "storage_building"
        public int Id { get; set; }
        public string Label { get; set; }
        public string Priority { get; set; }      // StoragePriority: Unstored|Low|Normal|Preferred|Important|Critical
        public int AllowedCount { get; set; }     // storable defs this storage accepts
        public int StorableCount { get; set; }    // all defs that could ever be stored
        public string Listed { get; set; }        // which list DefNames holds: "allowed" or "blocked"
        public List<string> DefNames { get; set; }
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyInsightService
    {
        ApiResult<StorageFilterDto> GetStorageFilter(int mapId, int? zoneId, int? buildingId, string include);
    }

    public partial class ColonyInsightService
    {
        public ApiResult<StorageFilterDto> GetStorageFilter(int mapId, int? zoneId, int? buildingId, string include)
        {
            if (zoneId.HasValue == buildingId.HasValue)
            {
                return ApiResult<StorageFilterDto>.ValidationFail("pass exactly one of zone_id or building_id");
            }
            if (include != "auto" && include != "allowed" && include != "blocked")
            {
                return ApiResult<StorageFilterDto>.ValidationFail("include must be auto, allowed or blocked");
            }
            Map map = FindMap(mapId);
            if (map == null)
            {
                return ApiResult<StorageFilterDto>.Fail(MapNotFound(mapId));
            }

            IStoreSettingsParent parent;
            string kind;
            int id;
            string label;
            if (zoneId.HasValue)
            {
                var zone = map.zoneManager.AllZones.OfType<Zone_Stockpile>().FirstOrDefault(z => z.ID == zoneId.Value);
                if (zone == null)
                {
                    return ApiResult<StorageFilterDto>.Fail($"Stockpile zone {zoneId.Value} not found");
                }
                parent = zone; kind = "stockpile_zone"; id = zone.ID; label = zone.label;
            }
            else
            {
                var building = map.listerBuildings.allBuildingsColonist
                    .OfType<IStoreSettingsParent>()
                    .FirstOrDefault(b => b is Thing t && t.thingIDNumber == buildingId.Value);
                if (building == null)
                {
                    return ApiResult<StorageFilterDto>.Fail($"Storage building {buildingId.Value} not found");
                }
                parent = building; kind = "storage_building"; id = ((Thing)building).thingIDNumber;
                label = ((Thing)building).LabelCap;
            }

            StorageSettings settings = parent.GetStoreSettings();
            if (settings == null)
            {
                return ApiResult<StorageFilterDto>.Fail($"{kind} {id} has no storage settings");
            }

            var storable = DefDatabase<ThingDef>.AllDefs.Where(d => d.EverStorable(false)).ToList();
            var allowed = storable.Where(d => settings.AllowedToAccept(d)).Select(d => d.defName).ToList();
            var blocked = storable.Where(d => !settings.AllowedToAccept(d)).Select(d => d.defName).ToList();
            bool listAllowed = include == "allowed" || (include == "auto" && allowed.Count <= blocked.Count);

            return ApiResult<StorageFilterDto>.Ok(new StorageFilterDto
            {
                Kind = kind,
                Id = id,
                Label = label,
                Priority = settings.Priority.ToString(),
                AllowedCount = allowed.Count,
                StorableCount = storable.Count,
                Listed = listAllowed ? "allowed" : "blocked",
                DefNames = (listAllowed ? allowed : blocked).OrderBy(n => n).ToList(),
            });
        }
    }
}
