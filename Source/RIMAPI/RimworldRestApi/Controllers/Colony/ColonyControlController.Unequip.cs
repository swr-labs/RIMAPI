using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Models;

namespace RIMAPI.Controllers
{
    public partial class ColonyControlController
    {
        [Post("/api/v1/pawn/unequip")]
        [EndpointMetadata("Order a colonist to take off a worn apparel piece or drop the equipped weapon (body: pawn_id, item_id)")]
        public async Task Unequip(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<UnequipRequestDto>();
            await context.SendJsonResponse(_control.Unequip(body));
        }
    }
}
