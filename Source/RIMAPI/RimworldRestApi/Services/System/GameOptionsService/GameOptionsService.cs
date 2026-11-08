using System;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Services
{
    /// <summary>
    /// The "Manual priorities" game option (PlaySettings.useWorkPriorities, stored in the save). While it is off, RimWorld ignores work priorities other than
    /// on/off, so priority writes through the API look successful but change nothing; callers need to read it.
    /// </summary>
    public class GameOptionsService : IGameOptionsService
    {
        public ApiResult<ManualPrioritiesDto> GetManualPriorities()
        {
            if (!IsPlaying())
            {
                return ApiResult<ManualPrioritiesDto>.Fail("No game is being played, so the option cannot be read.");
            }
            return ApiResult<ManualPrioritiesDto>.Ok(Describe(changed: false));
        }

        public ApiResult<ManualPrioritiesDto> SetManualPriorities(bool enabled)
        {
            if (!IsPlaying())
            {
                return ApiResult<ManualPrioritiesDto>.Fail("No game is being played, so the option cannot be set.");
            }
            try
            {
                var playSettings = Current.Game.playSettings;
                bool before = playSettings.useWorkPriorities;
                if (before == enabled)
                {
                    return ApiResult<ManualPrioritiesDto>.Ok(Describe(changed: false));
                }

                playSettings.useWorkPriorities = enabled;

                // Same follow-up the work tab's checkbox performs: pawns re-derive their work settings.
                foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                {
                    pawn.workSettings?.Notify_UseWorkPrioritiesChanged();
                }

                return ApiResult<ManualPrioritiesDto>.Ok(Describe(changed: true));
            }
            catch (Exception ex)
            {
                return ApiResult<ManualPrioritiesDto>.Fail($"Failed to set manual priorities: {ex.Message}");
            }
        }

        private static bool IsPlaying()
        {
            return Current.ProgramState == ProgramState.Playing && Current.Game?.playSettings != null;
        }

        private static ManualPrioritiesDto Describe(bool changed)
        {
            return new ManualPrioritiesDto
            {
                ManualPriorities = Current.Game.playSettings.useWorkPriorities,
                Changed = changed,
            };
        }
    }
}
