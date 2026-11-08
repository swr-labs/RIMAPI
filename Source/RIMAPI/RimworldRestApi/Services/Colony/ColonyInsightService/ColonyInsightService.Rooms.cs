using System.Collections.Generic;
using System.Linq;
using System.Text;
using RIMAPI.Core;
using RIMAPI.Models;
using RimWorld;
using Verse;

namespace RIMAPI.Models
{
    public class RoomStatusDto
    {
        public int CellX { get; set; }
        public int CellZ { get; set; }
        public bool InRoom { get; set; }          // false for a cell with no room (e.g. solid rock)
        public int RoomId { get; set; }
        public string Role { get; set; }          // RoomRoleDef defName: Bedroom, DiningRoom, None...
        public string RoleLabel { get; set; }
        public bool ProperRoom { get; set; }      // enclosed room the game rates (false: open-air or tiny)
        public bool PsychologicallyOutdoors { get; set; }
        public bool TouchesMapEdge { get; set; }
        public int CellsCount { get; set; }
        public int OpenRoofCount { get; set; }    // cells in the room with no roof
        public float Temperature { get; set; }
        public float Impressiveness { get; set; }
        public float Beauty { get; set; }
        public float Cleanliness { get; set; }
        public float Space { get; set; }
        public float Wealth { get; set; }
        public List<int> BedIds { get; set; } = new List<int>();
    }

    public class RoofGridDto
    {
        public int X1 { get; set; }
        public int Z1 { get; set; }
        public int X2 { get; set; }
        public int Z2 { get; set; }

        // One string per z from Z1 to Z2 (row 0 = Z1), one char per x from X1 to X2.
        // '.' no roof, 't' constructed roof, 'R' overhead mountain (thick) roof, '?' any other roof def.
        public List<string> Rows { get; set; } = new List<string>();
        public int NoRoofCells { get; set; }
        public int ConstructedRoofCells { get; set; }
        public int ThickRoofCells { get; set; }

        // Cells in the rectangle painted in the player's "no roof" / "build roof" areas.
        public int NoRoofAreaCells { get; set; }
        public int BuildRoofAreaCells { get; set; }
    }
}

namespace RIMAPI.Services
{
    public partial interface IColonyInsightService
    {
        ApiResult<RoomStatusDto> GetRoomAt(int mapId, int x, int z);
        ApiResult<RoofGridDto> GetRoofs(int mapId, int x1, int z1, int x2, int z2);
    }

    public partial class ColonyInsightService
    {
        // Largest rectangle one roof read may cover (200x200), so the response stays a few tens of KB.
        private const int MaxRoofCells = 40000;

        public ApiResult<RoomStatusDto> GetRoomAt(int mapId, int x, int z)
        {
            Map map = FindMap(mapId);
            if (map == null)
            {
                return ApiResult<RoomStatusDto>.Fail(MapNotFound(mapId));
            }
            var cell = new IntVec3(x, 0, z);
            if (!cell.InBounds(map))
            {
                return ApiResult<RoomStatusDto>.ValidationFail($"cell ({x},{z}) is outside the {map.Size.x}x{map.Size.z} map");
            }

            var dto = new RoomStatusDto { CellX = x, CellZ = z };
            Room room = cell.GetRoom(map);
            if (room == null)
            {
                return ApiResult<RoomStatusDto>.Ok(dto);
            }

            dto.InRoom = true;
            dto.RoomId = room.ID;
            dto.Role = room.Role?.defName;
            dto.RoleLabel = room.GetRoomRoleLabel();
            dto.ProperRoom = room.ProperRoom;
            dto.PsychologicallyOutdoors = room.PsychologicallyOutdoors;
            dto.TouchesMapEdge = room.TouchesMapEdge;
            dto.CellsCount = room.CellCount;
            dto.OpenRoofCount = room.OpenRoofCount;
            dto.Temperature = room.Temperature;
            dto.Impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
            dto.Beauty = room.GetStat(RoomStatDefOf.Beauty);
            dto.Cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
            dto.Space = room.GetStat(RoomStatDefOf.Space);
            dto.Wealth = room.GetStat(RoomStatDefOf.Wealth);
            dto.BedIds = room.ContainedBeds.Select(b => b.thingIDNumber).ToList();
            return ApiResult<RoomStatusDto>.Ok(dto);
        }

        public ApiResult<RoofGridDto> GetRoofs(int mapId, int x1, int z1, int x2, int z2)
        {
            Map map = FindMap(mapId);
            if (map == null)
            {
                return ApiResult<RoofGridDto>.Fail(MapNotFound(mapId));
            }
            if (x1 > x2 || z1 > z2)
            {
                return ApiResult<RoofGridDto>.ValidationFail("rectangle corners must satisfy x1<=x2 and z1<=z2");
            }
            if (!new IntVec3(x1, 0, z1).InBounds(map) || !new IntVec3(x2, 0, z2).InBounds(map))
            {
                return ApiResult<RoofGridDto>.ValidationFail(
                    $"rectangle ({x1},{z1})-({x2},{z2}) is outside the {map.Size.x}x{map.Size.z} map"
                );
            }
            long cells = (long)(x2 - x1 + 1) * (z2 - z1 + 1);
            if (cells > MaxRoofCells)
            {
                return ApiResult<RoofGridDto>.ValidationFail($"rectangle has {cells} cells; the limit is {MaxRoofCells}");
            }

            var dto = new RoofGridDto { X1 = x1, Z1 = z1, X2 = x2, Z2 = z2 };
            Area noRoof = map.areaManager.NoRoof;
            Area buildRoof = map.areaManager.BuildRoof;
            for (int z = z1; z <= z2; z++)
            {
                var row = new StringBuilder(x2 - x1 + 1);
                for (int x = x1; x <= x2; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    RoofDef roof = map.roofGrid.RoofAt(cell);
                    if (roof == null)
                    {
                        row.Append('.');
                        dto.NoRoofCells++;
                    }
                    else if (roof.isThickRoof)
                    {
                        row.Append('R');
                        dto.ThickRoofCells++;
                    }
                    else if (roof == RoofDefOf.RoofConstructed)
                    {
                        row.Append('t');
                        dto.ConstructedRoofCells++;
                    }
                    else
                    {
                        row.Append('?');
                    }
                    if (noRoof[cell]) dto.NoRoofAreaCells++;
                    if (buildRoof[cell]) dto.BuildRoofAreaCells++;
                }
                dto.Rows.Add(row.ToString());
            }
            return ApiResult<RoofGridDto>.Ok(dto);
        }
    }
}
