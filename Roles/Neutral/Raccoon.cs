using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral ;

public sealed class Raccoon : RoleBase, IKiller, IUsePhantomButton, IRoomTasker
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Raccoon),
            player => new Raccoon(player),
            CustomRoles.Raccoon,
            () => OptionCanMakeSidekick.GetBool() ? RoleTypes.Phantom : RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            27000,
            SetupOptionItem,
            "Rcn",
            "#a0785a",
            (2, 7),
            true,
            countType: CountTypes.Raccoon,
            from: From.TownOfHost_hamo
        );
    public Raccoon(PlayerControl player)
        : base(RoleInfo, player)
    {
        waitcount = 0;
        KnowRolePlayers.Clear();
        MeetingKill = OptionMeetingKill.GetBool();
        CanVent = OptionCanVent.GetBool();
        PendingChildTargetId = byte.MaxValue;

        CustomRoleManager.OnEnterVentOthers.Add(OnEnterVentOthers);
    }
    public override void OnDestroy()
    {
        Raccoons = new();
        GrabPlayer = null;
    }
    static OptionItem KillCooldown;
    static OptionItem SidekickCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionMeetingKill;
    static OptionItem OptionCanMakeSidekick;
    static OptionItem OptImpVision;
    public static OptionItem CanKill;
    public static OptionItem ChildKillCooldown;
    public static OptionItem ChildMeetingKill;
    public static OptionItem ChildCanVent;
    public static OptionItem OptionVentCooldown;
    public static OptionItem OptionVentIntime;
    enum OptionName
    {
        PenguinMeetingKill,
        RaccoonSidekickCooldown,
        UltraStarCankill,
    }
    public static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.Raccoon))
            ;
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 16, GeneralOption.CanVent, true, false)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.Raccoon))
            ;
        OptImpVision = BooleanOptionItem.Create(RoleInfo, 15, GeneralOption.ImpostorVision, true, false);

        SidekickCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.RaccoonSidekickCooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.Raccoon))
            ;

        OptionMeetingKill = BooleanOptionItem.Create(RoleInfo, 12, OptionName.PenguinMeetingKill, true, false)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.Raccoon))
            ;
        OptionCanMakeSidekick = BooleanOptionItem.Create(RoleInfo, 18, GeneralOption.CanCreateSideKick, true, false)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.Raccoon))
            ;

        //ObjectOptionitem.Create(RoleInfo, 13, "RaccoonChildSetting", true, null, parent: OptionCanMakeSidekick)
        //    .SetOptionName(() => "Child Setting").SetColor(UtilsRoleText.GetRoleColor(CustomRoles.Raccoon));
        CanKill = BooleanOptionItem.Create(RoleInfo, 19, OptionName.UltraStarCankill, false, false, OptionCanMakeSidekick)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.RaccoonChild))
            ;
        ChildKillCooldown = FloatOptionItem.Create(RoleInfo, 14, GeneralOption.KillCooldown, OptionBaseCoolTime, 40f, false, CanKill)
            .SetValueFormat(OptionFormat.Seconds)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.RaccoonChild))
            ;
        ChildMeetingKill = BooleanOptionItem.Create(RoleInfo, 15, OptionName.PenguinMeetingKill, true, false, CanKill)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.RaccoonChild))
            ;
        ChildCanVent = BooleanOptionItem.Create(RoleInfo, 17, GeneralOption.CanVent, true, false, OptionCanMakeSidekick)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.RaccoonChild))
            ;
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, 20, StringNames.EngineerCooldown, OptionBaseCoolTime, 15f, false, ChildCanVent)
            .SetValueFormat(OptionFormat.Seconds)
            .SetEnabled(() => CanKill.GetBool() is false)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.RaccoonChild))
            ;
        OptionVentIntime = FloatOptionItem.Create(RoleInfo, 21, StringNames.EngineerInVentCooldown, OptionBaseCoolTime, 5f, false, ChildCanVent)
            .SetZeroNotation(OptionZeroNotation.Infinity)
            .SetValueFormat(OptionFormat.Seconds)
            .SetEnabled(() => CanKill.GetBool() is false)
            .SetInfo(UtilsRoleText.GetRoleColorAndtext(CustomRoles.RaccoonChild))
            ;
        RoleAddAddons.Create(RoleInfo, 30, NeutralKiller: true);
    }
    static bool CanVent;
    static bool MeetingKill;
    private PlayerControl GrabPlayer;
    static HashSet<Raccoon> Raccoons = new();
    public bool IsKiller => GrabPlayer == null;
    int waitcount;
    public bool IsAssignRoomTask() => GrabPlayer != null && !KnowRolePlayers.Contains(GrabPlayer?.PlayerId ?? 255);
    public HashSet<byte> KnowRolePlayers = new();
    bool Haschild = !OptionCanMakeSidekick.GetBool();
    public byte PendingChildTargetId;
    public float CalculateKillCooldown() => KillCooldown.GetFloat();
    bool IUsePhantomButton.IsPhantomRole => !Haschild && OptionCanMakeSidekick.GetBool();
    bool IKiller.CanUseImpostorVentButton() => CanVent;
    public override void Add()
    {
        Raccoons.Add(this);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptImpVision.GetBool());
        AURoleOptions.PhantomCooldown = SidekickCooldown.GetFloat();
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.SetVictim);
        sender.Writer.Write(GrabPlayer?.PlayerId ?? 255);
    }
    void AddVictim(PlayerControl target)
    {
        //var position = Player.transform.position;
        //waitcount = 0;
        //GrabPlayer.RpcSnapToForced(position, SendOption.None);

        PlayerState.GetByPlayerId(target.PlayerId).CanUseMovingPlatform = MyState.CanUseMovingPlatform = false;
        CheckMurderPatch.TimeSinceLastKill[Player.PlayerId] = 0f;
        GrabPlayer = target;
        Player.RpcResetAbilityCooldown(Sync: true);
        SendRPC();
        target.GetPlayerState().CanMove = false;
        target.MarkDirtySettings();
    }
    void RemoveVictim()
    {
        if (GrabPlayer != null)
        {
            PlayerState.GetByPlayerId(GrabPlayer.PlayerId).CanUseMovingPlatform = true;
            GrabPlayer.GetPlayerState().CanMove = true;
            GrabPlayer.MarkDirtySettings();
            GrabPlayer = null;
        }
        MyState.CanUseMovingPlatform = true;
        Player.RpcResetAbilityCooldown(Sync: true);
        SendRPC();
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;
        if (target.Is(CountTypes.Raccoon))
        {
            info.DoKill = false;
            return;
        }
        if (GrabPlayer != null)
        {
            if (target != GrabPlayer)
            {
                //拉致中は拉致相手しか切れない
                Player.RpcMurderPlayer(GrabPlayer);
                Player.ResetKillCooldown();
                info.DoKill = false;
            }
            RemoveVictim();
        }
        else
        {
            if (info.CheckHasGuard())
            {
                info.IsGuard = false;
                return;
            }
            info.DoKill = false;
            AddVictim(target);
        }
    }
    public override void AfterMeetingTasks()
    {
        if (PendingChildTargetId == byte.MaxValue) return;
        var pendingpc = PendingChildTargetId.GetPlayerControl();
        pendingpc.RpcSetCustomRole(CustomRoles.RaccoonChild);
        PendingChildTargetId = byte.MaxValue;
        if (!Utils.RoleSendList.Contains(PendingChildTargetId)) Utils.RoleSendList.Add(PendingChildTargetId);
        UtilsOption.MarkEveryoneDirtySettings();
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        var target = Player.GetKillTarget(true);

        if (target == null || Haschild || !OptionCanMakeSidekick.GetBool()) return;
        Haschild = true;
        PendingChildTargetId = target.PlayerId;
        UtilsGameLog.AddGameLog($"SideKick", string.Format(GetString("log Sidekick"), UtilsName.GetPlayerColor(target, true) + $"({UtilsRoleText.GetTrueRoleName(target.PlayerId)})", UtilsName.GetPlayerColor(Player, true)));
        SendRPC_SyncPendingChildTargetId();
    }
    void IRoomTasker.OnComplete(int completeroom)
    {
        SendRPC_CompleteRoom(completeroom);
    }
    void IRoomTasker.ChangeRoom(PlainShipRoom TaskRoom)
    {
        SendRPC_ChengeRoom(TaskRoom);
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seer != seen) return "";
        return (seer.GetRoleClass() as IRoomTasker)?.GetLowerText(seer, RoleInfo.RoleColorCode) ?? "";
    }
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        //幽霊役見えるのどうしよう。
        if (!Player.IsAlive() || !KnowRolePlayers.Contains(seen.PlayerId)) return;
        enabled = true;
        addon = false;
        roleText = UtilsRoleText.GetRoleColorAndtext(seen.GetCustomRole());
    }
    public void SendRPC_CompleteRoom(int completeroom)
    {
        if (Player.PlayerId == 0)
        {
            KnowRolePlayers.Add(GrabPlayer?.PlayerId ?? 255);
            RemoveVictim();
        }
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.CompleteRoom);
        sender.Writer.Write(completeroom);
        sender.Writer.Write(GrabPlayer?.PlayerId ?? 255);
    }
    public void SendRPC_ChengeRoom(PlainShipRoom TaskPSR)
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.ChengeRoom);
        sender.Writer.Write((byte)TaskPSR.RoomId);
    }
    void SendRPC_SyncPendingChildTargetId()
    {
        using var sender = CreateSender();
        sender.Writer.WritePacked((int)RPC_Types.SyncPendingChildTargetId);
        sender.Writer.Write(PendingChildTargetId);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        var iroomtasker = Player.GetRoleClass() is IRoomTasker roomTasker ? roomTasker : null;
        switch ((RPC_Types)reader.ReadPackedInt32())
        {
            case RPC_Types.ChengeRoom:
                iroomtasker?.ReceiveRoom(Player.PlayerId, reader);
                break;
            case RPC_Types.CompleteRoom:
                iroomtasker?.ReceiveCompleteRoom(Player.PlayerId, reader);
                KnowRolePlayers.Add(reader.ReadByte());
                RemoveVictim();
                break;
            case RPC_Types.SetVictim:
                var victim = reader.ReadByte();
                if (victim == 255)
                {
                    GrabPlayer = null;
                }
                else
                {
                    GrabPlayer = PlayerCatch.GetPlayerById(victim);
                }
                break;
            case RPC_Types.SyncPendingChildTargetId:
                PendingChildTargetId = reader.ReadByte();
                break;
        }
    }

    enum RPC_Types
    {
        ChengeRoom,
        CompleteRoom,
        SetVictim,
        SyncPendingChildTargetId,
    }
    public bool OverrideKillButtonText(out string text)
    {
        if (GrabPlayer != null)
        {
            text = GetString("KillButtonText");
        }
        else
        {
            text = GetString("RaccoonKillButtonText");
        }
        return true;
    }

    public override string GetAbilityButtonText()
    {
        return GetString("RaccoonTimerText");
    }
    public override bool OverrideAbilityButton(out string text)
    {
        text = "Raccoon_Ability";
        return true;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (AddOns.Common.Amnesia.CheckAbilityreturn(Player)) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GrabPlayer == null || !MeetingKill) return;

        Player.RpcMurderPlayer(GrabPlayer);
        RemoveVictim();
    }
    public override void OnSpawn(bool initialState)
    {
        RestartAbduct();
    }
    public void RestartAbduct()
    {
        if (GrabPlayer != null)
        {
            state = 0;
        }
    }
    static int state = 0;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.IsInTask) return;

        if (GrabPlayer != null)
        {
            if (!Player.IsAlive() || !GrabPlayer.IsAlive())
            {
                RemoveVictim();
                return;
            }
            if (!GrabPlayer.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
            {
                int div = 3;
                state++;
                if (state % div == 0)
                {
                    var position = Player.transform.position;
                    if (Player.PlayerId != 0)
                    {
                        var dis = Vector2.Distance(Player.GetTruePosition(), GrabPlayer.GetTruePosition());
                        if (dis > 1f)
                        {
                            waitcount++;

                            if (waitcount >= 2)
                            {
                                waitcount = 0;
                                GrabPlayer.RpcSnapToForced(position, SendOption.None);
                            }
                        }
                    }
                    else
                    {
                        _ = new LateTask(() =>
                        {
                            if (GrabPlayer != null)
                            {
                                var dis = Vector2.Distance(Player.GetTruePosition(), GrabPlayer.GetTruePosition());
                                if (dis > 1f)
                                {
                                    waitcount++;

                                    if (waitcount >= 2)
                                    {
                                        waitcount = 0;
                                        GrabPlayer.RpcSnapToForced(position, SendOption.None);
                                    }
                                }
                            }
                        }
                        , Main.LagTime, "", true);
                    }
                }
            }
        }
    }
    public static bool OnEnterVentOthers(PlayerPhysics physics, int ventId)
    {
        var user = physics.myPlayer;
        foreach (var rac in Raccoons)
        {
            if (rac.GrabPlayer != null)
            {
                if (rac.GrabPlayer.PlayerId == user.PlayerId)
                {
                    _ = new LateTask(() =>
                    {
                        if (user.inVent && user.IsAlive())
                            physics.RpcBootFromVent(ventId);
                    }, 0.8f, "PenginTargetnonvent", true);
                    return true;
                }
            }
        }
        return true;
    }
    public override void CheckWinner(GameOverReason reason)
    {
        if (PendingChildTargetId == byte.MaxValue) return;
        var pendingpc = PendingChildTargetId.GetPlayerControl();
        pendingpc.RpcSetCustomRole(CustomRoles.RaccoonChild);
        PendingChildTargetId = byte.MaxValue;
    }
}
