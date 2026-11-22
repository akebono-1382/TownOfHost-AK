using AmongUs.GameOptions;
using TownOfHostL.Roles.Core;

namespace TownOfHostL.Roles.Crewmate;

public sealed class NormalDetective : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
         SimpleRoleInfo.Create(
            typeof(NormalDetective),
            player => new NormalDetective(player),
            CustomRoles.NormalDetective,
            () => RoleTypes.Detective,
            CustomRoleTypes.Crewmate,
            (int)Options.offsetId.CrewDefault + 400,
            SetupOptionItem,
            "探偵",
            "#8cffff"
        );
    public NormalDetective(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        detectiveSuspectLimit = OptionDetectiveSuspectLimit.GetFloat();
    }
    private static OptionItem OptionDetectiveSuspectLimit;
    enum OptionName
    {
        DetectiveSuspectLimit

    }
    private static float detectiveSuspectLimit;

    private static void SetupOptionItem()
    {
        OptionDetectiveSuspectLimit = FloatOptionItem.Create(RoleInfo, OptionName.DetectiveSuspectLimit, new(1f, 4f, 1f), 2f, false)
            .SetValueFormat(OptionFormat.Players);
       
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.DetectiveSuspectLimit = detectiveSuspectLimit;
        
    }
}