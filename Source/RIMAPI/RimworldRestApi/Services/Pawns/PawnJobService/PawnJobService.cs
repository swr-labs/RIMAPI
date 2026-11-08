using System;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Helpers;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Services
{
    public class PawnJobService : IPawnJobService
    {
        public PawnJobService() { }

        public ApiResult AssignJob(PawnJobRequestDto request)
        {
            try
            {
                Pawn pawn = PawnHelper.FindPawnById(request.PawnId);
                if (pawn == null)
                {
                    return ApiResult.Fail($"Pawn not found: {request.PawnId}");
                }

                JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(request.JobDef);
                if (jobDef == null)
                {
                    return ApiResult.Fail($"JobDef not found: {request.JobDef}");
                }

                LocalTargetInfo target = LocalTargetInfo.Invalid;

                LocalTargetInfo secondTarget = LocalTargetInfo.Invalid;
                if (request.TargetThingId.HasValue)
                {
                    // target_thing_id is the thingIDNumber (the "id" map/pawns and map/things list). Pawns are
                    // not in listerThings.AllThings, so they are looked up through mapPawns first.
                    Thing thing = null;
                    foreach (Map map in Find.Maps)
                    {
                        thing = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == request.TargetThingId.Value)
                            ?? map.listerThings.AllThings.FirstOrDefault(t => t.thingIDNumber == request.TargetThingId.Value);
                        if (thing != null) break;
                    }
                    if (thing == null)
                    {
                        return ApiResult.Fail($"Target thing not found: {request.TargetThingId}");
                    }
                    target = thing;

                    // Capture and Rescue carry the victim in targetA and the bed to carry them to in targetB;
                    // without that bed the driver dereferences null (the old HTTP 500).
                    if (jobDef == JobDefOf.Capture || jobDef == JobDefOf.Rescue)
                    {
                        var victim = thing as Pawn;
                        if (victim == null)
                        {
                            return ApiResult.Fail($"{request.JobDef} needs a pawn as target_thing_id, got {thing.def.defName} {request.TargetThingId}");
                        }
                        bool capture = jobDef == JobDefOf.Capture;
                        Building_Bed bed = RestUtility.FindBedFor(victim, pawn, false, false,
                            capture ? GuestStatus.Prisoner : (GuestStatus?)null);
                        if (bed == null)
                        {
                            return ApiResult.Fail($"No {(capture ? "prison" : "free")} bed that {pawn.LabelShort} can carry {victim.LabelShort} to; build one first");
                        }
                        secondTarget = bed;
                    }
                }
                else if (request.TargetPosition != null)
                {
                    target = new IntVec3(
                        request.TargetPosition.X, 0, request.TargetPosition.Z);
                }

                bool success = PawnHelper.AssignJob(pawn, jobDef, target, request.Queue, secondTarget);
                if (!success)
                {
                    return ApiResult.Fail($"Pawn {request.PawnId} could not accept job {request.JobDef}");
                }
                return ApiResult.Ok();
            }
            catch (Exception ex)
            {
                return ApiResult.Fail(ex.Message);
            }
        }

        public ApiResult AssignTendJob(MedicalTendRequestDto request)
        {
            try
            {
                Pawn patient = PawnHelper.FindPawnById(request.PatientPawnId);
                if (patient == null)
                {
                    return ApiResult.Fail($"Patient pawn not found: {request.PatientPawnId}");
                }

                Pawn doctor;
                if (request.DoctorPawnId.HasValue)
                {
                    doctor = PawnHelper.FindPawnById(request.DoctorPawnId.Value);
                    if (doctor == null)
                    {
                        return ApiResult.Fail($"Doctor pawn not found: {request.DoctorPawnId}");
                    }
                }
                else
                {
                    // Find the best available doctor on the same map
                    doctor = patient.Map?.mapPawns.FreeColonists
                        .Where(p => p != patient && !p.Downed && !p.Dead)
                        .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
                        .FirstOrDefault();

                    if (doctor == null)
                    {
                        return ApiResult.Fail("No available doctor found on the map");
                    }
                }

                bool success = PawnHelper.AssignTendJob(doctor, patient);
                if (!success)
                {
                    return ApiResult.Fail("Doctor could not accept tend job");
                }
                return ApiResult.Ok();
            }
            catch (Exception ex)
            {
                return ApiResult.Fail(ex.Message);
            }
        }

        public ApiResult AssignBedRest(MedicalBedRestRequestDto request)
        {
            try
            {
                Pawn patient = PawnHelper.FindPawnById(request.PatientPawnId);
                if (patient == null)
                {
                    return ApiResult.Fail($"Patient pawn not found: {request.PatientPawnId}");
                }

                Building_Bed bed = null;
                if (request.BedBuildingId.HasValue)
                {
                    Building building = BuildingHelper.FindBuildingByID(request.BedBuildingId.Value);
                    bed = building as Building_Bed;
                    if (bed == null)
                    {
                        return ApiResult.Fail($"Bed not found: {request.BedBuildingId}");
                    }
                }

                bool success = PawnHelper.AssignBedRest(patient, bed);
                if (!success)
                {
                    return ApiResult.Fail("Patient could not be assigned to bed rest");
                }
                return ApiResult.Ok();
            }
            catch (Exception ex)
            {
                return ApiResult.Fail(ex.Message);
            }
        }
    }
}
