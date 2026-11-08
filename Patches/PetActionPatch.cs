using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Patches;
/*
    参考→https://github.com/AsumuAkaguma/TownOfHost_ForE
    うまくいかないところはこっちも参考→https://github.com/satokazoku/TownOfHost-Pko
*/

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
internal static class LocalPetPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (GameStates.IsLobby || !__instance.IsAlive()) return true;
        if (__instance.petting) return true;

        __instance.petting = true;

        ExternalRpcPetPatch.HandlePet(__instance.MyPhysics);
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
    private const long CooldownMs = 1000;
    private static readonly Dictionary<byte, long> LastProcess = new();

    [GameModuleInitializer]
    public static void Init() => LastProcess.Clear();

    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        if ((RpcCalls)callId != RpcCalls.Pet) return;
        HandlePet(__instance);
    }

    public static void HandlePet(PlayerPhysics physics)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby) return;

        var pc = physics.myPlayer;
        if (pc == null || !pc.IsAlive()) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (LastProcess.TryGetValue(pc.PlayerId, out var last) && now - last < CooldownMs) return;
        LastProcess[pc.PlayerId] = now;

        bool canCancel = !pc.AmOwner
            && !pc.inVent
            && !pc.inMovingPlat
            && !pc.walkingToVent
            && !pc.onLadder
            && !physics.Animations.IsPlayingEnterVentAnimation()
            && !physics.Animations.IsPlayingAnyLadderAnimation()
            && GameStates.IsInTask;

        if (canCancel)
        {
            CancelPetNow(physics);
            _ = new LateTask(() => CancelPetNow(physics), 0.4f, "ExternalRpcPetPatch.CancelPet", true);
        }

        Logger.Info($"{pc.Data?.GetLogPlayerName()} がペットを撫でた", "PetActionPatch");

        OnPetUse(pc);
    }

    private static void CancelPetNow(PlayerPhysics physics)
    {
        try { physics.CancelPet(); }
        catch { }

        try
        {
            MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(
                physics.NetId, (byte)RpcCalls.CancelPet, SendOption.None);
            AmongUsClient.Instance.FinishRpcImmediately(w);
        }
        catch { }
    }

    private static void OnPetUse(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return;
        if (GameStates.IsLobby || GameStates.IsMeeting) return;
        if (pc.inVent || pc.inMovingPlat || pc.onLadder || pc.walkingToVent) return;
        if (pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return;

        if (pc.GetRoleClass() is IPetTrigger p && GameStates.IsInGame)
        {
            p.OnPetUsed();
        }
    }
}
public static class PetSettings
{
    public static HashSet<byte> petNotSetPlayerIds = new();
    public const string FREEPET_STRING = "pet_HamPet";

    [GameModuleInitializer]
    public static void GameInit()
    {
        petNotSetPlayerIds.Clear();
        SetPetRoleInPet();
    }


    public static void CheckNotHasPetPlayers()
    {
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            if (!pc.CanPet())
            {
                petNotSetPlayerIds.Add(pc.PlayerId);
            }
        }
    }

    //ペット付けてない人がいたらペットつけるための処理
    public static void SetPetRoleInPet()
    {
        CheckNotHasPetPlayers();
        foreach (byte playerId in petNotSetPlayerIds)
        {
            var pc = PlayerCatch.GetPlayerById(playerId);
            pc.RpcSetPet(FREEPET_STRING);
        }
    }
}