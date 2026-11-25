using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;

public sealed class GM : RoleBase, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(GM),
            player => new GM(player),
            CustomRoles.GM,
            () => RoleTypes.CrewmateGhost,
            CustomRoleTypes.Neutral,
            28900,
            null,
            "Gm",
            "#ff5b70",
            (7, 1),
            assignInfo: new(CustomRoles.GM, CustomRoleTypes.Neutral)
            {
                IsInitiallyAssignableCallBack = () => false
            },
            countType: CountTypes.OutOfGame,
            from: From.TOR_GM_Edition
        );
    public GM(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False 
    )
    {
    }
    bool? IKillFlashSeeable.CheckKillFlash(MurderInfo _) => true;
    public override void StartGameTasks()
    {
        if (Player.PlayerId != 0)
        {
            Player.RpcSetCustomRole(CustomRoles.Crewmate);
            return;
        }
        Player.RpcExileV3();
        Player.GetPlayerState().SetDead();
    }
}