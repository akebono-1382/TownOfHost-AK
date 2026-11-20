using System;
using TownOfHostL.Modules;
using UnityEngine;

namespace TownOfHostL.Roles.Crewmate;

/// <summary>
/// バスカー系をペットで発動する時の入口とクール (ホストのみ)。
/// ペットには本編のクールが無いので、ホストで数える。クールは戻った時と会議明けから数え直し、幽霊の間は止めておく。
/// </summary>
public sealed class BuskerPet
{
    private readonly float cooldown;
    private readonly Func<Action<PlayerControl>, bool> tryBecomeGhost;
    private float readyAt;

    /// <param name="tryBecomeGhost">幽霊になれたら true。引数は戻った時に呼ぶ処理 (TempGhost.Begin の onReturned に渡す)</param>
    public BuskerPet(float cooldown, Func<Action<PlayerControl>, bool> tryBecomeGhost)
    {
        this.cooldown = cooldown;
        this.tryBecomeGhost = tryBecomeGhost;
    }

    /// <summary>クールを最初から数える (試合の始め・会議明け・戻った時)</summary>
    public void Restart() => readyAt = Time.realtimeSinceStartup + cooldown;

    /// <summary>ペットをかわいがった時 (Modules/PetAbility から役職を経て呼ばれる)</summary>
    public void OnPet()
    {
        if (Time.realtimeSinceStartup < readyAt) return;
        if (!tryBecomeGhost(_ => Restart())) return;
        // 戻るまでは数えない (会議で戻った時は会議明けに数え直す)
        readyAt = float.PositiveInfinity;
    }
}
