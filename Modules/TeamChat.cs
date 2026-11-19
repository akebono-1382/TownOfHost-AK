// Based on the team chats (ImpostorChat / LoversChat) of https://github.com/Gurge44/EndlessHostRoles Patches/ChatCommandPatch.cs and
// https://github.com/haru03741-glitch/More-Roles-Plus src/Commands/TeamChat.cs (GPL-3.0)
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using TownOfHostL.Roles.AddOns.Common;
using TownOfHostL.Roles.Core;
using static TownOfHostL.Translator;

namespace TownOfHostL.Modules;

/// <summary>
/// 陣営チャット: 会議中に、生きている仲間にだけ文を送る (ホストが仲間へ 1 人ずつ送り直す)。
/// /tc 文 はインポスターどうし、/lc 文 は恋人どうし。仲間を互いに知っている陣営だけを扱う。
/// 打った文は他の人に見せないコマンドとして扱う (/cmd を付けずに送られたら、空白で押し出してから送り直す)
/// </summary>
public static class TeamChat
{
    public static OptionItem Enabled;

    public static void SetupCustomOption()
    {
        Enabled = StringOptionItem.Create("TeamChat", new string[] { "ColoredOff", "ColoredOn" }, 0, TabGroup.ModMainSettings, false)
            .SetColor(Palette.ImpostorRed)
            .SetGameMode(CustomGameMode.Standard);
    }

    public enum Team { Impostor, Lovers }

    private static string NameOf(PlayerControl pc)
        => ChatPushOut.Escape(Main.AllPlayerNames != null && Main.AllPlayerNames.TryGetValue(pc.PlayerId, out var n) ? n : pc.Data?.PlayerName ?? "?");

    private static bool IsImpostorMember(PlayerControl pc)
        => pc != null && pc.GetCustomRole().IsImpostor() && pc.GetCountTypes() == CountTypes.Impostor;

    /// <summary>from を含む、生きている仲間 (from が仲間に入っていなければ空)</summary>
    private static List<PlayerControl> Members(PlayerControl from, Team team)
    {
        var list = new List<PlayerControl>();
        switch (team)
        {
            case Team.Impostor:
                if (!IsImpostorMember(from)) return list;
                list.AddRange(Main.AllAlivePlayerControls.Where(IsImpostorMember));
                break;
            case Team.Lovers:
                if (!Lovers.playersList.Any(p => p != null && p.PlayerId == from.PlayerId)) return list;
                list.AddRange(Lovers.playersList.Where(p => p != null && p.IsAlive()));
                break;
        }
        return list;
    }

    private static void Tell(PlayerControl to, string title, string text) => Utils.SendMessageAutoSplit(text, true, title, to.PlayerId);

    public static void ImpostorCommand(ChatCommandContext c) => Command(c, Team.Impostor);
    public static void LoversCommand(ChatCommandContext c) => Command(c, Team.Lovers);

    private static void Command(ChatCommandContext c, Team team)
    {
        var from = c.Player;
        if (from == null) return;
        string system = $"<color=#aaaaff>{GetString("DefaultSystemMessageTitle")}</color>";
        if (Enabled == null || !Enabled.GetBool())
        {
            Tell(from, system, GetString("TeamChat.Disabled"));
            return;
        }
        if (c.Args.Length < 2)
        {
            Tell(from, system, $"{c.Args[0]} {GetString("CommandUsage.TeamChat")}");
            return;
        }
        if (!GameStates.IsMeeting || !from.IsAlive())
        {
            Tell(from, system, GetString("TeamChat.NotNow"));
            return;
        }
        var members = Members(from, team);
        if (members.Count < 2)
        {
            Tell(from, system, GetString("TeamChat.NoTeam"));
            return;
        }
        var text = ChatPushOut.Escape(string.Join(" ", c.Args.Skip(1)));
        var color = team == Team.Impostor ? "#ff1919" : Utils.GetRoleColorCode(CustomRoles.Lovers);
        var title = $"<color={color}>{string.Format(GetString(team == Team.Impostor ? "TeamChat.ImpostorTitle" : "TeamChat.LoversTitle"), NameOf(from))}</color>";
        Logger.Info($"陣営チャット ({team}): {from.PlayerId} → {members.Count - 1} 人", "TeamChat");
        // 送った本人にも控えを出す (他の人と同じ順番で出るように、本人宛ても送信待ちの列に積む)
        foreach (var to in members) Tell(to, title, text);
    }
}
