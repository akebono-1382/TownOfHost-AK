using UnityEngine;
using AmongUs.GameOptions;
using Hazel;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using System.Collections.Generic;

namespace TownOfHost.Roles.Neutral;

public sealed class RaccoonChild : RoleBase, IKiller, IRoomTasker
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(RaccoonChild),
            player => new RaccoonChild(player),
            CustomRoles.RaccoonChild,
            () => Raccoon.CanKill.GetBool() ? RoleTypes.Impostor : Raccoon.ChildCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            27100,
            null,
            "Rcl",
            "#a0785a",
            (2, 7),
            true,
            countType: CountTypes.Raccoon,
            assignInfo: new(CustomRoles.RaccoonChild, CustomRoleTypes.Neutral)
            {
                IsInitiallyAssignableCallBack = () => false
            },
            from: From.TownOfHost_hamo
        );
    public RaccoonChild(PlayerControl player)
        : base(RoleInfo, player)
    {
        waitcount = 0;
        KnowRolePlayers.Clear();
        MeetingKill = Raccoon.ChildMeetingKill.GetBool();
        CanVent = Raccoon.ChildCanVent.GetBool();

        CustomRoleManager.OnEnterVentOthers.Add(OnEnterVentOthers);
    }
    public override void OnDestroy()
    {
        Raccoons = new();
        GrabPlayer = null;
    }
    static bool CanVent;
    static bool MeetingKill;
    private PlayerControl GrabPlayer;
    static HashSet<RaccoonChild> Raccoons = new();
    public bool CanKill => Raccoon.CanKill.GetBool();
    public bool IsKiller => GrabPlayer == null && Raccoon.CanKill.GetBool();
    int waitcount;
    public bool IsAssignRoomTask() => GrabPlayer != null && !KnowRolePlayers.Contains(GrabPlayer?.PlayerId ?? 255);
    public HashSet<byte> KnowRolePlayers = new();
    public float CalculateKillCooldown() => Raccoon.ChildKillCooldown.GetFloat();
    bool IKiller.CanUseImpostorVentButton() => CanVent;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        AURoleOptions.EngineerCooldown = Raccoon.OptionVentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = Raccoon.OptionVentIntime.GetFloat();
    }
    public override void Add()
    {
        Raccoons.Add(this);
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
        roleText = UtilsRoleText.GetRoleName(seen.GetCustomRole());
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
        }
    }

    enum RPC_Types
    {
        ChengeRoom,
        CompleteRoom,
        SetVictim,
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
        return GetString("PenguinTimerText");
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
}
