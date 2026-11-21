using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class ShapeMaster : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(ShapeMaster),
            player => new ShapeMaster(player),
            CustomRoles.ShapeMaster,
            () => RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            5900,
            SetupOptionItem,
            "sha",
            OptionSort: (6, 4),
            from: From.TownOfHost
        );
    public ShapeMaster(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        //anime = OptionShapeAnime.GetBool();
    }
    public static OptionItem ShapeshifterCooldown;
    public static OptionItem ShapeshifterDuration;
    public static OptionItem ShapeshifterLeaveSkin;
    //static OptionItem OptionShapeAnime;
    private static float shapeshiftDuration;
    ///static bool anime;
    public static void SetupOptionItem()
    {
        ShapeshifterCooldown = FloatOptionItem.Create(RoleInfo, 3, "ShapeshifterCooldown", new(0f, 180f, 0.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshifterDuration = FloatOptionItem.Create(RoleInfo, 4, "ShapeshifterDuration", new(0f, 180f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Infinity);
        ShapeshifterLeaveSkin = BooleanOptionItem.Create(RoleInfo, 5, StringNames.ShapeshifterLeaveSkin, false, false);
        //OptionShapeAnime = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.PlayShapeAnimate, false, false);
    }

    /*public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = 0f;
        AURoleOptions.ShapeshifterLeaveSkin = false;
        AURoleOptions.ShapeshifterDuration = shapeshiftDuration;
    }*/
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshifterCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = ShapeshifterDuration.GetFloat();
        AURoleOptions.ShapeshifterLeaveSkin = ShapeshifterLeaveSkin.GetBool();
    }
    public override bool CheckShapeshift(PlayerControl target, ref bool animate)
    {
        animate = false;
        //animate = anime;
        return true;
    }
}
