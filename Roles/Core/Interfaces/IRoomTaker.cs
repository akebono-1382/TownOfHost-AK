using System.Linq;
using Hazel;
using TownOfHostL.Modules;

namespace TownOfHostL.Roles.Core.Interfaces;

public interface IRoomTasker
{
    public void AddRoomTaker(byte playerid)
    {
        if (RoomTaskAssign.AllRoomTasker.TryAdd(playerid, new(playerid)) is false)
        {
            RoomTaskAssign.AllRoomTasker[playerid] = new(playerid);
        }
    }
    public RoomTaskAssign GetMyRoomData(byte playerid)
        => RoomTaskAssign.AllRoomTasker.TryGetValue(playerid, out var data) ? data : null;

    /// <summary>
    /// 部屋タスクをアサインするか否か。
    /// </summary>
    /// <returns></returns>
    public bool IsAssignRoomTask() => false;
    /// <summary>
    /// 部屋到着時に呼ばれる関数。<br/>
    /// RPC、GameLog処理を入れるように。
    /// </summary>
    public void OnComplete(int completeroom) { }

    /// <summary>
    /// 部屋が変わった時に呼ばれる関数。<br/>
    /// RPC処理を忘れないように。
    /// </summary>
    public void ChangeRoom(PlainShipRoom TaskPSR) { }
    /// <summary>
    /// LowerTextの文を生成する。
    /// </summary>
    /// <param name="seer"></param>
    /// <param name="colorcode"></param>
    public string GetLowerText(PlayerControl seer, string colorcode = "#ffffff") => GetMyRoomData(seer.PlayerId)?.GetLowerText(seer, colorcode);

    /// <summary>
    /// Maxのタスク数を設定する。<br/>
    /// nullの場合、設定しない(妖狐的な。)<br/>
    /// </summary>
    /// <returns></returns>
    public int? GetMaxTaskCount() => null;

    //↓ RPC受け取り処理

}
