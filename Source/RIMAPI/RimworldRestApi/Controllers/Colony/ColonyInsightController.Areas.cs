using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;

namespace RIMAPI.Controllers
{
    public partial class ColonyInsightController
    {
        [Get("/api/v1/map/areas")]
        [EndpointMetadata("List the map's areas (Home, allowed areas, roof areas) with ids, cell counts, bounding box; query: map_id, include_cells=true for horizontal runs")]
        public async Task GetAreas(RimApiContext context)
        {
            int mapId = RequestParser.GetMapId(context);
            bool includeCells = RequestParser.GetBooleanParameter(context, "include_cells", false);
            await context.SendJsonResponse(_insight.GetAreas(mapId, includeCells));
        }
    }
}
