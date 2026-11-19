using HarmonyLib;
using TownOfHostL.Core.Modes;

namespace TownOfHostL;

/// <summary>
/// 自分を対象にした Shapeshift を受けると、本編は能力ボタンを自分の役職の能力で組み直す。
/// 変身の能力を持たない役職 (ロビーの全員・クルー系) では能力の設定が無く、本編の中で例外になって
/// 同じ束の後ろの電文が捨てられる。ホストは外見の着せ直しにこの形をふだんから送るので、設定が無い時は何もしない。
/// </summary>
[HarmonyPatch(typeof(AbilityButton), nameof(AbilityButton.SetFromSettings)), PatchBundle(PatchBundles.Core)]
public static class AbilityButtonGuardPatch
{
    public static bool Prefix(AbilityButtonSettings settings) => settings != null;
}
