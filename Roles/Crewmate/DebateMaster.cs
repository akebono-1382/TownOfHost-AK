using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

public sealed class DebateMaster : RoleBase, IKiller, ISchrodingerCatOwner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(DebateMaster),
            player => new DebateMaster(player),
            CustomRoles.DebateMaster,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Crewmate,
            26500,
            SetupOptionItem,
            "dsm",
            "#0B621F",
            (2, 3),
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );
    public DebateMaster(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        ShotLimit = ShotLimitOpt.GetInt();
        CurrentKillCooldown = KillCooldown.GetFloat();
    }

    public static OptionItem KillCooldown;
    public static OptionItem ShotLimitOpt;
    public static OptionItem CanKillAllAlive;
    enum OptionName
    {
        SheriffShotLimit,
        SheriffCanKillAllAlive,
        WolfBoySchrodingerCatTime,
        Wolfboycanwinkillcount,
        WolfBoyCountteam
    }
    public int ShotLimit = 0;
    public float CurrentKillCooldown = 30;

    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Crew;
    private static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OverrideKilldistance.Create(RoleInfo, 11);
        ShotLimitOpt = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SheriffShotLimit, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        CanKillAllAlive = BooleanOptionItem.Create(RoleInfo, 13, OptionName.SheriffCanKillAllAlive, true, false);
    }
    public override void Add()
    {
        var playerId = Player.PlayerId;
        CurrentKillCooldown = KillCooldown.GetFloat();

        ShotLimit = ShotLimitOpt.GetInt();
        Logger.Info($"{PlayerCatch.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()} : 残り{ShotLimit}発", "WolfBoy");
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(ShotLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        ShotLimit = reader.ReadInt32();
    }
    public float CalculateKillCooldown() => CanUseKillButton() ? CurrentKillCooldown : 0f;
    public bool CanUseKillButton()
        => Player.IsAlive()
        && (CanKillAllAlive.GetBool() || GameStates.AlreadyDied)
        && ShotLimit > 0;
    public bool CanUseImpostorVentButton() => true;
    public bool CanUseSabotageButton() => false;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
    }
    public override string GetProgressText(bool comms = false, bool gamelog = false) =>
        Utils.ColorString(CanUseKillButton() ? Color.yellow : Color.gray, $"({ShotLimit})");
    public override void OnStartMeeting()
    {
        if (!Player.IsAlive()) return;
        _ = new LateTask(() =>
        {
            //割れて投票もできなくなる上に幽霊チャットに入れるからから自分以外
            foreach (var seer in PlayerCatch.AllAlivePlayerControls.Where(p => p.PlayerId != Player.PlayerId))
            {
                Player.RpcExileV2(seer);
            }
        }, Main.NormalOptions.DiscussionTime < 0.2f ? 0.2f : Main.NormalOptions.DiscussionTime, "");
    }
    public override void AfterMeetingTasks()
    {
        if (!Player.IsAlive()) return;

        Player.Revive();
    }

    public override (byte? votedForId, int? numVotes, bool doVote) ModifyVote(byte voterId, byte sourceVotedForId, bool isIntentional)
    {
        // 既定値
        var (votedForId, numVotes, doVote) = base.ModifyVote(voterId, sourceVotedForId, isIntentional);

        numVotes = 0;

        return (votedForId, numVotes, doVote);
    }
}

static class DebateMasterChat
{
    /// <summary>議論タイム中か(投票フェーズ・会議外は false)</summary>
    public static bool IsDiscussionTime()
        => GameStates.IsMeeting
        && MeetingHud.Instance != null
        && MeetingHud.Instance.state == MeetingHud.MeetingStates.Discussion;
}

// 受信側: DebateMasterの発言を、議論タイム以外は表示しない
[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
class DebateMasterAddChatPatch
{
    public static bool Prefix([HarmonyArgument(0)] PlayerControl sourcePlayer)
    {
        if (sourcePlayer == null) return true;
        if (!sourcePlayer.Is(CustomRoles.DebateMaster)) return true;
        return DebateMasterChat.IsDiscussionTime(); // false なら表示しない
    }
}

// 送信側: 自分がDebateMasterなら、議論タイム以外は送信自体を止める
[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
class DebateMasterSendChatPatch
{
    public static bool Prefix(ChatController __instance)
    {
        var me = PlayerControl.LocalPlayer;
        if (me == null || !me.Is(CustomRoles.DebateMaster)) return true;
        if (DebateMasterChat.IsDiscussionTime()) return true;

        __instance.freeChatField.textArea.Clear(); // 入力欄を空にする
        return false;
    }
}