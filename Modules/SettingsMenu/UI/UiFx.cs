using System;
using System.Collections.Generic;
using UnityEngine;

namespace TownOfHostL.SettingsUI;

// 設定画面の短い動き (押した時に弾む・値の棒が伸び縮みする)。動いている物が無い間は何もしない。
// 同じ物の同じ種類の動きは後から来た物で置き換える (つまみを引いている間は毎フレーム来る)。
internal static class UiFx
{
    private sealed class Run
    {
        public UnityEngine.Object Owner;
        public int Channel;
        public float Start, Duration;
        public Action<float> Step;
    }

    private static readonly List<Run> Active = new();

    public const int ChannelScale = 0, ChannelBar = 1, ChannelColor = 2;

    // owner が生きている間 duration 秒かけて step(0→1) を呼ぶ。最後は必ず step(1)
    public static void Play(UnityEngine.Object owner, int channel, float duration, Action<float> step)
    {
        if (!owner || step == null) return;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var r = Active[i];
            if (r.Channel == channel && r.Owner == owner) Active.RemoveAt(i);
        }
        if (duration <= 0f) { step(1f); return; }
        Active.Add(new Run { Owner = owner, Channel = channel, Start = Time.unscaledTime, Duration = duration, Step = step });
        step(0f);
    }

    // 一度ふくらんで (amount > 0) か沈んで (amount < 0) 元の大きさへ戻る
    public static void Pop(Transform t, float amount, float duration = 0.18f)
    {
        if (!t) return;
        Play(t, ChannelScale, duration, p =>
        {
            // 最初の 3 割で山まで行き、残りで元へ戻る (戻りは緩やかに)
            float k = p < 0.3f ? p / 0.3f : MathF.Cos((p - 0.3f) / 0.7f * MathF.PI / 2f);
            float s = 1f + amount * k;
            t.localScale = new Vector3(s, s, 1f);
        });
    }

    public static float EaseOut(float p) => 1f - (1f - p) * (1f - p);

    public static void Tick()
    {
        if (Active.Count == 0) return;
        float now = Time.unscaledTime;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var r = Active[i];
            if (!r.Owner) { Active.RemoveAt(i); continue; }
            float p = Math.Clamp((now - r.Start) / r.Duration, 0f, 1f);
            try { r.Step(p); }
            catch { p = 1f; }
            if (p >= 1f) Active.Remove(r);
        }
    }

    public static void Clear() => Active.Clear();
}
