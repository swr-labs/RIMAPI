using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RIMAPI.Models
{
    public class GuestPawnDto
    {
        public int PawnId { get; set; }
        public string Name { get; set; }
        public string Faction { get; set; }
        public string GuestStatus { get; set; }     // Guest|Prisoner|Slave
        public string HostFaction { get; set; }
        public bool IsQuestLodger { get; set; }
        public bool Downed { get; set; }
        public float? MoodPercent { get; set; }     // 0..1; null when the pawn has no mood need
        public int X { get; set; }
        public int Z { get; set; }
        public string CurrentJob { get; set; }
        public string LordJob { get; set; }         // e.g. LordJob_VisitColony, LordJob_TravelAndExit ("leaving")
        public string LordToil { get; set; }
    }

    public class GuestsDto
    {
        public List<GuestPawnDto> Guests { get; set; } = new List<GuestPawnDto>();
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyInsightService
    {
        ApiResult<GuestsDto> GetGuests(int mapId, string status);
    }

    public partial class ColonyInsightService
    {
        public ApiResult<GuestsDto> GetGuests(int mapId, string status)
        {
            Map map = FindMap(mapId);
            if (map == null)
            {
                return ApiResult<GuestsDto>.Fail(MapNotFound(mapId));
            }
            GuestStatus? wanted = null;
            if (status != "all")
            {
                switch (status)
                {
                    case "guest": wanted = GuestStatus.Guest; break;
                    case "prisoner": wanted = GuestStatus.Prisoner; break;
                    case "slave": wanted = GuestStatus.Slave; break;
                    default:
                        return ApiResult<GuestsDto>.ValidationFail("status must be all, guest, prisoner or slave");
                }
            }

            var dto = new GuestsDto();
            // Pawns hosted by the player's colony but not its own members: visitors, quest lodgers, prisoners, slaves.
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(p => p.HostFaction == Faction.OfPlayer && p.Faction != Faction.OfPlayer))
            {
                GuestStatus guestStatus = pawn.GuestStatus ?? GuestStatus.Guest;
                if (wanted.HasValue && guestStatus != wanted.Value)
                {
                    continue;
                }
                Lord lord = pawn.GetLord();
                dto.Guests.Add(new GuestPawnDto
                {
                    PawnId = pawn.thingIDNumber,
                    Name = pawn.LabelShort,
                    Faction = pawn.Faction?.Name,
                    GuestStatus = guestStatus.ToString(),
                    HostFaction = pawn.HostFaction?.Name,
                    IsQuestLodger = pawn.IsQuestLodger(),
                    Downed = pawn.Downed,
                    MoodPercent = pawn.needs?.mood?.CurLevelPercentage,
                    X = pawn.Position.x,
                    Z = pawn.Position.z,
                    CurrentJob = pawn.CurJob?.def?.defName,
                    LordJob = lord?.LordJob?.GetType().Name,
                    LordToil = lord?.CurLordToil?.GetType().Name,
                });
            }
            return ApiResult<GuestsDto>.Ok(dto);
        }
    }
}
