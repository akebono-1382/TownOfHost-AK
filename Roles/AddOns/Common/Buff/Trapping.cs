using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Trapping
    {
        private static readonly int Id = 26000;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Trapping);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "Ｔr");
        public static OptionItem BlockMoveTime;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Trapping);
            AddOnsAssignData.Create(Id + 10, CustomRoles.Trapping, true, true, true, true);

            ObjectOptionitem.Create(Id + 20, "AddonOption", true, "", TabGroup.Addons).SetOptionName(() => "Role Option")
                .SetSubRoleOptionItem(CustomRoles.Trapping);
            BlockMoveTime = FloatOptionItem.Create(Id + 21, "TrapperBlockMoveTime", new(0f, 180f, 0.5f), 10f, TabGroup.Addons, false)
                .SetValueFormat(OptionFormat.Seconds).SetSubRoleOptionItem(CustomRoles.Trapping);
        }
    }
}