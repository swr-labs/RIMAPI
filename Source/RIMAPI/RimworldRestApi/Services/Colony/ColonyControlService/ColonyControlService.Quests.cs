using System.Linq;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class QuestActionRequestDto
    {
        public int QuestId { get; set; }

        // accept|dismiss|choose_reward
        public string Action { get; set; }

        // accept: the colonist who accepts and receives personal rewards (e.g. royal favor); optional.
        public int? AccepteeId { get; set; }

        // choose_reward: index into the quest's reward choices (see quests/detail reward_choices).
        public int? ChoiceIndex { get; set; }
    }

    public class QuestActionResultDto
    {
        public int QuestId { get; set; }
        public string State { get; set; }
        public bool EverAccepted { get; set; }
        public bool HiddenInUi { get; set; }
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyControlService
    {
        ApiResult<QuestActionResultDto> ApplyQuestAction(QuestActionRequestDto request);
    }

    public partial class ColonyControlService
    {
        public ApiResult<QuestActionResultDto> ApplyQuestAction(QuestActionRequestDto request)
        {
            if (Current.ProgramState != ProgramState.Playing || Find.QuestManager == null)
                return ApiResult<QuestActionResultDto>.Fail("Quests are unavailable: no game is being played.");
            if (request == null || string.IsNullOrEmpty(request.Action))
                return ApiResult<QuestActionResultDto>.ValidationFail("quest_id and action are required");
            Quest quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q => q.id == request.QuestId);
            if (quest == null)
                return ApiResult<QuestActionResultDto>.Fail($"Quest {request.QuestId} not found");

            string problem;
            try
            {
                switch (request.Action)
                {
                    case "accept": problem = Accept(quest, request.AccepteeId); break;
                    case "dismiss": problem = Dismiss(quest); break;
                    case "choose_reward": problem = ChooseReward(quest, request.ChoiceIndex); break;
                    default: problem = "action must be accept, dismiss or choose_reward"; break;
                }
            }
            catch (System.Exception ex)
            {
                // The quest system throws from deep inside quest parts; say so rather than leave the state unknown.
                return ApiResult<QuestActionResultDto>.Fail(
                    $"Quest {request.QuestId} action '{request.Action}' threw {ex.GetType().Name}: {ex.Message}; its state is now {quest.State}");
            }
            if (problem != null)
                return ApiResult<QuestActionResultDto>.ValidationFail(problem);

            return ApiResult<QuestActionResultDto>.Ok(new QuestActionResultDto
            {
                QuestId = quest.id,
                State = quest.State.ToString(),
                EverAccepted = quest.EverAccepted,
                HiddenInUi = quest.hiddenInUI,
            });
        }

        private static string Accept(Quest quest, int? accepteeId)
        {
            if (quest.State != QuestState.NotYetAccepted)
                return $"quest is {quest.State}, only a NotYetAccepted quest can be accepted";
            Pawn acceptee = null;
            if (accepteeId.HasValue)
            {
                acceptee = FindColonist(accepteeId.Value, out string who);
                if (acceptee == null) return who;
            }
            if (acceptee != null)
            {
                AcceptanceReport report = QuestUtility.CanPawnAcceptQuest(acceptee, quest);
                if (!report.Accepted)
                    return $"the game refuses {acceptee.LabelShort} as acceptee: {report.Reason}";
            }
            quest.Accept(acceptee);
            return quest.State == QuestState.NotYetAccepted ? "the quest is still not accepted after Accept()" : null;
        }

        // The Dismiss button: hides a quest from the list. An offer that is dismissed is simply not taken.
        private static string Dismiss(Quest quest)
        {
            if (quest.State == QuestState.Ongoing)
                return "an accepted (Ongoing) quest cannot be dismissed";
            quest.hiddenInUI = true;
            return null;
        }

        private static string ChooseReward(Quest quest, int? choiceIndex)
        {
            if (!choiceIndex.HasValue)
                return "choice_index is required for choose_reward";
            QuestPart_Choice part = quest.PartsListForReading.OfType<QuestPart_Choice>().FirstOrDefault();
            if (part == null)
                return "this quest has no reward choice";
            if (choiceIndex.Value < 0 || choiceIndex.Value >= part.choices.Count)
                return $"choice_index must be 0..{part.choices.Count - 1}";
            if (quest.State != QuestState.Ongoing)
                return $"rewards can be chosen while the quest is Ongoing, it is {quest.State}";
            part.Choose(part.choices[choiceIndex.Value]);
            return null;
        }
    }
}
