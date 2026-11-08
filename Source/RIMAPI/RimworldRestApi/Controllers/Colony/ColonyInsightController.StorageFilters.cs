using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;

namespace RIMAPI.Controllers
{
    public partial class ColonyInsightController
    {
        [Get("/api/v1/map/storage/filter")]
        [EndpointMetadata("Read what a stockpile zone or storage building accepts; query: map_id, zone_id|building_id, include=auto|allowed|blocked")]
        public async Task GetStorageFilter(RimApiContext context)
        {
            int mapId = RequestParser.GetMapId(context);
            int? zoneId = RequestParser.HasParameter(context, "zone_id")
                ? RequestParser.GetIntParameter(context, "zone_id")
                : (int?)null;
            int? buildingId = RequestParser.HasParameter(context, "building_id")
                ? RequestParser.GetIntParameter(context, "building_id")
                : (int?)null;
            string include = RequestParser.GetStringParameter(context, "include", false) ?? "auto";
            await context.SendJsonResponse(_insight.GetStorageFilter(mapId, zoneId, buildingId, include));
        }
    }
}
