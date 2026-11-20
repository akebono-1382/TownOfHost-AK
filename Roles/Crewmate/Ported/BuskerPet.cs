using System;
using TownOfHostL.Modules;
using TownOfHostL.Patches;
using UnityEngine;

namespace TownOfHostL.Roles.Crewmate;

/// <summary>
/// バスカー系をペットで発動する時の入口とクール (ホストのみ)。
/// ペットには本編のクールが無いので、ホストで数える。クールは戻った時と会議明けから数え直し、幽霊の間は止めておく。
/// </summary>
public sealed class BuskerPet
{
    private readonly PlayerControl player;
    private readonly float cooldown;
    private readonly Func<Action<PlayerControl>, bool> tryBecomeGhost;
    private float readyAt;

    /// <param name="tryBecomeGhost">幽霊になれたら true。引数は戻った時に呼ぶ処理 (TempGhost.Begin の onReturned に渡す)</param>
    public BuskerPet(PlayerControl player, float cooldown, Func<Action<PlayerControl>, bool> tryBecomeGhost)
    {
        this.player = player;
        this.cooldown = cooldown;
        this.tryBecomeGhost = tryBecomeGhost;
    }

    public void Register()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        Restart();
        PetActionManager.Register(player.PlayerId, OnPet);
    }

    public void Unregister() => PetActionManager.Unregister(player.PlayerId);

    /// <summary>クールを最初から数える (試合の始め・会議明け・戻った時)</summary>
    public void Restart() => readyAt = Time.realtimeSinceStartup + cooldown;

    private void OnPet()
    {
        if (Time.realtimeSinceStartup < readyAt) return;
        if (!tryBecomeGhost(_ => Restart())) return;
        // 戻るまでは数えない (会議で戻った時は会議明けに数え直す)
        readyAt = float.PositiveInfinity;
    }
}
