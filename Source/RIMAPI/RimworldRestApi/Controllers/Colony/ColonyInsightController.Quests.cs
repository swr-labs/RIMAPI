using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;

namespace RIMAPI.Controllers
{
    public partial class ColonyInsightController
    {
        [Get("/api/v1/quests/detail")]
        [EndpointMetadata("One quest in detail: state, offer expiry vs quest expiry (hours), reward choices, factions, parts; query: id")]
        public async Task GetQuestDetail(RimApiContext context)
        {
            int id = RequestParser.GetIntParameter(context, "id");
            await context.SendJsonResponse(_insight.GetQuestDetail(id));
        }
    }
}
