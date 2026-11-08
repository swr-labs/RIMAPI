namespace RIMAPI.Services
{
    /// <summary>
    /// Colony-management writes that mirror what a player does in the UI (claim a building, pick a policy,
    /// designate an animal...). Deliberately NOT here: anything that spawns, teleports, heals instantly or edits
    /// stats, skills or needs (those are the cheat routes under pawn/edit/*). Each concern adds its members in its
    /// own partial file.
    /// </summary>
    public partial interface IColonyControlService { }
}
