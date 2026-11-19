using HarmonyLib;

namespace TownOfHostL
{
    // 本編の不具合の手元の対策なので、TOHL 入りの客でもどのモードでも当てる
    [TownOfHostL.Core.Modes.PatchBundle(TownOfHostL.Core.Modes.PatchBundles.Core)]
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Toggle))]
    class CancelBanMenuStuckPatch
    {
        public static void Prefix(ChatController __instance)
        {
            if (__instance.IsOpenOrOpening && !__instance.IsAnimating) // (IsOpen==true) == 今から閉じないといけない
            {
                // BanButtonを非表示にする
                __instance.banButton.SetVisible(false);
            }
        }
    }
}