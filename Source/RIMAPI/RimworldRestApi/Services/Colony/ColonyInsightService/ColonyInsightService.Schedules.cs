using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Helpers;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class PawnScheduleDto
    {
        public int PawnId { get; set; }
        public string Name { get; set; }

        // 24 TimeAssignmentDef defNames, index = hour of day (0-23): Anything|Work|Joy|Sleep|Meditate...
        public List<string> Hours { get; set; }
    }

    public class SchedulesDto
    {
        public List<PawnScheduleDto> Pawns { get; set; } = new List<PawnScheduleDto>();
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyInsightService
    {
        ApiResult<SchedulesDto> GetSchedules(int? pawnId);
    }

    public partial class ColonyInsightService
    {
        public ApiResult<SchedulesDto> GetSchedules(int? pawnId)
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return ApiResult<SchedulesDto>.Fail("Schedules are unavailable: no game is being played.");
            }

            var dto = new SchedulesDto();
            IEnumerable<Pawn> pawns = PawnsFinder.AllMaps_FreeColonists;
            if (pawnId.HasValue)
            {
                pawns = pawns.Where(p => p.thingIDNumber == pawnId.Value);
            }

            foreach (Pawn pawn in pawns)
            {
                if (pawn.timetable == null)
                {
                    return ApiResult<SchedulesDto>.Fail(
                        $"Pawn {pawn.LabelShort} ({pawn.thingIDNumber}) has no timetable (not a colonist who follows a schedule)."
                    );
                }
                var hours = new List<string>(24);
                for (int hour = 0; hour < 24; hour++)
                {
                    hours.Add(pawn.timetable.GetAssignment(hour).defName);
                }
                dto.Pawns.Add(new PawnScheduleDto
                {
                    PawnId = pawn.thingIDNumber,
                    Name = pawn.LabelShort,
                    Hours = hours,
                });
            }

            if (pawnId.HasValue && dto.Pawns.Count == 0)
            {
                return ApiResult<SchedulesDto>.Fail($"Colonist {pawnId.Value} not found");
            }
            return ApiResult<SchedulesDto>.Ok(dto);
        }
    }
}
