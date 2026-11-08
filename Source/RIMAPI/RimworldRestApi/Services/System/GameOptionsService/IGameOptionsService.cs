using RIMAPI.Core;
using RIMAPI.Models;

namespace RIMAPI.Services
{
    public interface IGameOptionsService
    {
        ApiResult<ManualPrioritiesDto> GetManualPriorities();
        ApiResult<ManualPrioritiesDto> SetManualPriorities(bool enabled);
    }
}
