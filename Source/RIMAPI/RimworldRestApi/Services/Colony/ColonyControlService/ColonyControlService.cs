using System.Linq;
using RimWorld;
using Verse;

namespace RIMAPI.Services
{
    public partial class ColonyControlService : IColonyControlService
    {
        // Largest list of ids one write may touch.
        private const int MaxBatch = 500;

        private static Map FindMap(int mapId)
        {
            return Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
        }

        private static string MapNotFound(int mapId)
        {
            return $"Map {mapId} not found";
        }

        /// <summary>A pawn of the player's colony: a free colonist or slave, not a guest, prisoner or animal.</summary>
        private static Pawn FindColonist(int pawnId, out string problem)
        {
            problem = null;
            foreach (Pawn p in PawnsFinder.AllMaps_FreeColonistsAndPrisonersSpawned)
            {
                if (p.thingIDNumber == pawnId)
                {
                    if (p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike)
                    {
                        return p;
                    }
                    problem = $"Pawn {pawnId} is not a member of the player's colony";
                    return null;
                }
            }
            problem = $"Colonist {pawnId} not found";
            return null;
        }
    }
}
