using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Neutral;

public sealed class Emptiness : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Emptiness),
            player => new Emptiness(player),
            CustomRoles.Emptiness,
            () => RoleTypes.CrewmateGhost,
            CustomRoleTypes.Neutral,
            28800,
            null,
            "Emp",
            "#221d26",
            (7, 1),
            assignInfo: new(CustomRoles.Emptiness, CustomRoleTypes.Neutral)
            {
                IsInitiallyAssignableCallBack = () => false
            },
            from: From.TownOfHost_K
        );
    public Emptiness(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
    }
}