using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Madmate;

public sealed class SKMadmate : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(SKMadmate),
            player => new SKMadmate(player),
            CustomRoles.SKMadmate,
            () => Options.SkMadCanUseVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            28700,
            null,
            "sm",
            OptionSort: (1, 0),
            introSound: () => GetIntroSound(RoleTypes.Impostor),
            assignInfo: new RoleAssignInfo(CustomRoles.Madmate, CustomRoleTypes.Madmate)
            {
                AssignCountRule = new(0, 15, 1),
                IsInitiallyAssignableCallBack = () => false
            },
            from: From.TownOfHost
        );
    public SKMadmate(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
    }
    private static bool canSeeKillFlash;
    private static bool canSeeDeathReason;
    public bool? CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override CustomRoles TellResults(PlayerControl player) => Options.MadTellOpt();
}
