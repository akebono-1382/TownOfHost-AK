using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Jester : RoleBase, IKiller
{
    //Memo
    //エンジニア置き換えのベントをいつかする。
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Jester),
            player => new Jester(player),
            CustomRoles.Jester,
            () => CanUseShape.GetBool() ? RoleTypes.Shapeshifter :
                  CanUseVent.GetBool() || CanOutburst.GetBool() || CanSabotage.GetBool() ? RoleTypes.Impostor : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            14200,
            SetupOptionItem,
            "je",
            "#ec62a5",
            (4, 0),
            true,
            assignInfo: new RoleAssignInfo(CustomRoles.Jester, CustomRoleTypes.Neutral)
            {
                AssignCountRule = new(0, 15, 1)
            },
            from: From.Jester,
            Desc: () =>
            {
                string info = GetString("JesterInfoLong");
                if (CanOutburst.GetBool())
                {
                    info += string.Format(GetString("JesterDescOutburst"), OutburstDelay.GetFloat());
                }

                return info;
            }
        );
    public Jester(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Outburst = OutburstLimit.GetInt();
    }
    static OptionItem CanUseShape;
    static OptionItem CanUseVent;
    static OptionItem Cooldown;
    static OptionItem Duration;
    static OptionItem CanVentMove;
    public static OptionItem CanOutburst;
    static OptionItem OutburstCooldown;
    static OptionItem OutburstLimit;
    static OptionItem OutburstDelay;
    static OptionItem CanSabotage;
    int Outburst;
    enum Op
    {
        JesterCanUseShapeshift, MadmateCanMovedByVent,
        JesterCanOutburst,
        JesterOutburstDelay,
        JesterOutburstCooldown,
        JesterOutburstLimit,
    }
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 8, defo: 1);
        CanUseShape = BooleanOptionItem.Create(RoleInfo, 3, Op.JesterCanUseShapeshift, false, false);
        Cooldown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, new(0f, 180f, 0.5f), 30f, false, CanUseShape).SetValueFormat(OptionFormat.Seconds);
        Duration = FloatOptionItem.Create(RoleInfo, 5, GeneralOption.ShapeDuration, new(0f, 180f, 0.5f), 5f, false, CanUseShape).SetZeroNotation(OptionZeroNotation.Infinity).SetValueFormat(OptionFormat.Seconds);
        CanUseVent = BooleanOptionItem.Create(RoleInfo, 6, GeneralOption.CanVent, false, false);
        CanVentMove = BooleanOptionItem.Create(RoleInfo, 7, Op.MadmateCanMovedByVent, false, false, CanUseVent);
        CanSabotage = BooleanOptionItem.Create(RoleInfo, 13, GeneralOption.CanUseSabotage, true, false);

        CanOutburst = BooleanOptionItem.Create(RoleInfo, 14, Op.JesterCanOutburst, true, false);
        OverrideKilldistance.Create(RoleInfo, 9);
        OutburstCooldown = FloatOptionItem.Create(RoleInfo, 10, Op.JesterOutburstCooldown, OptionBaseCoolTime, 20f, false, CanOutburst)
            .SetValueFormat(OptionFormat.Seconds);
        OutburstDelay = FloatOptionItem.Create(RoleInfo, 11, Op.JesterOutburstDelay, OptionBaseCoolTime, 5f, false, CanOutburst)
            .SetValueFormat(OptionFormat.Seconds);
        OutburstLimit = IntegerOptionItem.Create(RoleInfo, 12, Op.JesterOutburstLimit, new(1, 99, 1), 5, false, CanOutburst)
            .SetValueFormat(OptionFormat.Times);
    }
    public bool CanUseImpostorVentButton() => CanUseVent.GetBool();
    public override bool CanUseAbilityButton() => CanUseShape.GetBool();
    public bool CanUseSabotageButton() => CanSabotage.GetBool();
    public override bool OnInvokeSabotage(SystemTypes systemType) => CanSabotage.GetBool();
    public bool CanKill { get; private set; } = false;
    public bool CanUseKillButton() => CanOutburst.GetBool() && Outburst > 0;
    float IKiller.CalculateKillCooldown() => Outburst > 0 ? OutburstCooldown.GetFloat() : 255f;
    void IKiller.OnCheckMurderAsKiller(MurderInfo info)
    {
        info.DoKill = false;
        if (Outburst <= 0) return;
        if (!CanOutburst.GetBool()) return;
        Player.SetKillCooldown();

        var target = info.AttemptTarget;
        if (target.GetRoleClass() is not IKiller killer) return;
        if (!killer.CanUseKillButton()) return;
        if (target.GetCustomRole() is
            CustomRoles.Arsonist or CustomRoles.Chef or CustomRoles.CurseMaker or
            CustomRoles.Koala or CustomRoles.MassMedia or CustomRoles.PhantomThief or
            CustomRoles.Remotekiller or CustomRoles.PlagueDoctor or CustomRoles.Strawdoll or
            CustomRoles.Turncoat or CustomRoles.Jester or CustomRoles.Vega or CustomRoles.MadHacker or
            CustomRoles.MadSuicide or CustomRoles.MadChanger or CustomRoles.Fortuner or CustomRoles.NiceLogger or
            CustomRoles.UltraStar
            ) return;
        if (target.GetRoleClass() is Nue nue)
        {
            if (nue.IsKilled || !nue.GuessedFool) return;
        }
        if (target.Is(CustomRoles.Opportunist) && !Opportunist.CanKill.GetBool()) return;

        --Outburst;
        SendRPC();

        _ = new LateTask(() =>
        {
            var targ = target.GetKillTarget(true);
            if (targ == null) return;
            if (Is(targ)) return;

            if (CustomRoleManager.OnCheckMurder(target, targ, target, targ))
            {
                RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
            }
        }, OutburstDelay.GetFloat(), "");
    }
    bool IKiller.OverrideKillButtonText(out string text)
    {
        text = GetString("JesterOutburst");
        if (CanOutburst.GetBool() is false) return false;
        return true;
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (!CanOutburst.GetBool()) return "";
        return Utils.ColorString(
            Outburst <= 0 ? Color.gray : UtilsRoleText.GetRoleColor(CustomRoles.Jester),
            $"({Outburst})"
            );
    }
    void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked(Outburst);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        Outburst = reader.ReadPackedInt32();
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = Cooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = Duration.GetFloat();
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
        opt.SetVision(false);
    }
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => CanVentMove.GetBool();
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player)) return;
        if (!AmongUsClient.Instance.AmHost || Player.PlayerId != exiled.PlayerId) return;

        if (CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Jester, Player.PlayerId))
        {
            CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        }
        Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
        if (10 <= UtilsGameLog.LastLogRole.Count && PlayerCatch.AllAlivePlayersCount <= 3)
            DecidedWinner = true;
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var sp1 = new Achievement(RoleInfo, 1, 1, 0, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, sp1);
    }
}