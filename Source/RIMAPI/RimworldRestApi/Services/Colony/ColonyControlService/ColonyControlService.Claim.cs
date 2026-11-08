using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class SetClaimedRequestDto
    {
        public int MapId { get; set; }
        public List<int> ThingIds { get; set; }
        public bool Claimed { get; set; }
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyControlService
    {
        ApiResult SetClaimed(SetClaimedRequestDto request);
    }

    public partial class ColonyControlService
    {
        /// <summary>
        /// Claim (make player property) or unclaim (return to no faction) buildings, like the Claim designator.
        /// Claimed ruins get repaired and counted as wealth; unclaiming undoes that. Only buildings whose def is
        /// claimable are touched; anything else is reported, never silently skipped.
        /// </summary>
        public ApiResult SetClaimed(SetClaimedRequestDto request)
        {
            if (request?.ThingIds == null || request.ThingIds.Count == 0)
                return ApiResult.ValidationFail("thing_ids is required and must not be empty");
            if (request.ThingIds.Count > MaxBatch)
                return ApiResult.ValidationFail($"at most {MaxBatch} thing_ids per call, got {request.ThingIds.Count}");
            Map map = FindMap(request.MapId);
            if (map == null)
                return ApiResult.Fail(MapNotFound(request.MapId));

            var warnings = new List<string>();
            int changed = 0;
            foreach (int id in request.ThingIds.Distinct())
            {
                Thing thing = map.listerThings.AllThings.FirstOrDefault(t => t.thingIDNumber == id);
                if (thing == null)
                {
                    warnings.Add($"Thing {id} not found");
                    continue;
                }
                var building = thing as Building;
                if (building == null || !building.Spawned || !building.def.Claimable)
                {
                    warnings.Add($"Thing {id} ({thing.def.defName}) is not a claimable building");
                    continue;
                }

                if (request.Claimed)
                {
                    if (building.Faction == Faction.OfPlayer) { warnings.Add($"Thing {id} ({thing.def.defName}) is already claimed"); continue; }
                    if (building.Faction != null) { warnings.Add($"Thing {id} ({thing.def.defName}) belongs to {building.Faction.Name}; it cannot be claimed"); continue; }
                    building.SetFaction(Faction.OfPlayer);
                }
                else
                {
                    if (building.Faction != Faction.OfPlayer) { warnings.Add($"Thing {id} ({thing.def.defName}) is not claimed by the player"); continue; }
                    building.SetFaction(null);
                }

                bool nowClaimed = building.Faction == Faction.OfPlayer;
                if (nowClaimed != request.Claimed)
                {
                    warnings.Add($"Thing {id} ({thing.def.defName}) did not change to claimed={request.Claimed}");
                    continue;
                }
                changed++;
            }

            if (changed == 0)
                return ApiResult.Fail($"No things were updated: {string.Join("; ", warnings)}");
            return warnings.Count > 0 ? ApiResult.Partial(warnings) : ApiResult.Ok();
        }
    }
}
