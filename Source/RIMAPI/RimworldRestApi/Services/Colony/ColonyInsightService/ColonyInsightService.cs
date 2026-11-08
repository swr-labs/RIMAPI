using System.Linq;
using Verse;

namespace RIMAPI.Services
{
    public partial class ColonyInsightService : IColonyInsightService
    {
        // Upper bound on any list a read returns, so one call cannot produce an unbounded body.
        private const int MaxListItems = 500;

        private static Map FindMap(int mapId)
        {
            return Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
        }

        private static string MapNotFound(int mapId)
        {
            return $"Map {mapId} not found";
        }
    }
}
