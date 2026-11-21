using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TownOfHostL.SettingsUI;

// 設定の説明に使う本編の映像。Resources/Footage/<名前>.jpg (コマを格子に並べた 1 枚の絵) と <名前>.txt (大きさ・コマ数・fps・値) の組。
// 使う時に初めて読み込み、設定画面を閉じたら捨てる (常には持たない)。
internal sealed class Footage
{
    public int W, H, Cols, Count;
    public float Fps = 20f;
    public float[] Values = Array.Empty<float>();
    // 値ごとの動画の時の、各値の始めのコマ (Values と同じ並び)。止め絵・1 本の動画の時は空
    public int[] Starts = Array.Empty<int>();
    private Texture2D _tex;
    private Sprite[] _frames;

    private static readonly Dictionary<string, Footage> Loaded = new();

    public static Footage Get(string name)
    {
        // シーンの移り変わりで絵が捨てられていたら読み直す
        if (Loaded.TryGetValue(name, out var f) && (f == null || f._tex)) return f;
        f?.Release();
        f = Load(name);
        Loaded[name] = f;   // 読めなかった時も null を覚え、開いている間は何度も試さない
        return f;
    }

    // name 以外の映像を捨てる (図を切り替えた時。持つのは今の図の 1 本だけ)
    public static void KeepOnly(string name)
    {
        List<string> drop = null;
        foreach (var kv in Loaded)
            if (kv.Key != name) (drop ??= new()).Add(kv.Key);
        if (drop == null) return;
        foreach (var k in drop)
        {
            Loaded[k]?.Release();
            Loaded.Remove(k);
        }
    }

    public static void ReleaseAll()
    {
        foreach (var f in Loaded.Values) f?.Release();
        Loaded.Clear();
    }

    private static Footage Load(string name)
    {
        Footage f = null;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            string head = "TownOfHost_L.Resources.Footage." + name;
            using var meta = asm.GetManifestResourceStream(head + ".txt");
            using var img = asm.GetManifestResourceStream(head + ".jpg");
            if (meta == null || img == null) return null;
            f = new Footage();
            using (var r = new StreamReader(meta))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line[..eq], v = line[(eq + 1)..];
                    switch (k)
                    {
                        case "w": f.W = int.Parse(v); break;
                        case "h": f.H = int.Parse(v); break;
                        case "cols": f.Cols = int.Parse(v); break;
                        case "count": f.Count = int.Parse(v); break;
                        case "fps": f.Fps = float.Parse(v, CultureInfo.InvariantCulture); break;
                        case "values": f.Values = Array.ConvertAll(v.Split(','), s => float.Parse(s, CultureInfo.InvariantCulture)); break;
                        case "starts": f.Starts = Array.ConvertAll(v.Split(','), int.Parse); break;
                    }
                }
            }
            if (f.W <= 0 || f.H <= 0 || f.Cols <= 0 || f.Count <= 0) return null;
            using var ms = new MemoryStream();
            img.CopyTo(ms);
            f._tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "TohlFootage_" + name };
            if (!ImageConversion.LoadImage(f._tex, ms.ToArray(), true)) { f.Release(); return null; }
            f._tex.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            f._tex.filterMode = FilterMode.Bilinear;
            f._tex.wrapMode = TextureWrapMode.Clamp;
            f._frames = new Sprite[f.Count];
            return f;
        }
        catch (Exception ex)
        {
            f?.Release();
            Logger.Error($"映像 {name} を読めませんでした: {ex.Message}", "Footage");
            return null;
        }
    }

    // i 番目のコマ (絵は上の段から並ぶ・Unity の絵は下が原点)。幅が 1 単位になる大きさで作る
    public Sprite Frame(int i)
    {
        if (_frames == null || !_tex) return null;
        i = ((i % Count) + Count) % Count;
        var sp = _frames[i];
        if (sp) return sp;
        int x = i % Cols * W, y = _tex.height - (i / Cols + 1) * H;
        sp = Sprite.Create(_tex, new Rect(x, y, W, H), new Vector2(0.5f, 0.5f), W);
        sp.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        _frames[i] = sp;
        return sp;
    }

    // 値ごとの動画で、i 番目の値のコマの範囲 (始め・数)
    public (int start, int count) ClipOf(int i)
    {
        if (Starts.Length == 0) return (0, Count);
        i = Math.Clamp(i, 0, Starts.Length - 1);
        int end = i + 1 < Starts.Length ? Starts[i + 1] : Count;
        return (Starts[i], Math.Max(end - Starts[i], 1));
    }

    // 値ごとの止め絵・動画で、v にいちばん近い値の番号 (止め絵ならコマの番号と同じ)
    public int Nearest(float v)
    {
        int best = 0;
        for (int i = 1; i < Values.Length; i++)
            if (Math.Abs(Values[i] - v) < Math.Abs(Values[best] - v)) best = i;
        return best;
    }

    private void Release()
    {
        if (_frames != null)
            foreach (var sp in _frames)
                if (sp) UnityEngine.Object.Destroy(sp);
        _frames = null;
        if (_tex) UnityEngine.Object.Destroy(_tex);
        _tex = null;
    }
}
