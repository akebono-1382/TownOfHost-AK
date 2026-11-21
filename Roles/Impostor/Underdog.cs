using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Underdog : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Underdog),
            player => new Underdog(player),
            CustomRoles.Underdog,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            28000,
            SetupOptionItem,
            "Udg",
            from: From.TownOfUs,
            OptionSort: (7, 8),
            Desc: () => string.Format(GetString("UnderdogDesc"), ChengeAliveImpostor.GetInt(), AfterKillCooldown.GetFloat()) 
        );
    public Underdog(PlayerControl player)
    : base(RoleInfo, player)
    {
    }
    enum Option
    {
        UnderdogBeforeKillCooldown,
        UnderdogChengeAliveImpostor,
        UnderdogAfterKillCooldown,
    }

    static OptionItem BeforeKillCooldown; //覚醒前のキルク
    static OptionItem ChengeAliveImpostor; //覚醒するインポ人数
    static OptionItem AfterKillCooldown; //覚醒後のキルク
    private static void SetupOptionItem()
    {
        BeforeKillCooldown = FloatOptionItem.Create(RoleInfo, 3, Option.UnderdogBeforeKillCooldown, new(0, 180, 0.5f), 45f, false)
            .SetValueFormat(OptionFormat.Seconds);
        ChengeAliveImpostor = IntegerOptionItem.Create(RoleInfo, 4, Option.UnderdogChengeAliveImpostor, new(1, 2, 1), 1)
            .SetValueFormat(OptionFormat.Players);
        AfterKillCooldown = FloatOptionItem.Create(RoleInfo, 5, Option.UnderdogAfterKillCooldown, new(0, 180, 0.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public float CalculateKillCooldown()
    {
        if (PlayerCatch.AliveImpostorCount > ChengeAliveImpostor.GetInt()) return BeforeKillCooldown.GetFloat();
        return AfterKillCooldown.GetFloat();
    }
}