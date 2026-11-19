using System.Collections.Generic;
using TownOfHostL.Attributes;
using TownOfHostL.Core;
using UnityEngine;

namespace TownOfHostL.Modules;

/// <summary>
/// 死んだ場所に、死因ごとの一瞬の演出 (爆発・狙撃・炎・毒・魔法・血しぶき) を CNO で出す。MOD の無い客にも見える。
/// 死因は死んだ後に書かれることがある (キルの RPC の後で DeathReason を入れる役職がある) ので、少し待ってから読む。
/// 近くで続けて死んだ時 (爆発で何人も巻き込むなど) は 1 つにまとめる。
/// </summary>
public static class DeathBurst
{
    private const float ReasonDelay = 0.15f;
    private const float MergeDistance = 2.5f, MergeTime = 1.5f;

    private readonly struct Pending
    {
        public readonly byte PlayerId;
        public readonly Vector2 Position;
        public readonly float At;
        public Pending(byte playerId, Vector2 position, float at) { PlayerId = playerId; Position = position; At = at; }
    }

    private sealed class Fx
    {
        public string[] Frames;
        public byte BodyColor;
        public float FrameGap, Life, Shake;
    }

    // 体の色は Palette.PlayerColors の番号。体は絵の下に描かれるので、絵の色に寄せる
    private const byte Red = 0, Green = 2, Orange = 4, White = 7, Purple = 8, Maroon = 12;

    private static readonly Fx Explosion = new()
    {
        Frames = new[]
        {
            "<size=10><color=#fff3a0>●</color></size>",
            "<size=20><color=#ff8a1a>★</color></size>",
            "<size=16><color=#6b6b6bc0>●</color></size>",
        },
        BodyColor = Orange, FrameGap = 0.3f, Life = 1.3f, Shake = 0.12f,
    };
    private static readonly Fx Sniped = new()
    {
        Frames = new[]
        {
            "<size=9><color=#ffffff>★</color></size>",
            "<size=8><color=#c00000>●</color></size>",
        },
        BodyColor = White, FrameGap = 0.3f, Life = 0.9f,
    };
    private static readonly Fx Flame = new()
    {
        Frames = new[]
        {
            "<size=11><color=#ffb020>●</color></size>",
            "<size=15><color=#ff4010>★</color></size>",
            "<size=12><color=#402010c0>●</color></size>",
        },
        BodyColor = Orange, FrameGap = 0.35f, Life = 1.4f, Shake = 0.06f,
    };
    private static readonly Fx Poison = new()
    {
        Frames = new[]
        {
            "<size=11><color=#3ccf4e>●</color></size>",
            "<size=15><color=#8ef06aa0>●</color></size>",
        },
        BodyColor = Green, FrameGap = 0.4f, Life = 1.3f,
    };
    private static readonly Fx Magic = new()
    {
        Frames = new[]
        {
            "<size=12><color=#b45cff>★</color></size>",
            "<size=9><color=#ff9cf0>★</color></size>",
        },
        BodyColor = Purple, FrameGap = 0.35f, Life = 1.1f,
    };
    private static readonly Fx Blood = new()
    {
        Frames = new[]
        {
            "<size=11><color=#a00000>●</color></size>",
            "<size=9><color=#5c0000c0>●</color></size>",
        },
        BodyColor = Maroon, FrameGap = 0.4f, Life = 1.2f,
    };

    private static readonly List<Pending> pending = new();
    private static readonly List<(Vector2 pos, float at)> recent = new();

    [GameModuleInitializer]
    public static void Init()
    {
        pending.Clear();
        recent.Clear();
    }

    private static Fx FxOf(CustomDeathReason reason) => reason switch
    {
        CustomDeathReason.Bombed or CustomDeathReason.IgnitionBombed => Explosion,
        CustomDeathReason.Sniped or CustomDeathReason.Shot => Sniped,
        CustomDeathReason.Torched => Flame,
        CustomDeathReason.Poisoned or CustomDeathReason.Poisoning or CustomDeathReason.Infected => Poison,
        CustomDeathReason.Spell or CustomDeathReason.Magic or CustomDeathReason.MagicC or CustomDeathReason.Grim => Magic,
        CustomDeathReason.Bite or CustomDeathReason.Revenge or CustomDeathReason.Counter or CustomDeathReason.CounterAttack
            or CustomDeathReason.Retaliation => Blood,
        _ => null,
    };

    /// <summary>ホストで、誰かが死んだ時に呼ぶ (PlayerState.SetDead)。</summary>
    public static void OnDead(byte playerId)
    {
        if (!(Options.DeathBurst?.GetBool() ?? true)) return;
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || !GameStates.IsInTask) return;
        var pc = Utils.GetPlayerById(playerId);
        if (pc == null) return;
        pending.Add(new Pending(playerId, pc.transform.position, Time.realtimeSinceStartup));
    }

    // 毎フレーム (ホスト)
    public static void Tick()
    {
        if (pending.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var p = pending[i];
            if (now - p.At < ReasonDelay) continue;
            pending.RemoveAt(i);
            if (!GameStates.IsInTask) continue;
            var fx = FxOf(PlayerState.GetByPlayerId(p.PlayerId)?.DeathReason ?? CustomDeathReason.etc);
            if (fx == null || IsMerged(p.Position, now)) continue;
            if (CnoBurst.Play(p.Position, fx.Frames, fx.BodyColor, fx.FrameGap, fx.Life, fx.Shake))
                recent.Add((p.Position, now));
        }
    }

    private static bool IsMerged(Vector2 pos, float now)
    {
        recent.RemoveAll(r => now - r.at > MergeTime);
        foreach (var r in recent)
            if ((r.pos - pos).sqrMagnitude < MergeDistance * MergeDistance) return true;
        return false;
    }
}
