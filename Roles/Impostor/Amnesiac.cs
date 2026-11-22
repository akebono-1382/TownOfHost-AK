using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using UnityEngine;
using static TownOfHost.Roles.Impostor.Assassin;

namespace TownOfHost.Roles.Impostor;

public sealed class Amnesiac : RoleBase, IImpostor, IUsePhantomButton, IDoubleTrigger, IKillFlashSeeable, IPetTrigger
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Amnesiac),
            player => new Amnesiac(player),
            CustomRoles.Amnesiac,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            7200,
            SetupCustomOption,
            "am",
            "#f8cd46",
            OptionSort: (8, 0),
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            isCantSeeTeammates: true,
            from: From.TownOfHost_K
        );
    public Amnesiac(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CantKillImpostor = OptCantKillImpostor.GetBool();
        //MatchSettingstoSheriff = OptMatchSettingstoSheriff.GetBool();
        MatchSettingstoSheriff = true;
        CanRealize = OptRealize.GetBool();
        ImpNeedtoKill = OptImpNeedtoKill.GetBool();
        NeedtoKill = OptNeedtoKill.GetBool();
        KillsRequired = OptKillsRequired.GetInt();
        CanUseVent = OptCanUseVent.GetBool();
        CanUseSabotage = OptCanUseSabotage.GetBool();
        IsWolf = OptIsWolfBoy.GetBool();
        ShShotLimit = IsWolf ? WolfBoy.ShotLimitOpt.GetInt() : Sheriff.ShotLimitOpt.GetInt();
        ShKillCooldown = IsWolf ? WolfBoy.KillCooldown.GetFloat() : Sheriff.KillCooldown.GetFloat();
        ShCanKillAllAlive = IsWolf ? WolfBoy.CanKillAllAlive.GetBool() : Sheriff.CanKillAllAlive.GetBool();
        IsNeedLastImpostor = OptNeedtoLastImpostor.GetBool();

        Realized = false;
        KillCount = 0;
    }

    static OptionItem OptCantKillImpostor;
    //static OptionItem OptMatchSettingstoSheriff;
    static OptionItem OptRealize;
    static OptionItem OptionHasOtherRole;
    public static FilterOptionItem OptionHaveRole; static CustomRoles haverole;
    static OptionItem OptImpNeedtoKill;
    static OptionItem OptNeedtoKill;
    static OptionItem OptKillsRequired;
    static OptionItem OptCanUseVent;
    static OptionItem OptCanUseSabotage;
    static OptionItem OptNeedtoLastImpostor;
    static OptionItem OptIsWolfBoy;
    static OptionItem KillCooldown;
    public static bool CantKillImpostor;
    public static bool MatchSettingstoSheriff;
    public static bool CanRealize;
    public static bool ImpNeedtoKill;
    public static bool NeedtoKill;
    public static int KillsRequired;
    public static bool CanUseVent;
    public static bool CanUseSabotage;
    public static int ShShotLimit;
    public static float ShKillCooldown;
    public static bool ShCanKillAllAlive;
    public static bool IsWolf;
    static bool IsNeedLastImpostor;

    public bool Realized;
    public int KillCount;
    RoleBase AddRole;
    static CustomRoles[] InvalidRoles()
    {
        List<CustomRoles> InvalidRoles = new();
        foreach (var data in CustomRoleManager.AllRolesInfo)
        {
            if (data.Key.IsImpostor() is false && data.Key
                is not CustomRoles.Remotekiller and not CustomRoles.GrimReaper
                and not CustomRoles.Havoc and not CustomRoles.Raccoon
                && data.Key is not CustomRoles.NotAssigned) continue;

            if (data.Key is CustomRoles.AlienHijack or CustomRoles.Mafia or CustomRoles.Godfather
                         or CustomRoles.Janitor or CustomRoles.Amnesiac
                         or CustomRoles.Limiter or CustomRoles.Assassin or CustomRoles.Driver)
            {
                InvalidRoles.Add(data.Key);
                continue;
            }
        }
        return InvalidRoles.ToArray();
    }
    enum Options
    {
        AmnesiacCantKillImpostor,
        AmnesiacMatchSettingstoSheriff,
        AmnesiacRealize,//←がONの状態での追加設定↓
        AssasinHasOtherRole,
        AssassinHaveRole,
        AmnesiacImpNeedtoKill,
        AmnesiacNeedtoKill,
        AmnesiacKillsRequired,
        AmnesiacNeedtoLastImpostor,
        AmnesiacCanUseVent,
        AmnesiacCanUseSabotage,
        AmnesiacIsWolfboy,
        AmnesiacKillCooldown,
    }

    public static void SetupCustomOption()
    {
        OptCantKillImpostor = BooleanOptionItem.Create(RoleInfo, 10, Options.AmnesiacCantKillImpostor, true, false);
        //OptMatchSettingstoSheriff = BooleanOptionItem.Create(RoleInfo, 11, Options.AmnesiacMatchSettingstoSheriff, true, false);
        OptRealize = BooleanOptionItem.Create(RoleInfo, 12, Options.AmnesiacRealize, false, false);
        KillCooldown = FloatOptionItem.Create(RoleInfo, 23, Options.AmnesiacKillCooldown, OptionBaseCoolTime, 20f, false, OptRealize)
            .SetValueFormat(OptionFormat.Seconds);
        OptionHasOtherRole = BooleanOptionItem.Create(RoleInfo, 21, Options.AssasinHasOtherRole, false, false, OptRealize);
        OptionHaveRole = FilterOptionItem.Create(RoleInfo, 22, Options.AssassinHaveRole, 0, false, OptionHasOtherRole, true, false, false, false, false, () => InvalidRoles());
        OptImpNeedtoKill = BooleanOptionItem.Create(RoleInfo, 13, Options.AmnesiacImpNeedtoKill, false, false, OptRealize);
        OptNeedtoKill = BooleanOptionItem.Create(RoleInfo, 14, Options.AmnesiacNeedtoKill, false, false, OptRealize);
        OptKillsRequired = IntegerOptionItem.Create(RoleInfo, 15, Options.AmnesiacKillsRequired, new(1, 6, 1), 2, false, OptNeedtoKill);
        OptNeedtoLastImpostor = BooleanOptionItem.Create(RoleInfo, 18, Options.AmnesiacNeedtoLastImpostor, false, false, OptRealize);
        OptCanUseVent = BooleanOptionItem.Create(RoleInfo, 16, Options.AmnesiacCanUseVent, false, false, OptRealize);
        OptCanUseSabotage = BooleanOptionItem.Create(RoleInfo, 17, Options.AmnesiacCanUseSabotage, false, false, OptRealize);
        OptIsWolfBoy = BooleanOptionItem.Create(RoleInfo, 20, Options.AmnesiacIsWolfboy, false, false);
    } 
    //public override CustomRoles Misidentify() => Realized ? CustomRoles.NotAssigned : IsWolf ? CustomRoles.WolfBoy : CustomRoles.Sheriff;
    public override void OnDestroy()
    {
        if (AddRole is not null)
        {
            AddRole?.OnDestroy();
        }
        AddRole = null;
        haverole = CustomRoles.NotAssigned;
    }
    public override void OverrideTrueRoleName(ref Color roleColor, ref string roleText) =>
        //記憶喪失者表示いらないかも。
        roleText = Translator.GetString($"{haverole}").Color(UtilsRoleText.GetRoleColor(haverole)) + Translator.GetString("Amnesiac");
    //併せ持ちのが短かったらそっちにする
    public float CalculateKillCooldown() =>
        MatchSettingstoSheriff && !Realized ? ShKillCooldown :
        Realized && AddRole is not null && AddRole is IKiller kl && kl.CalculateKillCooldown() < KillCooldown.GetFloat() ? kl.CalculateKillCooldown()
        : KillCooldown.GetFloat();

    public bool CanUseImpostorVentButton() =>
        Realized && /*AddRole is not null && AddRole is IKiller kl ? kl.CanUseImpostorVentButton()
        : */ CanUseVent;

    public bool CanUseSabotageButton() => Realized && CanUseSabotage;
    public bool CanUseKillButton()
    {
        if (!Player.IsAlive()) return false;
        if (!MatchSettingstoSheriff || Realized) return true;
        return ShCanKillAllAlive || GameStates.AlreadyDied || (AddRole is not null && AddRole is IKiller kl && kl.CanUseKillButton());
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (AddRole is not null)
        {
            AddRole.ApplyGameOptions(opt);
        }
        opt.SetVision(Realized || (MatchSettingstoSheriff && IsWolf && WolfBoy.ImpostorVision.GetBool()));
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (!Is(killer)) return;
        if (AddRole is not null && AddRole is IKiller kl)
        {
            kl.OnCheckMurderAsKiller(info);
            return;
        }
        if (Realized) return;

        if (CantKillImpostor && (target.GetCustomRole().IsImpostor() || target.GetCustomRole() is CustomRoles.Egoist))
            info.DoKill = false;
        if (CanRealize && ImpNeedtoKill && !Realized)
            Realize();
    }
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (!Is(killer)) return;
        if (AddRole is not null && AddRole is IKiller kl)
        {
            kl.OnCheckMurderAsKiller(info);
        }

        KillCount++;
        if (CanRealize && NeedtoKill && !Realized && KillCount >= KillsRequired)
            Realize();
    }

    private void Realize()
    {
        Realized = true;
        SendRPC();

        var clientId = Player.GetClientId();
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            var role = pc.GetCustomRole();
            if (!role.IsImpostor() && role is not CustomRoles.Egoist) continue;

            RoleTypes roleTypes = role.GetRoleTypes();
            if (!pc.IsAlive()) roleTypes = RoleTypes.ImpostorGhost;
            pc.RpcSetRoleDesync(roleTypes, clientId);
        }

        if (!Utils.RoleSendList.Contains(Player.PlayerId))
            Utils.RoleSendList.Add(Player.PlayerId);

        if (OptionHasOtherRole.GetBool())
        {
            haverole = OptionHaveRole.GetRole();
            if (CustomRoleManager.AllRolesInfo.TryGetValue(haverole, out var roleinfo))
            {
                AddRole = roleinfo.CreateInstance(Player);
                Player.RpcSetRole(roleinfo.BaseRoleType(), true);
                Logger.Info($"Assasin + {haverole}", "Assasin");
            }
        }
        else
        {
            AddRole = null;
            haverole = CustomRoles.NotAssigned;
        }
        AddRole?.Add();
        Player.AddDoubleTrigger();
        CustomButtonHud.BottonHud();

        Player.SetKillCooldown();
        UtilsNotifyRoles.NotifyRoles();
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !player.IsAlive()) return;

        AddRole?.OnFixedUpdate(player);

        if (Realized || !IsNeedLastImpostor || !MyState.HasSpawned) return;

        if (PlayerCatch.AliveImpostorCount is 1)
        {
            Realize();
        }
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        seer ??= Player;
        if (seer.GetCustomRole().IsImpostor() || seer.Is(CustomRoles.Egoist))
        {
            roleColor = Vanilla.Impostor.RoleInfo.RoleColor;
            roleText = Realized ? roleText : GetString("Amnesiac");
        }
        //本人にはシェリフ、インポスターにはロールカラーを赤に
        if (Is(seer))
        {
            roleColor = IsWolf ? WolfBoy.RoleInfo.RoleColor : Sheriff.RoleInfo.RoleColor;
            roleText = Realized ? roleText : (IsWolf ? GetString(CustomRoles.WolfBoy.ToString()) : GetString(CustomRoles.Sheriff.ToString()));
        }
        if (!seer.IsAlive() && !Realized)
        {
            roleText += $"(<#ff1919>{GetString("Amnesiac")}</color>)";
        }
        AddRole?.OverrideDisplayRoleNameAsSeen(seer, ref enabled, ref roleColor, ref roleText, ref addon);
    }
    public bool OverrideKillButton(out string text)
    {
        text = IsWolf ? "WolfBoy_Kill" : "Sheriff_Kill";

        if (AddRole is not null && AddRole is IKiller kl)
        {
            bool a = kl.OverrideKillButton(out var tx);
            text = tx;
            return a;
        }
        return !Realized;
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false) =>
        AddRole != null ? AddRole.GetProgressText(comms, gamelog) : MatchSettingstoSheriff && !Realized ? Utils.ColorString(ShShotLimit - KillCount > 0 ? Color.yellow : Color.gray, $"({ShShotLimit - KillCount})") : "";

    #region addrole
    public override void OnExitVent(PlayerPhysics physics, int ventId) => AddRole?.OnExitVent(physics, ventId);
    public override void OnSpawn(bool initialState = false) => AddRole?.OnSpawn(initialState);
    public override void OnStartMeeting() => AddRole?.OnStartMeeting();
    public override bool VotingResults(ref NetworkedPlayerInfo Exiled, ref bool IsTie, Dictionary<byte, int> vote, byte[] mostVotedPlayers, bool ClearAndExile)
        => AddRole != null ? AddRole.VotingResults(ref Exiled, ref IsTie, vote, mostVotedPlayers, ClearAndExile)
           : base.VotingResults(ref Exiled, ref IsTie, vote, mostVotedPlayers, ClearAndExile);

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter) =>
        AddRole != null ? AddRole.CheckVoteAsVoter(votedForId, voter) : base.CheckVoteAsVoter(votedForId, voter);

    public override void ChengeRoleAdd() => AddRole?.ChengeRoleAdd();

    public override bool CanUseAbilityButton() => AddRole != null ? AddRole.CanUseAbilityButton() : base.CanUseAbilityButton();
    public override bool OnCheckMurderAsTarget(MurderInfo info) => AddRole != null ? AddRole.OnCheckMurderAsTarget(info) : base.OnCheckMurderAsTarget(info);
    public override void OnMurderPlayerAsTarget(MurderInfo info) => AddRole?.OnMurderPlayerAsTarget(info);
    public override void OnDead(PlayerControl player) => AddRole?.OnDead(player);
    public override void OnShapeshift(PlayerControl target) => AddRole?.OnShapeshift(target);
    public override bool CanDesyncShapeshift => AddRole == null ? base.CanDesyncShapeshift : AddRole.CanDesyncShapeshift;
    public override bool CheckShapeshift(PlayerControl target, ref bool a) => AddRole == null ? base.CheckShapeshift(target, ref a) : AddRole.CheckShapeshift(target, ref a );
    public override bool CheckVanish() => AddRole == null ? base.CheckVanish() : AddRole.CheckVanish();
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => AddRole?.OnReportDeadBody(reporter, target);
    public override bool CanClickUseVentButton => AddRole == null ? base.CanClickUseVentButton : AddRole.CanClickUseVentButton;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => AddRole == null ? base.OnEnterVent(physics, ventId) : AddRole.OnEnterVent(physics, ventId);
    public override string MeetingAddMessage() => AddRole == null ? base.MeetingAddMessage() : AddRole.MeetingAddMessage();
    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional) =>
        AddRole is null ? base.ModifyVote(voterId, sourceVotedForId, isIntentional) : AddRole.ModifyVote(voterId, sourceVotedForId, isIntentional);
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner) => AddRole?.OnExileWrapUp(exiled, ref DecidedWinner);
    public override void AfterMeetingTasks() => AddRole?.AfterMeetingTasks();
    public override void StartGameTasks() => AddRole?.StartGameTasks();
    public override void ChangeColor() => AddRole?.ChangeColor();
    public override bool OnInvokeSabotage(SystemTypes systemType) => AddRole != null ? AddRole.OnInvokeSabotage(systemType) : base.OnInvokeSabotage(systemType);
    public override bool OnSabotage(PlayerControl player, SystemTypes systemType) =>
        AddRole != null ? AddRole.OnSabotage(player, systemType) : base.OnSabotage(player, systemType);
    //引数長くてだるいから...
    public override void OnFixSabotage(PlayerControl a, SystemTypes b, byte c) => AddRole?.OnFixSabotage(a, b, c);
    public override void AfterSabotage(SystemTypes systemType) => AddRole?.AfterSabotage(systemType);
    public override bool NotifyRolesCheckOtherName => AddRole == null ? base.NotifyRolesCheckOtherName : AddRole.NotifyRolesCheckOtherName;
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl a, ref bool b, ref Color c, ref string d, ref bool e)
        => AddRole?.OverrideDisplayRoleNameAsSeer(a, ref b, ref c, ref d, ref e);
    public override void OverrideProgressTextAsSeer(PlayerControl a, ref bool b, ref string c) => AddRole?.OverrideProgressTextAsSeer(a, ref b, ref c);
    public override void OverrideProgressTextAsSeen(PlayerControl a, ref bool b, ref string c) => AddRole?.OverrideProgressTextAsSeer(a, ref b, ref c);
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (AddRole is null) return base.GetMark(seer, seen, isForMeeting);

        return AddRole.GetMark(seer, seen, isForMeeting);
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (AddRole is null) return base.GetLowerText(seer, seen, isForMeeting, isForHud);

        return AddRole.GetLowerText(seer, seen, isForMeeting, isForHud);
    }
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (AddRole is null) return base.GetSuffix(seer, seen, isForMeeting);

        return AddRole.GetSuffix(seer, seen, isForMeeting);
    }
    public override bool AllEnabledColor => AddRole == null ? base.AllEnabledColor : AddRole.AllEnabledColor;
    public override string GetAbilityButtonText()
    {
        if (AddRole is null) return base.GetAbilityButtonText();

        return AddRole.GetAbilityButtonText();
    }
    public override bool OverrideAbilityButton(out string text)
    {
        if (AddRole is null)
        {
            base.OverrideAbilityButton(out var tx);
            text = tx;
            return base.OverrideAbilityButton(out var _);
        }

        AddRole.OverrideAbilityButton(out var t);
        text = t;
        return AddRole.OverrideAbilityButton(out var _);
    }
    public override bool CancelReportDeadBody(PlayerControl a, NetworkedPlayerInfo b, ref DontReportreson c)
        => AddRole is null ? base.CancelReportDeadBody(a, b, ref c) : AddRole.CancelReportDeadBody(a, b, ref c);

    public override CustomRoles TellResults(PlayerControl player) => AddRole is null ? base.TellResults(player) : AddRole.TellResults(player);
    public override void OnVentilationSystemUpdate(PlayerControl user, VentilationSystem.Operation Operation, int ventId) => AddRole?.OnVentilationSystemUpdate(user, Operation, ventId);
    public override bool GetTemporaryName(ref string a, ref bool b, bool c, PlayerControl d, PlayerControl e = null)
    {
        e ??= d;
        if (AddRole is null) return base.GetTemporaryName(ref a, ref b, c, d, e);
        return AddRole.GetTemporaryName(ref a, ref b, c, d, e);
    }
    public override void OnLeftPlayer(PlayerControl player) => AddRole?.OnLeftPlayer(player);
    public override bool? CheckGuess(PlayerControl a)
    {
        if (AddRole is null) return base.CheckGuess(a);
        return AddRole.CheckGuess(a);
    }
    public override bool CanTask() => AddRole is null ? base.CanTask() : AddRole.CanTask();
    public override RoleTypes? AfterMeetingRole => AddRole is null ? base.AfterMeetingRole :
        AddRole.AfterMeetingRole is null ? haverole.GetRoleInfo().BaseRoleType() : AddRole.AfterMeetingRole;
    public override void CheckWinner(GameOverReason reason) => AddRole?.CheckWinner(reason);
    public override bool CallJudgeVote(PlayerControl voter, PlayerControl votefor, ref byte ExilePlayerid)
    {
        if (AddRole is not null) return AddRole.CallJudgeVote(voter, votefor, ref ExilePlayerid);
        return base.CallJudgeVote(voter, votefor, ref ExilePlayerid);
    }
    public bool OnPetUsed() => AddRole is IPetTrigger p ? p.OnPetUsed() : false;
    bool IImpostor.CanBeLastImpostor => AddRole is IImpostor impostor ? impostor.CanBeLastImpostor : Realized;
    public bool CanKill => AddRole is IKiller impostor ? impostor.CanKill : true;
    public bool IsKiller => AddRole is IKiller impostor ? impostor.IsKiller : true;
    public void OnCheckMurderDontKill(MurderInfo info) { if (AddRole is IKiller impostor) impostor.OnCheckMurderDontKill(info); }
    public bool OverrideKillButtonText(out string text)
    {
        if ((AddRole as IImpostor)?.OverrideKillButtonText(out var killbuttontext) is true)
        {
            text = killbuttontext;
            return true;
        }
        text = default;
        return false;
    }
    public bool OverrideImpVentButton(out string text)
    {
        if ((AddRole as IImpostor)?.OverrideImpVentButton(out var ventbutton) is true)
        {
            text = ventbutton;
            return true;
        }
        text = default;
        return false;
    }
    bool IUsePhantomButton.IsPhantomRole => AddRole is IUsePhantomButton iusephantom && iusephantom?.IsPhantomRole is true;
    bool IUsePhantomButton.UseOneclickButton => AddRole is IUsePhantomButton iusephantom && iusephantom?.UseOneclickButton is true;
    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (AddRole is IUsePhantomButton iusephantom)
        {
            iusephantom.OnClick(ref AdjustKillCooldown, ref ResetCooldown);
        }
    }
    public bool CheckAction => (AddRole as IDoubleTrigger)?.CheckAction ?? false;
    public bool SingleAction(PlayerControl killer, PlayerControl target)
    {
        return (AddRole as IDoubleTrigger)?.SingleAction(killer, target) ?? true;
    }

    public bool DoubleAction(PlayerControl killer, PlayerControl target)
    {
        return (AddRole as IDoubleTrigger)?.DoubleAction(killer, target) ?? true;
    }
    public bool? CheckKillFlash(MurderInfo info) => (AddRole as IKillFlashSeeable)?.CheckKillFlash(info) ?? false;
    public override void ReceiveRPC(MessageReader reader)
    {
        try

        {
            AddRole.ReceiveRPC(reader);
        }
        catch (Exception ex)
        {
            Logger.Error($"{ex}", "Amnesiac");
        }
    }
    #endregion
    public override CustomRoles HaveAddRole() => haverole;
    public void SendRPC() //これも一時用
    {
        if (!PlayerCatch.AnyModClient() || !AmongUsClient.Instance.AmHost) return;
        var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AmnesiacSyncRealize, SendOption.Reliable);
        writer.Write(Player.PlayerId);
        writer.Write(Realized);
    }
    public void ReceiveRealizeRPC(MessageReader reader)
    {
        Realized = reader.ReadBoolean();
    }
}

