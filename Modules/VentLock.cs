using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using UnityEngine;

namespace TownOfHostL.Modules;

/// <summary>
/// ミラHQ のベントのつながりを区切る (設定で ON の時)。行けないベントは、その人にだけ「掃除中」と見せて通れなくする。
/// Mod 無しの人には本編の掃除中の情報を 1 人ずつ送り、Mod を入れた人の画面では矢印に掃除中の印を付けて押しても動かない。
/// </summary>
/// <remarks>TownOfHost-hamo (rar006/TownOfHost-hamo@f7b00a9e Patches/VentLockPatch.cs) を元にした。hamo ではいつも ON だが、L では設定で選べるようにした。</remarks>
public static class VentLock
{
    static OptionItem enabled;

    public static void SetupCustomOption()
    {
        enabled = StringOptionItem.Create("MiraVentLock", new string[] { "ColoredOff", "ColoredOn" }, 0, TabGroup.ModMainSettings, false)
            .SetColor(Color.yellow)
            .SetGameMode(CustomGameMode.Standard);
    }

    // 通れなくするベントの組み合わせ (両方向)
    static readonly (int, int)[] BlockedPairs =
    {
        (6, 7),   // 管理室 ⇔ 温室
        (6, 5),   // 管理室 ⇔ オフィス
        (5, 4),   // オフィス ⇔ 研究室
        (4, 3),   // 研究室 ⇔ 原子炉
        (3, 9),   // 原子炉 ⇔ 除染室
        (9, 10),  // 除染室 ⇔ ロッカールーム
        (10, 11), // ロッカールーム ⇔ 発射台
        (10, 8),  // ロッカールーム ⇔ 医務室
        (1, 2),   // バルコニー ⇔ 廊下
    };
    // 最後にその人へ送った「掃除中に見せたベント」
    static readonly Dictionary<byte, string> LastSent = new();
    static float nextRefresh;
    public static bool ResendAll;

    /// <summary>設定が ON で、ミラHQ (マップ番号 1) の試合か</summary>
    public static bool IsActive() =>
        enabled != null && enabled.GetBool() && Options.CurrentGameMode == CustomGameMode.Standard
        && GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions.MapId == 1;

    public static bool IsBlocked(int a, int b)
    {
        foreach (var (x, y) in BlockedPairs)
            if ((x == a && y == b) || (x == b && y == a)) return true;
        return false;
    }

    public static void Reset()
    {
        LastSent.Clear();
        nextRefresh = 0f;
    }

    // 毎フレーム: その人が今いるベントから行けないベントを、その人にだけ「掃除中」と送る
    public static void Update()
    {
        if (!ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out ISystemType sys)) return;
        var vs = sys.TryCast<VentilationSystem>();
        if (vs == null) return;

        var refresh = ResendAll || Time.time >= nextRefresh;
        if (Time.time >= nextRefresh) nextRefresh = Time.time + 1f;
        ResendAll = false;

        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected) continue;

            List<byte> fake = new();
            if (vs.PlayersInsideVents.TryGetValue(pc.PlayerId, out byte inVent))
                foreach (var v in ShipStatus.Instance.AllVents)
                    if (v.Id == inVent)
                        foreach (var next in v.NearbyVents)
                            if (next != null && IsBlocked(inVent, next.Id)) fake.Add((byte)next.Id);

            var now = string.Join(",", fake);
            LastSent.TryGetValue(pc.PlayerId, out var last);
            if (now == (last ?? "") && !(refresh && fake.Count > 0)) continue;

            Send(vs, pc, fake);
            LastSent[pc.PlayerId] = now;
        }
    }

    // その人にだけ、ベントの情報 (掃除中のベント + ベントの中にいる人) を送る
    static void Send(VentilationSystem vs, PlayerControl target, List<byte> fake)
    {
        Dictionary<byte, byte> cleaning = new();
        foreach (var kv in vs.PlayersCleaningVents) cleaning[kv.Key] = kv.Value;

        // 「掃除している人」の番号は、空いている番号を借りる (本人以外)
        byte id = 250;
        foreach (var vent in fake)
        {
            while (cleaning.ContainsKey(id) || id == target.PlayerId) id--;
            cleaning[id] = vent;
        }

        var w = MessageWriter.Get(SendOption.Reliable);
        w.StartMessage(6); // 特定の 1 人にだけ送る
        w.Write(AmongUsClient.Instance.GameId);
        w.WritePacked(target.OwnerId);
        w.StartMessage(1);
        w.WritePacked(ShipStatus.Instance.NetId);
        w.StartMessage((byte)SystemTypes.Ventilation);
        w.WritePacked(cleaning.Count);
        foreach (var kv in cleaning) { w.Write(kv.Key); w.Write(kv.Value); }
        w.WritePacked(vs.PlayersInsideVents.Count);
        foreach (var kv in vs.PlayersInsideVents) { w.Write(kv.Key); w.Write(kv.Value); }
        w.EndMessage();
        w.EndMessage();
        w.EndMessage();
        AmongUsClient.Instance.SendOrDisconnect(w);
        w.Recycle();
    }

    // 試合が始まったら最初から
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
    static class ResetPatch
    {
        public static void Postfix() => Reset();
    }

    // 本当のベントの情報が全員に送られたら、送り直す
    [HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.Serialize))]
    static class SerializePatch
    {
        public static void Postfix() => ResendAll = true;
    }

    // 毎フレーム (ホストだけ・ミラHQ だけ)
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    static class UpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (__instance != PlayerControl.LocalPlayer || !AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !IsActive()) return;
            try { Update(); }
            catch (System.Exception e) { Logger.Warn($"ベントの区切りでエラー: {e.Message}", nameof(VentLock)); }
        }
    }

    // 自分の画面: 行けない矢印に掃除中の印を付ける
    [HarmonyPatch(typeof(Vent), nameof(Vent.UpdateArrows))]
    static class ArrowPatch
    {
        public static void Postfix(Vent __instance)
        {
            if (!IsActive()) return;
            var nearby = __instance.NearbyVents;
            var marks = __instance.CleaningIndicators;
            for (var i = 0; i < nearby.Length && i < marks.Length; i++)
                if (nearby[i] != null && marks[i] != null && IsBlocked(__instance.Id, nearby[i].Id))
                    marks[i].SetActive(true);
        }
    }

    // 自分の画面: 行けない矢印を押しても動かない
    [HarmonyPatch(typeof(Vent), nameof(Vent.TryMoveToVent))]
    static class MovePatch
    {
        public static bool Prefix(Vent __instance, Vent otherVent, ref bool __result)
        {
            if (!IsActive() || otherVent == null || !IsBlocked(__instance.Id, otherVent.Id)) return true;
            __result = false;
            return false;
        }
    }
}
