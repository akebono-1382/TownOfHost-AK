#if DEBUG
using System;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace TownOfHostL.Modules;

[HarmonyPatch(typeof(PassiveButtonManager), nameof(PassiveButtonManager.Update))]
[TownOfHostL.Core.Modes.PatchBundle(TownOfHostL.Core.Modes.PatchBundles.Core)]
static class DevBridge
{
    private const string InPath = "./TOHL_DATA/devcmd.txt";
    private const string OutPath = "./TOHL_DATA/devout.txt";
    private static float nextCheck;

    public static void Prefix()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.2f;
        if (!File.Exists(InPath)) return;
        string[] lines;
        try { lines = File.ReadAllLines(InPath); File.Delete(InPath); }
        catch { return; }
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try { sb.AppendLine($"> {line}"); Run(line.Trim().Split(' ', 2), sb); }
            catch (Exception e) { sb.AppendLine("ERR " + e); }
        }
        try { File.AppendAllText(OutPath, sb.ToString()); } catch { }
    }

    private static PassiveButton[] Buttons()
        => UnityEngine.Object.FindObjectsOfType<PassiveButton>().Where(b => b && b.isActiveAndEnabled).ToArray();

    private static string PathOf(Transform t)
    {
        var s = t.name;
        for (int i = 0; i < 2 && t.parent; i++) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    private static void Run(string[] a, StringBuilder sb)
    {
        switch (a[0])
        {
            case "buttons":
                foreach (var b in Buttons())
                {
                    var sp = Camera.main ? Camera.main.WorldToScreenPoint(b.transform.position) : Vector3.zero;
                    sb.AppendLine($"{PathOf(b.transform)} @({(int)sp.x},{Screen.height - (int)sp.y})");
                }
                break;
            case "press":
                {
                    var b = Buttons().FirstOrDefault(x => x.name == a[1]) ?? Buttons().FirstOrDefault(x => PathOf(x.transform).Contains(a[1]));
                    if (!b) { sb.AppendLine("NOTFOUND"); break; }
                    b.OnClick.Invoke();
                    sb.AppendLine("PRESSED " + PathOf(b.transform));
                    break;
                }
            case "click":
                {
                    var p = a[1].Split(' ');
                    var sp = new Vector3(float.Parse(p[0]), Screen.height - float.Parse(p[1]), 0f);
                    var cam = Camera.main;
                    var w = cam.ScreenToWorldPoint(sp);
                    var hit = Physics2D.OverlapPointAll(w).Select(c => c.GetComponent<PassiveButton>()).Where(x => x && x.isActiveAndEnabled).FirstOrDefault();
                    if (!hit) { sb.AppendLine("NOHIT"); break; }
                    hit.OnClick.Invoke();
                    sb.AppendLine("CLICKED " + PathOf(hit.transform));
                    break;
                }
            case "room":
                SpawnEditorKit.SpawnStore.Host?.ToggleRoom(SpawnEditorKit.SpawnStore.Host.EditMapId, a[1]);
                sb.AppendLine("ROOM " + a[1]);
                sb.AppendLine($"choosing={TownOfHostL.Modules.SpawnEditor.SpawnRoomOptions.IsChoosing(0)} on={TownOfHostL.Modules.SpawnEditor.SpawnRoomOptions.IsOn(0, a[1])} pins={string.Join(",", SpawnEditorKit.SpawnStore.Host.RoomPins(0).Select(p => p.Key + ":" + p.On))}");
                break;
            case "unchoose":
                TownOfHostL.Modules.SpawnEditor.SpawnRoomOptions.Reset(int.Parse(a[1]));
                sb.AppendLine("UNCHOOSE " + a[1]);
                break;
            case "dict":
                RoleDictionary.Toggle();
                sb.AppendLine("DICT");
                break;
            case "find":
                foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>().Where(t => t.name.Contains(a[1])))
                    sb.AppendLine($"{PathOf(t)} active={t.gameObject.activeInHierarchy} pos={t.position}");
                break;
            default:
                sb.AppendLine("UNKNOWN");
                break;
        }
    }
}
#endif
