using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace TownOfHostL.Core;

/// <summary>
/// Unity の == null と == をゲーム側を呼ばずに行う。
/// IL2CPP 越しの UnityEngine.Object の == は 1 回ごとに本編の比較関数を呼ぶので 0.3µs ほどかかり、名前の組み立てや生死の判定で
/// 見る人 × 見られる人のぶん重なる。本体の場所 (m_CachedPtr) を直に読めば、同じ判定が 0.02µs ほどで済む。
/// </summary>
public static class NativeObject
{
    static int cachedPtrOffset = -1; // UnityEngine.Object.m_CachedPtr の位置 (0 = 使えない → Unity の == に任せる)

    static int CachedPtrOffset
    {
        get
        {
            if (cachedPtrOffset < 0) cachedPtrOffset = ResolveOffset(Il2CppClassPointerStore<UnityEngine.Object>.NativeClassPtr, "m_CachedPtr");
            return cachedPtrOffset;
        }
    }

    internal static int ResolveOffset(IntPtr klass, string name)
    {
        try
        {
            IntPtr field = klass == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_get_field_from_name(klass, name);
            uint off = field == IntPtr.Zero ? 0 : IL2CPP.il2cpp_field_get_offset(field);
            return off is 0 or uint.MaxValue ? 0 : (int)off;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>obj != null と同じ。Unity 側の本体が生きているか (破棄済みなら false)。</summary>
    public static bool IsAlive(UnityEngine.Object obj)
    {
        if (obj is null) return false;
        IntPtr p = obj.Pointer;
        if (p == IntPtr.Zero) return false;
        int off = CachedPtrOffset;
        if (off == 0) return obj != null;
        return Marshal.ReadIntPtr(p, off) != IntPtr.Zero;
    }

    /// <summary>
    /// a == b と同じ。片方が null なら、もう片方が null か破棄済みの時に true。
    /// どちらも null でなければ同じ物か (破棄済みでも、別の物どうしは等しくない)。
    /// </summary>
    public static bool Same(UnityEngine.Object a, UnityEngine.Object b)
    {
        if (a is null) return !IsAlive(b);
        if (b is null) return !IsAlive(a);
        return a.Pointer == b.Pointer;
    }
}
