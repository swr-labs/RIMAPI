using RIMAPI.Services;

namespace RIMAPI.Controllers
{
    /// <summary>Thin HTTP layer for the read-only views in IColonyInsightService; routes live in the partial files.</summary>
    public partial class ColonyInsightController
    {
        private readonly IColonyInsightService _insight;

        public ColonyInsightController(IColonyInsightService insight)
        {
            _insight = insight;
        }
    }
}
