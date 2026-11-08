using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Models;

namespace RIMAPI.Controllers
{
    public partial class ColonyControlController
    {
        [Post("/api/v1/pawn/draft")]
        [EndpointMetadata("Draft or undraft a colonist (body: pawn_id, drafted)")]
        public async Task SetDrafted(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<DraftRequestDto>();
            await context.SendJsonResponse(_control.SetDrafted(body));
        }

        [Post("/api/v1/pawn/draft/move")]
        [EndpointMetadata("Move a drafted colonist to a cell (body: pawn_id, x, z)")]
        public async Task DraftMove(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<DraftMoveRequestDto>();
            await context.SendJsonResponse(_control.DraftMove(body));
        }

        [Post("/api/v1/pawn/draft/attack")]
        [EndpointMetadata("Order a drafted colonist to attack a hostile target (body: pawn_id, target_id)")]
        public async Task DraftAttack(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<DraftAttackRequestDto>();
            await context.SendJsonResponse(_control.DraftAttack(body));
        }
    }
}
