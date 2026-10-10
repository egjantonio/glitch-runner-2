using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Cámara / mundo")]
    public float orthoSize = 5f;     
    public float groundY = -3f;      

    [Header("Escala de sprites (píxeles por unidad: más alto = más chico)")]
    public float playerPPU = 20f;        // 36 px de personaje = 1.8 unidades
    public float groundTilePPU = 42f;
    public float platformTilePPU = 85f;
    public float bgParallax = 0.3f;

    [Header("Nivel 2 por pisos")]
    public float floorLength = 32f;
    public float floorPlayerStartX = 2.7f;

    [Header("Jefe (nivel 2)")]
    public int bossFloor = 5;            // piso donde aparece el jefe
    public int bossMaxHp = 15;          
    public float bigPortalScale = 2.2f;  

    // --- constantes de diseño ---
    private const int EnemyMaxHp = 3;
    private const float EnemyBox = 1.5f;
    private const float EnemyPatrolSpeed = 1.4f;
    private const float EnemyLeash = 1.4f;
    private const float BossBox = 2.8f;
    private const float PortalHeight = 2.2f;
    private const float PortalWidth = 1.1f;
    private const float PlatformThickness = 0.31f;
    private const float GroundDepth = 4f;

    // --- referencias ---
    private Camera cam;
    private PlayerController player;
    private Transform levelRoot;
    private Transform bgTransform;
    private float bgTileW;

    private readonly List<Rect> platforms = new List<Rect>();
    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<SpriteRenderer> portalRenderers = new List<SpriteRenderer>();

    private Rect entryPortal, nextFloorPortal, returnPortal;
    private bool hasReturnPortal;

    // --- jefe / portal bloqueado ---
    private Boss boss;
    private bool bossDefeated;
    private float bossArenaMin;
    private int bossFloorSeed;   //para que no cambie el nivel del boss al recibir daño
    private SpriteRenderer nextPortalRenderer;
    private bool nextPortalLocked;

    private int currentLevel = 1;
    private int currentFloor = 1;

    // --- sprites ---
    private SpriteAnim idleAnim, walkAnim, runAnim, jumpAnim, fallAnim, attackAnim, portalAnim;
    private Sprite groundSprite, platformSprite, enemySprite, heartSprite, bgSprite, bossSprite, heartUiSprite;
    private float enemyW, enemyH, heartH, bossW, bossH;
    private Color bossBaseColor = Color.white;

    // --- UI ---
    private Text stageText;
    private Sprite whiteSprite;
    private Font uiFont;
    private Image[] playerHearts;
    private GameObject bossBarRoot;
    private Image bossBarFill;
    private Text messageText;
    private float messageTimer;
    private bool messagePauses;
    private System.Action onMessageEnd;

    private void Start()
    {
        Application.targetFrameRate = 60;

        // Orientación horizontal
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;

        SetupCamera();
        LoadAssets();
        SetupBackground();
        SetupPlayer();
        SetupUI();
        BuildLevel1();
    }

    // ------------------------------------------------------------------ SETUP

    private void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    private Texture2D Tex(string name)
    {
        var t = Resources.Load<Texture2D>("Sprites/" + name);
        if (t == null)
        {
            Debug.LogError("Falta el sprite 'Assets/Resources/Sprites/" + name + ".png'");
            t = new Texture2D(240, 32, TextureFormat.RGBA32, false);
            var cols = new Color32[240 * 32];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 0, 255, 255);
            t.SetPixels32(cols);
            t.Apply();
        }
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    private Sprite FullSprite(Texture2D t, float ppu, Vector2 pivot)
    {
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, ppu, 0, SpriteMeshType.FullRect);
    }

    private void LoadAssets()
    {
        // Jugador (mismos frameCount y duraciones que el Kotlin; duraciones en segundos)
        idleAnim = new SpriteAnim(0.12f, playerPPU, (Tex("male_hero_idle"), 10));
        walkAnim = new SpriteAnim(0.08f, playerPPU, (Tex("male_hero_walk"), 10));
        runAnim = new SpriteAnim(0.05f, playerPPU, (Tex("male_hero_run"), 10));
        jumpAnim = new SpriteAnim(0.06f, playerPPU, (Tex("male_hero_jump"), 6));
        fallAnim = new SpriteAnim(0.09f, playerPPU, (Tex("male_hero_fall"), 4));
        // El ataque son dos sheets (3 + 4 frames) que se reproducen seguidos
        attackAnim = new SpriteAnim(0.045f, playerPPU,
            (Tex("male_hero_combo_1"), 3), (Tex("male_hero_combo_1_end"), 4));

        // Portal 
        var portalTex = Tex("portal_spin");
        portalAnim = new SpriteAnim(0.06f, portalTex.height / PortalHeight, (portalTex, 12));

       
        var enemyTex = Tex("enemy_glitch");
        Rect b = SpriteAnim.OpaqueBounds(enemyTex);
        float aspect = b.width / b.height;
        if (aspect >= 1f) { enemyW = EnemyBox; enemyH = EnemyBox / aspect; }
        else { enemyH = EnemyBox; enemyW = EnemyBox * aspect; }
        enemySprite = Sprite.Create(enemyTex, b, new Vector2(0.5f, 0f), b.width / enemyW, 0, SpriteMeshType.FullRect);

    
        
        Texture2D bossTex = Resources.Load<Texture2D>("Sprites/boss_glitch");
        bossBaseColor = Color.white;
        if (bossTex == null)
        {
            bossTex = enemyTex;
            bossBaseColor = new Color(1f, 0.6f, 0.9f);
        }
        bossTex.filterMode = FilterMode.Point;
        Rect bb = SpriteAnim.OpaqueBounds(bossTex);
        float bossAspect = bb.width / bb.height;
        if (bossAspect >= 1f) { bossW = BossBox; bossH = BossBox / bossAspect; }
        else { bossH = BossBox; bossW = BossBox * bossAspect; }
        bossSprite = Sprite.Create(bossTex, bb, new Vector2(0.5f, 0f), bb.width / bossW, 0, SpriteMeshType.FullRect);

        // Corazón 
        var heartTex = Tex("health_hearts");
        heartH = enemyW * 0.42f;
        heartSprite = FullSprite(heartTex, heartTex.height / heartH, new Vector2(0.5f, 0.5f));
        heartUiSprite = FullSprite(heartTex, 100f, new Vector2(0.5f, 0.5f));

        
        groundSprite = FullSprite(Tex("foreground"), groundTilePPU, new Vector2(0.5f, 0.5f));
        platformSprite = FullSprite(Tex("platform"), platformTilePPU, new Vector2(0.5f, 0.5f));

        
        var bgTex = Tex("cyberpunk_street");
        float bgPPU = bgTex.height / (orthoSize * 2f);
        bgSprite = FullSprite(bgTex, bgPPU, new Vector2(0.5f, 0.5f));
        bgTileW = bgTex.width / bgPPU;
    }

    private void SetupBackground()
    {
        var go = new GameObject("Background");
        bgTransform = go.transform;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = bgSprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(bgTileW * 4f, orthoSize * 2f);
        sr.sortingOrder = -10;
    }

    private void SetupPlayer()
    {
        var go = new GameObject("Player");
        player = go.AddComponent<PlayerController>();
        player.Platforms = platforms;
        player.Enemies = enemies;
        player.Init(idleAnim, walkAnim, runAnim, jumpAnim, fallAnim, attackAnim);
    }

    // ------------------------------------------------------------------ NIVELES

    private void ClearLevel()
    {
        if (levelRoot != null) Destroy(levelRoot.gameObject);
        levelRoot = new GameObject("Level").transform;
        platforms.Clear();
        enemies.Clear();
        portalRenderers.Clear();

        boss = null;
        bossDefeated = false;
        nextPortalRenderer = null;
        nextPortalLocked = false;
    }

    private void MakeTiled(string name, Sprite sprite, Rect r, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(levelRoot, false);
        go.transform.position = new Vector3(r.center.x, r.center.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = r.size;
        sr.sortingOrder = order;
    }

    private void AddGround(float xMin, float xMax)
    {
        var r = new Rect(xMin, groundY - GroundDepth, xMax - xMin, GroundDepth);
        platforms.Add(r);
        MakeTiled("Ground", groundSprite, r, 0);
    }

    // heightAboveGround = altura de la parte de arriba de la plataforma sobre el piso
    private void AddPlatform(float x, float heightAboveGround, float w)
    {
        var r = new Rect(x, groundY + heightAboveGround - PlatformThickness, w, PlatformThickness);
        platforms.Add(r);
        MakeTiled("Platform", platformSprite, r, 0);
    }

    private void AddEnemy(float x)
    {
        var go = new GameObject("MissingNo");
        go.transform.SetParent(levelRoot, false);
        var e = go.AddComponent<Enemy>();
        e.Init(enemySprite, enemyW, enemyH, heartSprite, heartH, EnemyMaxHp, EnemyPatrolSpeed, EnemyLeash, x, groundY);
        enemies.Add(e);
    }

    private void AddBoss(float x, float arenaMin, float arenaMax)
    {
        var go = new GameObject("Boss");
        go.transform.SetParent(levelRoot, false);
        var b = go.AddComponent<Boss>();
        b.InitBoss(bossSprite, bossW, bossH, bossMaxHp, x, groundY, arenaMin, arenaMax, player, bossBaseColor);
        enemies.Add(b);   // al estar en la lista, el ataque del jugador y el contacto ya lo detectan
        boss = b;
    }

    private Rect AddPortal(float centerX)
    {
        return AddPortal(centerX, 1f, out _);
    }

    
    private Rect AddPortal(float centerX, float scale, out SpriteRenderer renderer)
    {
        var go = new GameObject("Portal");
        go.transform.SetParent(levelRoot, false);
        go.transform.position = new Vector3(centerX, groundY, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = portalAnim.Current;
        sr.sortingOrder = 1;
        portalRenderers.Add(sr);
        renderer = sr;
        return new Rect(centerX - PortalWidth * scale / 2f, groundY, PortalWidth * scale, PortalHeight * scale);
    }

    private void BuildLevel1()
    {
        currentLevel = 1;
        currentFloor = 1;
        ClearLevel();
        cam.backgroundColor = new Color32(0x12, 0x00, 0x24, 255);

        AddGround(-10f, 70f);

       
        float[,] steps =
        {
            { 7.0f, 2.35f }, { 10.6f, 3.76f }, { 14.1f, 2.35f },
            { 17.6f, 4.30f }, { 21.1f, 2.74f }, { 24.7f, 3.52f }
        };
        for (int i = 0; i < steps.GetLength(0); i++)
            AddPlatform(steps[i, 0], steps[i, 1], 2.0f);

        AddEnemy(9.4f);
        AddEnemy(15.7f);
        AddEnemy(22.7f);

        entryPortal = AddPortal(28.75f);
        hasReturnPortal = false;

        player.minX = 0f;
        player.maxX = 59f;
        player.Place(0.8f, groundY);
        UpdateStageText();
    }

    // Un piso completo del nivel 2, generado de una sola vez y distinto cada vez
    private void BuildFloor(int floorNumber, bool keepLayout = false)
    {
        currentLevel = 2;
        currentFloor = floorNumber;
        ClearLevel();
        cam.backgroundColor = new Color32(0x05, 0x24, 0x00, 255);

        AddGround(-4f, floorLength + 4f);

        bool isBossFloor = floorNumber == bossFloor;

        
        Random.State savedRandom = Random.state;
        if (isBossFloor)
        {
            if (!keepLayout) bossFloorSeed = Random.Range(int.MinValue, int.MaxValue);
            Random.InitState(bossFloorSeed);
        }

        float arenaMin = floorLength - 18f;
        float arenaMax = floorLength - 4.5f;

        float x = 3.9f;
        float usableEnd = isBossFloor ? arenaMin - 2f : floorLength - 3.1f;
        int spawnChance = Mathf.Min(30 + floorNumber * 4, 75); // más enemigos en pisos altos

        while (x < usableEnd)
        {
            AddPlatform(x, Random.Range(1.2f, 3.3f), Random.Range(1.6f, 2.7f));

            if (x > 6.5f && Random.Range(0, 100) < spawnChance)
                AddEnemy(x);

            x += Random.Range(2.0f, 3.3f);
        }

        if (isBossFloor)
        {
            AddPlatform(arenaMin + 2.5f, 3.2f, 2.6f);
            AddPlatform(arenaMax - 5.1f, 3.2f, 2.6f);

            bossArenaMin = arenaMin;
            AddBoss((arenaMin + arenaMax) / 2f + 2f, arenaMin, arenaMax);

            // Portal grande = entrada al nivel 3. Bloqueado hasta vencer al jefe
            nextFloorPortal = AddPortal(floorLength - 1.6f, bigPortalScale, out nextPortalRenderer);
            nextPortalLocked = true;
            nextPortalRenderer.color = new Color(1f, 1f, 1f, 0.3f);
        }
        else
        {
            nextFloorPortal = AddPortal(floorLength - 1.0f);
        }

        hasReturnPortal = floorNumber == 1;
        if (hasReturnPortal) returnPortal = AddPortal(1.2f);

        if (isBossFloor) Random.state = savedRandom;   

        player.minX = 0f;
        player.maxX = floorLength + 3f;
        UpdateStageText();
    }

    private void EnterLevel2() { BuildFloor(1); player.Place(floorPlayerStartX, groundY); }
    private void AdvanceFloor() { BuildFloor(currentFloor + 1); player.Place(floorPlayerStartX, groundY); }
    private void ReturnToLevel1() { BuildLevel1(); }

    
    private void EnterLevel3()
    {
        ShowMessage("NIVEL 3\nPróximamente", 3.5f, true, () =>
        {
            player.ResetHealth();
            ReturnToLevel1();
        });
    }

    
    private void Respawn()
    {
        player.ResetHealth();
        if (currentLevel == 2)
        {
            BuildFloor(currentFloor, keepLayout: currentFloor == bossFloor);
            player.Place(floorPlayerStartX, groundY);
        }
        else
        {
            BuildLevel1();
        }
    }

    private void OnBossDefeated()
    {
        bossDefeated = true;
        nextPortalLocked = false;
        if (nextPortalRenderer != null) nextPortalRenderer.color = Color.white;
        ShowMessage("¡JEFE DERROTADO!", 2.5f, false, null);
    }


    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (messageTimer > 0f)
        {
            messageTimer -= dt;
            if (messageTimer <= 0f)
            {
                messageText.gameObject.SetActive(false);
                var cb = onMessageEnd;
                onMessageEnd = null;
                if (cb != null) cb();
            }
            if (messagePauses) return;
        }

        player.Tick(dt);
        foreach (var e in enemies) e.Tick(dt);

        Rect pb = player.Bounds;

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (pb.Overlaps(e.Bounds) && player.TakeDamage(e.ContactDamage, e.transform.position.x))
            {
                if (player.Health <= 0) { Respawn(); return; }
                break;
            }
        }

        if (boss != null && !bossDefeated && boss.Hp <= 0) OnBossDefeated();

        if (currentLevel == 1)
        {
            if (pb.Overlaps(entryPortal)) { EnterLevel2(); return; }
        }
        else
        {
            if (!nextPortalLocked && pb.Overlaps(nextFloorPortal))
            {
                if (currentFloor == bossFloor) EnterLevel3();
                else AdvanceFloor();
                return;
            }
            if (hasReturnPortal && pb.Overlaps(returnPortal)) { ReturnToLevel1(); return; }
        }

        portalAnim.Update(dt);
        foreach (var sr in portalRenderers) sr.sprite = portalAnim.Current;

        RefreshHud();
    }

    private void LateUpdate()
    {
        if (cam == null || player == null) return;

        float camW = 2f * orthoSize * cam.aspect;
        float left = Mathf.Max(0f, player.Position.x - 0.3f * camW);
        float cx = left + camW / 2f;
        cam.transform.position = new Vector3(cx, 0f, -10f);

        bgTransform.position = new Vector3(cx - Mathf.Repeat(cx * bgParallax, bgTileW), 0f, 0f);
    }

    private void UpdateStageText()
    {
        if (stageText == null) return;
        if (currentLevel == 2)
            stageText.text = "Arcade Actual: 2  ·  Piso " + currentFloor + (currentFloor == bossFloor ? "  ·  JEFE" : "");
        else
            stageText.text = "Arcade Actual: 1";
    }

    private void ShowMessage(string text, float duration, bool pauseGame, System.Action onEnd)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        messageTimer = duration;
        messagePauses = pauseGame;
        onMessageEnd = onEnd;
    }

    private void RefreshHud()
    {
        // Corazones del jugador: llenos = vida restante, oscuros = vida perdida
        for (int i = 0; i < playerHearts.Length; i++)
            playerHearts[i].color = i < player.Health ? Color.white : new Color(0.2f, 0.2f, 0.2f, 0.7f);

        // Barra del jefe: aparece cuando te acercas a la arena y se oculta al derrotarlo
        bool showBar = boss != null && !bossDefeated && player.Position.x > bossArenaMin - 5f;
        if (bossBarRoot.activeSelf != showBar) bossBarRoot.SetActive(showBar);
        if (showBar) bossBarFill.fillAmount = boss.HpFraction;
    }

    private void SetupUI()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var wt = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(wt, new Rect(0, 0, wt.width, wt.height), new Vector2(0.5f, 0.5f), 100f);

        var canvasGO = new GameObject("UI");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Texto del arcade / piso
        var tgo = new GameObject("StageText", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(canvasGO.transform, false);
        var rt = (RectTransform)tgo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -30f);
        rt.sizeDelta = new Vector2(1200f, 80f);
        stageText = tgo.GetComponent<Text>();
        stageText.font = uiFont;
        stageText.fontSize = 48;
        stageText.alignment = TextAnchor.UpperLeft;
        stageText.color = Color.white;
        stageText.raycastTarget = false;

        // Corazones del jugador (debajo del texto, arriba a la izquierda)
        playerHearts = new Image[player.maxHealth];
        for (int i = 0; i < playerHearts.Length; i++)
        {
            var hgo = new GameObject("PlayerHeart" + i, typeof(RectTransform), typeof(Image));
            hgo.transform.SetParent(canvasGO.transform, false);
            var hrt = (RectTransform)hgo.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            hrt.anchoredPosition = new Vector2(40f + i * 95f, -120f);
            hrt.sizeDelta = new Vector2(80f, 80f);
            var himg = hgo.GetComponent<Image>();
            himg.sprite = heartUiSprite;
            himg.preserveAspect = true;
            himg.raycastTarget = false;
            playerHearts[i] = himg;
        }

        // Barra de vida del jefe 
        bossBarRoot = new GameObject("BossBar", typeof(RectTransform));
        bossBarRoot.transform.SetParent(canvasGO.transform, false);
        var brt = (RectTransform)bossBarRoot.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.anchoredPosition = new Vector2(-40f, -30f);
        brt.sizeDelta = new Vector2(700f, 90f);

        var lgo = new GameObject("BossName", typeof(RectTransform), typeof(Text));
        lgo.transform.SetParent(bossBarRoot.transform, false);
        var lrt = (RectTransform)lgo.transform;
        lrt.anchorMin = new Vector2(0f, 1f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(0f, 50f);
        var bossName = lgo.GetComponent<Text>();
        bossName.font = uiFont;
        bossName.fontSize = 38;
        bossName.alignment = TextAnchor.MiddleCenter;
        bossName.color = Color.white;
        bossName.text = "MISSINGNO PRIME";
        bossName.raycastTarget = false;

        var backGO = new GameObject("BossBarBack", typeof(RectTransform), typeof(Image));
        backGO.transform.SetParent(bossBarRoot.transform, false);
        var backRT = (RectTransform)backGO.transform;
        backRT.anchorMin = new Vector2(0f, 0f);
        backRT.anchorMax = new Vector2(1f, 0f);
        backRT.pivot = new Vector2(0.5f, 0f);
        backRT.anchoredPosition = Vector2.zero;
        backRT.sizeDelta = new Vector2(0f, 36f);
        var backImg = backGO.GetComponent<Image>();
        backImg.sprite = whiteSprite;
        backImg.color = new Color(0f, 0f, 0f, 0.7f);
        backImg.raycastTarget = false;

        var fillGO = new GameObject("BossBarFill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(backGO.transform, false);
        var fillRT = (RectTransform)fillGO.transform;
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(4f, 4f);
        fillRT.offsetMax = new Vector2(-4f, -4f);
        bossBarFill = fillGO.GetComponent<Image>();
        bossBarFill.sprite = whiteSprite;
        bossBarFill.type = Image.Type.Filled;
        bossBarFill.fillMethod = Image.FillMethod.Horizontal;
        bossBarFill.fillOrigin = 0;
        bossBarFill.fillAmount = 1f;
        bossBarFill.color = new Color(0.9f, 0.15f, 0.2f);
        bossBarFill.raycastTarget = false;

        bossBarRoot.SetActive(false);

        // Mensaje grande en el centro (jefe derrotado / nivel 3 próximamente)
        var mgo = new GameObject("Message", typeof(RectTransform), typeof(Text), typeof(Outline));
        mgo.transform.SetParent(canvasGO.transform, false);
        var mrt = (RectTransform)mgo.transform;
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.5f);
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.anchoredPosition = Vector2.zero;
        mrt.sizeDelta = new Vector2(1400f, 400f);
        messageText = mgo.GetComponent<Text>();
        messageText.font = uiFont;
        messageText.fontSize = 96;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = Color.white;
        messageText.raycastTarget = false;
        mgo.GetComponent<Outline>().effectColor = Color.black;
        mgo.SetActive(false);
    }
}
