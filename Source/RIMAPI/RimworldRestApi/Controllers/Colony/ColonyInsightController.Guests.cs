using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;

namespace RIMAPI.Controllers
{
    public partial class ColonyInsightController
    {
        [Get("/api/v1/pawns/guests")]
        [EndpointMetadata("Pawns hosted by the colony but not part of it (visitors, quest lodgers, prisoners, slaves): status, mood, position, job, lord job; query: map_id, status=all|guest|prisoner|slave")]
        public async Task GetGuests(RimApiContext context)
        {
            int mapId = RequestParser.GetMapId(context);
            string status = RequestParser.GetStringParameter(context, "status", false) ?? "all";
            await context.SendJsonResponse(_insight.GetGuests(mapId, status));
        }
    }
}
