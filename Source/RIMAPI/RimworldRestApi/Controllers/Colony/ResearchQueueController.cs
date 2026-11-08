using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Services;

namespace RIMAPI.Controllers
{
    public class ResearchQueueController
    {
        private readonly IResearchQueueService _queue;

        public ResearchQueueController(IResearchQueueService queue)
        {
            _queue = queue;
        }

        [Get("/api/v1/research/queue")]
        [EndpointMetadata("Read the research queue (ResearchPal Forked); 400 when no queue mod is loaded")]
        public async Task GetQueue(RimApiContext context)
        {
            await context.SendJsonResponse(_queue.GetQueue());
        }

        [Post("/api/v1/research/queue/append")]
        [EndpointMetadata("Append a project (query: name = defName) to the research queue; missing prerequisites are queued first")]
        public async Task Append(RimApiContext context)
        {
            await context.SendJsonResponse(_queue.Append(RequestParser.GetStringParameter(context, "name")));
        }

        [Post("/api/v1/research/queue/remove")]
        [EndpointMetadata("Remove a project (query: name = defName) from the research queue; queued projects that need it go too")]
        public async Task Remove(RimApiContext context)
        {
            await context.SendJsonResponse(_queue.Remove(RequestParser.GetStringParameter(context, "name")));
        }
    }
}
