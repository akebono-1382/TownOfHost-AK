using UnityEngine;
using AmongUs.GameOptions;
using Hazel;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace TownOfHost.Roles.Impostor;

public sealed class RocketLauncher : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(RocketLauncher),
            player => new RocketLauncher(player),
            CustomRoles.RocketLauncher,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            25400,
            SetupOptionItem,
            "rlr",
            OptionSort: (3, 12),
            from: From.SuperNewRoles
        );
    public RocketLauncher(PlayerControl player)
        : base(RoleInfo, player)
    {
        CustomRoleManager.OnEnterVentOthers.Add(OnEnterVentOthers);
    }
    public override void Add()
    {
        GrabedPlayer = null;
        LaunchedPlayer = null;
        LaunchPos = Vector2.zero;
        Traveled = 0f;
        lastTime = 0f;
        left = false;
        exploding = false;
        victims.Clear();
    }
    public override void OnDestroy()
    {
        Add();
    }

    static OptionItem Cooldown;
    static OptionItem GrabTime;
    static OptionItem LaunchSpeed;
    static OptionItem LaunchDistance;
    enum OptionName
    {
        RocketLauncherGrabTime,
        RocketLauncherLaunchSpeed,
        RocketLauncherLaunchDistance,
    }

    PlayerControl GrabedPlayer;
    PlayerControl LaunchedPlayer;
    Vector2 LaunchPos;
    float Traveled;
    float lastTime;
    bool left;
    bool exploding;
    readonly Queue<PlayerControl> victims = new();

    public static void SetupOptionItem()
    {
        Cooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        GrabTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.RocketLauncherGrabTime, OptionBaseCoolTime, 10f, false)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
        // 単位は 距離/秒。通常移動は約2.5
        LaunchSpeed = FloatOptionItem.Create(RoleInfo, 12, OptionName.RocketLauncherLaunchSpeed, new(1f, 12f, 1f), 5f, false);
        LaunchDistance = FloatOptionItem.Create(RoleInfo, 13, OptionName.RocketLauncherLaunchDistance, new(5f, 60f, 5f), 30f, false);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Cooldown.GetFloat();
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (exploding) return; // 爆発によるキルは通す
        var (killer, target) = info.AttemptTuple;
        if (!Is(killer)) return;

        var id = target.PlayerId;
        if (id == GrabedPlayer?.PlayerId || id == LaunchedPlayer?.PlayerId)
        {
            info.DoKill = false;
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        // 爆発の犠牲者を1tickに1人ずつ処理する
        if (victims.Count > 0)
        {
            var v = victims.Dequeue();
            if (v != null && v.IsAlive())
            {
                CustomRoleManager.OnCheckMurder(player, v, v, v, Killpower: 3, deathReason: CustomDeathReason.Bombed);
            }
            if (victims.Count == 0)
            {
                exploding = false;
                RPC.PlaySoundRPC(player.PlayerId, Sounds.KillSound);
            }
            return;
        }

        if (GrabedPlayer != null)
        {
            GrabedPlayer.RpcSnapToForced(player.transform.position);
            return;
        }
        if (LaunchedPlayer == null) return;
        if (!LaunchedPlayer.IsAlive())
        {
            LaunchedPlayer = null;
            return;
        }

        // 0.05秒に1回だけ動かす(RPCの送りすぎ防止)
        float now = Time.time;
        float dt = now - lastTime;
        if (dt < 0.05f) return;
        lastTime = now;

        float step = LaunchSpeed.GetFloat() * Mathf.Min(dt, 0.2f);
        LaunchPos.x += left ? -step : step;
        Traveled += step;
        LaunchedPlayer.RpcSnapToForced(LaunchPos);

        bool hit = false;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc == LaunchedPlayer || pc == player) continue;
            if (Vector2.Distance(pc.transform.position, LaunchPos) < 1f)
            {
                hit = true;
                break;
            }
        }

        if (hit)
        {
            Explode(player);
            return;
        }

        if (Traveled >= LaunchDistance.GetFloat())
        {
            LaunchedPlayer = null;
        }
    }

    void Explode(PlayerControl player)
    {
        victims.Clear();
        foreach (var p in PlayerCatch.AllAlivePlayerControls.ToArray())
        {
            if (p == player) continue;
            if (Vector2.Distance(p.transform.position, LaunchPos) < 3f)
            {
                victims.Enqueue(p);
            }
        }
        // 爆弾にされた本人は最後
        victims.Enqueue(LaunchedPlayer);

        exploding = true;
        LaunchedPlayer = null;
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        if (GrabedPlayer != null)
        {
            AURoleOptions.PhantomCooldown = Cooldown.GetFloat();
            Player.RpcResetAbilityCooldown();
            Launch();
            return;
        }

        var target = Player.GetKillTarget(true);
        if (target == null) return;

        GrabedPlayer = target;
        SendRPC();
    }

    void Launch()
    {
        if (GrabedPlayer == null) return;

        left = Player.cosmetics.currentBodySprite.BodySprite.flipX;
        LaunchedPlayer = GrabedPlayer;
        LaunchPos = LaunchedPlayer.transform.position;
        Traveled = 0f;
        lastTime = Time.time;
        GrabedPlayer = null;
        SendRPC();
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)(GrabedPlayer?.PlayerId ?? 255));
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var grab = reader.ReadByte();
        GrabedPlayer = PlayerCatch.GetPlayerById(grab);
    }

    public static bool OnEnterVentOthers(PlayerPhysics physics, int ventId)
    {
        var user = physics.myPlayer;

        return true;
    }
}