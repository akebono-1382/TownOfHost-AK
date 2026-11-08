using System;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

//Memo
//予感実装する場合ベント/ファントムで切り替える

//キル > 0 タスク > 0の時　ペット切り替え
//キル > 0 タスク = 0の時　インポ固定
//キル = 0 タスク > 0の時　クルー固定

public sealed class Alice : RoleBase, IKiller, IPetTrigger
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Alice),
            player => new Alice(player),
            CustomRoles.Alice,
            () => NeedKills.GetInt() > 0 ? RoleTypes.Impostor : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            26600,
            SetupOptionItem,
            "Alc",
            "#ffff0f",
            (6, 7),
            from: From.ExtremeRoles,
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter),
            Desc: () =>
            {
                if (NeedKills.GetInt() > 0 && NeedTask.GetInt() == 0)
                {
                    return string.Format(GetString("AliceDescKill"), NeedKills.GetInt(), NeedGuard.GetInt());
                }
                else if (NeedKills.GetInt() == 0 && NeedTask.GetInt() > 0)
                {
                    return string.Format(GetString("AliceDescTask"), NeedTask.GetInt(), NeedGuard.GetInt());
                }
                else if (NeedKills.GetInt() > 0 && NeedTask.GetInt() > 0)
                {
                    return string.Format(GetString("AliceDescKillTask"), NeedKills.GetInt(), NeedTask.GetInt(), NeedGuard.GetInt()) + GetString("AliceDescMode");
                }
                return string.Format(GetString("AliceDescNone"), NeedGuard.GetInt());
            }
        );

    public Alice(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => NeedTask.GetInt() <= 0 ? HasTask.False : HasTask.ForRecompute
    )
    {
        NowKillCool = KillCool.GetFloat();
        TaskMode = NeedKills.GetInt() <= 0;
        GuardCount = 0;
    }

    private static OptionItem NeedGuard;
    public static OptionItem NeedTask;
    private static OptionItem NeedKills;
    private static OptionItem KillCool;
    private static OptionItem CanSabo;
    private static OptionItem Impvis;
    int GuardCount;
    float NowKillCool;
    bool TaskMode;
    bool NeedKill() => NeedKills.GetInt() > 0;
    bool NeedTasks() => NeedTask.GetInt() > 0;
    public bool Comp() => MyTaskState.HasCompletedEnoughCountOfTasks(NeedTask.GetInt()) || IsTaskFinished || NeedTask.GetInt() == 0;
    public bool CanKill => NeedKills.GetInt() > 0 && !TaskMode;
    public float CalculateKillCooldown() => KillCool.GetFloat();
    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => CanSabo.GetBool();
    enum OptionName
    {
        AliceNeedGuard,
        AliceNeedTasks,
        AliceNeedKill,
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9);
        NeedGuard = IntegerOptionItem.Create(RoleInfo, 10, OptionName.AliceNeedGuard, new(1, 99, 1), 2, false)
            .SetValueFormat(OptionFormat.Times);
        NeedTask = IntegerOptionItem.Create(RoleInfo, 11, OptionName.AliceNeedTasks, new(0, 255, 1), 5, false)
            .SetZeroNotation(OptionZeroNotation.Off).SetValueFormat(OptionFormat.Pieces);
        NeedKills = IntegerOptionItem.Create(RoleInfo, 12, OptionName.AliceNeedKill, new(0, 14, 1), 2, false)
            .SetZeroNotation(OptionZeroNotation.Off).SetValueFormat(OptionFormat.Times);
        KillCool = FloatOptionItem.Create(RoleInfo, 13, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false, NeedKills)
            .SetEnabled(() => NeedKills.GetInt() > 0).SetValueFormat(OptionFormat.Seconds);
        CanSabo = BooleanOptionItem.Create(RoleInfo, 14, GeneralOption.CanUseSabotage, false, false, NeedKills)
            .SetEnabled(() => NeedKills.GetInt() > 0);
        Impvis = BooleanOptionItem.Create(RoleInfo, 15, GeneralOption.ImpostorVision, false, false, NeedKills)
            .SetEnabled(() => NeedKills.GetInt() > 0);
        OverrideTasksData.Create(RoleInfo, 20);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(Impvis.GetBool());
    }
    /// <summary>
    /// アリスだけ先判定じゃダメなので個別
    /// </summary>
    public new bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (info.KillPower > 9 || !Comp() || MyState.GetKillCount(false) < NeedKills.GetInt()) return true;

        if (TaskMode)
        {
            Player.RpcProtectedMurderPlayer();
        }
        else
        {
            var cool = Math.Max(NowKillCool, 0.1f);
            Player.SetKillCooldown(cool);
        }

        //強制的にfalseでブロックしてるけど、一応
        info.GuardPower = 9;
        GuardCount++;
        if (GuardCount >= NeedGuard.GetInt())
        {
            CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Alice, Player.PlayerId);
        }
        return false;
    }
    public override void OnSpawn(bool initialState = false)
    {
        NowKillCool = KillCool.GetFloat();
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (NowKillCool <= 0f) return;
        var old = NowKillCool;
        NowKillCool -= Time.fixedDeltaTime;
        if (Math.Ceiling(old) - Math.Ceiling(NowKillCool) >= 1)
            SendRPC();
    }
    public override void OnStartMeeting() => NowKillCool = KillCool.GetFloat();
    public override void AfterMeetingTasks() => NowKillCool = KillCool.GetFloat();
    void IKiller.OnMurderPlayerAsKiller(MurderInfo info)
    {
        NowKillCool = KillCool.GetFloat();
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var mode = TaskMode ? "[タスク]" : "[キル]";
        var kill = !NeedKill() ? "": Utils.ColorString(MyState.GetKillCount(false) >= NeedKills.GetInt() ? Color.green : Color.gray,
            $"[{MyState.GetKillCount(false)}/{NeedKills.GetInt()}]");

        return $"{kill}<#ffff0f>({GuardCount}/{NeedGuard.GetInt()}){mode}</color>";
    }
    void SetRoleForAliceClient(PlayerControl target, RoleTypes role)
    {
        if (target == null || !target.IsAlive() || target.GetClientId() == -1) return;

        if (target == PlayerControl.LocalPlayer && Is(PlayerControl.LocalPlayer))
        {
            RoleManager.Instance.SetRole(target, role);
        }
        target.RpcSetRoleDesync(role, target.GetClientId());
    }
    bool IPetTrigger.OnPetUsed()
    {
        bool NeedModeswitch = NeedTasks() && NeedKill();
        if (!NeedModeswitch) return true;
        TaskMode = !TaskMode;
        SendRPC();

        if (TaskMode)
        {
            SetRoleForAliceClient(Player, RoleTypes.Crewmate);
        }
        else
        {
            SetRoleForAliceClient(Player, RoleTypes.Impostor);
            _ = new LateTask(() =>
            {
                if (Player == null || !Player.IsAlive() || TaskMode) return;
                var cool = Math.Max(NowKillCool, 0.1f);
                Logger.Info($"Alice restore killcool: {cool}", "Alice");
                Player.SetKillCooldown(cool);
            }, Main.LagTime + 0.2f, "Alice SetKillCooldown", true);
        }

        return false;
    }

    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(NowKillCool);
        sender.Writer.WritePacked(GuardCount);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        NowKillCool = reader.ReadSingle();
        GuardCount = reader.ReadPackedInt32();
    }
}
