using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Helpers;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class QuestDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string RootDef { get; set; }
        public string Description { get; set; }
        public string State { get; set; }               // NotYetAccepted|Ongoing|EndedSuccess|EndedFailed|EndedOfferExpired|...
        public bool EverAccepted { get; set; }
        public bool Historical { get; set; }            // finished and archived
        public bool HiddenInUi { get; set; }            // dismissed by the player
        public float ChallengeRating { get; set; }
        public float HoursSinceAppeared { get; set; }

        // Hours left to ACCEPT the offer (only meaningful while NotYetAccepted); null = never expires.
        public float? HoursUntilOfferExpires { get; set; }

        // Hours until the running quest itself runs out (its own time limit); null = no limit. While the quest is
        // still being offered this is the same number as the offer window.
        public float? HoursUntilQuestExpires { get; set; }
        public List<string> InvolvedFactions { get; set; } = new List<string>();
        public List<string> PartTypes { get; set; } = new List<string>();

        // Hours left on each timer part of the quest (arrival delays, the monument window, ...).
        public List<float> DelayHoursLeft { get; set; } = new List<float>();
        public List<List<string>> RewardChoices { get; set; } = new List<List<string>>();
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyInsightService
    {
        ApiResult<QuestDetailDto> GetQuestDetail(int questId);
    }

    public partial class ColonyInsightService
    {
        public ApiResult<QuestDetailDto> GetQuestDetail(int questId)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.QuestManager == null)
            {
                return ApiResult<QuestDetailDto>.Fail("Quests are unavailable: no game is being played.");
            }
            Quest quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == questId);
            if (quest == null)
            {
                return ApiResult<QuestDetailDto>.Fail($"Quest {questId} not found");
            }

            var dto = new QuestDetailDto
            {
                Id = quest.id,
                Name = quest.name,
                RootDef = quest.root?.defName,
                Description = quest.description.ToString(),
                State = quest.State.ToString(),
                EverAccepted = quest.EverAccepted,
                Historical = quest.Historical,
                HiddenInUi = quest.hiddenInUI,
                ChallengeRating = quest.challengeRating,
                HoursSinceAppeared = quest.TicksSinceAppeared / (float)GenDate.TicksPerHour,
                HoursUntilOfferExpires = quest.acceptanceExpireTick >= 0 ? HoursOrNull(quest.acceptanceExpireTick - Find.TickManager.TicksGame) : null,
                HoursUntilQuestExpires = HoursOrNull(quest.TicksUntilExpiry),
                InvolvedFactions = quest.InvolvedFactions.Where(f => f != null).Select(f => f.Name).ToList(),
            };
            foreach (QuestPart part in quest.PartsListForReading)
            {
                dto.PartTypes.Add(part.GetType().Name);
                var delay = part as QuestPart_Delay;
                if (delay != null && HoursOrNull(delay.TicksLeft).HasValue)
                {
                    dto.DelayHoursLeft.Add(HoursOrNull(delay.TicksLeft).Value);
                }
                var choice = part as QuestPart_Choice;
                if (choice != null)
                {
                    foreach (var option in choice.choices)
                    {
                        dto.RewardChoices.Add(option.rewards.Select(r => r?.ToString() ?? "Unknown").ToList());
                    }
                }
            }
            return ApiResult<QuestDetailDto>.Ok(dto);
        }

        // RimWorld stores "no limit" as a negative tick count (or int.MaxValue-sized values).
        private static float? HoursOrNull(int ticks)
        {
            const int NoLimitTicksAtLeast = 60 * GenDate.TicksPerYear; // far beyond any real quest window
            if (ticks < 0 || ticks >= NoLimitTicksAtLeast)
            {
                return null;
            }
            return ticks / (float)GenDate.TicksPerHour;
        }
    }
}
