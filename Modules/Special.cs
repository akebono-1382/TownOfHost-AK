using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Roles;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost;

static class Event
{
    public static bool IsChristmas = DateTime.Now.Month == 12 && DateTime.Now.Day is 24 or 25;
    public static bool White = DateTime.Now.Month == 3 && DateTime.Now.Day is 14;
    public static bool IsInitialRelease = DateTime.Now.Month == 11 && DateTime.Now.Day is 3;
    public static bool IsHalloween = (DateTime.Now.Month == 10 && DateTime.Now.Day is 31) || (DateTime.Now.Month == 11 && DateTime.Now.Day is 1 or 2 or 3 or 4 or 5 or 6 or 7);
    public static bool GoldenWeek = DateTime.Now.Month == 5 && DateTime.Now.Day is 3 or 4 or 5;
    public static bool April = DateTime.Now.Month == 4 && DateTime.Now.Day is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8;
    public static bool Tanabata = DateTime.Now.Month == 7 && DateTime.Now.Day is > 6 and < 15;
    public static bool IsEventDay => IsChristmas || White || IsInitialRelease || IsHalloween || GoldenWeek || April;
    public static bool Special = false;
    public static bool NowRoleEvent => false ;
    public static List<string> OptionLoad = new();

    /// <summary>
    /// 役職が使用可能かチェックします
    /// (元期間限定用。期間限定役は追加予定なし)
    /// </summary>
    /// <returns>役職が使用可能か</returns>
    public static bool CheckRole(CustomRoles role, bool useApiData = true)
    {
        if (remove.Contains(role))
        {
            if (Options.CustomRoleSpawnChances.ContainsKey(role) && Options.CustomRoleSpawnChances[role].GetValue() > 0)
            {
                Options.CustomRoleSpawnChances[role].SetValue(0, true);
                //Options.CustomRoleSpawnChances[role].SetEnabled(() => false);
            }
            return false;
        }

        //封印役でない場合のみ実行
        //期間限定役がいないならtrue
        if (EventRoles.Count < 1) return true;

        //イベント役職以外はtrueを返す!!
        if (!EventRoles.TryGetValue(role, out var check)) return true;
        //キャッシュ済みならそっちを使う
        if (cachedEventFlags.TryGetValue((role, useApiData), out bool cachedData))
            return cachedData;

        bool result = check.Invoke();
        if (!useApiData || (VersionInfoManager.version?.Events == null && VersionInfoManager.allversion?.Events == null))
        {
            //apiが使えない状態orフラグがfalseの場合はローカルの状態を使用
            cachedEventFlags[(role, false)] = result;
            return result;
        }

        var roleId = (int)role;
        var events = VersionInfoManager.version?.Events;
        var allverevents = VersionInfoManager.allversion?.Events;

        //全イベント情報をチェック
        if (events != null)
        {
            foreach (var data in events)
            {
                if (result) continue;
                result |= data.RoleId == roleId && data.Period.IsActive;
            }
        }
        if (allverevents != null)
        {
            foreach (var data in allverevents)
            {
                if (result) continue;
                result |= data.RoleId == roleId && data.Period.IsActive;
            }
        }
        cachedEventFlags[(role, true)] = result;
        return result;
    }
    public static Dictionary<CustomRoles, Func<bool>> EventRoles = new()
    {
    };
    public static HashSet<CustomRoles> remove = new()
    {
        //Event
        CustomRoles.Altair,
        CustomRoles.Vega,
        CustomRoles.Cakeshop,

        //封印
        CustomRoles.Explosioner,
        CustomRoles.RocketLauncher,
        CustomRoles.Busker,
        CustomRoles.Usurper,

        //一旦封印
        CustomRoles.Godfather,
        CustomRoles.Janitor,
        CustomRoles.Mafia,
        CustomRoles.EvilChainer,
        CustomRoles.Poisoner,
    };

    public static Dictionary<(CustomRoles role, bool isApiData), bool> cachedEventFlags = new(EventRoles.Count);
}