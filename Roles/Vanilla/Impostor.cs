using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Vanilla;

public sealed class Impostor : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Impostor),
            player => new Impostor(player),
            CustomRoles.Impostor,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            27600,
            null,
            "imp",
            OptionSort: (0, 0),
            from: From.AmongUs
        );
    public Impostor(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
}