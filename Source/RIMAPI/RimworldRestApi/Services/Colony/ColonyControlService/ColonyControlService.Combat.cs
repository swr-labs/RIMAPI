using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;
using Verse.AI;

namespace RIMAPI.Models
{
    public class DraftRequestDto
    {
        public int PawnId { get; set; }
        public bool Drafted { get; set; }
    }

    public class DraftMoveRequestDto
    {
        public int PawnId { get; set; }
        public int X { get; set; }
        public int Z { get; set; }
    }

    public class DraftAttackRequestDto
    {
        public int PawnId { get; set; }

        // thingIDNumber of the target (a pawn id from map/pawns or a thing id from map/things).
        public int TargetId { get; set; }
    }

    public class DraftStateDto
    {
        public int PawnId { get; set; }
        public bool Drafted { get; set; }
        public string CurrentJob { get; set; }
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyControlService
    {
        ApiResult<DraftStateDto> SetDrafted(DraftRequestDto request);
        ApiResult<DraftStateDto> DraftMove(DraftMoveRequestDto request);
        ApiResult<DraftStateDto> DraftAttack(DraftAttackRequestDto request);
    }

    public partial class ColonyControlService
    {
        /// <summary>
        /// The Draft button. Separate from pawn/edit/status (a cheat route) because it only flips the pawn's own
        /// drafter, exactly as the player's gizmo does. Pawns that cannot fight are refused for draft; undraft
        /// is always allowed.
        /// </summary>
        public ApiResult<DraftStateDto> SetDrafted(DraftRequestDto request)
        {
            if (request == null)
                return ApiResult<DraftStateDto>.ValidationFail("pawn_id and drafted are required");
            Pawn pawn = FindColonist(request.PawnId, out string problem);
            if (pawn == null)
                return ApiResult<DraftStateDto>.Fail(problem);
            if (pawn.drafter == null)
                return ApiResult<DraftStateDto>.ValidationFail($"{pawn.LabelShort} cannot be drafted (no drafter)");
            if (request.Drafted && (pawn.Dead || pawn.Downed))
                return ApiResult<DraftStateDto>.ValidationFail($"{pawn.LabelShort} is dead or downed and cannot be drafted");
            if (request.Drafted && pawn.WorkTagIsDisabled(WorkTags.Violent))
                return ApiResult<DraftStateDto>.ValidationFail($"{pawn.LabelShort} is incapable of violence and cannot be drafted");

            pawn.drafter.Drafted = request.Drafted;
            if (pawn.drafter.Drafted != request.Drafted)
                return ApiResult<DraftStateDto>.ValidationFail($"The game refused to set {pawn.LabelShort}'s draft state to {request.Drafted}");
            return ApiResult<DraftStateDto>.Ok(DraftState(pawn));
        }

        /// <summary>The "Move here" order for a drafted colonist (the game's own Goto, player-forced).</summary>
        public ApiResult<DraftStateDto> DraftMove(DraftMoveRequestDto request)
        {
            if (request == null)
                return ApiResult<DraftStateDto>.ValidationFail("pawn_id, x and z are required");
            Pawn pawn = FindDraftedColonist(request.PawnId, out string problem);
            if (pawn == null)
                return ApiResult<DraftStateDto>.ValidationFail(problem);

            var cell = new IntVec3(request.X, 0, request.Z);
            if (!cell.InBounds(pawn.Map))
                return ApiResult<DraftStateDto>.ValidationFail($"({request.X},{request.Z}) is outside the map");
            if (!cell.Standable(pawn.Map))
                return ApiResult<DraftStateDto>.ValidationFail($"({request.X},{request.Z}) is not a standable cell");
            if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                return ApiResult<DraftStateDto>.ValidationFail($"{pawn.LabelShort} cannot reach ({request.X},{request.Z})");

            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.playerForced = true;
            if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                return ApiResult<DraftStateDto>.ValidationFail($"{pawn.LabelShort} did not accept the move order");
            return ApiResult<DraftStateDto>.Ok(DraftState(pawn));
        }

        /// <summary>
        /// The "Attack" order for a drafted colonist, built by the game's own FloatMenuUtility so melee vs ranged,
        /// reach and "no weapon" checks are the player's. Only targets hostile to the colonist are accepted: this
        /// is a defence tool, not a way to make colonists kill allies, animals or prisoners.
        /// </summary>
        public ApiResult<DraftStateDto> DraftAttack(DraftAttackRequestDto request)
        {
            if (request == null)
                return ApiResult<DraftStateDto>.ValidationFail("pawn_id and target_id are required");
            Pawn pawn = FindDraftedColonist(request.PawnId, out string problem);
            if (pawn == null)
                return ApiResult<DraftStateDto>.ValidationFail(problem);

            Thing target = FindSpawnedThing(pawn.Map, request.TargetId);
            if (target == null)
                return ApiResult<DraftStateDto>.Fail($"Target {request.TargetId} not found on {pawn.LabelShort}'s map (use thingIDNumber, i.e. the id from map/pawns)");
            if (target is Pawn targetPawn && (targetPawn.Dead || targetPawn.Downed))
                return ApiResult<DraftStateDto>.ValidationFail($"Target {request.TargetId} is dead or downed");
            if (!target.HostileTo(pawn))
                return ApiResult<DraftStateDto>.ValidationFail($"Target {request.TargetId} ({target.def.defName}) is not hostile to {pawn.LabelShort}; attack orders are limited to hostile targets");

            System.Action attack = FloatMenuUtility.GetAttackAction(pawn, target, out string failStr);
            if (attack == null)
                return ApiResult<DraftStateDto>.ValidationFail($"{pawn.LabelShort} cannot attack that target: {failStr}");
            attack();
            return ApiResult<DraftStateDto>.Ok(DraftState(pawn));
        }

        private static DraftStateDto DraftState(Pawn pawn)
        {
            return new DraftStateDto
            {
                PawnId = pawn.thingIDNumber,
                Drafted = pawn.drafter != null && pawn.drafter.Drafted,
                CurrentJob = pawn.CurJob?.def.defName,
            };
        }

        /// <summary>A colonist that is currently drafted (combat orders make no sense otherwise).</summary>
        private static Pawn FindDraftedColonist(int pawnId, out string problem)
        {
            Pawn pawn = FindColonist(pawnId, out problem);
            if (pawn == null)
                return null;
            if (pawn.drafter == null || !pawn.drafter.Drafted || pawn.Map == null || pawn.jobs == null)
            {
                problem = $"{pawn.LabelShort} is not drafted; draft them first";
                return null;
            }
            return pawn;
        }

        private static Thing FindSpawnedThing(Map map, int thingId)
        {
            Thing thing = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == thingId);
            return thing ?? map.listerThings.AllThings.FirstOrDefault(t => t.thingIDNumber == thingId && t.Spawned);
        }
    }
}
