using System.Collections.Generic;
using RIMAPI.Core;
using RIMAPI.Models;

namespace RIMAPI.Services
{
    public interface IBuilderService
    {
        ApiResult<BlueprintDto> CopyArea(CopyAreaRequestDto request);
        ApiResult PasteArea(PasteAreaRequestDto request);
        ApiResult PlaceBlueprints(PasteAreaRequestDto request);
        ApiResult<List<ConstructionSiteDto>> ListConstructionSites(int mapId);
        ApiResult CancelConstructionSite(int mapId, int thingId);
        ApiResult<CheckZoneResultDto> CheckZone(CheckZoneRequestDto request);
    }
}