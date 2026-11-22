using HarmonyLib;
using TownOfHostL.Core.Modes;

namespace TownOfHostL.Modules.Net;

/// <summary>
/// 同じ版の TOHL のホストの部屋にいる客の画面に、ホストから届いた役職と設定で TOHL の表示を出す。
/// 客の間はホストの処理 (Standard) を外しているので、表示だけをここ (Core) で当て、描き方はホストの画面と同じ処理を呼ぶ。
/// 役職の判定や送る処理は持たない。頭の上の名前はホストが一人ずつ送っている物をそのまま使う。
/// </summary>
[HarmonyPatch, PatchBundle(PatchBundles.Core)]
public static class ModClientView
{
    // ロビーの左上の設定の一覧。ホストが替わった時は届いた物を捨てる
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update)), HarmonyPostfix]
    public static void HudUpdatePostfix(HudManager __instance)
    {
        if (!TownOfHostL.Core.VanillaHideAndSeek.IsClientMode) return;
        ModClientSync.Tick();
        if (!ModClientSync.IsLinked) return;
        HudManagerPatch.EnsureOptionShower(__instance);
        HudManagerPatch.UpdateOptionShower();
    }

    // イントロの役職の名前・色・説明
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin)), HarmonyPostfix]
    public static void CoBeginPostfix(IntroCutscene __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (!ModClientSync.ViewReady) return;
        var intro = __instance;
        var patcher = new CoroutinPatcher(__result);
        patcher.AddPrefix(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole), () => SetUpRoleTextPatch.Apply(intro));
        __result = patcher.EnumerateWithPatch();
    }

    // イントロの陣営の帯と、一緒に並べる仲間
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate)), HarmonyPrefix]
    public static void BeginCrewmatePrefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        if (ModClientSync.ViewReady) BeginCrewmatePatch.Prefix(__instance, ref teamToDisplay);
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate)), HarmonyPostfix]
    public static void BeginCrewmatePostfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        if (ModClientSync.ViewReady) BeginCrewmatePatch.Postfix(__instance, ref teamToDisplay);
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor)), HarmonyPrefix]
    public static bool BeginImpostorPrefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
    {
        return !ModClientSync.ViewReady || BeginImpostorPatch.Prefix(__instance, ref yourTeam);
    }

    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor)), HarmonyPostfix]
    public static void BeginImpostorPostfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
    {
        if (ModClientSync.ViewReady) BeginImpostorPatch.Postfix(__instance, ref yourTeam);
    }

    // タスクの欄の頭の、自分の役職の名前と説明
    [HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText)), HarmonyPostfix]
    public static void SetTaskTextPostfix(TaskPanelBehaviour __instance)
    {
        if (!ModClientSync.ViewReady || GameStates.IsLobby) return;
        TaskPanelBehaviourPatch.PrependRoleInfo(__instance);
    }

    // ホスト: 試合を終える電文の直前に、同じ版の TOHL の客へ結果を送る (終わりの電文の後では客の側で受ける物が無くなる)
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.RpcEndGame)), HarmonyPrefix]
    public static void RpcEndGamePrefix()
    {
        try { ModClientSync.SendEnd(); }
        catch (System.Exception ex) { Logger.Exception(ex, "ModClientSync.SendEnd"); }
    }

    // 結果画面の勝者の文と、全員の役職の一覧
    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp)), HarmonyPostfix]
    public static void SetEverythingUpPostfix(EndGameManager __instance)
    {
        if (!ModClientSync.EndReady) return;
        SetEverythingUpPatch.Postfix(__instance);
        ModClientSync.OnEndShown();
    }
}
