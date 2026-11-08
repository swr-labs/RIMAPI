namespace RIMAPI.Services
{
    /// <summary>
    /// Read-only views of colony state that the stock routes do not expose. Each concern adds its members in
    /// its own partial file (ColonyInsightService.Letters.cs, ...Schedules.cs, ...). Nothing here changes the game.
    /// </summary>
    public partial interface IColonyInsightService { }
}
