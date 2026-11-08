using System;
using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class ManagedAnimalDto
    {
        public int PawnId { get; set; }
        public string Name { get; set; }
        public string Def { get; set; }
        public string Faction { get; set; }            // null = wild
        public bool Wild { get; set; }
        public bool Tame { get; set; }                 // belongs to the player's faction
        public int? MasterId { get; set; }
        public string MasterName { get; set; }
        public NamedIdDto AreaRestriction { get; set; }
        public List<string> Designations { get; set; } = new List<string>(); // Tame|Slaughter|ReleaseAnimalToWild|Hunt
        public int X { get; set; }
        public int Z { get; set; }
    }

    public class ManagedAnimalsDto
    {
        public List<ManagedAnimalDto> Animals { get; set; } = new List<ManagedAnimalDto>();
    }

    public class AnimalActionRequestDto
    {
        public int MapId { get; set; }
        public List<int> PawnIds { get; set; }

        // tame|cancel_tame|slaughter|cancel_slaughter|release|cancel_release
        public string Action { get; set; }
    }

    public class SetMasterRequestDto
    {
        public int AnimalId { get; set; }
        public int? MasterId { get; set; }   // null = no master
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyControlService
    {
        ApiResult<ManagedAnimalsDto> GetManagedAnimals(int mapId);
        ApiResult<ManagedAnimalsDto> ApplyAnimalAction(AnimalActionRequestDto request);
        ApiResult<ManagedAnimalDto> SetAnimalMaster(SetMasterRequestDto request);
        ApiResult<ManagedAnimalDto> SetAnimalAreaRestriction(int animalId, string areaId);
    }

    public partial class ColonyControlService
    {
        public ApiResult<ManagedAnimalsDto> GetManagedAnimals(int mapId)
        {
            Map map = FindMap(mapId);
            if (map == null)
                return ApiResult<ManagedAnimalsDto>.Fail(MapNotFound(mapId));
            var dto = new ManagedAnimalsDto();
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal))
            {
                dto.Animals.Add(Describe(map, p));
            }
            return ApiResult<ManagedAnimalsDto>.Ok(dto);
        }

        public ApiResult<ManagedAnimalsDto> ApplyAnimalAction(AnimalActionRequestDto request)
        {
            if (request?.PawnIds == null || request.PawnIds.Count == 0 || string.IsNullOrEmpty(request.Action))
                return ApiResult<ManagedAnimalsDto>.ValidationFail("pawn_ids and action are required");
            if (request.PawnIds.Count > MaxBatch)
                return ApiResult<ManagedAnimalsDto>.ValidationFail($"at most {MaxBatch} pawn_ids per call");
            Map map = FindMap(request.MapId);
            if (map == null)
                return ApiResult<ManagedAnimalsDto>.Fail(MapNotFound(request.MapId));

            var warnings = new List<string>();
            var done = new ManagedAnimalsDto();
            foreach (int id in request.PawnIds.Distinct())
            {
                Pawn animal = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == id && p.RaceProps.Animal);
                if (animal == null) { warnings.Add($"Animal {id} not found on map {request.MapId}"); continue; }
                string problem = ApplyAnimalAction(map, animal, request.Action);
                if (problem != null) { warnings.Add($"Animal {id} ({animal.LabelShort}): {problem}"); continue; }
                done.Animals.Add(Describe(map, animal));
            }

            if (done.Animals.Count == 0)
                return ApiResult<ManagedAnimalsDto>.ValidationFail($"nothing was applied: {string.Join("; ", warnings)}");
            return warnings.Count > 0 ? ApiResult<ManagedAnimalsDto>.Partial(done, warnings) : ApiResult<ManagedAnimalsDto>.Ok(done);
        }

        // Returns null on success, else why the action does not apply to this animal.
        private static string ApplyAnimalAction(Map map, Pawn animal, string action)
        {
            switch (action)
            {
                case "tame": return Designate(map, animal, DesignationDefOf.Tame, animal.Faction == null, "is not wild");
                case "cancel_tame": return Undesignate(map, animal, DesignationDefOf.Tame);
                case "slaughter": return Designate(map, animal, DesignationDefOf.Slaughter, animal.Faction == Faction.OfPlayer, "is not a tame animal of the colony");
                case "cancel_slaughter": return Undesignate(map, animal, DesignationDefOf.Slaughter);
                case "release": return Designate(map, animal, DesignationDefOf.ReleaseAnimalToWild, animal.Faction == Faction.OfPlayer, "is not a tame animal of the colony");
                case "cancel_release": return Undesignate(map, animal, DesignationDefOf.ReleaseAnimalToWild);
                default:
                    return "action must be one of tame, cancel_tame, slaughter, cancel_slaughter, release, cancel_release";
            }
        }

        private static string Designate(Map map, Pawn animal, DesignationDef def, bool allowed, string notAllowedWhy)
        {
            if (!allowed) return notAllowedWhy;
            if (map.designationManager.DesignationOn(animal, def) != null) return null; // already set: idempotent
            map.designationManager.AddDesignation(new Designation(animal, def));
            return map.designationManager.DesignationOn(animal, def) != null ? null : "the game did not add the designation";
        }

        private static string Undesignate(Map map, Pawn animal, DesignationDef def)
        {
            map.designationManager.TryRemoveDesignationOn(animal, def);
            return map.designationManager.DesignationOn(animal, def) == null ? null : "the game did not remove the designation";
        }

        public ApiResult<ManagedAnimalDto> SetAnimalMaster(SetMasterRequestDto request)
        {
            if (request == null)
                return ApiResult<ManagedAnimalDto>.ValidationFail("animal_id is required");
            Pawn animal = FindOwnAnimal(request.AnimalId, out string problem);
            if (animal == null)
                return ApiResult<ManagedAnimalDto>.Fail(problem);
            Pawn master = null;
            if (request.MasterId.HasValue)
            {
                master = FindColonist(request.MasterId.Value, out string masterProblem);
                if (master == null)
                    return ApiResult<ManagedAnimalDto>.Fail(masterProblem);
            }
            animal.playerSettings.Master = master;
            if (animal.playerSettings.Master != master)
                return ApiResult<ManagedAnimalDto>.ValidationFail("the game did not accept that master (the animal may need training first)");
            return ApiResult<ManagedAnimalDto>.Ok(Describe(animal.MapHeld, animal));
        }

        public ApiResult<ManagedAnimalDto> SetAnimalAreaRestriction(int animalId, string areaId)
        {
            Pawn animal = FindOwnAnimal(animalId, out string problem);
            if (animal == null)
                return ApiResult<ManagedAnimalDto>.Fail(problem);
            Area area = null;
            if (!string.IsNullOrEmpty(areaId) && areaId != "none")
            {
                if (!int.TryParse(areaId, out int id))
                    return ApiResult<ManagedAnimalDto>.ValidationFail("area_id must be an area id or 'none'");
                area = animal.MapHeld.areaManager.AllAreas.FirstOrDefault(a => a.ID == id);
                if (area == null)
                    return ApiResult<ManagedAnimalDto>.ValidationFail($"area {id} does not exist on the animal's map");
                if (!area.AssignableAsAllowed())
                    return ApiResult<ManagedAnimalDto>.ValidationFail($"area {id} '{area.Label}' cannot be used as an allowed area");
            }
            animal.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            if (animal.playerSettings.AreaRestrictionInPawnCurrentMap != area)
                return ApiResult<ManagedAnimalDto>.ValidationFail("the game did not accept the area restriction");
            return ApiResult<ManagedAnimalDto>.Ok(Describe(animal.MapHeld, animal));
        }

        private static Pawn FindOwnAnimal(int animalId, out string problem)
        {
            problem = null;
            foreach (Map map in Find.Maps)
            {
                Pawn p = map.mapPawns.AllPawnsSpawned.FirstOrDefault(x => x.thingIDNumber == animalId);
                if (p == null) continue;
                if (!p.RaceProps.Animal || p.Faction != Faction.OfPlayer || p.playerSettings == null)
                {
                    problem = $"Pawn {animalId} is not a tame animal of the colony";
                    return null;
                }
                return p;
            }
            problem = $"Animal {animalId} not found";
            return null;
        }

        private static ManagedAnimalDto Describe(Map map, Pawn p)
        {
            Area area = p.playerSettings?.AreaRestrictionInPawnCurrentMap;
            Pawn master = p.playerSettings?.Master;
            var dto = new ManagedAnimalDto
            {
                PawnId = p.thingIDNumber,
                Name = p.LabelShort,
                Def = p.def.defName,
                Faction = p.Faction?.Name,
                Wild = p.Faction == null,
                Tame = p.Faction == Faction.OfPlayer,
                MasterId = master?.thingIDNumber,
                MasterName = master?.LabelShort,
                AreaRestriction = area == null ? null : new NamedIdDto { Id = area.ID, Label = area.Label },
                X = p.Position.x,
                Z = p.Position.z,
            };
            foreach (Designation d in map.designationManager.AllDesignationsOn(p))
            {
                dto.Designations.Add(d.def.defName);
            }
            return dto;
        }
    }
}
