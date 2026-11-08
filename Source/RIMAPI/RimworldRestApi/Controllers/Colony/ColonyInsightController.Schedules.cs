using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;

namespace RIMAPI.Controllers
{
    public partial class ColonyInsightController
    {
        [Get("/api/v1/colonists/schedules")]
        [EndpointMetadata("Read colonists' schedules (24 hourly time assignments each); query: pawn_id (optional)")]
        public async Task GetSchedules(RimApiContext context)
        {
            int? pawnId = RequestParser.HasParameter(context, "pawn_id")
                ? RequestParser.GetIntParameter(context, "pawn_id")
                : (int?)null;
            await context.SendJsonResponse(_insight.GetSchedules(pawnId));
        }
    }
}
