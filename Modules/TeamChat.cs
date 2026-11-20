// Based on the team chats (ImpostorChat / LoversChat) of https://github.com/Gurge44/EndlessHostRoles Patches/ChatCommandPatch.cs and
// https://github.com/haru03741-glitch/More-Roles-Plus src/Commands/TeamChat.cs (GPL-3.0)
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using TownOfHostL.Roles.AddOns.Common;
using TownOfHostL.Roles.Core;
using static TownOfHostL.Translator;

namespace TownOfHostL.Modules;

public static class TeamChat
{
    public static OptionItem Enabled;
    public static OptionItem AllowInTask;
    private static readonly Dictionary<Team, OptionItem> TeamEnabled = new();

    public enum Team { Impostor, Lovers, Jackal, Pavlov, Stand }

    private static readonly (Team team, string option, string title, string color)[] Teams =
    {
        (Team.Impostor, "ImpostorHideChat", "TeamChat.ImpostorTitle", "#ff1919"),
        (Team.Lovers, "LoversHideChat", "TeamChat.LoversTitle", null),
        (Team.Jackal, "JackalHideChat", "TeamChat.JackalTitle", "#00b4eb"),
        (Team.Pavlov, "PavlovHideChat", "TeamChat.PavlovTitle", "#e6ab6c"),
        (Team.Stand, "StandHideChat", "TeamChat.StandTitle", "#b98bd8"),
    };

    public static void SetupCustomOption()
    {
        TeamEnabled.Clear();
        Enabled = StringOptionItem.Create("TeamChat", new string[] { "ColoredOff", "ColoredOn" }, 0, TabGroup.ModMainSettings, false)
            .SetColor(Palette.ImpostorRed)
            .SetGameMode(CustomGameMode.Standard);
        foreach (var (team, option, _, _) in Teams)
        {
            TeamEnabled[team] = StringOptionItem.Create(option, new string[] { "ColoredOff", "ColoredOn" }, 1, TabGroup.ModMainSettings, false)
                .SetParent(Enabled)
                .SetGameMode(CustomGameMode.Standard);
        }
        AllowInTask = StringOptionItem.Create("GameDisplayHideChat", new string[] { "ColoredOff", "ColoredOn" }, 0, TabGroup.ModMainSettings, false)
            .SetParent(Enabled)
            .SetGameMode(CustomGameMode.Standard);
    }

    private static string NameOf(PlayerControl pc)
        => ChatPushOut.Escape(Main.AllPlayerNames != null && Main.AllPlayerNames.TryGetValue(pc.PlayerId, out var n) ? n : pc.Data?.PlayerName ?? "?");

    private static bool IsImpostorMember(PlayerControl pc)
        => pc != null && pc.GetCustomRole().IsImpostor() && pc.GetCountTypes() == CountTypes.Impostor;

    private static bool IsCountMember(PlayerControl pc, CountTypes type)
        => pc != null && pc.GetCountTypes() == type;

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
            default:
                var type = team switch
                {
                    Team.Jackal => CountTypes.Jackal,
                    Team.Pavlov => CountTypes.Pavlov,
                    _ => CountTypes.StandMaster,
                };
                if (!IsCountMember(from, type)) return list;
                list.AddRange(Main.AllAlivePlayerControls.Where(p => IsCountMember(p, type)));
                break;
        }
        return list;
    }

    private static void Tell(PlayerControl to, string title, string text) => Utils.SendMessageAutoSplit(text, true, title, to.PlayerId);

    public static void ImpostorCommand(ChatCommandContext c) => Command(c, Team.Impostor);
    public static void LoversCommand(ChatCommandContext c) => Command(c, Team.Lovers);
    public static void JackalCommand(ChatCommandContext c) => Command(c, Team.Jackal);
    public static void PavlovCommand(ChatCommandContext c) => Command(c, Team.Pavlov);
    public static void StandCommand(ChatCommandContext c) => Command(c, Team.Stand);

    private static void Command(ChatCommandContext c, Team team)
    {
        var from = c.Player;
        if (from == null) return;
        string system = $"<color=#aaaaff>{GetString("DefaultSystemMessageTitle")}</color>";
        if (Enabled == null || !Enabled.GetBool() || !TeamEnabled.TryGetValue(team, out var teamOn) || !teamOn.GetBool())
        {
            Tell(from, system, GetString("TeamChat.Disabled"));
            return;
        }
        if (c.Args.Length < 2)
        {
            Tell(from, system, $"{c.Args[0]} {GetString("CommandUsage.TeamChat")}");
            return;
        }
        bool inTask = AllowInTask != null && AllowInTask.GetBool();
        if (!(GameStates.IsMeeting || (inTask && GameStates.IsInTask)) || !from.IsAlive())
        {
            Tell(from, system, GetString(inTask ? "TeamChat.NotNowAlive" : "TeamChat.NotNow"));
            return;
        }
        var members = Members(from, team);
        if (members.Count < 2)
        {
            Tell(from, system, GetString("TeamChat.NoTeam"));
            return;
        }
        var info = Teams.First(t => t.team == team);
        var text = ChatPushOut.Escape(string.Join(" ", c.Args.Skip(1)));
        var color = info.color ?? Utils.GetRoleColorCode(CustomRoles.Lovers);
        var title = $"<color={color}>{string.Format(GetString(info.title), NameOf(from))}</color>";
        Logger.Info($"陣営チャット ({team}): {from.PlayerId} → {members.Count - 1} 人", "TeamChat");
        foreach (var to in members) Tell(to, title, text);
    }
}
