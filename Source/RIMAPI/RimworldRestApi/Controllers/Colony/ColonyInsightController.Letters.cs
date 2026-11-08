using System.Threading.Tasks;
using RIMAPI.Core;
using RIMAPI.Http;

namespace RIMAPI.Controllers
{
    public partial class ColonyInsightController
    {
        // How many letters a read returns when the caller does not say.
        private const int DefaultLetterLimit = 50;

        [Get("/api/v1/letters")]
        [EndpointMetadata("Pending letters (the letter stack on screen), newest first: label, text, def, age, targets (query: limit)")]
        public async Task GetLetters(RimApiContext context)
        {
            int limit = RequestParser.HasParameter(context, "limit")
                ? RequestParser.GetIntParameter(context, "limit")
                : DefaultLetterLimit;
            await context.SendJsonResponse(_insight.GetLetters(limit));
        }
    }
}
