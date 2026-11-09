using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Opportunist : RoleBase, IAdditionalWinner, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Opportunist),
            player => new Opportunist(player),
            CustomRoles.Opportunist,
            () => CanKill.GetBool() ? RoleTypes.Impostor : CanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            15200,
            SetupOptionItem,
            "op",
            "#00ff00",
            (6, 2),
            from: From.TOR_GM_Edition
        );
    public Opportunist(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => NeedTask.GetBool() ? HasTask.ForRecompute : HasTask.False
    )
    {
        timer = 0;
        pos = new(0, 0);
    }
    static OptionItem NeedTask;
    static OptionItem TaskWin;
    public static OptionItem CanKill;
    static OptionItem KillCool;
    static OptionItem NeedKills;
    static OptionItem CanVent;
    static OptionItem OptionVentCooldown;
    static OptionItem OptionVentIntime;
    public bool CanUseImpostorVentButton() => CanVent.GetBool();
    public float CalculateKillCooldown() => KillCool.GetFloat();
    enum Op
    {
        CountKillerVictoryCount,
        OpportunistCanKill,
    }

    static void SetupOptionItem()
    {
        NeedTask = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.NeedTaskWin, true, false)
            .SetEnabled(() => CanKill.GetBool() is false);
        TaskWin = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.TaskWin, new(1, 255, 1), 8, false, NeedTask)
            .SetValueFormat(OptionFormat.Pieces).SetEnabled(() => CanKill.GetBool() is false);
        CanKill = BooleanOptionItem.Create(RoleInfo, 15, Op.OpportunistCanKill, true, false);
        KillCool = FloatOptionItem.Create(RoleInfo, 16, GeneralOption.KillCooldown, OptionBaseCoolTime, 40f, false, CanKill)
            .SetValueFormat(OptionFormat.Seconds);
        NeedKills = IntegerOptionItem.Create(RoleInfo, 17, Op.CountKillerVictoryCount, new(0, 14, 1), 1, false, CanKill)
            .SetZeroNotation(OptionZeroNotation.Off).SetValueFormat(OptionFormat.Times);

        CanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, false, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 13, StringNames.EngineerCooldown, OptionBaseCoolTime, 15f, false, CanVent)
            .SetValueFormat(OptionFormat.Seconds).SetEnabled(() => CanKill.GetBool() is false);
        OptionVentIntime = FloatOptionItem.Create(RoleInfo, 14, StringNames.EngineerInVentCooldown, OptionBaseCoolTime, 5f, false, CanVent)
            .SetZeroNotation(OptionZeroNotation.Infinity).SetValueFormat(OptionFormat.Seconds).SetEnabled(() => CanKill.GetBool() is false);

        OverrideTasksData.Create(RoleInfo, 20);
        RoleAddAddons.Create(RoleInfo, 30);
    }
    
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = OptionVentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = OptionVentIntime.GetInt();
    }
    float timer; Vector2 pos;
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (CanKill.GetBool() && NeedKills.GetInt() > 0)
        {
            if (MyState.GetKillCount() < NeedKills.GetInt()) return false;
        }
        else if (NeedTask.GetBool() && !CanKill.GetBool())
        {
            if (!MyTaskState.HasCompletedEnoughCountOfTasks(TaskWin.GetInt())) return false;
        }
        if (Player.IsAlive())
        {
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
            if (PlayerCatch.AllAlivePlayersCount <= 4) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
            if (timer > 100) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[2]);
            if (timer < 10) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[3]);
            return true;
        }
        return false;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            var nowpos = player.GetTruePosition();
            if (nowpos == pos)
            {
                timer += Time.fixedDeltaTime;
            }
            pos = nowpos;
        }
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (CanKill.GetBool() && NeedKills.GetInt() > 0)
        {
            if (MyState.GetKillCount() < NeedKills.GetInt()) return "";
        }
        else if (NeedTask.GetBool() && !CanKill.GetBool())
        {
            if (!MyTaskState.HasCompletedEnoughCountOfTasks(TaskWin.GetInt())) return "";
        }
        if ((seen == seer) && Player.IsAlive()) return Utils.AdditionalAliveWinnerMark;
        return "";
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
        var l2 = new Achievement(RoleInfo, 2, 1, 0, 1, true);
        var sp1 = new Achievement(RoleInfo, 3, 1, 0, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
        achievements.Add(2, l2);
        achievements.Add(3, sp1);
    }
}
