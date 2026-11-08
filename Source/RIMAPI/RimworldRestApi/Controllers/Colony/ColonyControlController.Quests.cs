using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Models;

namespace RIMAPI.Controllers
{
    public partial class ColonyControlController
    {
        [Post("/api/v1/quests/action")]
        [EndpointMetadata("Quest decision (body: quest_id, action=accept|dismiss|choose_reward, acceptee_id?, choice_index?)")]
        public async Task ApplyQuestAction(RimApiContext context)
        {
            var body = await context.Request.ReadBodyAsync<QuestActionRequestDto>();
            await context.SendJsonResponse(_control.ApplyQuestAction(body));
        }
    }
}
