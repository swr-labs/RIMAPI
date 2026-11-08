using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;
using RIMAPI.Services;

namespace RIMAPI.Controllers
{
    public class GameOptionsController
    {
        private readonly IGameOptionsService _gameOptionsService;

        public GameOptionsController(IGameOptionsService gameOptionsService)
        {
            _gameOptionsService = gameOptionsService;
        }

        // Deliberately uncached: callers read it right after toggling it.
        [Get("/api/v1/game/settings/manual-priorities")]
        [EndpointMetadata("Read the 'Manual priorities' game option (work priorities 1-4 only apply while it is on)")]
        public async Task GetManualPriorities(RimApiContext context)
        {
            var result = _gameOptionsService.GetManualPriorities();
            await context.SendJsonResponse(result);
        }

        [Post("/api/v1/game/settings/manual-priorities")]
        [EndpointMetadata("Turn the 'Manual priorities' game option on or off (query: enabled=true|false)")]
        public async Task SetManualPriorities(RimApiContext context)
        {
            var enabled = RequestParser.GetBooleanParameter(context, "enabled");
            var result = _gameOptionsService.SetManualPriorities(enabled);
            await context.SendJsonResponse(result);
        }
    }
}
