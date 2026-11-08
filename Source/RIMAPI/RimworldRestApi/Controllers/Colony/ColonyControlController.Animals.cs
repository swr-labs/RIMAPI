using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Models;

namespace RIMAPI.Controllers
{
    public partial class ColonyControlController
    {
        [Get("/api/v1/map/animals/managed")]
        [EndpointMetadata("Animals with ownership: wild or tame, master, area restriction, designations (Tame/Slaughter/Release/Hunt); query: map_id")]
        public async Task GetManagedAnimals(RimApiContext context)
        {
            int mapId = RequestParser.GetMapId(context);
            await context.SendJsonResponse(_control.GetManagedAnimals(mapId));
        }

        [Post("/api/v1/map/animals/action")]
        [EndpointMetadata("Designate animals (body: map_id, pawn_ids, action=tame|cancel_tame|slaughter|cancel_slaughter|release|cancel_release)")]
        public async Task ApplyAnimalAction(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<AnimalActionRequestDto>();
            await context.SendJsonResponse(_control.ApplyAnimalAction(body));
        }

        [Post("/api/v1/animal/master")]
        [EndpointMetadata("Set or clear a tame animal's master (body: animal_id, master_id|null)")]
        public async Task SetAnimalMaster(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<SetMasterRequestDto>();
            await context.SendJsonResponse(_control.SetAnimalMaster(body));
        }

        [Post("/api/v1/animal/area-restriction")]
        [EndpointMetadata("Restrict a tame animal to an allowed area or lift it (query: animal_id, area_id|none)")]
        public async Task SetAnimalAreaRestriction(RimApiContext context)
        {
            int animalId = RequestParser.GetIntParameter(context, "animal_id");
            string areaId = RequestParser.GetStringParameter(context, "area_id");
            await context.SendJsonResponse(_control.SetAnimalAreaRestriction(animalId, areaId));
        }
    }
}
