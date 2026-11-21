using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Crewmate;

namespace TownOfHost.Roles.Vanilla;

public sealed class Crewmate : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Crewmate),
            player => new Crewmate(player),
            CustomRoles.Crewmate,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            27500,
            null,
            "crw",
            "#8cffff",
            (0, 0),
            from: From.AmongUs
        );
    public Crewmate(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
}