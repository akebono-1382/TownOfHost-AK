using System.Collections.Generic;
using HarmonyLib;
using TownOfHostL.Core.Modes;
using TownOfHostL.Modules.Net;
using UnityEngine;

namespace TownOfHostL.Core;

/// <summary>
/// その場に一瞬だけ出す演出 (爆発・着弾など) の CNO。絵を何コマか順に変え、出始めに少し揺らし、決めた時間で消す。
///
/// CNO の決まりの上で動く:
/// - 出せない間 (イントロ明け 13 秒・会議明け 10 秒・会議中) や順番待ちで <see cref="MaxWait"/> 秒より遅れる物は出さずに捨てる
///   (遅れて出ると、何の演出か分からない)。
/// - まだ出ていない物があれば次は受けない。ほかの CNO (ビームなど) の出番を押しのけないため。
/// - 絵の切り替えは全 CNO で 0.2 秒に 1 回なので、コマの間隔はそれより長くする。
/// - 揺れは SnapTo の予算を使うので、予算に余裕がある時だけ揺らす。
/// - 会議が始まったら全部消す。
/// </summary>
[PatchBundle(PatchBundles.Core)]
public static class CnoBurst
{
    private const float MaxWait = 2f;
    private const int MaxAlive = 3;
    private const float ShakeFor = 0.5f, ShakeGap = 0.1f;
    // 揺れの TP はこれだけ予算が残っている時だけ (他の役職の TP を先に通す)
    private const float ShakeReserve = 40f;

    private sealed class Burst
    {
        public CustomNetObject Cno;
        public string[] Frames;
        public float FrameGap, Life, Shake;
        public Vector2 Position;
        public float CreatedAt, LiveAt = -1f, NextFrameAt, NextShakeAt;
        public int Frame, ShakeStep;
        public bool Settled;
    }

    private static readonly List<Burst> bursts = new();

    /// <summary>
    /// 一発物の演出を出す。frames は出す順の絵 (CNO の名前の絵)、frameGap はコマの間隔、life は出てから消すまでの秒、
    /// shake は出始めに横へ揺らす幅 (0 で揺らさない)。出せない時 (ホストでない・混んでいる) は false。
    /// </summary>
    public static bool Play(Vector2 position, string[] frames, byte bodyColor, float frameGap, float life, float shake = 0f)
    {
        if (frames == null || frames.Length == 0) return false;
        if (bursts.Count >= MaxAlive) return false;
        foreach (var b in bursts)
            if (!b.Cno.IsLive) return false;
        var cno = CustomNetObject.Create(frames[0], position, bodyColor);
        if (cno == null) return false;
        bursts.Add(new Burst
        {
            Cno = cno,
            Frames = frames,
            FrameGap = Mathf.Max(frameGap, 0.25f),
            Life = life,
            Shake = shake,
            Position = position,
            CreatedAt = Time.realtimeSinceStartup,
        });
        return true;
    }

    private static void Update()
    {
        if (bursts.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        bool meeting = MeetingHud.Instance || ExileController.Instance;
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            var b = bursts[i];
            if (b.Cno.IsGone) { bursts.RemoveAt(i); continue; }
            if (meeting) { b.Cno.Despawn(); bursts.RemoveAt(i); continue; }
            if (!b.Cno.IsLive)
            {
                if (now - b.CreatedAt > MaxWait) { b.Cno.Despawn(); bursts.RemoveAt(i); }
                continue;
            }
            if (b.LiveAt < 0f)
            {
                b.LiveAt = now;
                b.NextFrameAt = now + b.FrameGap;
                b.NextShakeAt = now;
            }
            float age = now - b.LiveAt;
            if (age >= b.Life) { b.Cno.Despawn(); bursts.RemoveAt(i); continue; }

            if (b.Frame + 1 < b.Frames.Length && now >= b.NextFrameAt)
            {
                b.Frame++;
                b.Cno.ChangeSprite(b.Frames[b.Frame]);
                b.NextFrameAt = now + b.FrameGap;
            }

            if (b.Shake <= 0f || b.Settled) continue;
            if (age >= ShakeFor)
            {
                b.Cno.MoveTo(b.Position);
                b.Settled = true;
                continue;
            }
            if (now < b.NextShakeAt || !SnapToBudget.HasHeadroom(ShakeReserve)) continue;
            // 右・左と交互に、だんだん小さく
            float fade = 1f - age / ShakeFor;
            float side = (b.ShakeStep++ % 2 == 0) ? 1f : -1f;
            b.Cno.MoveTo(b.Position + new Vector2(side * b.Shake * fade, 0f));
            b.NextShakeAt = now + ShakeGap;
        }
    }

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
    private static class DrivePatch
    {
        public static void Postfix() => Update();
    }
}
