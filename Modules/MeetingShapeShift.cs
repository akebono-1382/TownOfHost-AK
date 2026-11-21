using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;

namespace TownOfHost;
public static class MeetingShapeshift
{
    private const float Delay = 8f; // 会議開始から何秒後に役職を書き換えるか
    private static readonly HashSet<byte> Applied = new();

    /// <summary>MeetingHud.Start の Postfix から呼ぶ</summary>
    public static void OnMeetingStart()
    {
        Applied.Clear();
        _ = new LateTask(() =>
        {
            if (!MeetingHud.Instance) return;
            if (MeetingHud.Instance.state is MeetingHud.MeetingStates.Results or MeetingHud.MeetingStates.Proceeding) return;

            var alive = PlayerCatch.AllAlivePlayerControls.ToArray();
            foreach (var pc in alive)
            {
                //秘匿のやつと競合するかも。
                if (pc.AmOwner) continue; // ホスト自身には desync できない
                if (pc.GetRoleClass() is not IUseMeetingShapeshift r || !r.CanUseMeetingShapeshift|| pc.IsAlive() is false) continue;

                var sender = CustomRpcSender.Create("MeetingShapeshift", SendOption.Reliable);
                sender.RpcSetRole(pc, RoleTypes.Shapeshifter, pc.GetClientId());
                if (!pc.Is(CustomRoleTypes.Impostor))
                {
                    // 本人の画面でだけ、インポスターを全員クルーに見せる
                    foreach (var imp in alive.Where(x => x != pc && x.Is(CustomRoleTypes.Impostor)))
                        sender.RpcSetRole(imp, RoleTypes.Crewmate, pc.GetClientId());
                }
                sender.SendMessage();
                Applied.Add(pc.PlayerId);
            }
        }, Delay, "MeetingShapeshift.Apply");
    }

    /// <summary>CheckShapeshift の Prefix の先頭から呼ぶ。true なら処理済みなので return false する</summary>
    public static bool OnCheckShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (!GameStates.IsMeeting || !Applied.Contains(shapeshifter.PlayerId)) return false;

        if (target != null && target.IsAlive()
            && shapeshifter.GetRoleClass() is IUseMeetingShapeshift r && r.CanUseMeetingShapeshift)
        {
            r.OnMeetingShapeshift(target);
        }
        shapeshifter.RpcRejectShapeshift(); // 実際には変身させず、クライアントの待機状態を解除する
        return true;
    }

    /// <summary>会議後処理(Utils.AfterMeetingTasks など)から呼ぶ。役職を本人の画面だけ元に戻す</summary>
    public static void OnMeetingEnd()
    {
        if (Applied.Count == 0) return;
        var alive = PlayerCatch.AllAlivePlayerControls.ToArray();
        foreach (var id in Applied)
        {
            var pc = PlayerCatch.GetPlayerById(id);
            if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;

            var sender = CustomRpcSender.Create("MeetingShapeshift.Revert", SendOption.Reliable);
            // 書き換えはホスト側の Data には反映されていないので、Data.Role.Role が元の役職
            sender.RpcSetRole(pc, pc.Data.Role.Role, pc.GetClientId());
            foreach (var imp in alive.Where(x => x != pc && x.Is(CustomRoleTypes.Impostor)))
                sender.RpcSetRole(imp, imp.Data.Role.Role, pc.GetClientId());
            sender.SendMessage();
        }
        Applied.Clear();
    }
}