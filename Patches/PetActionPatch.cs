using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using TownOfHostL.Modules.Net;
using TownOfHostL.Roles.Core;

namespace TownOfHostL.Patches;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
internal static class LocalPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = new();

    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (GameStates.IsLobby || !__instance.IsAlive()) return true;
        if (__instance.petting) return true;

        __instance.petting = true;

        if (!LastProcess.ContainsKey(__instance.PlayerId))
            LastProcess[__instance.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
        if (LastProcess[__instance.PlayerId] + 1 >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, (byte)RpcCalls.Pet);

        LastProcess[__instance.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return true;
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        __instance.petting = false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
internal static class ExternalRpcPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = new();

    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callID)
    {
        if ((RpcCalls)callID != RpcCalls.Pet) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby) return;

        var pc = __instance.myPlayer;
        if (pc == null || !pc.IsAlive()) return;
        if (!LastProcess.ContainsKey(pc.PlayerId))
            LastProcess[pc.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2;
        if (LastProcess[pc.PlayerId] + 1 >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return;

        LastProcess[pc.PlayerId] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // かわいがりはホストから止めない。本人がボタンを離すか歩くと本編が自分で止める。押している間にホストが止めると、
        // 離した時の本編の解除 (PlayerPhysics.CheckCancelPetting) が走らず、ペットのボタンが押せないまま残る (2026-11-08 実機で確認)
        Logger.Info($"{pc.Data?.PlayerName} がペットを撫でた", "PetActionPatch");

        OnPetUse(pc);
    }

    private static void OnPetUse(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby || GameStates.IsMeeting) return;

        // ケイドロ: 牢屋の前で撫でると解放(ホスト自身の撫でもここを通る)

        if (pc.inVent || pc.inMovingPlat || pc.onLadder || pc.walkingToVent) return;
        if (pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return;

        if (PetActionManager.Handlers.TryGetValue(pc.PlayerId, out var handler))
        {
            handler.Invoke();
            Logger.Info($"{pc.Data?.PlayerName} のOnPet実行", "PetActionPatch");
        }
    }
}

public static class PetsHelper
{
    // 今の既定の外見のペットだけを変えて OutfitSync で送る (他人の SetPetStr はホストから送らない)。
    // カモフラ中などで戻す先の外見も同じペットにそろえる
    public static void SetPet(PlayerControl pc, string petId)
    {
        if (pc == null) return;
        if (Camouflage.PlayerSkins.TryGetValue(pc.PlayerId, out var original)) original.PetId = petId;
        var cur = pc.Data?.DefaultOutfit;
        if (cur == null) return;
        OutfitSync.Apply(pc, new NetworkedPlayerInfo.PlayerOutfit().Set(cur.PlayerName, cur.ColorId, cur.HatId, cur.SkinId, cur.VisorId, petId, cur.NamePlateId));
    }

    public static void RemovePet(PlayerControl pc)
    {
        // 死んだ直後 (PlayerControl.Die) は TOHL の生死より先に本編の IsDead が立つので、どちらかで死んでいれば外す
        if (pc?.Data == null || (pc.IsAlive() && !pc.Data.IsDead)) return;
        if (string.IsNullOrEmpty(pc.Data.DefaultOutfit?.PetId)) return;

        SetPet(pc, "");

        Logger.Info($"{pc.Data?.PlayerName} が死亡したのでペットを外しました", "PetActionPatch");
    }
}

#region ペットを自動破棄
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class RemoveDeadPlayersPetsOnMeetingStartPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        foreach (var pc in Main.AllPlayerControls)
            PetsHelper.RemovePet(pc);
    }
}
#endregion

public static class PetActionManager
{
    private const string DefaultPetIdForPetAction = "pet_test";

    public static readonly Dictionary<byte, System.Action> Handlers = new();
    public static bool AutoGrantPetEnabled => Options.AutoGrantPet?.GetBool() ?? true;

    public static void Register(byte playerId, System.Action action)
    {
        Handlers[playerId] = action;
        EnsureDefaultPet(playerId);
    }

    public static void Unregister(byte playerId)
    {
        Handlers.Remove(playerId);
    }

    public static void Reset()
    {
        Handlers.Clear();
    }

    public static void EnsureDefaultPet(byte playerId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!AutoGrantPetEnabled) return;
        if (!GameStates.IsInGame || GameStates.IsLobby) return;

        var pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || HasPet(pc)) return;

        // 本編の客は幽霊になると自分でペットを外し、生き返っても付け直さない。試合の始めに覚えた本人のペットがあればそれを戻す
        string petId = Camouflage.PlayerSkins.TryGetValue(playerId, out var original) && IsPet(original.PetId) ? original.PetId : DefaultPetIdForPetAction;
        PetsHelper.SetPet(pc, petId);

        Logger.Info($"{pc.Data?.PlayerName} にペットを自動付与: {petId}", "PetActionPatch");
    }

    /// <summary>
    /// 外見を着せ替える時のペット (OutfitSync.Apply が使う)。付与が有効な試合中は、生きている人のペットを空にしない
    /// (ペットは能力の押す口。カモフラの外見などにはペットが無い)
    /// </summary>
    public static string KeepPet(PlayerControl target, string petId)
    {
        if (IsPet(petId) || !AutoGrantPetEnabled || !GameStates.IsInGame || GameStates.IsLobby) return petId ?? "";
        var data = target?.Data;
        if (data == null || data.IsDead || data.Disconnected) return petId ?? "";
        return DefaultPetIdForPetAction;
    }

    private static bool HasPet(PlayerControl pc) => IsPet(pc.Data?.DefaultOutfit?.PetId ?? pc.CurrentOutfit?.PetId);

    private static bool IsPet(string petId)
    {
        if (string.IsNullOrEmpty(petId)) return false;

        petId = petId.ToLowerInvariant();
        return petId != "none"
            && petId != "pet_none"
            && petId != "pet_emptypet"
            && petId != "pet_enmptypet";
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
internal static class AutoPetAssignPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() => AssignPets(), 0.6f, "AutoPetAssignAfterIntro");
        _ = new LateTask(() => AssignPets(), 2f, "AutoPetAssignAfterIntroRetry");
    }

    private static void AssignPets()
    {
        foreach (var pc in Main.AllAlivePlayerControls)
            PetActionManager.EnsureDefaultPet(pc.PlayerId);
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
internal static class AfterMeetingPetAssignPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(() =>
        {
            foreach (var pc in Main.AllAlivePlayerControls)
                PetActionManager.EnsureDefaultPet(pc.PlayerId);
        }, 1.5f, "AfterMeetingPetAssign");
    }
}
