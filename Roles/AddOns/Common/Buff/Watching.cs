using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;
using static TownOfHost.Options;

namespace TownOfHost.Roles.AddOns.Common
{
    public static class Watching
    {
        private static readonly int Id = 18100;
        private static Color RoleColor = UtilsRoleText.GetRoleColor(CustomRoles.Watching);
        public static string SubRoleMark = Utils.ColorString(RoleColor, "∑");
        private static List<byte> playerIdList = new();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Watching, fromtext: UtilsOption.GetFrom(From.TOR_GM_Edition));
            AddOnsAssignData.Create(Id + 10, CustomRoles.Watching, true, true, true, true);
        }
        public static void Init()
        {
            playerIdList = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }
    }
}