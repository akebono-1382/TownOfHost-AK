using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace TownOfHostL.Core.Modes;

/// TOHL のパッチの束。モードは <see cref="GameMode.LiveBundles"/> で、どの束を当てておくかを宣言する。
/// 束を付けていないパッチは <see cref="Standard"/> に入る。
[Flags]
public enum PatchBundles
{
    None = 0,
    /// <summary>通常モードの仕組み全体。束を付けていないパッチは全部ここ。</summary>
    Standard = 1 << 0,
    /// <summary>どのモードでも要る下回り (試合の寿命・プレイヤーの一覧・LateTask の駆動・TOHL の表記)。電文は送らない。</summary>
    Core = 1 << 1,
    /// <summary>本編のかくれんぼで TOHL が休んでいる間だけ当てる物 (かくれんぼ用の役職)。</summary>
    VanillaHideAndSeek = 1 << 2,
    /// <summary>ロビーの設定の画面 (TOHL のタブ)。どの設定を出すかはモードの <see cref="GameMode.ShowsOption"/> で決まる。</summary>
    Settings = 1 << 3,

    All = Standard | Core | VanillaHideAndSeek | Settings,
}

/// <summary>パッチのクラス (入れ子なら外側のどれか) に付けて、束を決める。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PatchBundleAttribute(PatchBundles bundle) : Attribute
{
    public PatchBundles Bundle { get; } = bundle;
}

/// <summary>束ごとに Harmony を分けて、当てる・外すを束の単位で行う。</summary>
public static class PatchBundleSet
{
    // 当てる順。同じメソッドに当てる束どうしの順番がいつも同じになるよう、足す時もこの順で全部を当て直す
    private static readonly PatchBundles[] Order = [PatchBundles.Core, PatchBundles.Settings, PatchBundles.VanillaHideAndSeek, PatchBundles.Standard];

    private static readonly Dictionary<PatchBundles, Harmony> harmonies = new();
    private static readonly Dictionary<PatchBundles, List<Type>> types = new();

    /// <summary>いま当たっている束。</summary>
    public static PatchBundles Live { get; private set; }

    /// <summary>束の分け方を読む。<see cref="Standard"/> は TOHL の元の Harmony (ID を変えない) を使う。</summary>
    public static void Init(Harmony standard)
    {
        foreach (var bundle in Order)
        {
            harmonies[bundle] = bundle == PatchBundles.Standard ? standard : new Harmony($"{Main.PluginGuid}.{bundle}");
            types[bundle] = new();
        }
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Main).Assembly))
        {
            // パッチではないクラス (数千個) は Harmony に渡さない。渡すだけで起動が遅くなる (TownOfHost-hamo の DeferredPatcher を元にした)。
            if (!IsPatchClass(type)) continue;
            var bundle = BundleOf(type);
            if (!types.TryGetValue(bundle, out var list))
                throw new InvalidOperationException($"{type.FullName} の束 {bundle} は 1 つの束ではありません");
            list.Add(type);
        }
    }

    private static bool IsPatchClass(Type type)
    {
        if (type.IsDefined(typeof(HarmonyAttribute), false)) return true;
        try
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (m.IsDefined(typeof(HarmonyAttribute), false)) return true;
        }
        catch { return true; }
        return false;
    }

    private static PatchBundles BundleOf(Type type)
    {
        for (var t = type; t != null; t = t.DeclaringType)
            if (t.GetCustomAttribute<PatchBundleAttribute>(false) is { } attr) return attr.Bundle;
        return PatchBundles.Standard;
    }

    /// <summary>当てる束を <paramref name="target"/> に合わせる。</summary>
    public static void Apply(PatchBundles target)
    {
        target &= PatchBundles.All;
        if (target == Live) return;
        DeferredPatches.FinishNow();
        var sw = Stopwatch.StartNew();
        if ((target & ~Live) != 0)
        {
            // 足す束があれば、当たっている物も外して決まった順で当て直す
            foreach (var bundle in Order) if (Live.HasFlag(bundle)) harmonies[bundle].UnpatchSelf();
            Live = PatchBundles.None;
            foreach (var bundle in Order)
            {
                if (!target.HasFlag(bundle)) continue;
                var harmony = harmonies[bundle];
                foreach (var type in types[bundle])
                {
                    if (DeferredPatches.TryDefer(harmony, type)) continue;
                    harmony.CreateClassProcessor(type).Patch();
                }
            }
        }
        else
        {
            foreach (var bundle in Order) if (Live.HasFlag(bundle) && !target.HasFlag(bundle)) harmonies[bundle].UnpatchSelf();
        }
        Live = target;
        int methods = harmonies.Values.Sum(h => h.GetPatchedMethods().Count());
        Logger.Info($"パッチの束を {target} にしました ({methods} メソッド・{sw.ElapsedMilliseconds}ms)", "PatchBundles");
    }
}

/// <remarks>TownOfHost-hamo (rar006/TownOfHost-hamo@f7b00a9e Modules/DeferredPatcher.cs) を元に、L の束 (<see cref="PatchBundleSet"/>) に合わせた。</remarks>
public static class DeferredPatches
{
    public static bool Enabled;

    static readonly HashSet<string> DeferredTargets = new()
    {
        "PlayerControl", "MeetingHud", "LobbyBehaviour", "HudManager", "ChatController", "ShipStatus",
        "PlayerPhysics", "IntroCutscene", "EndGameManager", "ExileController", "Vent", "NetworkedPlayerInfo",
        "SpawnInMinigame", "SabotageButton", "MovingPlatformBehaviour", "LogicGameFlowNormal", "ElectricTask",
        "GameManager", "TaskAddButton",
        "GameStartManager", "GameSettingMenu", "GameOptionsMenu", "StringOption", "NumberOption", "ToggleOption",
        "MapBehaviour", "VentilationSystem", "HeliSabotageSystem", "MushroomMixupSabotageSystem",
        "SwitchSystem", "SecurityCameraSystemType", "VitalsMinigame", "SurveillanceMinigame", "SecurityLogGame",
        "ShapeshifterMinigame", "ShapeshifterPanel", "TaskAdderGame", "TaskPanelBehaviour", "ViewSettingsInfoPanel",
        "BanMenu", "DetectiveRole",
        "RoleManager", "SabotageSystemType", "RolesSettingsMenu", "TutorialManager",
    };

    static readonly HashSet<string> RoomOnlyClientMethods = new()
    {
        "OnGameEnd", "OnPlayerJoined", "OnPlayerLeft", "StartGame", "CoStartGame", "KickPlayer", "CreatePlayer",
        "CanBan", "StartRpcImmediately", "FinishRpcImmediately", "SendAllStreamedObjects",
    };

    static readonly Queue<(Harmony harmony, Type type)> pending = new();
    static bool begun;
    static int total;
    static readonly Stopwatch totalWatch = new();

    public static int PendingCount => pending.Count;

    public static bool TryDefer(Harmony harmony, Type type)
    {
        if (!Enabled || !IsDeferred(type)) return false;
        pending.Enqueue((harmony, type));
        total++;
        return true;
    }

    static bool IsDeferred(Type type)
    {
        try
        {
            var attrs = HarmonyMethodExtensions.GetFromType(type);
            if (attrs == null || attrs.Count == 0) return false;
            var merged = HarmonyMethod.Merge(attrs);
            var target = merged?.declaringType;
            if (target == null) return false;
            if (DeferredTargets.Contains(target.Name)) return true;
            return target.Name is "AmongUsClient" or "InnerNetClient" && merged.methodName != null && RoomOnlyClientMethods.Contains(merged.methodName);
        }
        catch { return false; }
    }

    public static void Tick()
    {
        if (pending.Count == 0) return;
        if (!begun) { begun = true; totalWatch.Start(); }
        var frame = Stopwatch.StartNew();
        while (pending.Count > 0 && frame.ElapsedMilliseconds < 12) PatchOne(pending.Dequeue());
        if (pending.Count == 0) Logger.Info($"後回しのパッチを当て終えました ({total} クラス・{totalWatch.ElapsedMilliseconds}ms)", "DeferredPatches");
    }

    public static void FinishNow()
    {
        if (pending.Count == 0) return;
        var sw = Stopwatch.StartNew();
        var count = pending.Count;
        while (pending.Count > 0) PatchOne(pending.Dequeue());
        Logger.Info($"後回しのパッチを今すぐ当てました ({count} クラス・{sw.ElapsedMilliseconds}ms)", "DeferredPatches");
    }

    static void PatchOne((Harmony harmony, Type type) item)
    {
        try { item.harmony.CreateClassProcessor(item.type).Patch(); }
        catch (Exception e) { Logger.Error($"後回しのパッチを当てられませんでした: {item.type.FullName}\n{e}", "DeferredPatches"); }
    }

    [PatchBundle(PatchBundles.Core)]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
    static class MainMenuTickPatch
    {
        public static void Postfix() => Tick();
    }

    [PatchBundle(PatchBundles.Core)]
    [HarmonyPatch(typeof(MainMenuManager))]
    static class MainMenuFinishPatch
    {
        [HarmonyPatch(nameof(MainMenuManager.OpenGameModeMenu)), HarmonyPatch(nameof(MainMenuManager.OpenOnlineMenu)),
         HarmonyPatch(nameof(MainMenuManager.OpenCreateGame)), HarmonyPatch(nameof(MainMenuManager.OpenEnterCodeMenu))]
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        public static void Prefix() => FinishNow();
    }

    [PatchBundle(PatchBundles.Core)]
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    static class GameJoinedPatch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => FinishNow();
    }

    [PatchBundle(PatchBundles.Core)]
    [HarmonyPatch(typeof(SceneChanger), nameof(SceneChanger.ChangeScene), typeof(string))]
    static class ChangeScenePatch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => FinishNow();
    }
}
