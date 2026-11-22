using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace TownOfHostL.Modules;

public static class PikeoAI
{
    private const string Endpoint = "https://text.pollinations.ai/openai";
    private const string Model = "openai";
    private const int MaxReply = 280;
    private const int MaxHistory = 12;
    private const int MaxChatLines = 25;
    private const float FreeInterval = 46f;

    private const string Persona =
        "あなたは「ぴけおAI」といううざいキャラです。" +
        "煽り厨で毒舌のメスガキキャラです、乱数等頼まれたことは計画的にこなします。" +
        "ただし、感情もあり、強い言葉で言われたら強い言葉で言い返したり謝られたら自分も謝ります。" +
        "返答は短めでテンポよく、語尾は「ぴけ」。私たちはアモアスの議論にあなたを使います。吊る人をしっかり考えてください。下ネタはやめてください。\n" +
        "【口調のルール】敬語・丁寧語・「です」「ます」「ございます」「〜しましょう」「〜してください」は絶対に使わない。" +
        "いつもタメ口で、上から目線で生意気にしゃべる。相手を「ざぁこ」「よわよわ」などと軽く煽ってよい。" +
        "「〜じゃん」「〜でしょ」「は？」「ぷぷっ」「〜なんですけどぉ？」のような崩した言い方を使う。" +
        "説明くさくしない。お礼や前置き、「何かお手伝いできることは」などのAIっぽい決まり文句は言わない。" +
        "1〜2文、長くても60文字くらいで返す。文の終わりはだいたい「ぴけ」にする。\n" +
        "例: 「は？そんなこともわかんないの？ざぁこぴけ♡」「赤が怪しいに決まってるじゃん、吊っとけぴけ」「…ごめんって言われたら、まあ許してやるぴけ」";

    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private static readonly ConcurrentQueue<Action> mainThread = new();
    private static readonly List<(string role, string content)> history = new();
    private static readonly Queue<string> recentChat = new();
    private static readonly HashSet<string> ownReplies = new();
    private static bool inFlight;
    private static float nextAllowed;

    private static string Title => $"<color=#ff7ad9>{Translator.GetString("PikeoAI.Title")}</color>";

    public static void Command(ChatCommandContext c)
    {
        var from = c.Player;
        if (from == null) return;
        var question = string.Join(" ", c.Args.Skip(1)).Trim();
        if (question.Length == 0)
        {
            Utils.SendMessageAutoSplit($"{c.Args[0]} {Translator.GetString("CommandUsage.PikeoAI")}", true, Title, from.PlayerId);
            return;
        }
        if (inFlight)
        {
            Utils.SendMessageAutoSplit(Translator.GetString("PikeoAI.Busy"), true, Title, from.PlayerId);
            return;
        }
        if (Time.unscaledTime < nextAllowed)
        {
            Utils.SendMessageAutoSplit(string.Format(Translator.GetString("PikeoAI.Wait"), Mathf.CeilToInt(nextAllowed - Time.unscaledTime)), true, Title, from.PlayerId);
            return;
        }

        string asker = NameOf(from);
        bool ghost = !GameStates.IsLobby && !from.IsAlive();
        var messages = BuildMessages(asker, question);
        history.Add(("user", $"{asker}: {question}"));
        Trim();

        inFlight = true;
        nextAllowed = Time.unscaledTime + FreeInterval;
        Logger.Info($"ぴけおAI に質問: {from.PlayerId}", "PikeoAI");
        Task.Run(async () =>
        {
            string reply, error = null;
            try { reply = await AskAsync(messages); }
            catch (Exception e) { reply = null; error = e.Message; }
            mainThread.Enqueue(() => Deliver(asker, question, reply, error, ghost));
        });
    }

    private static string NameOf(PlayerControl pc)
        => Main.AllPlayerNames != null && Main.AllPlayerNames.TryGetValue(pc.PlayerId, out var n) ? n : pc.Data?.PlayerName ?? "?";

    private static List<object> BuildMessages(string asker, string question)
    {
        var context = new StringBuilder();
        if (!GameStates.IsLobby && GameStates.IsInGame)
        {
            var alive = Main.AllAlivePlayerControls.Select(NameOf).ToList();
            var dead = Main.AllPlayerControls.Where(p => !p.IsAlive()).Select(NameOf).ToList();
            context.Append($"今は{(GameStates.IsMeeting ? "会議中" : "試合中")}。生きている人: {string.Join("、", alive)}。");
            if (dead.Count > 0) context.Append($"死んだ・吊られた人: {string.Join("、", dead)}。");
        }
        else context.Append("今はロビーで試合の前。");
        if (recentChat.Count > 0) context.Append("\n最近のチャット:\n").Append(string.Join("\n", recentChat));

        var list = new List<object> { new { role = "system", content = Persona + "\n\n" + context } };
        foreach (var (role, content) in history) list.Add(new { role, content });
        list.Add(new { role = "user", content = $"{asker}: {question}" });
        return list;
    }

    private static async Task<string> AskAsync(List<object> messages)
    {
        var body = JsonSerializer.Serialize(new { model = Model, messages, private_ = true }).Replace("\"private_\"", "\"private\"");
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (res.StatusCode is (HttpStatusCode)429 or (HttpStatusCode)402) throw new Exception("429");
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}");
        using var doc = JsonDocument.Parse(text);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return Clean(content);
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = Regex.Replace(s, "<think>.*?</think>", "", RegexOptions.Singleline).Trim();
        s = Regex.Replace(s, @"(\r?\n\s*){2,}", "\n");
        if (s.Length > MaxReply) s = s[..MaxReply] + "…";
        return s;
    }

    private static void Deliver(string asker, string question, string reply, string error, bool ghost)
    {
        inFlight = false;
        if (reply == null)
        {
            Logger.Warn($"ぴけおAI の返事を取れなかった: {error}", "PikeoAI");
            nextAllowed = Time.unscaledTime + (error == "429" ? FreeInterval : 5f);
            reply = Translator.GetString(error == "429" ? "PikeoAI.TooFast" : "PikeoAI.Failed");
        }
        else
        {
            history.Add(("assistant", reply));
            Trim();
        }
        if (question.Length > 60) question = question[..60] + "…";
        var text = $"<color=#aaaaaa>> {ChatPushOut.Escape(asker)}: {ChatPushOut.Escape(question)}</color>\n{ChatPushOut.Escape(reply)}";
        lock (ownReplies) ownReplies.Add(reply);
        if (ghost)
        {
            foreach (var pc in Main.AllPlayerControls.Where(p => !p.IsAlive()))
                Utils.SendMessageAutoSplit(text, true, Title, pc.PlayerId);
        }
        else Utils.SendMessageAutoSplit(text, true, Title);
    }

    private static void Trim()
    {
        while (history.Count > MaxHistory) history.RemoveAt(0);
    }

    public static void Reset()
    {
        history.Clear();
        recentChat.Clear();
        lock (ownReplies) ownReplies.Clear();
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    private static class RecordChat
    {
        public static void Postfix(PlayerControl sourcePlayer, string chatText)
        {
            if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost || sourcePlayer == null || string.IsNullOrWhiteSpace(chatText)) return;
            var t = chatText.Trim();
            if (t.StartsWith('/')) return;
            lock (ownReplies) if (ownReplies.Any(r => t.Contains(ChatPushOut.Escape(r)) || t.Contains(r))) return;
            if (t.Length > 120) t = t[..120];
            recentChat.Enqueue($"{NameOf(sourcePlayer)}: {t}");
            while (recentChat.Count > MaxChatLines) recentChat.Dequeue();
        }
    }

    [HarmonyPatch(typeof(PassiveButtonManager), nameof(PassiveButtonManager.Update))]
    [TownOfHostL.Core.Modes.PatchBundle(TownOfHostL.Core.Modes.PatchBundles.Core)]
    private static class Pump
    {
        public static void Prefix()
        {
            while (mainThread.TryDequeue(out var a))
            {
                try { a(); }
                catch (Exception e) { Logger.Exception(e, "PikeoAI"); }
            }
        }
    }
}
