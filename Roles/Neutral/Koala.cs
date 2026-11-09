using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Koala : RoleBase, IUsePhantomButton, IKiller
{
    enum RPC_Calls
    {
        Sleep,
        SyncSleepTime,
    }

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Koala),
            player => new Koala(player),
            CustomRoles.Koala,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            26900,
            SetupCustomOption,
            "kla",
            "#00FEFC",
            (5, 8),
            Desc: () =>
                string.Format(GetString("KoalaDesc"), OptWinTime.GetFloat())
        );
    public Koala(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Cooldown = OptCooldown.GetFloat();
        WinTime = OptWinTime.GetInt();
        CanReport = OptCanReport.GetBool();
    }
    static OptionItem OptCooldown; static float Cooldown; //眠りクールダウン
    static OptionItem OptWinTime; static float WinTime; //勝てる眠り時間
    static OptionItem OptCanReport; static bool CanReport;
    float NowSleepTime; //今の眠り時間
    bool NowSleep; //眠っているか
    float tmpspeed; //元の速度
    public bool CanKill => false;
    public bool CanUseImpostorVentButton() => false;
    public override bool CanClickUseVentButton => false;
    enum OptionName
    {
        KoalaWinSleepTime,
        KoalaCanReport,
    }

    static void SetupCustomOption()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 2);
        OptCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptWinTime = IntegerOptionItem.Create(RoleInfo, 11, OptionName.KoalaWinSleepTime, new(5, 500, 5), 200, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptCanReport = BooleanOptionItem.Create(RoleInfo, 12, OptionName.KoalaCanReport, false, false);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        AURoleOptions.PhantomCooldown = Cooldown;
        AURoleOptions.EngineerCooldown = WinTime;
    }
    void SetRoleForKoalaClient(PlayerControl target, RoleTypes role)
    {
        if (target == null || !target.IsAlive() || target.GetClientId() == -1) return;

        if (target == PlayerControl.LocalPlayer && Is(PlayerControl.LocalPlayer))
        {
            RoleManager.Instance.SetRole(target, role);
            return;
        }
        target.RpcSetRoleDesync(role, target.GetClientId());
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = null;
        if (!AmongUsClient.Instance.AmHost) return;

        NowSleep = true;
        NowSleepTime = WinTime;
        SendSleepRPC();
        SetRoleForKoalaClient(Player, RoleTypes.Engineer);
        tmpspeed = Main.AllPlayerSpeed[Player.PlayerId];
        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        Player.MarkDirtySettings();
        if (Player.PlayerId == 0)
        {
            Player.RpcResetAbilityCooldown();
            return;
        }
        _ = new LateTask(() =>
        {
            Player.RpcResetAbilityCooldown();
        }, Main.LagTime + 0.1f, "");
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!NowSleep) return;
        int old = (int)NowSleepTime;

        NowSleepTime -= Time.fixedDeltaTime;

        if (NowSleepTime <= 0f)
        {
            NowSleep = false;
            NowSleepTime = 999f;
            CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Koala, Player.PlayerId);
            return;
        }

        int now = (int)NowSleepTime;
        if (old != now)
        {
            SendTimeRPC();
        }
    }
    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
    {
        if (!Is(reporter) || target.PlayerId == Player.PlayerId || !NowSleep || CanReport) return false;
        reason = DontReportreson.Other;
        return true;
    }
    void SendSleepRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write((byte)RPC_Calls.Sleep);
        sender.Writer.Write(NowSleep);
        sender.Writer.Write(NowSleepTime);
    }
    void SendTimeRPC()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write((byte)RPC_Calls.SyncSleepTime);
        sender.Writer.Write(NowSleepTime);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        RPC_Calls type = (RPC_Calls)reader.ReadByte();
        if (type == RPC_Calls.Sleep)
        {
            NowSleep = reader.ReadBoolean();
            NowSleepTime = reader.ReadSingle();
        }
        else if (type == RPC_Calls.SyncSleepTime)
        {
            NowSleepTime = reader.ReadSingle();
        }
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        NowSleep = false;
        NowSleepTime = 999f;
        Main.AllPlayerSpeed[Player.PlayerId] = tmpspeed;
        Player.MarkDirtySettings();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!NowSleep) return "";

        return Utils.ColorString(
            UtilsRoleText.GetRoleColor(CustomRoles.Koala),
            "Zzz..."
            );
    }
}
