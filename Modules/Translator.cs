using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Csv;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;

namespace TownOfHost
{
    public static class Translator
    {
        public static Dictionary<string, Dictionary<int, string>> translateMaps;
        public static Dictionary<string, Dictionary<int, string>> achievementMaps;
        public const string LANGUAGE_FOLDER_NAME = "Language";

        [PluginModuleInitializer]
        public static void Init()
        {
            Logger.Info("Language Dictionary Initialize...", "Translator");
            LoadLangs();
            LoadAchievementLangs();
            Logger.Info("Language Dictionary Initialize Finished", "Translator");
        }
        public static void LoadLangs()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("TownOfHost.Resources.string.csv");
            translateMaps = new Dictionary<string, Dictionary<int, string>>();

            var options = new CsvOptions()
            {
                HeaderMode = HeaderMode.HeaderPresent,
                AllowNewLineInEnclosedFieldValues = false,
            };
            foreach (var line in CsvReader.ReadFromStream(stream, options))
            {
                if (line.Values[0][0] == '#') continue;
                try
                {
                    Dictionary<int, string> dic = new();
                    for (int i = 1; i < line.ColumnCount; i++)
                    {
                        int id = int.Parse(line.Headers[i]);
                        dic[id] = line.Values[i].Replace("\\n", "\n").Replace("\\r", "\r");
                    }
                    if (!translateMaps.TryAdd(line.Values[0], dic))
                        Logger.Warn($"翻訳用CSVに重複があります。{line.Index}行目: \"{line.Values[0]}\"", "Translator");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex.ToString(), "Translator");
                }
            }

            if (Main.IsAndroid()) return;
            // カスタム翻訳ファイルの読み込み
            if (!Directory.Exists(LANGUAGE_FOLDER_NAME)) Directory.CreateDirectory(LANGUAGE_FOLDER_NAME);

            // 翻訳テンプレートの作成
            CreateTemplateFile();
            foreach (var lang in EnumHelper.GetAllValues<SupportedLangs>())
            {
                if (File.Exists(@$"./{LANGUAGE_FOLDER_NAME}/{lang}.dat"))
                    LoadCustomTranslation($"{lang}.dat", lang);
            }
        }
        public static void LoadAchievementLangs()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("TownOfHost.Resources.achievements.csv");
            achievementMaps = new Dictionary<string, Dictionary<int, string>>();

            var options = new CsvOptions()
            {
                HeaderMode = HeaderMode.HeaderPresent,
                AllowNewLineInEnclosedFieldValues = false,
            };
            foreach (var line in CsvReader.ReadFromStream(stream, options))
            {
                if (line.Values[0][0] == '#') continue;
                try
                {
                    Dictionary<int, string> dic = new();
                    for (int i = 1; i < line.ColumnCount; i++)
                    {
                        int id = int.Parse(line.Headers[i]);
                        dic[id] = line.Values[i].Replace("\\n", "\n").Replace("\\r", "\r");
                    }
                    if (!achievementMaps.TryAdd(line.Values[0], dic))
                        Logger.Warn($"実績用CSVに重複があります。{line.Index}行目: \"{line.Values[0]}\"", "Translator");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex.ToString(), "Translator");
                }
            }
        }

        public static string GetString(string s, Dictionary<string, string> replacementDic = null)
        {
            var langId = TranslationController.InstanceExists ? TranslationController.Instance.currentLanguage.languageID : SupportedLangs.English;
            string str = GetString(s, langId);
            if (replacementDic != null)
                foreach (var rd in replacementDic)
                {
                    str = str.Replace(rd.Key, rd.Value);
                }
            return str;
        }
        public static List<string> NotString = new();
        public static string GetString(string str, SupportedLangs langId)
        {
            var res = str;
            var ch = res;
            //役が使えないなら空欄を返す
            foreach (var re in Event.remove)
            {
                var name = nameof(re);
                if (str == name || str == $"{name}Info" || str.StartsWith($"{name}Desc")) return "";
            }
            foreach (var ev in Event.EventRoles.Keys)
            {
                if (Event.CheckRole(ev)) continue;
                var name = nameof(ev);
                if (str == name || str == $"{name}Info" || str.StartsWith($"{name}Desc")) return "";
            }

            if (translateMaps.TryGetValue(str, out var dic) && (!dic.TryGetValue((int)langId, out res) || res == "")) //strに該当する&無効なlangIdかresが空
            {
                res = dic.TryGetValue(0, out var en) ? $"*{en}" : str;
            }
            if (!translateMaps.ContainsKey(str)) //translateMapsにない場合、StringNamesにあれば取得する
            {
                var stringNames = EnumHelper.GetAllValues<StringNames>().Where(x => x.ToString() == str);
                if (stringNames != null && stringNames.Any())
                    res = GetString(stringNames.FirstOrDefault());
            }
            if (res == ch)
            {
                if (NotString.Contains(res)) return res;
                NotString.Add(res);
                Logger.Warn($"未翻訳のキー : {str}", "Translator");
            }
            return res;
        }
        public static string GetString(StringNames stringName)
            => DestroyableSingleton<TranslationController>.Instance.GetString(stringName, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
        public static string GetRoleString(string str)
        {
            var CurrentLanguage = TranslationController.Instance.currentLanguage.languageID;

            return GetString(str, CurrentLanguage);
        }
        public static void LoadCustomTranslation(string filename, SupportedLangs lang)
        {
            if (Main.IsAndroid()) return;
            string path = @$"./{LANGUAGE_FOLDER_NAME}/{filename}";
            if (File.Exists(path))
            {
                Logger.Info($"カスタム翻訳ファイル「{filename}」を読み込み", "LoadCustomTranslation");
                using StreamReader sr = new(path, Encoding.GetEncoding("UTF-8"));
                string text;
                string[] tmp = Array.Empty<string>();
                while ((text = sr.ReadLine()) != null)
                {
                    tmp = text.Split(":");
                    if (tmp.Length > 1 && tmp[1] != "")
                    {
                        try
                        {
                            translateMaps[tmp[0]][(int)lang] = tmp.Skip(1).Join(delimiter: ":").Replace("\\n", "\n").Replace("\\r", "\r");
                        }
                        catch (KeyNotFoundException)
                        {
                            Logger.Warn($"「{tmp[0]}」は有効なキーではありません。", "LoadCustomTranslation");
                        }
                    }
                }
            }
            else
            {
                Logger.Error($"カスタム翻訳ファイル「{filename}」が見つかりませんでした", "LoadCustomTranslation");
            }
        }

        private static void CreateTemplateFile()
        {
            var sb = new StringBuilder();
            foreach (var title in translateMaps) sb.Append($"{title.Key}:\n");
            File.WriteAllText(@$"./{LANGUAGE_FOLDER_NAME}/template.dat", sb.ToString());
            sb.Clear();
            foreach (var title in translateMaps)
            {
                if (!title.Value.TryGetValue(0, out var en))
                {
                    Logger.Warn($"英語(0)がありません: \"{title.Key}\" (登録言語: {string.Join(",", title.Value.Keys)})", "Translator");
                    en = "";
                }
                sb.Append($"{title.Key}:{en.Replace("\n", "\\n").Replace("\r", "\\r")}\n");
            }
            File.WriteAllText(@$"./{LANGUAGE_FOLDER_NAME}/template_English.dat", sb.ToString());
        }
        public static void ExportCustomTranslation()
        {
            LoadLangs();
            var sb = new StringBuilder();
            var lang = TranslationController.Instance.currentLanguage.languageID;
            foreach (var title in translateMaps)
            {
                if (!title.Value.TryGetValue((int)lang, out var text)) text = "";
                sb.Append($"{title.Key}:{text.Replace("\n", "\\n").Replace("\r", "\\r")}\n");
            }
            File.WriteAllText(@$"./{LANGUAGE_FOLDER_NAME}/export_{lang}.dat", sb.ToString());
        }
    }
}