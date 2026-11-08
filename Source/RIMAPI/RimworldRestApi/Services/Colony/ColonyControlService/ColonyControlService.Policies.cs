using System;
using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class NamedIdDto
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    public class PawnControlSettingsDto
    {
        public int PawnId { get; set; }
        public string Name { get; set; }
        public string HostilityResponse { get; set; }   // Ignore|Attack|Flee
        public string MedicalCare { get; set; }         // NoCare|NoMeds|HerbalOrWorse|NormalOrWorse|Best
        public NamedIdDto AreaRestriction { get; set; } // null = unrestricted
        public NamedIdDto Outfit { get; set; }
        public NamedIdDto Food { get; set; }
        public NamedIdDto Drug { get; set; }
    }

    public class PoliciesDto
    {
        public List<NamedIdDto> Outfits { get; set; } = new List<NamedIdDto>();
        public List<NamedIdDto> Food { get; set; } = new List<NamedIdDto>();
        public List<NamedIdDto> Drugs { get; set; } = new List<NamedIdDto>();
        public List<string> HostilityResponses { get; set; } = new List<string>();
        public List<string> MedicalCareLevels { get; set; } = new List<string>();
    }

    public class SetPawnSettingRequestDto
    {
        public int PawnId { get; set; }

        // outfit|food|drug (value = policy id), hostility (value = Ignore|Attack|Flee),
        // medical_care (value = MedicalCareCategory name), area (value = area id or null to remove the restriction)
        public string Setting { get; set; }
        public string Value { get; set; }
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyControlService
    {
        ApiResult<PoliciesDto> GetPolicies();
        ApiResult<PawnControlSettingsDto> GetPawnSettings(int pawnId);
        ApiResult<PawnControlSettingsDto> SetPawnSetting(SetPawnSettingRequestDto request);
    }

    public partial class ColonyControlService
    {
        public ApiResult<PoliciesDto> GetPolicies()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return ApiResult<PoliciesDto>.Fail("Policies are unavailable: no game is being played.");
            var dto = new PoliciesDto
            {
                Outfits = Current.Game.outfitDatabase.AllOutfits.Select(o => new NamedIdDto { Id = o.id, Label = o.label }).ToList(),
                Food = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.Select(f => new NamedIdDto { Id = f.id, Label = f.label }).ToList(),
                Drugs = Current.Game.drugPolicyDatabase.AllPolicies.Select(d => new NamedIdDto { Id = d.id, Label = d.label }).ToList(),
                HostilityResponses = Enum.GetNames(typeof(HostilityResponseMode)).ToList(),
                MedicalCareLevels = Enum.GetNames(typeof(MedicalCareCategory)).ToList(),
            };
            return ApiResult<PoliciesDto>.Ok(dto);
        }

        public ApiResult<PawnControlSettingsDto> GetPawnSettings(int pawnId)
        {
            Pawn pawn = FindColonist(pawnId, out string problem);
            if (pawn == null)
                return ApiResult<PawnControlSettingsDto>.Fail(problem);
            return ApiResult<PawnControlSettingsDto>.Ok(Describe(pawn));
        }

        public ApiResult<PawnControlSettingsDto> SetPawnSetting(SetPawnSettingRequestDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Setting))
                return ApiResult<PawnControlSettingsDto>.ValidationFail("pawn_id, setting and value are required");
            Pawn pawn = FindColonist(request.PawnId, out string problem);
            if (pawn == null)
                return ApiResult<PawnControlSettingsDto>.Fail(problem);

            string error;
            switch (request.Setting)
            {
                case "outfit": error = SetOutfit(pawn, request.Value); break;
                case "food": error = SetFood(pawn, request.Value); break;
                case "drug": error = SetDrug(pawn, request.Value); break;
                case "hostility": error = SetHostility(pawn, request.Value); break;
                case "medical_care": error = SetMedicalCare(pawn, request.Value); break;
                case "area": error = SetArea(pawn, request.Value); break;
                default:
                    error = "setting must be one of outfit, food, drug, hostility, medical_care, area";
                    break;
            }
            if (error != null)
                return ApiResult<PawnControlSettingsDto>.ValidationFail(error);
            return ApiResult<PawnControlSettingsDto>.Ok(Describe(pawn));
        }

        private static string SetOutfit(Pawn pawn, string value)
        {
            if (pawn.outfits == null) return $"{pawn.LabelShort} has no outfit settings";
            if (!int.TryParse(value, out int id)) return "value must be an outfit policy id (see GET policies)";
            var policy = Current.Game.outfitDatabase.AllOutfits.FirstOrDefault(o => o.id == id);
            if (policy == null) return $"outfit policy {id} does not exist";
            pawn.outfits.CurrentApparelPolicy = policy;
            return pawn.outfits.CurrentApparelPolicy == policy ? null : "the game did not accept the outfit policy";
        }

        private static string SetFood(Pawn pawn, string value)
        {
            if (pawn.foodRestriction == null) return $"{pawn.LabelShort} has no food policy";
            if (!int.TryParse(value, out int id)) return "value must be a food policy id (see GET policies)";
            var policy = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.FirstOrDefault(f => f.id == id);
            if (policy == null) return $"food policy {id} does not exist";
            pawn.foodRestriction.CurrentFoodPolicy = policy;
            return pawn.foodRestriction.CurrentFoodPolicy == policy ? null : "the game did not accept the food policy";
        }

        private static string SetDrug(Pawn pawn, string value)
        {
            if (pawn.drugs == null) return $"{pawn.LabelShort} has no drug policy";
            if (!int.TryParse(value, out int id)) return "value must be a drug policy id (see GET policies)";
            var policy = Current.Game.drugPolicyDatabase.AllPolicies.FirstOrDefault(d => d.id == id);
            if (policy == null) return $"drug policy {id} does not exist";
            pawn.drugs.CurrentPolicy = policy;
            return pawn.drugs.CurrentPolicy == policy ? null : "the game did not accept the drug policy";
        }

        private static string SetHostility(Pawn pawn, string value)
        {
            if (pawn.playerSettings == null) return $"{pawn.LabelShort} has no player settings";
            if (!Enum.TryParse(value, false, out HostilityResponseMode mode) || !Enum.IsDefined(typeof(HostilityResponseMode), mode))
                return $"hostility must be one of {string.Join(", ", Enum.GetNames(typeof(HostilityResponseMode)))}";
            pawn.playerSettings.hostilityResponse = mode;
            return pawn.playerSettings.hostilityResponse == mode ? null : "the game did not accept the hostility response";
        }

        private static string SetMedicalCare(Pawn pawn, string value)
        {
            if (pawn.playerSettings == null) return $"{pawn.LabelShort} has no player settings";
            if (!Enum.TryParse(value, false, out MedicalCareCategory level) || !Enum.IsDefined(typeof(MedicalCareCategory), level))
                return $"medical_care must be one of {string.Join(", ", Enum.GetNames(typeof(MedicalCareCategory)))}";
            pawn.playerSettings.medCare = level;
            return pawn.playerSettings.medCare == level ? null : "the game did not accept the medical care level";
        }

        private static string SetArea(Pawn pawn, string value)
        {
            if (pawn.playerSettings == null) return $"{pawn.LabelShort} has no player settings";
            Area area = null;
            if (!string.IsNullOrEmpty(value) && value != "none")
            {
                if (!int.TryParse(value, out int id)) return "value must be an area id (see GET map/areas) or 'none'";
                area = pawn.MapHeld?.areaManager.AllAreas.FirstOrDefault(a => a.ID == id);
                if (area == null) return $"area {id} does not exist on the pawn's map";
                if (!area.AssignableAsAllowed()) return $"area {id} '{area.Label}' cannot be used as an allowed area";
            }
            pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            return pawn.playerSettings.AreaRestrictionInPawnCurrentMap == area ? null : "the game did not accept the area restriction";
        }

        private static PawnControlSettingsDto Describe(Pawn pawn)
        {
            Area area = pawn.playerSettings?.AreaRestrictionInPawnCurrentMap;
            var outfit = pawn.outfits?.CurrentApparelPolicy;
            var food = pawn.foodRestriction?.CurrentFoodPolicy;
            var drug = pawn.drugs?.CurrentPolicy;
            return new PawnControlSettingsDto
            {
                PawnId = pawn.thingIDNumber,
                Name = pawn.LabelShort,
                HostilityResponse = pawn.playerSettings?.hostilityResponse.ToString(),
                MedicalCare = pawn.playerSettings?.medCare.ToString(),
                AreaRestriction = area == null ? null : new NamedIdDto { Id = area.ID, Label = area.Label },
                Outfit = outfit == null ? null : new NamedIdDto { Id = outfit.id, Label = outfit.label },
                Food = food == null ? null : new NamedIdDto { Id = food.id, Label = food.label },
                Drug = drug == null ? null : new NamedIdDto { Id = drug.id, Label = drug.label },
            };
        }
    }
}
