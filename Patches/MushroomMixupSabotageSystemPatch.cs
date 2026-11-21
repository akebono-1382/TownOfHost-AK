using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using TownOfHostL.Attributes;
using TownOfHostL.Roles.Core;
using TownOfHostL.Roles.Core.Interfaces;
using TownOfHostL.Roles.Neutral;

namespace TownOfHostL;

[HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.UpdateSystem))]
public static class MushroomMixupUpdateSystemPatch
{
    public static bool InSabotage => NameChanged && instance != null && instance.IsActive;
    /// <summary>名前を隠した見る人が居るか (チャットの名前の戻しを宛先ごとに分ける)</summary>
    public static bool HidesNamesForAnyone => InSabotage && ChangedPlayers.Count > 0;
    public static bool NameChanged = false;
    private static MushroomMixupSabotageSystem instance;
    public static List<PlayerControl> TargetPlayers = new();
    public static List<PlayerControl> ChangedPlayers = new();
    /// <summary>隠しと戻しのたびに進める。遅らせた送信が、後の戻しや会議の名前を上書きしないようにする</summary>
    public static int Generation;
    public const string HiddenName = "<color=#00000000>.";

    [GameModuleInitializer]
    public static void Init()
    {
        NameChanged = false;
        instance = null;
        TargetPlayers.Clear();
        ChangedPlayers.Clear();
        Generation++;
    }

    public static void Prefix(MushroomMixupSabotageSystem __instance, MessageReader msgReader)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        byte amount;
        {
            var newReader = MessageReader.Get(msgReader);
            amount = newReader.ReadByte();
            newReader.Recycle();
        }
        if ((MushroomMixupSabotageSystem.Operation)amount != MushroomMixupSabotageSystem.Operation.TriggerSabotage) return;
        // 起きている間の重ねての要求で、隠した人の一覧を作り直さない
        if (__instance.IsActive && NameChanged) return;

        TargetPlayers.Clear();
        ChangedPlayers.Clear();
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            TargetPlayers.Add(pc);
        }

        int token = ++Generation;
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            var role = pc.GetRoleClass();
            if (pc.PlayerId != PlayerControl.LocalPlayer.PlayerId &&
                role is IKiller && role is not IImpostor &&
                !role.SeesNamesInMushroomMixup)
            {
                ChangedPlayers.Add(pc);
                foreach (PlayerControl target in TargetPlayers)
                {
                    if (target.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                    {
                        var seer = pc;
                        _ = new LateTask(() =>
                        {
                            if (token != Generation || !InSabotage || GameStates.IsMeeting) return;
                            target.RpcSetNamePrivate(HiddenName, seer: seer, force: true);
                        }, 1f, "MushroomMixupSetName");
                    }
                    else
                    {
                        target.RpcSetNamePrivate(HiddenName, seer: pc, force: true);
                    }
                }
            }
        }
        instance = __instance;
        NameChanged = true;
    }
}
[HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.Deteriorate))]
public static class MushroomMixupDeterioratePatch
{
    public static void Prefix(MushroomMixupSabotageSystem __instance, float deltaTime)
    {
        if (!__instance.IsActive) return;
        if ((double)__instance.currentSecondsUntilHeal - deltaTime > 0.0) return;

        RestorName();
    }
    /// <summary>
    /// 隠した名前を戻す。名前は NotifyRoles で組み直して送る (変身中は変身先の名前・仲間の色などをそのまま出す)。
    /// ホストの名前は本編が直った時に送り直すので、少し遅らせてもう一度送る
    /// </summary>
    public static void RestorName()
    {
        if (!MushroomMixupUpdateSystemPatch.NameChanged) return;
        MushroomMixupUpdateSystemPatch.TargetPlayers.Clear();
        MushroomMixupUpdateSystemPatch.ChangedPlayers.Clear();
        MushroomMixupUpdateSystemPatch.NameChanged = false;
        int token = ++MushroomMixupUpdateSystemPatch.Generation;

        if (!AmongUsClient.Instance.AmHost) return;
        // 起きている間は NotifyRoles を止めていたので、全員の名前を組み直して送る
        Utils.NotifyRolesNow(NoCache: true);
        _ = new LateTask(() =>
        {
            // 会議に入っていれば、会議の名前を上書きしない
            if (token != MushroomMixupUpdateSystemPatch.Generation || !GameStates.IsInTask) return;
            Utils.NotifyRoles(NoCache: true);
        }, 0.3f, "MushroomMixupRestoreNotifyRoles");
    }
}
