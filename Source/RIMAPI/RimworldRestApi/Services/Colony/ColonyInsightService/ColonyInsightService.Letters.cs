using System;
using System.Collections.Generic;
using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RIMAPI.Models
{
    public class LetterDto
    {
        public int Index { get; set; }
        public string Label { get; set; }
        public string Text { get; set; }
        public string LetterDef { get; set; }
        public int ArrivalTick { get; set; }
        public float AgeHours { get; set; }
        public string RelatedFaction { get; set; }
        public int? QuestId { get; set; }
        public List<string> Targets { get; set; } = new List<string>();
    }

    public class LettersDto
    {
        public int Total { get; set; }
        public List<LetterDto> Letters { get; set; } = new List<LetterDto>();
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyInsightService
    {
        ApiResult<LettersDto> GetLetters(int limit);
    }

    public partial class ColonyInsightService
    {
        // Ticks per in-game hour (GenDate.TicksPerHour = 2500).
        private const float TicksPerHour = GenDate.TicksPerHour;

        public ApiResult<LettersDto> GetLetters(int limit)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.LetterStack == null)
            {
                return ApiResult<LettersDto>.Fail("Letters are unavailable: no game is being played.");
            }
            if (limit < 1 || limit > MaxListItems)
            {
                return ApiResult<LettersDto>.ValidationFail($"limit must be 1..{MaxListItems}");
            }

            // Newest first: the stack lists oldest first.
            List<Letter> all = Find.LetterStack.LettersListForReading;
            int now = Find.TickManager.TicksGame;
            var dto = new LettersDto { Total = all.Count };
            for (int i = all.Count - 1; i >= 0 && dto.Letters.Count < limit; i--)
            {
                Letter letter = all[i];
                var choice = letter as ChoiceLetter;
                var entry = new LetterDto
                {
                    Index = i,
                    Label = letter.Label.ToString(),
                    Text = choice != null ? choice.Text.ToString() : null,
                    LetterDef = letter.def?.defName,
                    ArrivalTick = letter.arrivalTick,
                    AgeHours = (now - letter.arrivalTick) / TicksPerHour,
                    RelatedFaction = letter.relatedFaction?.Name,
                    QuestId = choice?.quest?.id,
                };
                if (letter.lookTargets != null && letter.lookTargets.IsValid())
                {
                    foreach (GlobalTargetInfo target in letter.lookTargets.targets)
                    {
                        entry.Targets.Add(DescribeTarget(target));
                    }
                }
                dto.Letters.Add(entry);
            }
            return ApiResult<LettersDto>.Ok(dto);
        }

        private static string DescribeTarget(GlobalTargetInfo target)
        {
            if (target.HasThing)
                return $"thing:{target.Thing.thingIDNumber}:{target.Thing.def.defName}";
            if (target.HasWorldObject)
                return $"world_object:{target.WorldObject.ID}";
            if (target.Cell.IsValid)
                return $"cell:{target.Cell.x},{target.Cell.z}";
            return "unknown";
        }
    }
}
