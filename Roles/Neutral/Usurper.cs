using AmongUs.GameOptions;
using UnityEngine;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Roles.Core.Interfaces.ISchrodingerCatOwner;
using Hazel;
using System.Linq;
using System.Collections.Generic;

namespace TownOfHost.Roles.Neutral;

public sealed class Usurper : RoleBase, ILNKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Usurper),
            player => new Usurper(player),
            CustomRoles.Usurper,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            26400,
            SetupOptionItem,
            "usp",
            "#524506",
            (2, 6),
            true
    );
    public Usurper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        CanVent = OptionCanVent.GetBool();
        Killed = false;
        Need = OptNeedKills.GetInt();
        Kings.Clear();
        Telled.Clear();
        Tell = TellCount.GetInt();
    }
    HashSet<byte> Kings = new();
    HashSet<byte> Telled = new();
    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionVision;
    static OptionItem OptNeedKills;
    static OptionItem Cooldown;
    static OptionItem TellCount;
    static OptionItem OptDeadMode;
    static float KillCooldown;
    static bool CanVent;
    int Need;
    public bool Killed;
    int Tell;
    enum OptionName
    {
        UsurperNeedCrewKillCount,
        UsurperTellCooldown,
        UsurperTellCount,
        UsurperKingDeadMode,
        UsurperDeadTime,
    }
    enum DeadMode
    {
        KingDeadModeDie,
        KingDeadModeCh
    }
    enum DeadTime
    {
        UsurperDeadAfter,
        UsurperDead,
    }

    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        Cooldown = FloatOptionItem.Create(RoleInfo, 14, OptionName.UsurperTellCooldown, new(0f, 180f, 0.5f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        TellCount = IntegerOptionItem.Create(RoleInfo, 15, OptionName.UsurperTellCount, new(1, 14, 1), 5, false)
            .SetValueFormat(OptionFormat.Times);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
        OptionVision = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.ImpostorVision, false ,false);
        OptNeedKills = IntegerOptionItem.Create(RoleInfo, 13, OptionName.UsurperNeedCrewKillCount, new(1, 14, 1), 2, false);
        RoleAddAddons.Create(RoleInfo, 20);
    }
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => CanVent;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptionVision.GetBool());
        AURoleOptions.PhantomCooldown = Cooldown.GetFloat();
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        var target = Player.GetKillTarget(true);
        if (target == null) return;

        ResetCooldown = true;
        if (target.Is(CustomRoles.King))
        {
            Kings.Add(target.PlayerId);
        }
        Telled.Add(target.PlayerId);
    }
    public static void CheckWin()
    {
        bool alivecrew = false;
        bool alive = false;

        foreach (var p in PlayerCatch.AllAlivePlayerControls)
        {
            if (p.Is(CustomRoles.Crewmate) && !alivecrew)
            {
                alivecrew = true;
            }
            else if (p.Is(CustomRoles.Usurper) && !alive)
            {
                alive = true;
            }
        }

        if (alivecrew || !alive) return;
        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Usurper, byte.MaxValue))
        {
            foreach (var p in PlayerCatch.AllPlayerControls)
            {
                if (p.GetRoleClass() is Usurper u && u.Killed && u.Need <= 0) CustomWinnerHolder.WinnerIds.Add(p.PlayerId);
            }
        }
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (Killed) return;

        var (killer, target) = info.AttemptTuple;
        if (target.Is(CustomRoles.King))
        {
            info.KillPower = 10;
        }
    }
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        if (target.Is(CustomRoleTypes.Crewmate) && Need > 0)
        {
            Need--;
        }
        if (target.Is(CustomRoles.King) && !Killed)
        {
            RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);
            Killed = true;
            Player.SetKillCooldown();
            bool alivecrew = false;
            foreach (var p in PlayerCatch.AllAlivePlayerControls)
            {
                if (p.Is(CustomRoles.Crewmate))
                {
                    alivecrew = true;
                    break;
                }
            }
            if (!alivecrew) Need = 0;
        }
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var Kill = Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Usurper), $"[{Need}]");
        var tell = Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Usurper), $"({Tell})");
        if (Tell <= 0) tell = Utils.ColorString(Palette.DisabledGrey, $"({Tell})");
        if (Need <= 0) Kill = Utils.ColorString(Palette.AcceptedGreen, $"({Need})");
        if (!Killed) Kill = "";

        return $"{tell}{Kill}";
    }
    public void SendRPC()
    {
        using var sender = CreateSender();
    }

    public override void ReceiveRPC(MessageReader reader)
    {
    }
}