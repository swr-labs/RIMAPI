using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Models;

namespace RIMAPI.Controllers
{
    public partial class ColonyControlController
    {
        [Get("/api/v1/policies")]
        [EndpointMetadata("List outfit, food and drug policies (id + label) and the valid hostility-response and medical-care values")]
        public async Task GetPolicies(RimApiContext context)
        {
            await context.SendJsonResponse(_control.GetPolicies());
        }

        [Get("/api/v1/pawn/control-settings")]
        [EndpointMetadata("A colonist's hostility response, medical care, area restriction and outfit/food/drug policy; query: id")]
        public async Task GetPawnSettings(RimApiContext context)
        {
            int id = RequestParser.GetIntParameter(context, "id");
            await context.SendJsonResponse(_control.GetPawnSettings(id));
        }

        [Post("/api/v1/pawn/control-settings")]
        [EndpointMetadata("Set one of a colonist's settings (body: pawn_id, setting=outfit|food|drug|hostility|medical_care|area, value); returns the settings read back")]
        public async Task SetPawnSetting(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<SetPawnSettingRequestDto>();
            await context.SendJsonResponse(_control.SetPawnSetting(body));
        }
    }
}
