using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;
using Verse.AI;

namespace RIMAPI.Models
{
    public class UnequipRequestDto
    {
        public int PawnId { get; set; }

        // thingIDNumber of a worn apparel piece or of the pawn's equipped weapon (see colonist/inventory).
        public int ItemId { get; set; }
    }

    public class UnequipResultDto
    {
        public int PawnId { get; set; }
        public int ItemId { get; set; }
        public string ItemDef { get; set; }
        public string Kind { get; set; }        // Apparel|Weapon
        public string Job { get; set; }         // the job the colonist was ordered to do
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyControlService
    {
        ApiResult<UnequipResultDto> Unequip(UnequipRequestDto request);
    }

    public partial class ColonyControlService
    {
        /// <summary>
        /// Orders a colonist to take off a worn piece of apparel or drop the equipped weapon, as the player's
        /// "remove"/"drop" commands do (a real job: it takes game time and leaves the item on the ground at the
        /// colonist's feet). Pair with jobs/make/equip for another colonist to swap gear.
        /// </summary>
        public ApiResult<UnequipResultDto> Unequip(UnequipRequestDto request)
        {
            if (request == null)
                return ApiResult<UnequipResultDto>.ValidationFail("pawn_id and item_id are required");
            Pawn pawn = FindColonist(request.PawnId, out string problem);
            if (pawn == null)
                return ApiResult<UnequipResultDto>.Fail(problem);
            if (pawn.Dead || pawn.Downed || pawn.jobs == null)
                return ApiResult<UnequipResultDto>.ValidationFail($"{pawn.LabelShort} cannot take orders right now (dead, downed or no job tracker)");

            Job job;
            string kind;
            Apparel apparel = pawn.apparel?.WornApparel.FirstOrDefault(a => a.thingIDNumber == request.ItemId);
            ThingWithComps weapon = pawn.equipment?.AllEquipmentListForReading.FirstOrDefault(e => e.thingIDNumber == request.ItemId);
            if (apparel != null)
            {
                job = JobMaker.MakeJob(JobDefOf.RemoveApparel, apparel);
                kind = "Apparel";
            }
            else if (weapon != null)
            {
                job = JobMaker.MakeJob(JobDefOf.DropEquipment, weapon);
                kind = "Weapon";
            }
            else
            {
                return ApiResult<UnequipResultDto>.ValidationFail(
                    $"item {request.ItemId} is not worn or equipped by {pawn.LabelShort}; ids come from colonist/inventory");
            }

            if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                return ApiResult<UnequipResultDto>.ValidationFail($"{pawn.LabelShort} did not accept the {job.def.defName} order");

            return ApiResult<UnequipResultDto>.Ok(new UnequipResultDto
            {
                PawnId = pawn.thingIDNumber,
                ItemId = request.ItemId,
                ItemDef = (apparel ?? (Thing)weapon).def.defName,
                Kind = kind,
                Job = job.def.defName,
            });
        }
    }
}
