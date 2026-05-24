using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MRMenuController : MonoBehaviour
{
    [Serializable]
    public class HandicapEntry
    {
        public string title = "Handicap";
        [TextArea(3, 8)] public string description = "Description du symptôme.";
        [TextArea(3, 8)] public string correctionDescription = "";
        public string anatomyPart = "";
        public Sprite tilePhoto;
        public Sprite illustration;
        public string sceneToLaunch = "";
        public Color accent = new Color(0.24f, 0.66f, 0.96f, 1f);
    }

    [Serializable]
    public class IntroSlide
    {
        public string title = "Slide";
        [TextArea(3, 8)] public string body = "";
        public string glyph = "i";
        public Color accent = new Color(0.20f, 0.65f, 0.95f, 1f);
        public Sprite illustration;
    }

    [Header("Suivi du joueur")]
    public Transform headAnchor;
    public float distance = 1.4f;
    public float verticalOffset = -0.15f;
    [Range(0f, 20f)] public float followSmoothing = 6f;

    [Header("Pointeur manette")]
    public Transform pointerOrigin;
    public float pointerLength = 5f;
    public float pointerWidth = 0.006f;
    public Color pointerColor = new Color(0.20f, 0.85f, 1f, 0.9f);

    [Header("Carrousel d'intro")]
    public List<IntroSlide> introSlides = new List<IntroSlide>();

    [Header("Handicaps")]
    public List<HandicapEntry> handicaps = new List<HandicapEntry>();

    [Header("Grille")]
    public int gridColumns = 4;

    public enum Page { Intro, Grid, Detail }
    [Header("Démarrage")]
    public Page startPage = Page.Intro;
    Page page = Page.Intro;
    int selectedIndex = 0;
    int currentSlide = 0;

    Canvas canvas;
    RectTransform canvasRect;
    GameObject introPanel, gridPanel, detailPanel;

    TextMeshProUGUI introTitle, introBody, introIconGlyph, introHint, introCounter;
    Image introIconBg, introIconShadow;
    readonly List<Image> introDots = new List<Image>();

    readonly List<TileRefs> tiles = new List<TileRefs>();
    Image detailIllustration;
    TextMeshProUGUI detailTitle, detailBody;
    RectTransform testerButtonRect;

    LineRenderer pointerLine;
    GameObject pointerReticle;
    TMP_FontAsset font;

    static readonly Color BG_CREAM        = new Color(0.96f, 0.94f, 0.88f, 0.98f);
    static readonly Color BG_TOP_FADE     = new Color(0.99f, 0.98f, 0.95f, 0.98f);
    static readonly Color TILE_WHITE      = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    static readonly Color TILE_SHADOW     = new Color(0.00f, 0.00f, 0.00f, 0.18f);
    static readonly Color SELECTION_BLUE  = new Color(0.13f, 0.67f, 1.00f, 1.00f);
    static readonly Color TEXT_DARK       = new Color(0.13f, 0.16f, 0.22f, 1.00f);
    static readonly Color TEXT_GRAY       = new Color(0.42f, 0.47f, 0.54f, 1.00f);
    static readonly Color BUTTON_BLUE     = new Color(0.13f, 0.57f, 0.96f, 1.00f);
    static readonly Color DOT_INACTIVE    = new Color(0.78f, 0.78f, 0.80f, 0.85f);

    Sprite spriteBg, spriteTile, spriteRing, spriteButton, spriteHeader, spriteShadow, spriteCircle;

    class TileRefs
    {
        public RectTransform rect;
        public Image background;
        public Image shadow;
        public Image selection;
        public Image photo;
        public TextMeshProUGUI label;
        public Vector2 baseSize;
    }

    void Awake()
    {
        if (introSlides == null || introSlides.Count == 0) PopulateDefaultSlides();
        if (handicaps == null || handicaps.Count == 0) PopulateDefaultHandicaps();
        ResolveFont();
        BuildSprites();
        BuildUI();
        BuildPointer();
        ShowPage(startPage);
    }

    void Start()
    {
        ResolveHeadAnchor();
        ResolvePointerOrigin();
        AttachToHead();
    }

    void Update()
    {
        UpdatePointer();
        HandleInput();
        if (page == Page.Grid) AnimateSelection();
    }

    void AttachToHead()
    {
        if (headAnchor == null) return;
        transform.SetParent(headAnchor, false);
        transform.localPosition = new Vector3(0f, verticalOffset, distance);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    void PopulateDefaultSlides()
    {
        introSlides.Add(new IntroSlide {
            title = "Bienvenue !",
            body = "Ce simulateur vous fait <b>vivre en réalité mixte</b> le quotidien des personnes atteintes de troubles visuels.\n\nL'objectif : <b>sensibiliser</b> et mieux comprendre.",
            glyph = "i",
            accent = new Color(0.20f, 0.65f, 0.95f, 1f)
        });
        introSlides.Add(new IntroSlide {
            title = "Comment ça marche",
            body = "<b>1.</b>  Pointez avec votre manette sur un trouble visuel\n\n<b>2.</b>  Lisez la description du symptôme\n\n<b>3.</b>  Lancez la simulation et explorez votre environnement réel modifié",
            glyph = "?",
            accent = new Color(0.40f, 0.78f, 0.45f, 1f)
        });
        introSlides.Add(new IntroSlide {
            title = "Sécurité",
            body = "•  Dégagez <b>au moins 2 mètres</b> autour de vous\n•  Restez à l'intérieur de votre <b>Guardian</b>\n•  Retirez chaussures, lacets, obstacles au sol\n•  Demandez à quelqu'un de vous <b>surveiller</b>",
            glyph = "!",
            accent = new Color(0.96f, 0.62f, 0.10f, 1f)
        });
        introSlides.Add(new IntroSlide {
            title = "Santé",
            body = "•  <b>Pause de 5 min</b> toutes les 20 min\n•  Arrêtez en cas de <b>nausée, vertige ou fatigue oculaire</b>\n•  <b>Déconseillé aux moins de 13 ans</b>\n•  Évitez en cas d'antécédents d'épilepsie",
            glyph = "+",
            accent = new Color(0.86f, 0.30f, 0.32f, 1f)
        });
        introSlides.Add(new IntroSlide {
            title = "Prêt ?",
            body = "<b>Pointez avec votre manette</b> sur l'un des troubles visuels pour découvrir ses symptômes, puis lancez la simulation.\n\nBonne immersion !",
            glyph = ">",
            accent = new Color(0.20f, 0.65f, 0.95f, 1f)
        });
    }

    void PopulateDefaultHandicaps()
    {
        handicaps.Add(new HandicapEntry {
            title = "Glaucome",
            description = "Perte progressive de la vision périphérique : le champ visuel se rétrécit en \"vision tubulaire\". Sans traitement, peut conduire à la cécité.",
            correctionDescription = "Collyres pour faire baisser la pression intra-oculaire, parfois laser ou chirurgie. Plus le diagnostic est précoce, mieux la vision restante est préservée.",
            anatomyPart = "OpticNerve",
            sceneToLaunch = "Sim_Glaucome",
            accent = new Color(0.13f, 0.67f, 1.00f, 1f)
        });
        handicaps.Add(new HandicapEntry {
            title = "Daltonisme",
            description = "Confusion entre certaines couleurs, le plus souvent rouge et vert (deutéranopie). Les nuances paraissent ternes et difficiles à distinguer.",
            correctionDescription = "Pas de traitement médical : le daltonisme est génétique. Des lunettes filtrantes (type EnChroma) renforcent le contraste entre rouge et vert.",
            anatomyPart = "Retina",
            sceneToLaunch = "Sim_Daltonisme",
            accent = new Color(0.40f, 0.78f, 0.45f, 1f)
        });
        handicaps.Add(new HandicapEntry {
            title = "Myopie",
            description = "L'image se forme avant la rétine : ce qui est loin devient flou alors que la vision de près reste nette.",
            correctionDescription = "Lunettes ou lentilles de contact à verres divergents, chirurgie laser (LASIK, PKR) pour remodeler la cornée.",
            anatomyPart = "Lens",
            sceneToLaunch = "Sim_Myopie",
            accent = new Color(0.55f, 0.45f, 0.85f, 1f)
        });
        handicaps.Add(new HandicapEntry {
            title = "Dyslexie",
            description = "Trouble cognitif (et non oculaire) : les lettres semblent s'inverser, sauter ou se mélanger. Lire fatigue énormément.",
            correctionDescription = "Pas de \"guérison\" : rééducation orthophonique, polices adaptées (OpenDyslexic), audiolivres, mise en page aérée et synthèse vocale.",
            anatomyPart = "Brain",
            sceneToLaunch = "Sim_Dyslexie",
            accent = new Color(0.95f, 0.55f, 0.30f, 1f)
        });
        handicaps.Add(new HandicapEntry {
            title = "Nystagmus",
            description = "Mouvements oscillants involontaires des yeux : l'image \"tremble\" en permanence. Très fatigant, gêne la lecture et l'écriture.",
            correctionDescription = "Lunettes à prismes pour stabiliser, parfois injections de toxine botulique ou chirurgie sur les muscles oculomoteurs.",
            anatomyPart = "Muscles",
            sceneToLaunch = "Sim_Nystagmus",
            accent = new Color(0.20f, 0.75f, 0.78f, 1f)
        });
        handicaps.Add(new HandicapEntry {
            title = "Photophobie",
            description = "Sensibilité douloureuse à la lumière : la moindre source lumineuse devient éblouissante et empêche de garder les yeux ouverts.",
            correctionDescription = "Lunettes teintées (filtres FL-41), traitement de la cause (migraine, sécheresse oculaire, uvéite). Adaptation de l'éclairage.",
            anatomyPart = "Iris",
            sceneToLaunch = "Sim_Photophobie",
            accent = new Color(0.96f, 0.80f, 0.20f, 1f)
        });
        handicaps.Add(new HandicapEntry {
            title = "Rétinopathie diabétique",
            description = "Atteinte des vaisseaux de la rétine causée par le diabète : taches sombres ou floues, lectures rendues difficiles, risque de cécité.",
            correctionDescription = "Contrôle strict du diabète, injections d'anti-VEGF dans l'œil, photocoagulation au laser pour cicatriser les vaisseaux fragiles.",
            anatomyPart = "Retina",
            sceneToLaunch = "Sim_RetinopathieDiabetique",
            accent = new Color(0.86f, 0.30f, 0.32f, 1f)
        });
        handicaps.Add(new HandicapEntry {
            title = "Troubles visiospatiaux",
            description = "Mauvaise perception des distances et de la profondeur : difficile d'attraper un objet, de viser ou d'éviter un obstacle.",
            correctionDescription = "Rééducation orthoptique (exercices de coordination œil-main, perception 3D), parfois lunettes prismatiques.",
            anatomyPart = "VisualCortex",
            sceneToLaunch = "Sim_TroublesVisiospatiaux",
            accent = new Color(0.30f, 0.40f, 0.75f, 1f)
        });
    }

    void ResolveFont()
    {
        font = TMP_Settings.defaultFontAsset;
        if (font != null) return;
        var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (all != null && all.Length > 0) font = all[0];
    }

    void ResolveHeadAnchor()
    {
        if (headAnchor != null) return;
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null) headAnchor = rig.centerEyeAnchor;
        if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
    }

    void ResolvePointerOrigin()
    {
        if (pointerOrigin != null) return;
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null && rig.rightHandAnchor != null) pointerOrigin = rig.rightHandAnchor;
        if (pointerOrigin == null && headAnchor != null) pointerOrigin = headAnchor;
    }

    void BuildSprites()
    {
        spriteBg      = MakeRoundedRect(512, 512, 48, Color.white);
        spriteTile    = MakeRoundedRect(256, 256, 36, Color.white);
        spriteRing    = MakeRoundedRing(256, 256, 36, 12, Color.white);
        spriteButton  = MakeRoundedRect(256, 96, 40, Color.white);
        spriteHeader  = MakeRoundedRect(512, 128, 36, Color.white);
        spriteShadow  = MakeSoftShadow(256, 256, 36, 18, new Color(0, 0, 0, 0.35f));
        spriteCircle  = MakeRoundedRect(64, 64, 32, Color.white);
    }

    void BuildUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var canvasGO = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1400, 900);
        canvasRect.localScale = Vector3.one * 0.001f;
        canvasRect.localPosition = Vector3.zero;

        var bg = NewImage("Background", canvasRect, BG_CREAM, spriteBg);
        Stretch(bg.rectTransform);

        var topFade = NewImage("TopFade", canvasRect, BG_TOP_FADE, spriteHeader);
        var topRT = topFade.rectTransform;
        topRT.anchorMin = new Vector2(0, 1);
        topRT.anchorMax = new Vector2(1, 1);
        topRT.pivot = new Vector2(0.5f, 1);
        topRT.anchoredPosition = new Vector2(0, -20);
        topRT.sizeDelta = new Vector2(-80, 130);

        BuildIntroCarousel();
        BuildGrid();
        BuildDetail();
    }

    void BuildIntroCarousel()
    {
        introPanel = NewPanel("IntroPanel", canvasRect);
        Stretch(introPanel.GetComponent<RectTransform>());

        introCounter = NewText("IntroCounter", introPanel.transform, "1 / 5", 24, TEXT_GRAY, FontStyles.Bold, TextAlignmentOptions.Right);
        Anchor(introCounter.rectTransform, new Vector2(0.78f, 0.92f), new Vector2(0.94f, 0.965f));

        introTitle = NewText("IntroTitle", introPanel.transform, "Bienvenue !", 76, TEXT_DARK, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(introTitle.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.92f));

        var iconArea = NewPanel("IconArea", introPanel.transform);
        var iconAreaRT = iconArea.GetComponent<RectTransform>();
        Anchor(iconAreaRT, new Vector2(0.08f, 0.22f), new Vector2(0.36f, 0.72f));

        introIconShadow = NewImage("IconShadow", iconAreaRT, TILE_SHADOW, spriteShadow);
        var ish = introIconShadow.rectTransform;
        ish.anchorMin = Vector2.zero; ish.anchorMax = Vector2.one;
        ish.offsetMin = new Vector2(-12, -22); ish.offsetMax = new Vector2(12, 2);

        introIconBg = NewImage("IconBg", iconAreaRT, Color.white, spriteTile);
        Stretch(introIconBg.rectTransform);

        introIconGlyph = NewText("IconGlyph", introIconBg.transform, "i", 220, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(introIconGlyph.rectTransform);

        introBody = NewText("IntroBody", introPanel.transform, "", 30, TEXT_DARK, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        Anchor(introBody.rectTransform, new Vector2(0.40f, 0.22f), new Vector2(0.93f, 0.72f));
        introBody.textWrappingMode = TextWrappingModes.Normal;
        introBody.lineSpacing = 14;

        var dotContainer = new GameObject("Dots", typeof(RectTransform));
        dotContainer.transform.SetParent(introPanel.transform, false);
        var dotRect = dotContainer.GetComponent<RectTransform>();
        Anchor(dotRect, new Vector2(0.30f, 0.10f), new Vector2(0.70f, 0.16f));
        var hLayout = dotContainer.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.spacing = 16;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = false;

        introDots.Clear();
        for (int i = 0; i < introSlides.Count; i++)
        {
            var dot = NewImage("Dot" + i, dotContainer.transform, DOT_INACTIVE, spriteCircle);
            dot.rectTransform.sizeDelta = new Vector2(22, 22);
            var le = dot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 22;
            le.preferredHeight = 22;
            introDots.Add(dot);
        }

        introHint = NewText("IntroHint", introPanel.transform, "", 26, TEXT_GRAY, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(introHint.rectTransform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.085f));

        currentSlide = 0;
        ShowSlide(0);
    }

    void ShowSlide(int idx)
    {
        if (introSlides.Count == 0) return;
        currentSlide = Mathf.Clamp(idx, 0, introSlides.Count - 1);
        var s = introSlides[currentSlide];
        if (introTitle != null)
        {
            introTitle.text = s.title;
            introTitle.color = Darken(s.accent, 0.45f);
        }
        if (introBody != null) introBody.text = s.body;
        if (introIconBg != null)
        {
            introIconBg.sprite = spriteTile;
            introIconBg.color = s.accent;
            if (s.illustration != null)
            {
                introIconBg.sprite = s.illustration;
                introIconBg.color = Color.white;
                if (introIconGlyph != null) introIconGlyph.text = "";
            }
            else if (introIconGlyph != null) introIconGlyph.text = s.glyph;
        }
        if (introCounter != null) introCounter.text = (currentSlide + 1) + " / " + introSlides.Count;
        for (int i = 0; i < introDots.Count; i++)
        {
            if (introDots[i] == null) continue;
            bool active = i == currentSlide;
            introDots[i].color = active ? s.accent : DOT_INACTIVE;
            introDots[i].rectTransform.localScale = active ? Vector3.one * 1.35f : Vector3.one;
        }
        if (introHint != null)
        {
            bool isFirst = currentSlide <= 0;
            bool isLast = currentSlide >= introSlides.Count - 1;
            string left = isFirst ? "" : "<b>B</b> précédent      ";
            string right = isLast ? "<b>A</b> / <b>gâchette</b>  commencer  >" : "<b>A</b> / <b>gâchette</b>  suivant  >";
            introHint.text = left + right;
        }
    }

    void BuildGrid()
    {
        gridPanel = NewPanel("GridPanel", canvasRect);
        Stretch(gridPanel.GetComponent<RectTransform>());

        var title = NewText("GridTitle", gridPanel.transform, "Choisissez un trouble visuel", 60, TEXT_DARK, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.96f));

        var hint = NewText("GridHint", gridPanel.transform,
            "<b>Pointez</b> avec votre manette   ·   <b>gâchette</b>  sélectionner   ·   <b>B</b>  retour",
            26, TEXT_GRAY, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(hint.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.10f));

        var grid = new GameObject("Tiles", typeof(RectTransform));
        grid.transform.SetParent(gridPanel.transform, false);
        var gridRect = grid.GetComponent<RectTransform>();
        Anchor(gridRect, new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.84f));

        int n = handicaps.Count;
        int cols = Mathf.Max(1, gridColumns);
        int rows = Mathf.CeilToInt(n / (float)cols);
        float gap = 28f;
        float w = (1260f - gap * (cols - 1)) / cols;
        float h = (610f - gap * (rows - 1)) / rows;

        tiles.Clear();
        for (int i = 0; i < n; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = -1260f * 0.5f + col * (w + gap) + w * 0.5f;
            float y = 610f * 0.5f - row * (h + gap) - h * 0.5f;
            tiles.Add(BuildTile(handicaps[i], gridRect, new Vector2(x, y), new Vector2(w, h)));
        }
        UpdateSelectionVisual();
    }

    TileRefs BuildTile(HandicapEntry e, Transform parent, Vector2 pos, Vector2 size)
    {
        var refs = new TileRefs();
        var tileGO = new GameObject(e.title + "_Tile", typeof(RectTransform));
        tileGO.transform.SetParent(parent, false);
        var rt = tileGO.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        refs.rect = rt;
        refs.baseSize = size;

        refs.shadow = NewImage("Shadow", rt, TILE_SHADOW, spriteShadow);
        var srt = refs.shadow.rectTransform;
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(-12, -22);
        srt.offsetMax = new Vector2(12, 2);

        refs.selection = NewImage("Selection", rt, SELECTION_BLUE, spriteRing);
        var elt = refs.selection.rectTransform;
        elt.anchorMin = Vector2.zero; elt.anchorMax = Vector2.one;
        elt.offsetMin = new Vector2(-8, -8); elt.offsetMax = new Vector2(8, 8);
        refs.selection.gameObject.SetActive(false);

        refs.background = NewImage("Bg", rt, TILE_WHITE, spriteTile);
        Stretch(refs.background.rectTransform);

        var photoGO = new GameObject("Photo", typeof(RectTransform));
        photoGO.transform.SetParent(rt, false);
        var photoRT = photoGO.GetComponent<RectTransform>();
        photoRT.anchorMin = new Vector2(0.05f, 0.36f);
        photoRT.anchorMax = new Vector2(0.95f, 0.94f);
        photoRT.offsetMin = Vector2.zero; photoRT.offsetMax = Vector2.zero;
        var photoMask = photoGO.AddComponent<Image>();
        photoMask.sprite = spriteTile;
        photoMask.type = Image.Type.Sliced;
        photoMask.color = e.accent;
        photoMask.raycastTarget = false;
        photoGO.AddComponent<Mask>().showMaskGraphic = true;

        var inner = new GameObject("PhotoInner", typeof(RectTransform));
        inner.transform.SetParent(photoRT, false);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = Vector2.zero; innerRT.offsetMax = Vector2.zero;
        refs.photo = inner.AddComponent<Image>();
        refs.photo.preserveAspect = true;
        refs.photo.raycastTarget = false;
        if (e.tilePhoto != null) refs.photo.sprite = e.tilePhoto;
        else
        {
            refs.photo.sprite = MakeGradient(256, 256, Lighten(e.accent, 0.15f), Darken(e.accent, 0.20f));
            refs.photo.preserveAspect = false;
        }

        if (e.tilePhoto == null)
        {
            var bigLetter = NewText("Letter", photoRT,
                e.title.Length > 0 ? e.title.Substring(0, 1).ToUpperInvariant() : "?",
                140, new Color(1, 1, 1, 0.92f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(bigLetter.rectTransform);
        }

        refs.label = NewText("Label", rt, e.title, 24, TEXT_DARK, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchor(refs.label.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.34f));
        refs.label.textWrappingMode = TextWrappingModes.Normal;
        refs.label.enableAutoSizing = true;
        refs.label.fontSizeMin = 16;
        refs.label.fontSizeMax = 24;

        return refs;
    }

    void BuildDetail()
    {
        detailPanel = NewPanel("DetailPanel", canvasRect);
        Stretch(detailPanel.GetComponent<RectTransform>());

        detailTitle = NewText("DetailTitle", detailPanel.transform, "Glaucome", 70, TEXT_DARK, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Anchor(detailTitle.rectTransform, new Vector2(0.07f, 0.83f), new Vector2(0.95f, 0.94f));

        var underline = NewImage("Underline", detailPanel.transform, SELECTION_BLUE, spriteTile);
        Anchor(underline.rectTransform, new Vector2(0.07f, 0.815f), new Vector2(0.20f, 0.825f));

        var illGO = new GameObject("Illustration", typeof(RectTransform));
        illGO.transform.SetParent(detailPanel.transform, false);
        var illRT = illGO.GetComponent<RectTransform>();
        Anchor(illRT, new Vector2(0.06f, 0.22f), new Vector2(0.46f, 0.78f));
        var illMask = illGO.AddComponent<Image>();
        illMask.sprite = spriteTile;
        illMask.type = Image.Type.Sliced;
        illMask.color = TILE_WHITE;
        illMask.raycastTarget = false;
        illGO.AddComponent<Mask>().showMaskGraphic = true;
        var inner = new GameObject("IllInner", typeof(RectTransform));
        inner.transform.SetParent(illRT, false);
        var innerRT = inner.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = Vector2.zero; innerRT.offsetMax = Vector2.zero;
        detailIllustration = inner.AddComponent<Image>();
        detailIllustration.preserveAspect = true;
        detailIllustration.raycastTarget = false;

        var bodyBg = NewImage("BodyBg", detailPanel.transform, new Color(1f, 1f, 1f, 0.85f), spriteTile);
        Anchor(bodyBg.rectTransform, new Vector2(0.49f, 0.22f), new Vector2(0.94f, 0.78f));

        detailBody = NewText("DetailBody", bodyBg.transform,
            "Description du symptôme.", 30, TEXT_DARK, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Anchor(detailBody.rectTransform, new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.92f));
        detailBody.textWrappingMode = TextWrappingModes.Normal;
        detailBody.lineSpacing = 12;

        var btnGO = new GameObject("TesterButton", typeof(RectTransform));
        btnGO.transform.SetParent(detailPanel.transform, false);
        testerButtonRect = btnGO.GetComponent<RectTransform>();
        Anchor(testerButtonRect, new Vector2(0.34f, 0.07f), new Vector2(0.66f, 0.16f));

        var btnShadow = NewImage("BtnShadow", testerButtonRect, new Color(0, 0, 0, 0.25f), spriteShadow);
        var bsr = btnShadow.rectTransform;
        bsr.anchorMin = Vector2.zero; bsr.anchorMax = Vector2.one;
        bsr.offsetMin = new Vector2(-8, -16); bsr.offsetMax = new Vector2(8, 4);

        var btnBg = NewImage("BtnBg", testerButtonRect, BUTTON_BLUE, spriteButton);
        Stretch(btnBg.rectTransform);

        var btnLabel = NewText("BtnLabel", testerButtonRect, ">  TESTER", 38, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(btnLabel.rectTransform);

        var hint = NewText("DetailHint", detailPanel.transform,
            "<b>A</b> / <b>gâchette</b>  lancer la simulation   ·   <b>B</b>  retour",
            26, TEXT_GRAY, FontStyles.Normal, TextAlignmentOptions.Center);
        Anchor(hint.rectTransform, new Vector2(0.05f, 0.01f), new Vector2(0.95f, 0.06f));
    }

    void BuildPointer()
    {
        var lineGO = new GameObject("MRMenuPointerLine", typeof(LineRenderer));
        lineGO.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
        pointerLine = lineGO.GetComponent<LineRenderer>();
        pointerLine.useWorldSpace = true;
        pointerLine.positionCount = 2;
        pointerLine.startWidth = pointerWidth;
        pointerLine.endWidth = pointerWidth * 0.4f;
        pointerLine.numCapVertices = 4;
        pointerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pointerLine.receiveShadows = false;
        var lineMat = new Material(Shader.Find("Sprites/Default"));
        pointerLine.material = lineMat;
        pointerLine.startColor = pointerColor;
        var endC = pointerColor; endC.a = 0.2f;
        pointerLine.endColor = endC;
        pointerLine.gameObject.SetActive(false);

        pointerReticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pointerReticle.name = "MRMenuPointerReticle";
        pointerReticle.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
        pointerReticle.transform.localScale = Vector3.one * 0.018f;
        var col = pointerReticle.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = pointerReticle.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = pointerColor;
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        pointerReticle.SetActive(false);
    }

    void UpdatePointer()
    {
        if (pointerOrigin == null || pointerLine == null) return;
        Vector3 origin = pointerOrigin.position;
        Vector3 fwd = pointerOrigin.forward;
        Ray ray = new Ray(origin, fwd);
        Vector3 endPoint = origin + fwd * pointerLength;
        bool gotHit = false;

        if (page == Page.Grid && tiles.Count > 0 && canvasRect != null)
        {
            Plane plane = new Plane(-canvasRect.forward, canvasRect.position);
            if (plane.Raycast(ray, out float dist) && dist > 0.05f && dist < pointerLength * 1.5f)
            {
                Vector3 hit = ray.GetPoint(dist);
                int hovered = -1;
                for (int i = 0; i < tiles.Count; i++)
                {
                    if (tiles[i].rect == null) continue;
                    Vector3 local = tiles[i].rect.InverseTransformPoint(hit);
                    Rect r = tiles[i].rect.rect;
                    if (r.Contains(new Vector2(local.x, local.y)))
                    {
                        hovered = i;
                        break;
                    }
                }
                if (hovered >= 0)
                {
                    if (hovered != selectedIndex)
                    {
                        selectedIndex = hovered;
                        UpdateSelectionVisual();
                    }
                    endPoint = hit;
                    gotHit = true;
                }
            }
        }

        bool show = page == Page.Grid;
        pointerLine.gameObject.SetActive(show);
        if (show)
        {
            pointerLine.SetPosition(0, origin);
            pointerLine.SetPosition(1, endPoint);
        }
        if (pointerReticle != null)
        {
            pointerReticle.SetActive(show && gotHit);
            if (show && gotHit) pointerReticle.transform.position = endPoint;
        }
    }

    GameObject NewPanel(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    Image NewImage(string name, Transform parent, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.type = Image.Type.Sliced;
        return img;
    }

    TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    void ShowPage(Page p)
    {
        page = p;
        if (introPanel != null) introPanel.SetActive(p == Page.Intro);
        if (gridPanel != null) gridPanel.SetActive(p == Page.Grid);
        if (detailPanel != null) detailPanel.SetActive(p == Page.Detail);
        if (p == Page.Intro) ShowSlide(currentSlide);
        if (p == Page.Detail) PopulateDetail();
        if (p == Page.Grid) UpdateSelectionVisual();
    }

    void PopulateDetail()
    {
        if (handicaps.Count == 0) return;
        var e = handicaps[Mathf.Clamp(selectedIndex, 0, handicaps.Count - 1)];
        if (detailTitle != null) detailTitle.text = e.title;
        if (detailBody != null) detailBody.text = e.description;
        if (detailIllustration != null)
        {
            if (e.illustration != null) { detailIllustration.sprite = e.illustration; detailIllustration.preserveAspect = true; }
            else if (e.tilePhoto != null) { detailIllustration.sprite = e.tilePhoto; detailIllustration.preserveAspect = true; }
            else
            {
                detailIllustration.sprite = MakeGradient(512, 512, Lighten(e.accent, 0.10f), Darken(e.accent, 0.25f));
                detailIllustration.preserveAspect = false;
            }
        }
    }

    void UpdateSelectionVisual()
    {
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].selection != null)
                tiles[i].selection.gameObject.SetActive(i == selectedIndex);
    }

    void AnimateSelection()
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t.rect == null) continue;
            float target = (i == selectedIndex) ? 1.06f : 1f;
            t.rect.localScale = Vector3.Lerp(t.rect.localScale, Vector3.one * target, Time.deltaTime * 12f);
            if (t.selection != null && i == selectedIndex)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
                var c = SELECTION_BLUE; c.a = pulse;
                t.selection.color = c;
            }
        }
    }

    void HandleInput()
    {
        bool confirm = SafeGetDown(OVRInput.Button.One)
                    || SafeGetDown(OVRInput.Button.Three)
                    || SafeGetDown(OVRInput.Button.PrimaryIndexTrigger)
                    || SafeGetDown(OVRInput.Button.SecondaryIndexTrigger);
        bool back    = SafeGetDown(OVRInput.Button.Two)
                    || SafeGetDown(OVRInput.Button.Four);

        switch (page)
        {
            case Page.Intro:
                if (confirm)
                {
                    if (currentSlide >= introSlides.Count - 1) ShowPage(Page.Grid);
                    else ShowSlide(currentSlide + 1);
                }
                else if (back && currentSlide > 0)
                {
                    ShowSlide(currentSlide - 1);
                }
                break;
            case Page.Grid:
                if (confirm) ShowPage(Page.Detail);
                else if (back) ShowPage(Page.Intro);
                break;
            case Page.Detail:
                if (confirm) LaunchSelected();
                else if (back) ShowPage(Page.Grid);
                break;
        }
    }

    bool SafeGetDown(OVRInput.Button b)
    {
        try { return OVRInput.GetDown(b); }
        catch { return false; }
    }

    void LaunchSelected()
    {
        if (handicaps.Count == 0) return;
        var e = handicaps[Mathf.Clamp(selectedIndex, 0, handicaps.Count - 1)];
        if (string.IsNullOrEmpty(e.sceneToLaunch))
        {
            Debug.Log("[MRMenu] Aucune scène configurée pour " + e.title + " — câblez sceneToLaunch dans l'inspecteur.");
            return;
        }
        if (Application.CanStreamedLevelBeLoaded(e.sceneToLaunch))
            SceneManager.LoadScene(e.sceneToLaunch);
        else
            Debug.LogWarning("[MRMenu] Scène introuvable dans Build Settings : " + e.sceneToLaunch);
    }

    Vector3 SafeHeadPos()
    {
        if (headAnchor == null) return new Vector3(0, 1.6f, 0);
        var p = headAnchor.position;
        if (float.IsNaN(p.x) || float.IsInfinity(p.x) || Mathf.Abs(p.x) > 100f) p.x = 0f;
        if (float.IsNaN(p.y) || float.IsInfinity(p.y) || Mathf.Abs(p.y) > 100f) p.y = 1.6f;
        if (float.IsNaN(p.z) || float.IsInfinity(p.z) || Mathf.Abs(p.z) > 100f) p.z = 0f;
        return p;
    }

    Vector3 SafeHeadFwd()
    {
        if (headAnchor == null) return Vector3.forward;
        var f = headAnchor.forward;
        if (float.IsNaN(f.x) || float.IsInfinity(f.x) || f.sqrMagnitude < 1e-4f) return Vector3.forward;
        return f;
    }

    void SnapToHead()
    {
        if (headAnchor == null) return;
        var p = SafeHeadPos();
        var f = SafeHeadFwd(); f.y = 0;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        f.Normalize();
        transform.position = p + f * distance + Vector3.up * verticalOffset;
        transform.rotation = Quaternion.LookRotation(transform.position - p, Vector3.up);
    }

    void FollowHead()
    {
        if (headAnchor == null) return;
        var p = SafeHeadPos();
        var f = SafeHeadFwd(); f.y = 0;
        if (f.sqrMagnitude < 1e-4f) return;
        f.Normalize();
        Vector3 target = p + f * distance + Vector3.up * verticalOffset;
        float k = followSmoothing <= 0.001f ? 1f : Time.deltaTime * followSmoothing;
        transform.position = Vector3.Lerp(transform.position, target, Mathf.Clamp01(k));
        Vector3 look = transform.position - p;
        if (look.sqrMagnitude > 1e-4f)
        {
            var rot = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Mathf.Clamp01(k));
        }
    }

    static Sprite MakeRoundedRect(int w, int h, int radius, Color fill)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = 0, dy = 0;
            if (x < radius) dx = radius - x - 0.5f;
            else if (x > w - radius - 1) dx = x - (w - radius - 1) - 0.5f;
            if (y < radius) dy = radius - y - 0.5f;
            else if (y > h - radius - 1) dy = y - (h - radius - 1) - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(radius - d);
            var c = fill; c.a *= a;
            px[y * w + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static Sprite MakeRoundedRing(int w, int h, int radius, int thickness, Color fill)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = 0, dy = 0;
            if (x < radius) dx = radius - x - 0.5f;
            else if (x > w - radius - 1) dx = x - (w - radius - 1) - 0.5f;
            if (y < radius) dy = radius - y - 0.5f;
            else if (y > h - radius - 1) dy = y - (h - radius - 1) - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float aOuter = Mathf.Clamp01(radius - d);
            float aInner = Mathf.Clamp01((radius - thickness) - d);
            float a = Mathf.Clamp01(aOuter - aInner);
            var c = fill; c.a *= a;
            px[y * w + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static Sprite MakeSoftShadow(int w, int h, int radius, int blur, Color fill)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = 0, dy = 0;
            if (x < radius + blur) dx = (radius + blur) - x - 0.5f;
            else if (x > w - radius - blur - 1) dx = x - (w - radius - blur - 1) - 0.5f;
            if (y < radius + blur) dy = (radius + blur) - y - 0.5f;
            else if (y > h - radius - blur - 1) dy = y - (h - radius - blur - 1) - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float t = Mathf.Clamp01((radius + blur - d) / (float)(blur));
            t = t * t * (3f - 2f * t);
            var c = fill; c.a *= t;
            px[y * w + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + blur, radius + blur, radius + blur, radius + blur));
    }

    static Sprite MakeGradient(int w, int h, Color top, Color bottom)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            var c = Color.Lerp(bottom, top, t);
            for (int x = 0; x < w; x++) px[y * w + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    static Color Lighten(Color c, float t) => Color.Lerp(c, Color.white, Mathf.Clamp01(t));
    static Color Darken(Color c, float t)  => Color.Lerp(c, Color.black, Mathf.Clamp01(t));

    void OnDestroy()
    {
        if (pointerLine != null) Destroy(pointerLine.gameObject);
        if (pointerReticle != null) Destroy(pointerReticle);
    }
}
