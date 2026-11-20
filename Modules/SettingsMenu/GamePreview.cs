using System;
using System.Collections.Generic;
using UnityEngine;

namespace TownOfHostL.SettingsUI;

// ゲームの頁の右の欄に出す、本編の設定の意味を見せる動く図。字は使わない。
// 移動速度 = 基準 (1 倍・灰) と今の速さのクルーが並んで走る / 視界 = 暗い中のクルーとインポスターの明かりの大きさ (半径は値の平方根) /
// キルのクールダウン = インポスターの周りの目盛りが埋まる速さ (10 秒で 1 周を 1 秒に縮めて見せる)。
// どの図を出すかは、最後に当てた・変えた本編の設定で決める (かくれんぼの設定など図の無い項目では変えない)。
// 図はすべて作った時に用意しておき、出し入れだけをする (後から作ると描く順が付かないため)。
internal sealed class GamePreview : Widget
{
    private enum Scene { Speed, Vision, KillCooldown }

    private static readonly Color Panel = new(0.06f, 0.07f, 0.1f, 1f);
    private static readonly Color Lane = new(0.22f, 0.24f, 0.3f, 1f);
    private static readonly Color RefCrew = new(0.5f, 0.52f, 0.58f, 1f);
    private static readonly Color CrewColor = new(0.35f, 0.9f, 1f, 1f);
    private static readonly Color ImpColor = new(1f, 0.25f, 0.25f, 1f);
    private static readonly Color LightColor = new(1f, 0.95f, 0.75f, 1f);
    private static readonly Color DotOff = new(0.25f, 0.26f, 0.32f, 1f);

    private const int Dots = 24;
    private const float CrewH = 0.34f;
    private const float RefCross = 2.2f;     // 1 倍の速さで欄を横切る秒数
    private const float CooldownScale = 0.1f; // クールダウンの 1 秒を何秒で見せるか

    // 最後に選んだ図 (頁を作り直しても残す)
    private static Scene _scene = Scene.Speed;
    private static bool _focusImpostor;

    public readonly float Width, Height;

    private SpriteRenderer _back;
    private readonly List<(SpriteRenderer r, int order, float alpha)> _parts = new();
    private int _appliedBase = int.MinValue;
    private GameObject _speedRoot, _visionRoot, _killRoot;

    private Transform _refCrew, _curCrew;
    private SpriteRenderer _crewGlow, _crewLight, _crewBody, _impGlow, _impLight, _impBody;
    private readonly SpriteRenderer[] _dots = new SpriteRenderer[Dots];
    private SpriteRenderer _victim;

    private float _speed = 1f;
    private float _crewR, _impR, _crewTarget, _impTarget;
    private float _cooldown = 25f;
    private float _last = -1f;
    private float _lightMax = 5f;
    private const float VisionCrewH = 0.2f;

    private float LightRadius(float v) => Math.Min(Height * 0.46f, Width * 0.25f) * MathF.Sqrt(Math.Max(v, 0f) / _lightMax);

    public GamePreview(float width, float height)
    {
        Width = width;
        Height = height;
    }

    // ---- どの設定に図があるか ----

    private static Scene? SceneOf(MenuOpt opt) => opt is VanillaMenuOpt v ? v.SettingTitle switch
    {
        StringNames.GamePlayerSpeed => Scene.Speed,
        StringNames.GameCrewLight or StringNames.GameImpostorLight => Scene.Vision,
        StringNames.GameKillCooldown => Scene.KillCooldown,
        _ => null,
    } : null;

    public static bool Has(MenuOpt opt) => SceneOf(opt) != null;

    private static VanillaMenuOpt Find(StringNames title)
    {
        foreach (var sec in VanillaGame.All)
            foreach (var o in sec.Opts)
                if (o.SettingTitle == title) return o;
        return null;
    }

    private static float Value(VanillaMenuOpt o, float fallback) => o?.ValueAt(o.Index) is double d ? (float)d : fallback;
    private static float Max(VanillaMenuOpt o, float fallback) => o != null && o.Count > 0 && o.ValueAt(o.Count - 1) is double d ? (float)d : fallback;

    // 当てた・変えた設定の図にする (図の無い設定なら今の図のまま)
    public void Focus(MenuOpt opt)
    {
        var s = SceneOf(opt);
        if (s == null) return;
        bool changed = s != _scene;
        _scene = s.Value;
        if (opt is VanillaMenuOpt v) _focusImpostor = v.SettingTitle == StringNames.GameImpostorLight;
        Refresh();
        if (changed) ShowScene();
    }

    // 値を読み直す (変えた時・外で変わった時)。毎フレームは読まない
    public void Refresh()
    {
        _speed = Math.Max(Value(Find(StringNames.GamePlayerSpeed), 1f), 0.05f);
        var crew = Find(StringNames.GameCrewLight);
        var imp = Find(StringNames.GameImpostorLight);
        // 明かりの半径は値の平方根に比べる (最大でも欄に入り、小さい値でもクルーより大きく見えるように。段ごとの差は残る)
        _lightMax = Math.Max(Math.Max(Max(crew, 5f), Max(imp, 5f)), 0.01f);
        _crewTarget = LightRadius(Value(crew, 1f));
        _impTarget = LightRadius(Value(imp, 1.5f));
        _cooldown = Value(Find(StringNames.GameKillCooldown), 25f);
        ApplyFocusAlpha();
    }

    // ---- 組み立て ----

    public override GameObject Build(Transform parent, BuildCtx ctx, float maxWidth, out Vector2 size)
    {
        var go = Node("GamePreview", parent, ctx.Layer);
        size = new Vector2(Width, Height);
        _parts.Clear();
        _appliedBase = int.MinValue;

        _back = Node(UiScreen.BackName, go.transform, ctx.Layer).AddComponent<SpriteRenderer>();
        _back.sprite = MenuArt.Round(0.1f);
        _back.drawMode = SpriteDrawMode.Sliced;
        _back.size = size;
        _back.color = Panel;

        var crew = CrewMeter.CrewSprite();
        var circle = UiAssets.Circle;
        int layer = ctx.Layer;

        // 移動速度: 上の段 = 1 倍 (灰)・下の段 = 今の速さ
        _speedRoot = Node("Speed", go.transform, layer);
        float laneY = Height * 0.22f;
        foreach (float y in new[] { laneY, -laneY })
        {
            var line = Part(_speedRoot.transform, "Lane", MenuArt.Round(0.015f), Lane, 1);
            line.drawMode = SpriteDrawMode.Sliced;
            line.size = new Vector2(Width - 0.2f, 0.03f);
            line.transform.localPosition = new Vector3(0f, y - CrewH / 2f, 0f);
        }
        _refCrew = Figure(_speedRoot.transform, crew, RefCrew, laneY, 3).transform;
        _curCrew = Figure(_speedRoot.transform, crew, CrewColor, -laneY, 3).transform;

        // 視界: 左 = クルー・右 = インポスター
        _visionRoot = Node("Vision", go.transform, layer);
        float side = Width * 0.25f;
        (_crewGlow, _crewLight, _crewBody) = Lit(_visionRoot.transform, crew, circle, CrewColor, -side);
        (_impGlow, _impLight, _impBody) = Lit(_visionRoot.transform, crew, circle, ImpColor, side);

        // キルのクールダウン: インポスターの周りの目盛り・右に狙われるクルー
        _killRoot = Node("Kill", go.transform, layer);
        float cx = -Width * 0.18f, ring = Math.Min(Height * 0.4f, Width * 0.24f);
        for (int i = 0; i < Dots; i++)
        {
            float a = MathF.PI / 2f - i * MathF.PI * 2f / Dots;
            var d = Part(_killRoot.transform, "Dot", circle, DotOff, 1);
            d.transform.localScale = new Vector3(0.07f, 0.07f, 1f);
            d.transform.localPosition = new Vector3(cx + MathF.Cos(a) * ring, MathF.Sin(a) * ring, 0f);
            _dots[i] = d;
        }
        Figure(_killRoot.transform, crew, ImpColor, 0f, 3).transform.localPosition = new Vector3(cx, 0f, 0f);
        _victim = Figure(_killRoot.transform, crew, CrewColor, 0f, 3);
        _victim.transform.localPosition = new Vector3(Width * 0.3f, 0f, 0f);
        // 相手を左向きに (インポスターの方を向く)
        _victim.flipX = true;

        Refresh();
        _crewR = _crewTarget;
        _impR = _impTarget;
        ShowScene();
        return go;
    }

    private SpriteRenderer Part(Transform parent, string name, Sprite sprite, Color c, int order)
    {
        var sr = Node(name, parent, parent.gameObject.layer).AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        _parts.Add((sr, order, c.a));
        return sr;
    }

    private SpriteRenderer Figure(Transform parent, Sprite crew, Color c, float y, int order, float height = CrewH)
    {
        var sr = Part(parent, "Crew", crew, c, order);
        var b = crew ? crew.bounds.size : Vector3.one;
        float sc = height / Math.Max(b.y, 0.0001f);
        sr.transform.localScale = new Vector3(sc, sc, 1f);
        sr.transform.localPosition = new Vector3(0f, y, 0f);
        return sr;
    }

    private (SpriteRenderer glow, SpriteRenderer light, SpriteRenderer body) Lit(Transform parent, Sprite crew, Sprite circle, Color c, float x)
    {
        var glow = Part(parent, "Glow", circle, new Color(LightColor.r, LightColor.g, LightColor.b, 0.16f), 1);
        glow.transform.localPosition = new Vector3(x, 0f, 0f);
        var light = Part(parent, "Light", circle, new Color(LightColor.r, LightColor.g, LightColor.b, 0.42f), 2);
        light.transform.localPosition = new Vector3(x, 0f, 0f);
        var body = Figure(parent, crew, c, 0f, 3, VisionCrewH);
        body.transform.localPosition = new Vector3(x, 0f, 0f);
        return (glow, light, body);
    }

    private void ShowScene()
    {
        if (_speedRoot) _speedRoot.SetActive(_scene == Scene.Speed);
        if (_visionRoot) _visionRoot.SetActive(_scene == Scene.Vision);
        if (_killRoot) _killRoot.SetActive(_scene == Scene.KillCooldown);
    }

    // 視界の図: 当てた方を明るく、もう片方を暗く
    private void ApplyFocusAlpha()
    {
        if (!_crewBody) return;
        SetDim(_crewGlow, _crewLight, _crewBody, _focusImpostor);
        SetDim(_impGlow, _impLight, _impBody, !_focusImpostor);
    }

    private void SetDim(SpriteRenderer glow, SpriteRenderer light, SpriteRenderer body, bool dim)
    {
        float k = dim ? 0.35f : 1f;
        foreach (var (r, _, a) in _parts)
            if (r == glow || r == light || r == body)
            {
                var c = r.color;
                c.a = a * k;
                r.color = c;
            }
    }

    // ---- 毎フレーム (ゲームの頁が見えている間だけ) ----

    public void Tick()
    {
        if (!_back) return;
        // 描く順: 欄の地の順を起点に、図の部品を地より手前へ (地の順は画面を組んだ時に決まる)
        int baseOrder = _back.sortingOrder;
        if (baseOrder != _appliedBase)
        {
            _appliedBase = baseOrder;
            foreach (var (r, order, _) in _parts)
                if (r) r.sortingOrder = baseOrder + order;
        }
        float now = Time.unscaledTime;
        float dt = _last < 0f ? 0f : Math.Min(now - _last, 0.1f);
        _last = now;
        switch (_scene)
        {
            case Scene.Speed: TickSpeed(now); break;
            case Scene.Vision: TickVision(dt); break;
            case Scene.KillCooldown: TickKill(now); break;
        }
    }

    private void TickSpeed(float now)
    {
        float margin = 0.2f, span = Width - margin * 2f;
        Run(_refCrew, now, 1f, margin, span, Height * 0.22f);
        Run(_curCrew, now, _speed, margin, span, -Height * 0.22f);
    }

    private void Run(Transform t, float now, float speed, float margin, float span, float y)
    {
        if (!t) return;
        float p = now * speed / RefCross;
        p -= MathF.Floor(p);
        // 歩くたびに小さく跳ねる (速いほど細かく)
        float bob = MathF.Abs(MathF.Sin(now * speed * 9f)) * 0.03f;
        t.localPosition = new Vector3(-Width / 2f + margin + p * span, y + bob, 0f);
    }

    private void TickVision(float dt)
    {
        float k = 1f - MathF.Exp(-dt * 12f);
        _crewR += (_crewTarget - _crewR) * k;
        _impR += (_impTarget - _impR) * k;
        SetLight(_crewGlow, _crewLight, _crewR);
        SetLight(_impGlow, _impLight, _impR);
    }

    private static void SetLight(SpriteRenderer glow, SpriteRenderer light, float r)
    {
        if (light) light.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
        if (glow) glow.transform.localScale = new Vector3(r * 2.5f, r * 2.5f, 1f);
    }

    private void TickKill(float now)
    {
        float period = Math.Max(_cooldown * CooldownScale, 0.3f);
        float p = now / period;
        p -= MathF.Floor(p);
        int lit = (int)(p * Dots) + 1;
        for (int i = 0; i < Dots; i++)
            if (_dots[i]) _dots[i].color = i < lit ? ImpColor : DotOff;
        // 1 周の終わりに、狙われたクルーが一瞬消える
        if (_victim)
        {
            var c = _victim.color;
            c.a = p > 0.92f ? 0.15f : 1f;
            _victim.color = c;
        }
    }
}
