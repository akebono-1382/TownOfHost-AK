using System;
using System.Collections.Generic;
using System.Linq;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Attributes;

using AmongUs.GameOptions;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;
// メモ
// 発射されるのダミーにする
public sealed class Rocket : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Rocket),
            player => new Rocket(player),
            CustomRoles.Rocket,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            29000,
            SetupOptionItem,
            "rkt",
            OptionSort: (4, 7),
            from: From.SuperNewRoles
        );
    public Rocket(PlayerControl player)
        : base(RoleInfo, player)
    {
        FirstCooldown = OptionFirstGrabCooldown.GetFloat();
        SecondCooldown = OptionSecondGrabCooldown.GetFloat();
        LaunchCooldown = OptionLaunchCooldown.GetFloat();

        GrabedCanKill = OptionGrabedCanKill.GetBool();
        LaunchAfterMeeting = OptionLaunchAfterMeeting.GetBool();

        GrabbedPlayers.Clear();
        SavedSpeeds.Clear();
        GrabWaits.Clear();
        Rockets.Add(this);
    }
    public static HashSet<Rocket> Rockets = new();
    public List<byte> GrabbedPlayers = new();
    public List<byte> Ignore = new();
    static List<byte> StaticGrabbedPlayers = new();
    public static Dictionary<byte, float> LaunchedPlayers = new();
    public static Dictionary<byte, float> Waits = new();
    Dictionary<byte, int> GrabWaits = new();
    Dictionary<byte, float> SavedSpeeds = new();
    static Dictionary<byte, Vector2> SavedPoss = new();
    const float LaunchTime = 15f;
    const float WaitTime = 0.2f;
    const float LaunchSpeed = 0.3f;
    const int TPWaits = 15;
    [GameModuleInitializer]
    public static void Init()
    {
        LaunchedPlayers.Clear();
        Rockets.Clear();
        Waits.Clear();
        StaticGrabbedPlayers.Clear();
        SavedPoss.Clear();
    }
    public override void OnDestroy()
    {
        Rockets.Remove(this);
    }
    #region Option
    private static OptionItem OptionFirstGrabCooldown; static float FirstCooldown;
    private static OptionItem OptionSecondGrabCooldown; static float SecondCooldown;
    private static OptionItem OptionLaunchCooldown; static float LaunchCooldown;
    public static OptionItem OptionGrabedCanKill; static bool GrabedCanKill;
    private static OptionItem OptionLaunchAfterMeeting; static bool LaunchAfterMeeting;
    private static void SetupOptionItem()
    {
        OptionFirstGrabCooldown = FloatOptionItem.Create(RoleInfo, 10, "RocketFirstGrabCooldown", OptionBaseCoolTime, 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSecondGrabCooldown = FloatOptionItem.Create(RoleInfo, 11, "RocketSecondGrabCooldown", OptionBaseCoolTime, 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionLaunchCooldown = FloatOptionItem.Create(RoleInfo, 12, "RocketLaunchCooldown", OptionBaseCoolTime, 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionGrabedCanKill = BooleanOptionItem.Create(RoleInfo, 13, "RocketGrabedCanKill", true, false);
        OptionLaunchAfterMeeting = BooleanOptionItem.Create(RoleInfo, 14, "RocketLaunchAfterMeeting", true, false);
    }
    #endregion
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = LaunchCooldown;
    }
    public float CalculateKillCooldown() => GrabbedPlayers.Any() is false ? FirstCooldown : SecondCooldown;
    bool IUsePhantomButton.IsPhantomRole => GrabbedPlayers.Any();
    bool IImpostor.CanBeLastImpostor => false;
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = true;
        if (GrabbedPlayers.Any() is false)
        {
            ResetCooldown = false;
            AdjustKillCooldown = true;
            return;
        }

        foreach (var grab in GrabbedPlayers)
        {
            var pc = PlayerCatch.GetPlayerById(grab);
            if (pc is null) return;
            pc.RpcSetRole(RoleTypes.Impostor); //キルターゲット扱いしなくする
            //キル能力剥奪
            pc.RpcSetRoleDesync(pc.GetCustomRole().GetRoleInfo().BaseRoleType().IsCrewmate() is false ? RoleTypes.Crewmate : pc.GetCustomRole().GetRoleInfo().BaseRoleType(), pc.GetClientId());
        }

        LaunchAll();
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        foreach (var launched in LaunchedPlayers.Keys)
        {
            var pc = PlayerCatch.GetPlayerById(launched);
            var state = PlayerState.GetByPlayerId(launched);

            if (pc is null) return;

            pc.SetRealKiller(Player);
            pc.RpcExileV3();
            state.SetDead();
            state.DeathReason = CustomDeathReason.Launched;
        }
        Waits.Clear();
        LaunchedPlayers.Clear();
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Player.IsAlive())
        {
            ReleaseAll();
            return;
        }
        List<byte> remove = new();
        foreach (var grab in GrabbedPlayers.ToArray())
        {
            if (SavedPoss.ContainsKey(grab))
            {
                GrabbedPlayers.Remove(grab);
                continue;
            }
            var pc = PlayerCatch.GetPlayerById(grab);
            if (pc is null) continue;
            if (Vector2.Distance(pc.GetTruePosition(), Player.GetTruePosition()) < 1f) continue;
            GrabWaits[grab]--;
            if (GrabWaits[grab] > 0) continue;
            pc.RpcSnapToForced(player.transform.position);
        }
        foreach (var launch in LaunchedPlayers.Keys)
        {
            var pc = PlayerCatch.GetPlayerById(launch);

            LaunchedPlayers[launch] -= Time.fixedDeltaTime;
            if (LaunchedPlayers[launch] <= 0f)
            {
                remove.Add(launch);

                if (pc is null) continue;

                var state = PlayerState.GetByPlayerId(launch);
                pc.SetRealKiller(Player);
                pc.RpcExileV3();
                if (SavedPoss.TryGetValue(launch, out var original))
                {
                    //元位置に戻す。
                    pc.RpcSnapToForced(original);
                }
                if (state is null) continue;
                state.SetDead();
                state.DeathReason = CustomDeathReason.Launched;
                continue;
            }
            if (pc is null) continue;
    
            if (Waits.ContainsKey(launch))
            {
                Waits[launch] -= Time.fixedDeltaTime;
                if (Waits[launch] <= 0f)
                {
                    float speed = WaitTime / Time.fixedDeltaTime;
                    var pos = pc.transform.position;
                    pos.y += LaunchSpeed * speed;
                    pc.RpcSnapToForced(pos);
                    Waits[launch] = WaitTime;
                }
            }
        }
        foreach (var rm in remove)
        {
            LaunchedPlayers.Remove(rm);
            StaticGrabbedPlayers.Remove(rm);
            Waits.Remove(rm);
            Ignore.Remove(rm);
        }
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        info.DoKill = false;
        var target = info.AttemptTarget;
        if (target is null) return;
        if (StaticGrabbedPlayers.Contains(target.PlayerId)) return;
        Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()}が{target.GetNameWithRole().RemoveHtmlTags()}を掴んだ", nameof(Rocket));

        Ignore.Add(target.PlayerId);
        GrabbedPlayers.Add(target.PlayerId);
        GrabWaits.Add(target.PlayerId, Math.Clamp(target.PlayerId, (byte)0, (byte)TPWaits));
        StaticGrabbedPlayers.Add(target.PlayerId);

        target.GetPlayerState().CanMove = false;
        SavedSpeeds.Add(target.PlayerId, Main.AllPlayerSpeed.TryGetValue(target.PlayerId, out var speed) ? speed : Main.NormalOptions.PlayerSpeedMod);
        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        target.MarkDirtySettings();

        //キルボ光らなくする。
        target.RpcSetRoleDesync(RoleTypes.Impostor, Player.GetClientId());
        Player.SetKillCooldown();

        target.RpcSnapToForced(Player.transform.position);
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (GrabedCanKill) return true;
        var target = info.AttemptTarget;
        if (!GrabbedPlayers.Contains(target?.PlayerId ?? byte.MaxValue)) return true; 
        return false;
    }
    public override void AfterMeetingTasks()
    {
        if (!LaunchAfterMeeting)
        {
            foreach (var grab in GrabbedPlayers)
            {
                StaticGrabbedPlayers.Remove(grab);
                Ignore.Remove(grab);
            }
            ReleaseAll();
            return;
        }

        LaunchAll();
    }
    void LaunchAll()
    {
        if (GrabbedPlayers.Any() is false) return;
        Player.SetKillCooldown();
        Player.RpcResetAbilityCooldown();

        Utils.AllPlayerKillFlash();
        foreach (var grab in GrabbedPlayers)
        {
            Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()}が{PlayerCatch.GetPlayerById(grab)?.GetNameWithRole().RemoveHtmlTags() ?? "???"}を打ち上げた", nameof(Rocket));
            LaunchedPlayers.Add(grab, LaunchTime);
            Waits.Add(grab, WaitTime);
        }
        GrabbedPlayers.Clear();
    }

    void ReleaseAll()
    {
        foreach (var grab in GrabbedPlayers)
        {
            Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()}が{PlayerCatch.GetPlayerById(grab)?.GetNameWithRole().RemoveHtmlTags() ?? "???"}を解放した", nameof(Rocket));

            Main.AllPlayerSpeed[grab] = SavedSpeeds[grab];
            StaticGrabbedPlayers.Remove(grab);      

            var state = PlayerState.GetByPlayerId(grab);
            if (state == null) return;

            state.CanMove = true;
        }
        GrabbedPlayers.Clear();
    }
}
