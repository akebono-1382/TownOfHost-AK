using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Vanilla;

public sealed class Shapeshifter : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Shapeshifter),
            player => new Shapeshifter(player),
            RoleTypes.Shapeshifter,
            SetUpCustomOption,
            from: From.AmongUs
        );
    public Shapeshifter(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
    public static OptionItem ShapeshifterCooldown;
    public static OptionItem ShapeshifterDuration;
    public static OptionItem ShapeshifterLeaveSkin;
    public static void SetUpCustomOption()
    {
        ShapeshifterCooldown = FloatOptionItem.Create(RoleInfo, 3, "ShapeshifterCooldown", new(0f, 180f, 0.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshifterDuration = FloatOptionItem.Create(RoleInfo, 4, "ShapeshifterDuration", new(0f, 180f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
        ShapeshifterLeaveSkin = BooleanOptionItem.Create(RoleInfo, 5, StringNames.ShapeshifterLeaveSkin, false, false);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshifterCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = ShapeshifterDuration.GetFloat();
        AURoleOptions.ShapeshifterLeaveSkin = ShapeshifterLeaveSkin.GetBool();
    }
}
