using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Godfather : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Godfather),
            player => new Godfather(player),
            CustomRoles.Godfather,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            26700,
            SetupOptionItem,
            "Gfr",
            OptionSort: (0, 1),
            tab: TabGroup.Combinations,
            combination: CombinationRoles.GodfatherandJanitor,
            assignInfo: new RoleAssignInfo(CustomRoles.Godfather, CustomRoleTypes.Impostor)
            {
                AssignCountRule = new(1, 1, 1),
                AssignUnitRoles = new CustomRoles[2] { CustomRoles.Godfather, CustomRoles.Janitor }
            },
            from: From.TheOtherRoles
        );
    public Godfather(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
    }
    static OptionItem OptionKillCoolDown;
    public static OptionItem Cooldown;
    private static void SetupOptionItem()
    {
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
        ObjectOptionitem.Create(RoleInfo, 11, "JanitorSetting", true, null).SetOptionName(() => "Janitor Setting");
        Cooldown = FloatOptionItem.Create(RoleInfo, 12, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
    }
    public float CalculateKillCooldown() => OptionKillCoolDown.GetFloat();
}