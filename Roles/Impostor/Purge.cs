//復活予定なし。
/*using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Purge : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Purge),
            player => new Purge(player),
            CustomRoles.Purge,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            27900,
            SetupOptionItem,
            "Pre",
            OptionSort: (7, 7)
        );
    public Purge(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
    }
    static OptionItem KillCoolDown;
    private static void SetupOptionItem()
    {
        KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OverrideKilldistance.Create(RoleInfo, 4);
    }
    public float CalculateKillCooldown() => KillCoolDown.GetFloat();

    public override bool GetTemporaryName(ref string name, ref bool NoMarker, bool isForMeeting, PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        if (!Is(seen)) return false;

        name = "<#ff1919><size=180>■</color></size>";
        return true;
    }
}*/