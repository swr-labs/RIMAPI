using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;

namespace RIMAPI.Controllers
{
    public partial class ColonyInsightController
    {
        [Get("/api/v1/map/room-at")]
        [EndpointMetadata("Room status of one cell: role, enclosed, temperature, open-roof cells, impressiveness/beauty/cleanliness/space; query: map_id, x, z")]
        public async Task GetRoomAt(RimApiContext context)
        {
            int mapId = RequestParser.GetMapId(context);
            int x = RequestParser.GetIntParameter(context, "x");
            int z = RequestParser.GetIntParameter(context, "z");
            await context.SendJsonResponse(_insight.GetRoomAt(mapId, x, z));
        }

        [Get("/api/v1/map/roofs")]
        [EndpointMetadata("Roof grid of a rectangle (<= 40000 cells): '.' none, 't' constructed, 'R' mountain; plus no-roof/build-roof area counts; query: map_id, x1, z1, x2, z2")]
        public async Task GetRoofs(RimApiContext context)
        {
            int mapId = RequestParser.GetMapId(context);
            int x1 = RequestParser.GetIntParameter(context, "x1");
            int z1 = RequestParser.GetIntParameter(context, "z1");
            int x2 = RequestParser.GetIntParameter(context, "x2");
            int z2 = RequestParser.GetIntParameter(context, "z2");
            await context.SendJsonResponse(_insight.GetRoofs(mapId, x1, z1, x2, z2));
        }
    }
}
