using RIMAPI.Services;

namespace RIMAPI.Controllers
{
    /// <summary>Thin HTTP layer for IColonyControlService; routes live in the partial files.</summary>
    public partial class ColonyControlController
    {
        private readonly IColonyControlService _control;

        public ColonyControlController(IColonyControlService control)
        {
            _control = control;
        }
    }
}
