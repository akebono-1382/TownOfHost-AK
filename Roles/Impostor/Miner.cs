using System.Collections.Generic;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class Miner : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Miner),
            player => new Miner(player),
            CustomRoles.Miner,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            28100,
            SetupOptionItem,
            "Mnr",
            from: From.TownOfUs,
            OptionSort: (6, 15)
        );
    public Miner(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillCooldown = OptionKillCoolDown.GetFloat();
        Cooldown = OptionCoolDown.GetFloat();
        ShapeShifting = false;
        NowPending = false;
        PendingVent = new Vector2(999f, 999f);

        CustomRoleManager.OnFixedUpdateOthers.Add(OnFixedUpdateOthers);
    }
    static OptionItem OptionKillCoolDown; static float KillCooldown;
    static OptionItem OptionCoolDown; static float Cooldown;
    bool ShapeShifting;
    bool NowPending;
    Vector2 PendingVent;
    public static Dictionary<Vector2, Vector2> Vents = new();
    //ベントが使えない人
    public static Dictionary<byte, float> Wait = new();
    const float VentDis = 1.2f;
    private static void SetupOptionItem()
    {
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, OptionBaseCoolTime, 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public float CalculateKillCooldown() => KillCooldown;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = Cooldown;
        AURoleOptions.ShapeshifterDuration = 0.1f;
        AURoleOptions.ShapeshifterLeaveSkin = true;
    }
    public override bool CheckShapeshift(PlayerControl target, ref bool shouldAnimate)
    {
        shouldAnimate = !ShapeShifting;
        ShapeShifting = !ShapeShifting;
        if (ShapeShifting is false) return true;
        if (NowPending)
        {
            NowPending = false;
            Vents.Add(PendingVent, Player.transform.position);
            PendingVent = new Vector2(999f, 999f);
            //すぐTPさせないための処置
            Wait.Add(Player.PlayerId, 5f);
        }
        else
        {
            NowPending = true;
            PendingVent = Player.transform.position;
        }
        return true;
    }
    public static void OnFixedUpdateOthers(PlayerControl player)
    {
        if (Wait.ContainsKey(player.PlayerId))
        {
            Wait[player.PlayerId] -= Time.fixedDeltaTime;
            if (Wait[player.PlayerId] <= 0f) Wait.Remove(player.PlayerId);
            else return;
        }
        foreach (var v in Vents)
        {
            var keyvent = v.Key;
            var valuevent = v.Value;

            if (Vector2.Distance(keyvent, player.transform.position) < VentDis)
            {
                Logger.Info($"{player.Data.PlayerName}が{keyvent}のベントを利用、{valuevent}にTP", "Miner");
                player.RpcSnapToForced(valuevent);
                Wait.Add(player.PlayerId, 5f);
                break;
            }
            if (Vector2.Distance(valuevent, player.transform.position) < VentDis)
            {
                Logger.Info($"{player.Data.PlayerName}が{valuevent}のベントを利用、{keyvent}にTP", "Miner");
                player.RpcSnapToForced(keyvent);
                Wait.Add(player.PlayerId, 5f);
                break;
            }
        }
    }
}