using System;
using System.Diagnostics;
using System.IO;
using HarmonyLib;

namespace TownOfHostL;

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
static class LoadingWindowPatch
{
    static bool sent;
    public static void Postfix()
    {
        if (sent) return;
        sent = true;
        try
        {
            var path = Path.Combine(BepInEx.Paths.CachePath, "loading_window.txt");
            if (!File.Exists(path)) return;
            var ms = (long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
            File.AppendAllText(path, $"S 3 done {ms}\nM タイトル画面が開きました\n");
        }
        catch { }
    }
}
