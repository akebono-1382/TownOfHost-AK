using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

//どうしてもいれたかったの！
public sealed class Magician : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Magician),
            player => new Magician(player),
            CustomRoles.Magician,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            26300,
            SetupOptionItem,
            "mag",
            OptionSort: (6, 14),
            from: From.ExtremeRoles
        );
    public Magician(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Use = OptUse.GetInt() == 0 ? -100 : OptUse.GetInt();
    }
    static OptionItem KillCoolDown;
    static OptionItem CoolDown;
    static OptionItem OptUse;
    int Use;
    private static void SetupOptionItem()
    {
        KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        CoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptUse = IntegerOptionItem.Create(RoleInfo, 5, GeneralOption.OptionCount, new(0, 99, 1), 0, false)
            .SetZeroNotation(OptionZeroNotation.Infinity);
    }
    public float CalculateKillCooldown() => KillCoolDown.GetFloat();
    bool IUsePhantomButton.IsPhantomRole => Use > 0 || Use < -90;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = CoolDown.GetFloat();
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = true;

        if (Use <= 0 && Use > -90) return;
        Use--;

        //RPC送りすぎだったら分割するかも...

        Dictionary<byte, Vector2> AllPlayersPos = new();
        HashSet<byte> Swapped = new();

        foreach (var p in PlayerCatch.AllAlivePlayerControls)
        {
            AllPlayersPos.Add(p.PlayerId, p.transform.position);
        }
        foreach (var p in PlayerCatch.AllAlivePlayerControls)
        {
            if (Swapped.Contains(p.PlayerId)) continue;

            var randomPlayer = PlayerCatch.AllAlivePlayerControls
                .Where(p => !Swapped.Contains(p.PlayerId))
                .OrderBy(p => System.Guid.NewGuid())
                .FirstOrDefault();
            var pos = randomPlayer.transform.position;
            var mypos = p.transform.position;

            Swapped.Add(p.PlayerId);
            Swapped.Add(randomPlayer.PlayerId);

            if (randomPlayer.inVent || randomPlayer.inMovingPlat || randomPlayer.onLadder || randomPlayer.walkingToVent) continue;
            if (randomPlayer.MyPhysics.Animations.IsPlayingEnterVentAnimation()) continue;
            if (randomPlayer.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) continue;
            if (randomPlayer == null || p == null) continue;

            randomPlayer.RpcSnapToForced(mypos);
            p.RpcSnapToForced(pos);
        }
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (Use <= 0) return "";

        return Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Magician), $"({Use})");
    }
}
