using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using InnerNet;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Madmate;

public sealed class MadSuicide : RoleBase, IKiller, IUsePhantomButton, IKillFlashSeeable, IDeathReasonSeeable, IPetTrigger
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadSuicide),
            player => new MadSuicide(player),
            CustomRoles.MadSuicide,
            () =>
            {
                var mode = (Modes)OptionMode.GetValue();
                if (mode is Modes.PetandReplaceCrew)
                {
                    if (OptionCanusevent.GetBool())
                        return RoleTypes.Engineer;
                    else
                        return RoleTypes.Crewmate;
                }
                else if (mode is Modes.Kill)
                {
                    return RoleTypes.Impostor;
                }
                return RoleTypes.Phantom;
            },
            CustomRoleTypes.Madmate,
            7700,
            SetupOptionItem,
            "MSu",
            OptionSort: (2, 1),
            isDesyncImpostor: true,
            introSound: () => GetIntroSound(RoleTypes.Impostor),
            from: From.SuperNewRoles
        );
    public MadSuicide(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        CoolDownTimer = OptionAbilityCoolDown.GetFloat();
        flug = false;
    }
    static OptionItem OptionKillCoolDown;
    static OptionItem OptionKillDeathreason;
    static OptionItem OptionAbilityCoolDown;
    static OptionItem OptionAbilityDeathreason;
    static OptionItem OptionCanusevent;
    static OptionItem OptionKillSuicidetargetkiller;
    static OptionItem OptionMode;
    static OptionItem OptionVentCooldown;
    static OptionItem OptionVentIntime;
    public static readonly CustomDeathReason[] deathReasons =
    {
        CustomDeathReason.Kill,CustomDeathReason.Suicide,CustomDeathReason.Misfire,CustomDeathReason.Counter
    };
    enum Modes
    {
        OcKillPet,
        OcKill,
        OcPet,
        Oc,
        Kill,
        PetandReplaceCrew,
    }

    private static readonly string[] ModeText =
    {
        "MadSuicide.Mode.OcAkiAP",
        "MadSuicide.Mode.OcAKi",
        "MadSuicide.Mode.OcAP",
        "MadSuicide.Mode.Oc",
        "MadSuicide.Mode.Kill",
        "MadSuicide.Mode.Pet",
    };
    enum OptionName
    {
        MadSuicideKillDeathreason,
        MadSuicideAbilityDeathreason,
        MadSuicideKillSuicidetargetkiller,
        MadSuicideMode
    }

    static void SetupOptionItem()
    {
        var cRolesString = deathReasons.Select(x => $"DeathReason.{x}".ToString()).ToArray();
        OptionMode = StringOptionItem.Create(RoleInfo, 23, OptionName.MadSuicideMode, ModeText, 1, false);
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 20f, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetEnabled(() => (Modes)OptionMode.GetValue() is not Modes.OcPet and not Modes.Oc and not Modes.PetandReplaceCrew);
        OptionKillDeathreason = StringOptionItem.Create(RoleInfo, 11, OptionName.MadSuicideKillDeathreason, cRolesString, 2, false)
            .SetEnabled(() => (Modes)OptionMode.GetValue() is not Modes.OcPet and not Modes.Oc and not Modes.PetandReplaceCrew);
        OptionKillSuicidetargetkiller = BooleanOptionItem.Create(RoleInfo, 12, OptionName.MadSuicideKillSuicidetargetkiller, false, false)
            .SetEnabled(() => (Modes)OptionMode.GetValue() is not Modes.OcPet and not Modes.Oc and not Modes.PetandReplaceCrew);
        OptionAbilityCoolDown = FloatOptionItem.Create(RoleInfo, 15, GeneralOption.Cooldown, OptionBaseCoolTime, 20f, false).SetValueFormat(OptionFormat.Seconds)
            .SetEnabled(() => (Modes)OptionMode.GetValue() is not Modes.Kill);
        OptionAbilityDeathreason = StringOptionItem.Create(RoleInfo, 16, OptionName.MadSuicideAbilityDeathreason, cRolesString, 0, false)
            .SetEnabled(() => (Modes)OptionMode.GetValue() is not Modes.Kill);
        OptionCanusevent = BooleanOptionItem.Create(RoleInfo, 20, GeneralOption.CanVent, true, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 21, StringNames.EngineerCooldown, OptionBaseCoolTime, 15, false, OptionCanusevent)
            .SetValueFormat(OptionFormat.Seconds)
            .SetEnabled(() => OptionCanusevent.GetBool() && (Modes)OptionMode.GetValue() is Modes.PetandReplaceCrew);
        OptionVentIntime = FloatOptionItem.Create(RoleInfo, 22, StringNames.EngineerInVentCooldown, OptionBaseCoolTime, 5, false, OptionCanusevent)
            .SetZeroNotation(OptionZeroNotation.Infinity).SetValueFormat(OptionFormat.Seconds)
            .SetEnabled(() => OptionCanusevent.GetBool() && (Modes)OptionMode.GetValue() is Modes.PetandReplaceCrew);
        RoleAddAddons.Create(RoleInfo, 30, MadMate: true);
    }
    public bool? CheckKillFlash(MurderInfo info) => Options.MadmateCanSeeKillFlash.GetBool();
    public bool? CheckSeeDeathReason(PlayerControl seen) => Options.MadmateCanSeeDeathReason.GetBool();
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = OptionAbilityCoolDown.GetFloat();
        AURoleOptions.EngineerCooldown = OptionVentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = OptionVentIntime.GetFloat();
    }
    public bool CanUseKillButton() => (Modes)OptionMode.GetValue() is Modes.Kill or Modes.OcKill or Modes.OcKillPet;
    public bool CanUseImpostorVentButton() => OptionCanusevent.GetBool() && (Modes)OptionMode.GetValue() is not Modes.PetandReplaceCrew;
    public bool CanUseSabotageButton() => false;
    public float CalculateKillCooldown() => OptionKillCoolDown.GetFloat();
    public override bool CanUseAbilityButton() =>
        (Modes)OptionMode.GetValue() is Modes.PetandReplaceCrew ? OptionCanusevent.GetBool() :
        (Modes)OptionMode.GetValue() is not Modes.PetandReplaceCrew and not Modes.Kill;

    bool IUsePhantomButton.IsPhantomRole => (Modes)OptionMode.GetValue() is not Modes.PetandReplaceCrew and not Modes.Kill;
    bool IUsePhantomButton.UseOneclickButton => (Modes)OptionMode.GetValue() is not Modes.PetandReplaceCrew and not Modes.Kill;
    bool IKiller.IsKiller => false;
    float CoolDownTimer;
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        info.DoKill = false;

        if (!Player.IsAlive()) return;
        MyState.DeathReason = deathReasons[OptionKillDeathreason.GetValue()];
        if (OptionKillSuicidetargetkiller.GetBool())
        {
            foreach (var seer in PlayerCatch.AllPlayerControls)
            {
                if (seer == null) return;
                if (!GameStates.InGame) break;

                if (seer.AmOwner)
                {
                    if (target == seer) killer.MurderPlayer(killer, MurderResultFlags.Succeeded);
                    else target.MurderPlayer(killer, MurderResultFlags.Succeeded);
                }
                else
                {
                    var t = target;
                    if (target == seer) t = killer;//ターゲットとキラーを入れ替えてるだけ★
                    //ターゲット視点は自爆に見える
                    MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(t.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, seer.GetClientId());
                    messageWriter.WriteNetObject(killer);
                    messageWriter.Write((int)MurderResultFlags.Succeeded);
                    AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
                }
            }
            flug = true;
            Player.SetRealKiller(Player);
            return;
        }
        Player.SetRealKiller(Player);
        killer.RpcMurderPlayer(killer);
    }
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (!Player.IsAlive()) return;
        AdjustKillCooldown = true;
        ResetCooldown = true;
        Suicide();
    }

    void Suicide()
    {
        if (flug || !Player.IsAlive()) return;

        flug = true;
        MyState.DeathReason = deathReasons[OptionAbilityDeathreason.GetValue()];
        Player.RpcMurderPlayer(Player);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (CoolDownTimer <= 0f) return;
        CoolDownTimer -= Time.fixedDeltaTime;
    }

    public override void OnSpawn(bool initialState = false)
    {
        CoolDownTimer = OptionAbilityCoolDown.GetFloat();
    }

    bool IPetTrigger.OnPetUsed()
    {
        if (CoolDownTimer > 0.1f ||
            (Modes)OptionMode.GetValue() is not Modes.PetandReplaceCrew
            and not Modes.OcKillPet
            and not Modes.OcPet
            ) return false;
        Suicide();
        return true;
    }

    public bool OverrideKillButtonText(out string text) { text = GetString("Suicide"); return true; }
    public override string GetAbilityButtonText()
    {
        return GetString("Suicide");
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "MadSuicide_Ability";
        return true;
    }
    bool IKiller.OverrideKillButton(out string text)
    {
        text = "MadSuicide_Kill";
        return true;
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen.PlayerId != seer.PlayerId || isForMeeting || !Player.IsAlive()) return "";

        if (isForHud) return GetString("MadSuicideLowerInfo");
        return $"<size=50%>{GetString("MadSuicideLowerInfo")}</size>";
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (flug && (target?.PlayerId ?? byte.MaxValue) == Player.PlayerId) return;
        flug = false;
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (flug && exiled is not null) Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[0]);
    }
    bool flug;
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 1);
        achievements.Add(0, n1);
    }
}
