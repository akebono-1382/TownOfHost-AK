using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using TMPro;

namespace TownOfHostL.Modules;

// 毎フレーム同じ文字を TMP に書き込むと、そのたびに文字列がゲーム側のヒープへ写され (GC のゴミになる)、
// 呼び出しもゲーム側を通る。前回書いた文字列と、TMP が持っている文字列の場所を覚えておき、
// どちらも変わっていなければ書き込みを省く。ほかの処理が書き換えた時は場所が変わるので書き直す。
public static class TextWriteCache
{
    struct Entry
    {
        public string Text;
        public IntPtr Native;
    }

    static readonly Dictionary<IntPtr, Entry> Entries = new();
    static int textOffset = -1;      // TMP_Text.m_text の位置 (0 = 使えない → 常に書く)

    static int TextOffset
    {
        get
        {
            if (textOffset < 0) textOffset = ResolveOffset(Il2CppClassPointerStore<TMP_Text>.NativeClassPtr, "m_text");
            return textOffset;
        }
    }

    static int ResolveOffset(IntPtr klass, string name) => Core.NativeObject.ResolveOffset(klass, name);

    /// <summary>
    /// tmp が今 value を表示していて、前に Remember してからほかに書き換えられていないか。
    /// 書き込みを本編の関数 (ボタンの OverrideText など) に任せる所で、SetText の代わりに Remember と組で使う。
    /// </summary>
    public static bool IsCurrent(TMP_Text tmp, string value)
    {
        if (value == null) return false;
        IntPtr p = tmp.Pointer;
        int off = TextOffset;
        if (off == 0 || p == IntPtr.Zero) return false;
        IntPtr current = Marshal.ReadIntPtr(p, off);
        return current != IntPtr.Zero && Entries.TryGetValue(p, out var e) && e.Native == current && string.Equals(e.Text, value, StringComparison.Ordinal);
    }

    /// <summary>tmp に value を書いた直後に呼び、次の IsCurrent で比べられるようにする。</summary>
    public static void Remember(TMP_Text tmp, string value)
    {
        if (value == null) return;
        IntPtr p = tmp.Pointer;
        int off = TextOffset;
        if (off == 0 || p == IntPtr.Zero) return;
        if (Entries.Count > 512) Entries.Clear();
        Entries[p] = new Entry { Text = value, Native = Marshal.ReadIntPtr(p, off) };
    }

    /// <summary>tmp.text = value と同じ。前回と同じ内容でほかから書き換えられてもいなければ何もしない。</summary>
    public static void SetText(TMP_Text tmp, string value)
    {
        value ??= "";
        IntPtr p = tmp.Pointer;
        int off = TextOffset;
        if (off == 0 || p == IntPtr.Zero)
        {
            tmp.text = value;
            return;
        }
        IntPtr current = Marshal.ReadIntPtr(p, off);
        if (Entries.TryGetValue(p, out var e) && e.Native == current && current != IntPtr.Zero && string.Equals(e.Text, value, StringComparison.Ordinal))
            return;
        tmp.text = value;
        if (Entries.Count > 512) Entries.Clear(); // 破棄された TMP の分が溜まり続けないように
        Entries[p] = new Entry { Text = value, Native = Marshal.ReadIntPtr(p, off) };
    }
}
