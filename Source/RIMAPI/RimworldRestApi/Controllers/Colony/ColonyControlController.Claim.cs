using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Models;

namespace RIMAPI.Controllers
{
    public partial class ColonyControlController
    {
        [Post("/api/v1/things/set-claimed")]
        [EndpointMetadata("Claim or unclaim buildings (body: map_id, thing_ids, claimed)")]
        public async Task SetClaimed(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<SetClaimedRequestDto>();
            await context.SendJsonResponse(_control.SetClaimed(body));
        }
    }
}
