using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.ParticleSystem.PlaybackState;

namespace TownOfHost.Roles.Crewmate;

public sealed class Busker : RoleBase, IPetTrigger
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Busker),
            player => new Busker(player),
            CustomRoles.Busker,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            27800,
            SetupOptionItem,
            "Bkr",
            "#ffac79",
            (5, 6),
            from: From.NebulaontheShip
        );

    public Busker(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CooldownTimer = OptionCooldown.GetFloat();
        Durationtimer = OptionDuration.GetFloat();
        NowFakeDeath = false;
    }
    public bool NowFakeDeath;
    static OptionItem OptionCooldown; float CooldownTimer;
    static OptionItem OptionDuration; public float Durationtimer;
    bool IsInfinityDuration => OptionDuration.GetFloat() == 0f;
    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 20f)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Duration, new(0f, 999f, 0.5f), 30f)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
    }

    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = OptionCooldown.GetFloat();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => !NowFakeDeath ? "" : Utils.ColorString(RoleInfo.RoleColor, $"[{(int)Durationtimer}]");

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        => !NowFakeDeath ? "" : Utils.ColorString(RoleInfo.RoleColor, string.Format(GetString("BuskerLower"), (int)Durationtimer));

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (NowFakeDeath)
        {
            var state = PlayerState.GetByPlayerId(Player.PlayerId);
            state.DeathReason = CustomDeathReason.Suicide;
            Player.RpcMurderPlayer(Player, true);
            state.SetDead();
        }
        NowFakeDeath = false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (NowFakeDeath)
        {
            if (IsInfinityDuration) return;
            if (Durationtimer <= 0f)
            {
                NowFakeDeath = false;
                Durationtimer = 999f;
                var state = PlayerState.GetByPlayerId(Player.PlayerId);
                state.DeathReason = CustomDeathReason.Suicide;
                Player.RpcMurderPlayer(Player, true);
                state.SetDead();
            }
            else Durationtimer -= Time.fixedDeltaTime;
        }
        else if (CooldownTimer > 0f)
        {
            var old = (int)CooldownTimer;
            CooldownTimer -= Time.fixedDeltaTime;
            if (old - CooldownTimer >= 1)
            {
                UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
            }
        }
    }

    bool IPetTrigger.OnPetUsed()
    {
        if (Player.IsAlive() is false) return true;
        if (CooldownTimer > 0.1f) return false;

        //ImpostorGhostだとタスク消される。
        //Player.RpcSetRoleDesync(RoleTypes.ImpostorGhost, Player.GetClientId());

        //キルモーションが...
        /*foreach (var seer in PlayerCatch.AllPlayerControls.Where(p => p.PlayerId != Player.PlayerId))
        {
            Player.RpcMurderPlayerDesync(Player, seer);
        }
        var faketarget = PlayerCatch.AllPlayerControls.FirstOrDefault(p => p.IsAlive() is false && p.PlayerId != Player.PlayerId);

        if (faketarget is null)
        {
            faketarget = PlayerCatch.AllPlayerControls.FirstOrDefault(p => p.PlayerId != Player.PlayerId);
        }
        if (faketarget != null)
        {
            if (faketarget.CurrentOutfit != null)
            {
                int originalcolorid = faketarget.CurrentOutfit.ColorId;
                string originalpet = faketarget.CurrentOutfit.PetId;

                if (Is(PlayerControl.LocalPlayer))
                {
                    faketarget.SetColor(Player.CurrentOutfit.ColorId);
                    faketarget.SetPet(Player.CurrentOutfit.PetId);
                }
                else
                {
                    var sender = CustomRpcSender.Create($"BuskerSetColor({faketarget.Data.GetLogPlayerName()})");
                    sender.AutoStartRpc(faketarget.NetId, (byte)RpcCalls.SetColor)
                        .Write(faketarget.Data.NetId)
                        .Write(Player.CurrentOutfit.ColorId)
                        .EndRpc();
                    sender.AutoStartRpc(faketarget.NetId, (byte)RpcCalls.SetPetStr)
                        .Write(faketarget.Data.NetId)
                        .Write(Player.CurrentOutfit.PetId)
                        .EndRpc();
                    sender.SendMessage();
                }
                bool alive = faketarget.IsAlive();
                var originalpos = faketarget.transform.position;
                faketarget.RpcSnapToDesync(Player, Player.transform.position);
                _ = new LateTask(() =>
                {
                    faketarget.RpcMurderPlayerDesync(faketarget, Player);
                }, 0.3f, "");
                _ = new LateTask(() =>
                {
                    faketarget.RpcSnapToDesync(Player, originalpos);
                }, 0.6f, "");
                if (alive)
                {
                    faketarget.RpcRevive();
                }

                if (Is(PlayerControl.LocalPlayer))
                {
                    faketarget.SetColor(originalcolorid);
                }
                else
                {
                    var sender = CustomRpcSender.Create($"BuskerSetColor({faketarget.Data.GetLogPlayerName()})");
                    sender.AutoStartRpc(faketarget.NetId, (byte)RpcCalls.SetColor)
                        .Write(faketarget.Data.NetId)
                        .Write(originalcolorid)
                        .EndRpc();
                    sender.AutoStartRpc(faketarget.NetId, (byte)RpcCalls.SetPetStr)
                        .Write(faketarget.Data.NetId)
                        .Write(originalpet)
                        .EndRpc();
                    sender.SendMessage();
                }
            }
            else
            {
                faketarget.RpcMurderPlayerDesync(faketarget, Player);
            }
        }*/
        NowFakeDeath = true;
        Durationtimer = OptionDuration.GetFloat();

        Player.RpcMurderPlayer(Player);
        //Player.RpcExileV3(true);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        if (!Is(PlayerControl.LocalPlayer))
        {
            _ = new LateTask(() =>
            {
                var w = AmongUsClient.Instance.StartRpcImmediately(
                    Player.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, Player.GetClientId());
                w.Write((ushort)RoleTypes.CrewmateGhost);
                w.Write(true);   // canOverride を強制
                AmongUsClient.Instance.FinishRpcImmediately(w);
                Player.GetPlayerState().NowRoleType = RoleTypes.CrewmateGhost;
            }, 0.3f, "ForceGhostRole");
        }
        return true;
    }
    //ImpostorGhostのタスク消し無効化できたとき用
    public override bool OnInvokeSabotage(SystemTypes systemType)
    {
        NowFakeDeath = false;
        Player.RpcRevive();
        return false;
    }

    public static void CheckBuskerCommand(PlayerControl sender)
    {
        if (sender.GetRoleClass() is Busker busker && busker.NowFakeDeath)
        {
            busker.NowFakeDeath = false;
            sender.RpcRevive();
            busker.Player.RpcResetAbilityCooldown();
        }
    }

    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
    {
        var targetpc = PlayerCatch.GetPlayerById(target.PlayerId);
        if (targetpc.Is(CustomRoles.Busker) && targetpc.IsAlive())
        {
            reason = DontReportreson.Other;
            return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRoleOnDeath))]
class AssignRoleOnDeathPatch
{
    public static bool Prefix(RoleManager __instance, PlayerControl player)
    {
        //デーモンサポーターは多分考えなくておけ。
        if (player.Is(CustomRoleTypes.Impostor)) return true;
        __instance.SetRole(player, RoleTypes.CrewmateGhost);
        return false; //ImpostorGhostでタスク消さないように。
    }
}