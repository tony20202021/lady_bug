using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneSetup
{
    const int LaneCount = 3;
    const float LaneWidth = 4f;
    const float RoadLength = 150f;
    const float RoadCenterZ = 1f;
    const float DashPeriod = 4f;
    const float ScrollSpeed = 10f;
    const float RoadTextureTileSize = 1.5f; // world units per asphalt-texture tile — must match CreateRoadTexture's mainTextureScale divisor

    [MenuItem("Tools/Rebuild Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateLight();
        CreateSpeedController();
        CreateGestureSensorSerial();
        CreateAchievementStats();
        CreatePlayerPhotoCapture();

        // Two players sharing one road/score/speed — co-op, not split-screen.
        // Right player: arrows for lane/jump/duck. Left player: WASD for
        // lane/jump/duck. No accel/brake keys anymore — the road always
        // accelerates on its own (SpeedController) and braking was removed
        // from every control scheme entirely.
        // Each also gets a gesture-sensor simulator (disabled by default): 2
        // distance sensors per player (left hand, right hand), 2 keys each
        // (up/down) — the up key doubles as the flap key (rapid taps =
        // jump), matching the on-screen square grid in CreateGesturePanel:
        // Right player:  U O       Left player:   Q E
        //                J L                       A D
        GameObject playerRight = CreatePlayer("PlayerRight", KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
            KeyCode.U, KeyCode.J, KeyCode.O, KeyCode.L, LaneCount - 1, Color.white, "LadyBug1.png");
        GameObject playerLeft = CreatePlayer("PlayerLeft", KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S,
            KeyCode.Q, KeyCode.A, KeyCode.E, KeyCode.D, 0, new Color(0.55f, 0.75f, 1f), "LadyBug2.png");

        CreateCamera(playerRight.transform);
        CreateRoad();
        CreateSpawner();
        CreateBigArchSpawner();
        CreateSideScenery();
        CreateSky();
        CreateAudio();
        CreateHelpScreen();
        CreateScoreUI(out Canvas scoreCanvas);
        CreateWinSequence(scoreCanvas);
        CreateTricksUI();
        (GameObject gestureCanvasLeft, GameObject gestureCanvasRight) = CreateGestureIndicators(playerRight, playerLeft);
        CreateStartScreen(playerRight, playerLeft, gestureCanvasLeft, gestureCanvasRight);
        CreatePauseDialog();
        CreateExitGesture();
        CreateIntroScreen();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        string scenePath = "Assets/Scenes/Main.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        Debug.Log("Scene setup complete: " + scenePath);
    }

    static void CreateSpeedController()
    {
        var go = new GameObject("GameManager");
        SpeedController speed = go.AddComponent<SpeedController>();

        SerializedObject so = new SerializedObject(speed);
        so.FindProperty("minSpeed").floatValue = 3f;
        so.FindProperty("maxSpeed").floatValue = 200f;
        so.FindProperty("baseAccel").floatValue = 6f;
        so.FindProperty("accelFalloffSpeed").floatValue = 15f;
        so.FindProperty("gearStepKmh").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Single shared reader for the 6-channel gesture-sensor board (both
    // players read out of it — see GestureSensorSerial and
    // ArduinoFirmware/GestureSensors). Harmless if no board is plugged in:
    // it just keeps retrying in the background and IsConnected stays false.
    static void CreateGestureSensorSerial()
    {
        var go = new GameObject("GestureSensorSerial");
        go.AddComponent<GestureSensorSerial>();
    }

    // Per-category collect/hit/trick counters, read once at the end by
    // WinSequence's achievements screen — see AchievementStats.cs.
    static void CreateAchievementStats()
    {
        var go = new GameObject("AchievementStats");
        go.AddComponent<AchievementStats>();
    }

    // Full-screen "smile, the camera is taking your picture" overlay used
    // when a run lands in a leaderboard's top 10 — see PlayerPhotoCapture.cs.
    static void CreatePlayerPhotoCapture()
    {
        var canvasGo = new GameObject("PhotoCaptureCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210; // above everything, including the pause dialog (200)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.8f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var messageGo = new GameObject("Message");
        messageGo.transform.SetParent(canvasGo.transform, false);
        Text message = messageGo.AddComponent<Text>();
        message.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        message.fontSize = 56;
        message.fontStyle = FontStyle.Bold;
        message.alignment = TextAnchor.MiddleCenter;
        message.color = new Color(1f, 0.85f, 0.2f);
        messageGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform messageRt = message.GetComponent<RectTransform>();
        messageRt.anchorMin = new Vector2(0.5f, 0.5f);
        messageRt.anchorMax = new Vector2(0.5f, 0.5f);
        messageRt.pivot = new Vector2(0.5f, 0.5f);
        messageRt.sizeDelta = new Vector2(1400f, 140f);
        messageRt.anchoredPosition = new Vector2(0f, 340f);

        var smileGo = new GameObject("SmileText");
        smileGo.transform.SetParent(canvasGo.transform, false);
        Text smile = smileGo.AddComponent<Text>();
        smile.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        smile.fontSize = 30;
        smile.fontStyle = FontStyle.Bold;
        smile.alignment = TextAnchor.MiddleCenter;
        smile.color = Color.white;
        smile.text = "УЛЫБНИТЕСЬ — ВАС СНИМАЕТ КАМЕРА ДЛЯ ИСТОРИИ";
        smileGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform smileRt = smile.GetComponent<RectTransform>();
        smileRt.anchorMin = new Vector2(0.5f, 0.5f);
        smileRt.anchorMax = new Vector2(0.5f, 0.5f);
        smileRt.pivot = new Vector2(0.5f, 0.5f);
        smileRt.sizeDelta = new Vector2(1200f, 50f);
        smileRt.anchoredPosition = new Vector2(0f, 250f);

        var previewGo = new GameObject("CameraPreview");
        previewGo.transform.SetParent(canvasGo.transform, false);
        RawImage preview = previewGo.AddComponent<RawImage>();
        preview.color = Color.white;
        RectTransform previewRt = preview.GetComponent<RectTransform>();
        previewRt.anchorMin = new Vector2(0.5f, 0.5f);
        previewRt.anchorMax = new Vector2(0.5f, 0.5f);
        previewRt.pivot = new Vector2(0.5f, 0.5f);
        previewRt.sizeDelta = new Vector2(800f, 600f);
        previewRt.anchoredPosition = new Vector2(0f, -60f);

        var countdownGo = new GameObject("Countdown");
        countdownGo.transform.SetParent(canvasGo.transform, false);
        Text countdown = countdownGo.AddComponent<Text>();
        countdown.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countdown.fontSize = 180;
        countdown.fontStyle = FontStyle.Bold;
        countdown.alignment = TextAnchor.MiddleCenter;
        countdown.color = new Color(1f, 1f, 1f, 0.85f);
        Outline countdownOutline = countdownGo.AddComponent<Outline>();
        countdownOutline.effectColor = Color.black;
        countdownOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform countdownRt = countdown.GetComponent<RectTransform>();
        countdownRt.anchorMin = new Vector2(0.5f, 0.5f);
        countdownRt.anchorMax = new Vector2(0.5f, 0.5f);
        countdownRt.pivot = new Vector2(0.5f, 0.5f);
        countdownRt.sizeDelta = new Vector2(400f, 400f);
        countdownRt.anchoredPosition = new Vector2(0f, -60f);

        canvasGo.SetActive(false);

        var controllerGo = new GameObject("PlayerPhotoCapture");
        PlayerPhotoCapture capture = controllerGo.AddComponent<PlayerPhotoCapture>();
        SerializedObject so = new SerializedObject(capture);
        so.FindProperty("overlayRoot").objectReferenceValue = canvasGo;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("smileText").objectReferenceValue = smile;
        so.FindProperty("cameraPreview").objectReferenceValue = preview;
        so.FindProperty("countdownText").objectReferenceValue = countdown;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateLight()
    {
        var lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    static GameObject CreatePlayer(string name, KeyCode left, KeyCode right, KeyCode up, KeyCode down,
        KeyCode leftHandUp, KeyCode leftHandDown, KeyCode rightHandUp, KeyCode rightHandDown,
        int startLane, Color tint, string spriteFile)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player.name = name;

        // Spawn already in the assigned lane — no startup slide-in.
        float startX = (startLane - (LaneCount - 1) / 2f) * LaneWidth;
        player.transform.position = new Vector3(startX, 0.5f, 0f);
        ApplyColor(player, new Color(0.85f, 0.1f, 0.1f));
        player.GetComponent<Renderer>().enabled = false; // cube stays as collider/anchor only
        PlayerController controller = player.AddComponent<PlayerController>();

        // Keep the player's lane spacing in sync with the visual road width —
        // these used to coincidentally match at LaneWidth=3, but must be
        // driven from the same constant now that it can change.
        SerializedObject playerSo = new SerializedObject(controller);
        playerSo.FindProperty("laneCount").intValue = LaneCount;
        playerSo.FindProperty("laneWidth").floatValue = LaneWidth;
        playerSo.FindProperty("startLane").intValue = startLane;
        playerSo.FindProperty("leftKey").intValue = (int)left;
        playerSo.FindProperty("rightKey").intValue = (int)right;
        playerSo.FindProperty("upKey").intValue = (int)up;
        playerSo.FindProperty("downKey").intValue = (int)down;
        playerSo.ApplyModifiedPropertiesWithoutUndo();

        // Gesture (distance-sensor) input — keyboard-simulated for now, since
        // no hardware is connected. Disabled by default; the start screen
        // enables it only if the sensor simulator is the chosen controller.
        GestureInput gesture = player.AddComponent<GestureInput>();
        SerializedObject gestureSo = new SerializedObject(gesture);
        gestureSo.FindProperty("leftHandUpKey").intValue = (int)leftHandUp;
        gestureSo.FindProperty("leftHandDownKey").intValue = (int)leftHandDown;
        gestureSo.FindProperty("rightHandUpKey").intValue = (int)rightHandUp;
        gestureSo.FindProperty("rightHandDownKey").intValue = (int)rightHandDown;
        gestureSo.ApplyModifiedPropertiesWithoutUndo();
        gesture.enabled = false;

        // Trigger detection against the spawned entities' colliders needs a
        // Rigidbody on at least one side of the pair; the player moves via
        // transform, not physics, so it stays kinematic with gravity off.
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Transform sprite = CreatePlayerSprite(player.transform, tint, spriteFile);
        CreatePlayerShadow(player.transform);
        CreatePlayerMovementSfx(player, controller);

        if (sprite != null)
        {
            PlayerAnimator animator = player.AddComponent<PlayerAnimator>();
            SerializedObject animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("player").objectReferenceValue = controller;
            animatorSo.FindProperty("sprite").objectReferenceValue = sprite;

            // Frames 2-4 are edited variants of frame1 (leg pose only, same
            // filename + "FrameN" suffix) — see PlayerAnimator's comment for
            // why they're edits of frame1 rather than independent generations.
            // Cycle order alternates a "tucked" pose with a "wide" one
            // instead of the raw generation order, so it reads as an actual
            // stride rather than 2 poses each held twice as long.
            Texture2D frame1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);
            Texture2D frame2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Frame2.png"));
            Texture2D frame3Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Frame3.png"));
            Texture2D frame4Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Frame4.png"));

            var groundFrames = new System.Collections.Generic.List<Texture2D> { frame1Tex, frame3Tex, frame2Tex, frame4Tex };
            groundFrames.RemoveAll(t => t == null);

            SerializedProperty framesProp = animatorSo.FindProperty("groundFrames");
            framesProp.arraySize = groundFrames.Count;
            for (int i = 0; i < groundFrames.Count; i++)
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = groundFrames[i];

            // Airborne cycle — wings-open frames, same "FrameN" edit-of-frame1
            // convention but under an "AirN" suffix instead.
            Texture2D air1Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Air1.png"));
            Texture2D air2Tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile.Replace(".png", "Air2.png"));

            var airFrames = new System.Collections.Generic.List<Texture2D> { air1Tex, air2Tex };
            airFrames.RemoveAll(t => t == null);

            SerializedProperty airFramesProp = animatorSo.FindProperty("airFrames");
            airFramesProp.arraySize = airFrames.Count;
            for (int i = 0; i < airFrames.Count; i++)
                airFramesProp.GetArrayElementAtIndex(i).objectReferenceValue = airFrames[i];

            animatorSo.ApplyModifiedPropertiesWithoutUndo();
        }

        return player;
    }

    // Tracks the player's X/Z every frame so it's always directly beneath
    // them on the road, regardless of jump/duck/bounce height — makes it
    // much easier to judge when to jump. Not baked into a prefab (players
    // live directly in the scene), so an in-memory Material is fine here.
    static void CreatePlayerShadow(Transform target)
    {
        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "Shadow";
        Object.DestroyImmediate(shadow.GetComponent<Collider>());
        // Parented so it's deactivated along with the player in 1-player
        // mode — GroundShadow still overrides world position every frame,
        // so the parenting doesn't affect its tracking.
        shadow.transform.SetParent(target, false);
        shadow.transform.localScale = new Vector3(1.1f, 0.01f, 0.75f);
        shadow.transform.position = new Vector3(target.position.x, 0.02f, target.position.z);

        Renderer renderer = shadow.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { color = new Color(0f, 0f, 0f, 0.4f) };
        renderer.sharedMaterial = material;

        GroundShadow follower = shadow.AddComponent<GroundShadow>();
        SerializedObject so = new SerializedObject(follower);
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("groundY").floatValue = 0.02f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform CreatePlayerSprite(Transform parent, Color tint, string spriteFile)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);
        if (tex == null)
        {
            Debug.LogWarning("LadyBug sprite not found at Assets/Sprites/" + spriteFile);
            return null;
        }

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(parent);

        float aspect = (float)tex.width / tex.height;
        float height = 1.3f;
        sprite.transform.localScale = new Vector3(height * aspect, height, 1f);
        sprite.transform.localPosition = new Vector3(0f, 0.15f, -0.51f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex, color = tint };
        renderer.sharedMaterial = material;

        return sprite.transform;
    }

    static void CreateCamera(Transform lookTarget)
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 1500f;
        camGo.transform.position = new Vector3(0f, 4f, -8f);
        camGo.transform.LookAt(new Vector3(0f, 0.5f, 20f));
        camGo.AddComponent<AudioListener>();
    }

    static void CreateRoad()
    {
        float roadWidth = LaneCount * LaneWidth;

        // Static road surface — starts a bit behind the player and stretches
        // far enough that it visually fades into the horizon instead of
        // showing a hard edge.
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "RoadSurface";
        surface.transform.position = new Vector3(0f, -0.05f, RoadCenterZ);
        surface.transform.localScale = new Vector3(roadWidth, 0.1f, RoadLength);

        Renderer surfaceRenderer = surface.GetComponent<Renderer>();
        Shader roadShader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material roadMaterial = new Material(roadShader)
        {
            mainTexture = CreateRoadTexture(),
            mainTextureScale = new Vector2(roadWidth / RoadTextureTileSize, RoadLength / RoadTextureTileSize)
        };
        surfaceRenderer.sharedMaterial = roadMaterial;

        // Same scroll trick as the lane dividers below — the road itself
        // never moves, but the texture offset animates so the (slightly
        // uneven) asphalt grain streams toward the camera along with
        // everything else on the road. ScrollingTexture's "dashPeriod" is
        // really "world distance per full UV cycle" — for this texture that
        // is RoadTextureTileSize, NOT the lane-divider's DashPeriod (using
        // that here previously made the asphalt visibly crawl slower than
        // the dashed lines instead of scrolling in lockstep with them).
        ScrollingTexture roadScroller = surface.AddComponent<ScrollingTexture>();
        SerializedObject roadSo = new SerializedObject(roadScroller);
        roadSo.FindProperty("dashPeriod").floatValue = RoadTextureTileSize;
        roadSo.ApplyModifiedPropertiesWithoutUndo();

        // Dashed lane markings scroll toward the camera via texture offset —
        // no moving/recycled geometry, so there is no seam or stutter.
        for (int i = 1; i < LaneCount; i++)
        {
            float dividerX = -roadWidth / 2f + i * LaneWidth;
            CreateDashedDivider(dividerX);
        }
    }

    static void CreateDashedDivider(float x)
    {
        GameObject divider = GameObject.CreatePrimitive(PrimitiveType.Plane);
        divider.name = "LaneDivider";
        // Default Plane is 10x10 units; scale to a thin strip covering the road length.
        divider.transform.position = new Vector3(x, 0.02f, RoadCenterZ);
        divider.transform.localScale = new Vector3(0.03f, 1f, RoadLength / 10f);
        Object.DestroyImmediate(divider.GetComponent<MeshCollider>());

        Renderer renderer = divider.GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material material = new Material(shader)
        {
            mainTexture = CreateDashTexture(),
            mainTextureScale = new Vector2(1f, RoadLength / DashPeriod)
        };
        renderer.sharedMaterial = material;

        ScrollingTexture scroller = divider.AddComponent<ScrollingTexture>();
        SerializedObject so = new SerializedObject(scroller);
        so.FindProperty("dashPeriod").floatValue = DashPeriod;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // (name, texture file, on-road height, score: +1 good pickup / -1 bad obstacle)
    static readonly (string, string, float, int)[] LaneObjects =
    {
        ("Flower", "Flower.png", 0.8f, 1),
        ("Heart", "Heart.png", 1.2f, 1),
        ("Cherry", "Cherry.png", 1.2f, 1),
        ("Mosquito", "Mosquito.png", 0.8f, -1),
        ("FlowerPink", "FlowerPink.png", 0.8f, 1),
        ("FlowerYellow", "FlowerYellow.png", 0.8f, 1),
        ("DaisyWhite", "DaisyWhite.png", 0.8f, 1),
        ("DaisyPink", "DaisyPink.png", 0.8f, 1),
        ("SunflowerYellow", "SunflowerYellow.png", 0.8f, 1),
        ("LotusYellow", "LotusYellow.png", 0.8f, 1),
        ("LotusBlue", "LotusBlue.png", 0.8f, 1),
        ("LotusPink", "LotusPink.png", 0.8f, 1),
        ("Star", "Star.png", 1.1f, 1),
        ("MoneyBag", "MoneyBag.png", 1.45f, 1),
        ("HoneyBarrel", "HoneyBarrel.png", 1.3f, 1),
        ("Candy", "Candy.png", 1.1f, 1),
        ("TrafficCone", "TrafficCone.png", 1.1f, -1),
        ("Wheel", "Wheel.png", 1f, -1),
        ("Bicycle", "Bicycle.png", 1.5f, -1),
        ("Motorbike", "Motorbike.png", 1.6f, -1),
        ("Motorcycle", "Motorcycle.png", 1.6f, -1),
        ("Dog", "Dog.png", 1.5f, -1),
        ("Cat", "Cat.png", 1.2f, -1),
        ("Rabbit", "Rabbit.png", 1.45f, -1),
        ("Crow", "Crow.png", 1.1f, -1),
        ("SandPile", "SandPile.png", 1.3f, -1),
        ("BrickPile", "BrickPile.png", 1.3f, -1),
        ("WoodPile", "WoodPile.png", 1.2f, -1),
        ("RockPile", "RockPile.png", 1.4f, -1),
    };

    // Living creatures among LaneObjects that drift sideways between lanes (LaneWalker).
    static readonly string[] WanderingAnimals = { "Dog", "Cat", "Crow", "Rabbit" };

    // Piles are meant to block the whole lane, not just sit in a corner of
    // it — stretched wider than their (roughly square) art would give on
    // its own, via CreateEntityPrefab's width override. Lane is LaneWidth
    // (4) wide; a bit past that so it can't be dodged by hugging the edge.
    static readonly System.Collections.Generic.Dictionary<string, float> LaneObjectWidthOverrides =
        new System.Collections.Generic.Dictionary<string, float>
    {
        { "SandPile", 4.3f },
        { "BrickPile", 4.3f },
        { "WoodPile", 4.3f },
        { "RockPile", 4.3f },
        { "HoneyBarrel", 1.7f }, // wider than its own aspect ratio gives — a stubbier, "thicker" barrel
        { "MoneyBag", 2.1f }, // much wider than its own aspect ratio — a fat, bulging sack
    };

    // The trigger box normally covers the sprite's full drawn height, which
    // for these three made a clean jump-over impossible: the player only
    // rises jumpHeightDelta (1.4) above the road, well under their tall
    // full-height art (handlebars/raised cobra head). Shrunk here to just
    // the ground-level "solid" part a jump should actually need to clear —
    // the sprite itself is untouched, only the invisible box is shorter.
    static readonly System.Collections.Generic.Dictionary<string, float> LaneObjectColliderHeightOverrides =
        new System.Collections.Generic.Dictionary<string, float>
    {
        { "Bicycle", 1.0f },
        { "Motorbike", 1.0f },
        { "Motorcycle", 1.0f },
        { "Dog", 1.0f },
        { "Rabbit", 1.0f },
    };

    // (name, texture file, roadside height)
    static readonly (string, string, float)[] SceneryObjects =
    {
        ("PalmTree", "PalmTree.png", 3.5f),
        ("Cactus", "Cactus.png", 1.8f),
        ("BigCactus", "BigCactus.png", 2f),
        ("PineForest", "PineForest.png", 4f),
        ("Mountain", "Mountain.png", 5f),
        ("GreenHill", "GreenHill.png", 3.5f),
        ("CactusFlowerOrange", "CactusFlowerOrange.png", 1.7f),
    };

    // Flat ground-level decals (lie flat on the road, same "Quad rotated 90°
    // on X" treatment) — (name, texture file, world size). Puddles used to be
    // plain tinted discs in 4 colors; now real images, just water (blue) and
    // an oil spill (dark) — the other 2 colors didn't mean anything specific.
    static readonly (string, string, float)[] GroundDecals =
    {
        ("Pothole", "Pothole.png", 2.2f),
        ("PuddleBlue", "PuddleBlue.png", 1.8f),
        ("PuddleDark", "PuddleDark.png", 1.8f),
    };

    static void CreateSpawner()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        // Three separate pools so the spawner can pick in two steps:
        // good vs. bad, then — only for bad — jump-over vs. duck-under.
        var goodPrefabs = new System.Collections.Generic.List<GameObject>();
        var badJumpPrefabs = new System.Collections.Generic.List<GameObject>();

        foreach (var (name, file, height, score) in LaneObjects)
        {
            bool canWander = System.Array.IndexOf(WanderingAnimals, name) >= 0;
            float? widthOverride = LaneObjectWidthOverrides.TryGetValue(name, out float overrideWidth) ? overrideWidth : (float?)null;
            float? colliderHeightOverride = LaneObjectColliderHeightOverrides.TryGetValue(name, out float overrideColliderHeight) ? overrideColliderHeight : (float?)null;
            GameObject prefab = CreateEntityPrefab(name, "Assets/Sprites/" + file, height, "Assets/Prefabs/" + name + ".prefab", score, canWander, widthOverride, colliderHeightOverride);
            if (prefab == null)
                continue;
            (score > 0 ? goodPrefabs : badJumpPrefabs).Add(prefab);
        }

        foreach (var (name, file, size) in GroundDecals)
        {
            GameObject decal = CreateGroundDecalPrefab(name, file, size);
            if (decal != null)
                badJumpPrefabs.Add(decal);
        }

        GameObject snake = CreateSnakePrefab();
        if (snake != null)
            badJumpPrefabs.Add(snake);

        var badDuckPrefabs = new System.Collections.Generic.List<GameObject>();
        GameObject arch = CreateArchPrefab();
        if (arch != null)
            badDuckPrefabs.Add(arch);

        var spawnerGo = new GameObject("Spawner");
        EntitySpawner spawner = spawnerGo.AddComponent<EntitySpawner>();

        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "goodPrefabs", goodPrefabs);
        SetPrefabArray(so, "badJumpPrefabs", badJumpPrefabs);
        SetPrefabArray(so, "badDuckPrefabs", badDuckPrefabs);
        so.FindProperty("laneCount").intValue = LaneCount;
        so.FindProperty("laneWidth").floatValue = LaneWidth;
        so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetPrefabArray(SerializedObject so, string propertyName, System.Collections.Generic.List<GameObject> list)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        prop.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
    }

    static void CreateSideScenery()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        var prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, file, height) in SceneryObjects)
        {
            GameObject prefab = CreateEntityPrefab(name, "Assets/Sprites/" + file, height, "Assets/Prefabs/" + name + ".prefab");
            if (prefab != null)
                prefabs.Add(prefab);
        }

        var spawnerGo = new GameObject("SceneSpawner");
        SideScenerySpawner spawner = spawnerGo.AddComponent<SideScenerySpawner>();

        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "prefabs", prefabs);
        so.FindProperty("sideOffset").floatValue = LaneCount * LaneWidth / 2f + 2f;
        so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // (name, texture file, world height) — two shape variants for a bit of
    // visual variety as they drift by overhead.
    static readonly (string, string, float)[] CloudSprites =
    {
        ("CloudWhite1", "CloudWhite1.png", 6f),
        ("CloudWhite2", "CloudWhite2.png", 5f),
    };

    // Sun sweeps back and forth along a dome-shaped arc (SunArc) instead of
    // sitting fixed — these are the arc's center/radii, in world units.
    // center.z is deliberately farther out than CloudSpawner's spawnZ (90)
    // so the sun's depth is always greater than any cloud's — with the
    // Cutout shader's normal depth testing, that guarantees it renders
    // behind every cloud, never in front. radiusY/center.y are tuned so the
    // arc peaks near the top of the screen at its centre and dips down to
    // roughly the horizon at its two ends (see the per-point FOV projection
    // math worked out for this camera — not just eyeballed).
    const float SunHeight = 16f;
    static readonly Vector3 SunArcCenter = new Vector3(0f, 5f, 120f);
    const float SunArcRadiusX = 100f;
    const float SunArcRadiusY = 44f;

    static void CreateSky()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        var prefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, file, height) in CloudSprites)
        {
            GameObject prefab = CreateCloudPrefab(name, "Assets/Sprites/" + file, height);
            if (prefab != null)
                prefabs.Add(prefab);
        }

        var spawnerGo = new GameObject("CloudSpawner");
        CloudSpawner spawner = spawnerGo.AddComponent<CloudSpawner>();
        SerializedObject so = new SerializedObject(spawner);
        SetPrefabArray(so, "prefabs", prefabs);
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateSunSprite();
    }

    static GameObject CreateCloudPrefab(string name, string texturePath, float height)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: " + texturePath);
            return null;
        }

        var root = new GameObject(name);
        root.AddComponent<CloudDrift>();

        float aspect = (float)tex.width / tex.height;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localScale = new Vector3(height * aspect, height, 1f);
        sprite.transform.localPosition = Vector3.zero;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateSunSprite()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sun.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/Sun.png");
            return;
        }

        float aspect = (float)tex.width / tex.height;

        var sun = new GameObject("Sun");
        sun.transform.position = SunArcCenter;

        SunArc arc = sun.AddComponent<SunArc>();
        SerializedObject arcSo = new SerializedObject(arc);
        arcSo.FindProperty("center").vector3Value = SunArcCenter;
        arcSo.FindProperty("radiusX").floatValue = SunArcRadiusX;
        arcSo.FindProperty("radiusY").floatValue = SunArcRadiusY;
        arcSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(sun.transform);
        sprite.transform.localScale = new Vector3(SunHeight * aspect, SunHeight, 1f);
        sprite.transform.localPosition = Vector3.zero;
        sprite.transform.localRotation = Quaternion.identity;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };
        renderer.sharedMaterial = material; // scene object, not a prefab — in-memory material is fine here
    }

    static void CreateAudio()
    {
        var audioGo = new GameObject("Audio");

        AudioSource sfxSource = audioGo.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        SfxManager sfx = audioGo.AddComponent<SfxManager>();
        SerializedObject sfxSo = new SerializedObject(sfx);
        sfxSo.FindProperty("source").objectReferenceValue = sfxSource;
        sfxSo.FindProperty("pickupClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PickupPositive.mp3");
        sfxSo.FindProperty("dogClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BadDog.mp3");
        sfxSo.FindProperty("catClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BadCat.mp3");
        sfxSo.FindProperty("crowClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BadCrow.mp3");
        sfxSo.FindProperty("snakeClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SnakeHiss.mp3");
        sfxSo.FindProperty("hitGenericClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HitGeneric.mp3");
        sfxSo.FindProperty("trickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TrickApplause.mp3");
        sfxSo.ApplyModifiedPropertiesWithoutUndo();

        AudioSource shiftSource = audioGo.AddComponent<AudioSource>();
        shiftSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GearShift.wav");
        shiftSource.playOnAwake = false;

        AudioSource humSource = audioGo.AddComponent<AudioSource>();
        humSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/EngineHum.mp3");
        humSource.loop = true;
        humSource.playOnAwake = false;

        GearSfx gearSfx = audioGo.AddComponent<GearSfx>();
        SerializedObject gearSo = new SerializedObject(gearSfx);
        gearSo.FindProperty("shiftSource").objectReferenceValue = shiftSource;
        gearSo.FindProperty("humSource").objectReferenceValue = humSource;
        gearSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // Per-player looping movement sound — running feet while grounded, wing
    // buzz while airborne (jumping/flying). Both sources play continuously
    // from the start at volume 0; PlayerMovementSfx just swaps which one is
    // audible each frame, so there's no restart/click when the state flips.
    static void CreatePlayerMovementSfx(GameObject player, PlayerController controller)
    {
        AudioSource feetSource = player.AddComponent<AudioSource>();
        feetSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/RunFeet.mp3");
        feetSource.loop = true;
        feetSource.playOnAwake = true;
        feetSource.volume = 0f;

        AudioSource wingsSource = player.AddComponent<AudioSource>();
        wingsSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Buzz.wav");
        wingsSource.loop = true;
        wingsSource.playOnAwake = true;
        wingsSource.volume = 0f;

        PlayerMovementSfx sfx = player.AddComponent<PlayerMovementSfx>();
        SerializedObject so = new SerializedObject(sfx);
        so.FindProperty("player").objectReferenceValue = controller;
        so.FindProperty("feetSource").objectReferenceValue = feetSource;
        so.FindProperty("wingsSource").objectReferenceValue = wingsSource;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The snake — a wandering bad object like Dog/Cat/Crow/Rabbit (LaneWalker
    // drives its side-to-side lane changes), but with an actual pose swap
    // instead of just a wiggle: reared up like a cobra while idle, a
    // zigzagging slither while crossing lanes (SnakePose reads
    // LaneWalker.IsMoving). Bespoke instead of going through
    // CreateEntityPrefab since it needs two textures wired to a component,
    // not one texture into a plain material.
    static GameObject CreateSnakePrefab()
    {
        Texture2D idleTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/SnakeCobra.png");
        Texture2D movingTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/SnakeSlither.png");
        if (idleTex == null || movingTex == null)
        {
            Debug.LogWarning("Snake textures not found in Assets/Sprites/");
            return null;
        }

        const string name = "Snake";
        const float height = 1.9f;

        var root = new GameObject(name);
        root.transform.position = new Vector3(0f, height / 2f, 0f);
        root.AddComponent<MovingEntity>();
        root.AddComponent<ScoreValue>().value = -1;

        LaneWalker walker = root.AddComponent<LaneWalker>();
        SerializedObject walkerSo = new SerializedObject(walker);
        walkerSo.FindProperty("laneWidth").floatValue = LaneWidth;
        walkerSo.FindProperty("laneCount").intValue = LaneCount;
        walkerSo.ApplyModifiedPropertiesWithoutUndo();

        float aspect = (float)idleTex.width / idleTex.height;

        // Full sprite height includes the raised cobra head, well above what
        // a jump (jumpHeightDelta 1.4) can clear — trigger box only covers
        // the coiled body at ground level, same fix as Bicycle/Motorcycle.
        const float colliderHeight = 1.0f;
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(height * aspect, colliderHeight, 0.3f);
        box.center = new Vector3(0f, -(height - colliderHeight) / 2f, 0f);

        AddStaticGroundShadow(root, height * aspect * 0.7f, height * 0.35f, name + "_Shadow");

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localScale = new Vector3(height * aspect, height, 1f);
        sprite.transform.localPosition = Vector3.zero;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = idleTex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        SnakePose pose = root.AddComponent<SnakePose>();
        SerializedObject poseSo = new SerializedObject(pose);
        poseSo.FindProperty("walker").objectReferenceValue = walker;
        poseSo.FindProperty("spriteRenderer").objectReferenceValue = renderer;
        poseSo.FindProperty("idleTexture").objectReferenceValue = idleTex;
        poseSo.FindProperty("movingTexture").objectReferenceValue = movingTex;
        poseSo.FindProperty("height").floatValue = height;
        poseSo.ApplyModifiedPropertiesWithoutUndo();

        string savePath = "Assets/Prefabs/Snake.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject CreateEntityPrefab(string name, string texturePath, float height, string savePath, int? score = null, bool canWander = false, float? width = null, float? colliderHeight = null)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: " + texturePath);
            return null;
        }

        var root = new GameObject(name);
        root.transform.position = new Vector3(0f, height / 2f, 0f);
        root.AddComponent<MovingEntity>();
        if (score.HasValue)
            root.AddComponent<ScoreValue>().value = score.Value;

        // A few living bad objects (dog/cat/crow) drift sideways between
        // lanes at random — see LaneWalker.
        if (canWander)
        {
            LaneWalker walker = root.AddComponent<LaneWalker>();
            SerializedObject walkerSo = new SerializedObject(walker);
            walkerSo.FindProperty("laneWidth").floatValue = LaneWidth;
            walkerSo.FindProperty("laneCount").intValue = LaneCount;
            walkerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        float aspect = (float)tex.width / tex.height;
        // Normally width just follows the image's own aspect ratio — an
        // explicit override lets a few obstacles (the road-spanning piles)
        // be stretched wider than their art alone would give, without
        // distorting everything else that goes through this function.
        float spriteWidth = width ?? height * aspect;

        float ch = colliderHeight ?? height;
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(spriteWidth, ch, 0.3f);
        // A shorter box still needs to sit on the ground, not float centered
        // in the middle of the (taller) sprite — shift it down by half the
        // height that got trimmed off.
        box.center = new Vector3(0f, -(height - ch) / 2f, 0f);

        // Bad obstacles get a ground shadow so their lane position is easy
        // to judge when jumping — good pickups don't need it.
        if (score.HasValue && score.Value < 0)
            AddStaticGroundShadow(root, spriteWidth * 0.7f, height * 0.35f, name + "_Shadow");

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);

        sprite.transform.localScale = new Vector3(spriteWidth, height, 1f);
        sprite.transform.localPosition = Vector3.zero;

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        // Prefab assets can only reference materials that are themselves saved
        // assets — an in-memory Material here would serialize as a broken
        // (magenta) reference once written to disk.
        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Flat ground decal — a Quad rotated flat on the road (same trick as
    // every other decal in this file) instead of a standing sprite. Used for
    // the pothole and both puddle colors — jump-over obstacles, no duck rule
    // (there's nothing to duck under, it's a hole/spill in the road).
    static GameObject CreateGroundDecalPrefab(string name, string textureFile, float size)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + textureFile);
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/" + textureFile);
            return null;
        }

        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<ScoreValue>().value = -1;

        float aspect = (float)tex.width / tex.height;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        sprite.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up
        sprite.transform.localScale = new Vector3(size * aspect, size, 1f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(size * aspect, 0.1f, size);

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateHelpScreen()
    {
        var canvasGo = new GameObject("HelpCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // above gameplay UI, below the pause dialog

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var textGo = new GameObject("HelpText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 23;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "СУТЬ ИГРЫ\n"
                  + "Дорога сама разгоняется всё быстрее, собирать хорошее, избегать плохое, вдвоём — делать трюки\n\n"
                  + "ЦЕЛЬ\n"
                  + "Проехать 100 км за самое короткое время, дополнительно набирая очки, трюки и скорость\n\n"
                  + "УПРАВЛЕНИЕ\n"
                  + "Правый: ← → полоса, ↑ прыжок, ↓ пригнуться\n"
                  + "Левый: A D полоса, W прыжок, S пригнуться\n"
                  + "Газа и тормоза больше нет — дорога разгоняется сама, столкновения замедляют\n\n"
                  + "ТРЮКИ\n"
                  + "АРКА: один приседает под аркой, другой в этот момент перепрыгивает её вместе с ним\n"
                  + "КОЛЬЦО: игроки одновременно меняются полосами — один в прыжке, другой понизу\n\n"
                  + "ДАТЧИКИ РАССТОЯНИЯ (ИМИТАТОР)\n"
                  + "2 датчика на игрока — по одному на руку (только верх/низ):\n"
                  + "обе руки вниз — пригнуться, одна вверх/другая вниз — полоса в сторону опущенной руки,\n"
                  + "быстро жать «верх» на обеих руках разом — прыжок-полёт\n"
                  + "Имитатор: левый Q/A верх/низ левой руки, E/D верх/низ правой; правый U/J, O/L\n\n"
                  + "ВЫХОД ИЗ ИГРЫ\n"
                  + "Все игроки разом держат «пригнуться» 10 секунд — после первых 5 молча, "
                  + "следующие 5 с обратным отсчётом на экране";

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1700f, 950f);
        rt.anchoredPosition = new Vector2(0f, 40f);

        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(canvasGo.transform, false);
        Text hint = hintGo.AddComponent<Text>();
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 26;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.85f, 0.85f, 0.85f);
        hint.text = "F1 — закрыть      Q — выйти из игры";
        RectTransform hintRt = hint.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(600f, 60f);
        hintRt.anchoredPosition = new Vector2(0f, 60f);

        canvasGo.SetActive(false);

        var controllerGo = new GameObject("HelpController");
        HelpController controller = controllerGo.AddComponent<HelpController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("helpRoot").objectReferenceValue = canvasGo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Small title pinned to the top of a HUD panel (e.g. "ОЧКИ" above the
    // score number) — same look TricksPanel already used, pulled out so
    // every stacked panel in CreateScoreUI can share it.
    static void CreatePanelLabel(Transform parent, string text)
    {
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(parent, false);
        Text label = labelGo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.color = new Color(0.85f, 0.85f, 0.85f);
        label.text = text;
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.sizeDelta = new Vector2(0f, 26f);
        labelRt.anchoredPosition = new Vector2(0f, -6f);
    }

    static RectTransform CreateScoreUI(out Canvas canvas)
    {
        var canvasGo = new GameObject("ScoreCanvas");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        // Background panel behind the number for a bit of polish.
        var panelGo = new GameObject("ScorePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.sizeDelta = new Vector2(260f, 100f);
        panelRt.anchoredPosition = new Vector2(1920f - 280f, 1080f - 120f);

        CreatePanelLabel(panelGo.transform, "ОЧКИ");

        var textGo = new GameObject("ScoreText");
        textGo.transform.SetParent(panelGo.transform, false);
        Text scoreText = textGo.AddComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 48;
        scoreText.fontStyle = FontStyle.Bold;
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.color = new Color(1f, 0.85f, 0.2f);
        scoreText.text = "0";

        Outline textOutline = textGo.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 0.75f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        // Invisible marker at the panel's center — popups fly toward this
        // point. Shares the same (0,0) anchor/pivot frame as the popups
        // themselves so their anchoredPosition values are directly comparable.
        var counterAnchorGo = new GameObject("CounterAnchor");
        counterAnchorGo.transform.SetParent(canvasGo.transform, false);
        RectTransform counterAnchor = counterAnchorGo.AddComponent<RectTransform>();
        counterAnchor.anchorMin = new Vector2(0f, 0f);
        counterAnchor.anchorMax = new Vector2(0f, 0f);
        counterAnchor.pivot = new Vector2(0f, 0f);
        counterAnchor.anchoredPosition = panelRt.anchoredPosition + panelRt.sizeDelta / 2f;

        var managerGo = new GameObject("ScoreManager");
        ScoreManager manager = managerGo.AddComponent<ScoreManager>();

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("popupParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        so.FindProperty("counterAnchor").objectReferenceValue = counterAnchor;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Timer panel, directly under the score panel.
        var timerPanelGo = new GameObject("TimerPanel");
        timerPanelGo.transform.SetParent(canvasGo.transform, false);
        Image timerPanelImage = timerPanelGo.AddComponent<Image>();
        timerPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform timerPanelRt = timerPanelGo.GetComponent<RectTransform>();
        timerPanelRt.anchorMin = new Vector2(0f, 0f);
        timerPanelRt.anchorMax = new Vector2(0f, 0f);
        timerPanelRt.pivot = new Vector2(0f, 0f);
        timerPanelRt.sizeDelta = new Vector2(260f, 70f);
        timerPanelRt.anchoredPosition = panelRt.anchoredPosition - new Vector2(0f, 10f + timerPanelRt.sizeDelta.y);

        CreatePanelLabel(timerPanelGo.transform, "ВРЕМЯ");

        var timerTextGo = new GameObject("TimerText");
        timerTextGo.transform.SetParent(timerPanelGo.transform, false);
        Text timerText = timerTextGo.AddComponent<Text>();
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize = 30;
        timerText.fontStyle = FontStyle.Bold;
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.color = Color.white;
        timerText.text = "00:00";

        Outline timerOutline = timerTextGo.AddComponent<Outline>();
        timerOutline.effectColor = Color.black;
        timerOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform timerTextRt = timerTextGo.GetComponent<RectTransform>();
        timerTextRt.anchorMin = new Vector2(0f, 0f);
        timerTextRt.anchorMax = new Vector2(1f, 0.7f);
        timerTextRt.offsetMin = Vector2.zero;
        timerTextRt.offsetMax = Vector2.zero;

        var timerManagerGo = new GameObject("GameTimer");
        GameTimer gameTimer = timerManagerGo.AddComponent<GameTimer>();
        SerializedObject timerSo = new SerializedObject(gameTimer);
        timerSo.FindProperty("timerText").objectReferenceValue = timerText;
        timerSo.ApplyModifiedPropertiesWithoutUndo();

        // Speed panel, directly under the timer panel — continues the stack.
        var speedPanelGo = new GameObject("SpeedPanel");
        speedPanelGo.transform.SetParent(canvasGo.transform, false);
        Image speedPanelImage = speedPanelGo.AddComponent<Image>();
        speedPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform speedPanelRt = speedPanelGo.GetComponent<RectTransform>();
        speedPanelRt.anchorMin = new Vector2(0f, 0f);
        speedPanelRt.anchorMax = new Vector2(0f, 0f);
        speedPanelRt.pivot = new Vector2(0f, 0f);
        speedPanelRt.sizeDelta = new Vector2(260f, 100f);
        speedPanelRt.anchoredPosition = timerPanelRt.anchoredPosition - new Vector2(0f, 10f + speedPanelRt.sizeDelta.y);

        CreatePanelLabel(speedPanelGo.transform, "СКОРОСТЬ");

        var speedTextGo = new GameObject("SpeedText");
        speedTextGo.transform.SetParent(speedPanelGo.transform, false);
        Text speedText = speedTextGo.AddComponent<Text>();
        speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        speedText.fontSize = 26;
        speedText.fontStyle = FontStyle.Bold;
        speedText.alignment = TextAnchor.MiddleCenter;
        speedText.color = new Color(0.6f, 0.9f, 1f);
        speedText.text = "0.0 км/ч\nпередача 1";

        Outline speedOutline = speedTextGo.AddComponent<Outline>();
        speedOutline.effectColor = Color.black;
        speedOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform speedTextRt = speedTextGo.GetComponent<RectTransform>();
        speedTextRt.anchorMin = new Vector2(0f, 0f);
        speedTextRt.anchorMax = new Vector2(1f, 0.75f);
        speedTextRt.offsetMin = Vector2.zero;
        speedTextRt.offsetMax = Vector2.zero;

        // Marks the speed panel's own centre in a (0,0)-anchored frame —
        // GearPopup lives in that same frame, so its anchoredPosition is
        // directly comparable/lerp-able against this point (mirrors
        // ScoreManager's counterAnchor setup).
        var speedCounterAnchorGo = new GameObject("SpeedCounterAnchor");
        speedCounterAnchorGo.transform.SetParent(canvasGo.transform, false);
        RectTransform speedCounterAnchor = speedCounterAnchorGo.AddComponent<RectTransform>();
        speedCounterAnchor.anchorMin = Vector2.zero;
        speedCounterAnchor.anchorMax = Vector2.zero;
        speedCounterAnchor.pivot = Vector2.zero;
        speedCounterAnchor.anchoredPosition = speedPanelRt.anchoredPosition + speedPanelRt.sizeDelta / 2f;

        var speedManagerGo = new GameObject("SpeedIndicator");
        SpeedIndicator speedIndicator = speedManagerGo.AddComponent<SpeedIndicator>();
        SerializedObject speedSo = new SerializedObject(speedIndicator);
        speedSo.FindProperty("speedText").objectReferenceValue = speedText;
        speedSo.FindProperty("popupParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        speedSo.FindProperty("counterAnchor").objectReferenceValue = speedCounterAnchor;
        speedSo.FindProperty("panelRoot").objectReferenceValue = speedPanelGo;
        speedSo.ApplyModifiedPropertiesWithoutUndo();

        // Distance panel, directly under the speed panel — same win
        // condition this now drives (100 km — see WinSequence).
        var distancePanelGo = new GameObject("DistancePanel");
        distancePanelGo.transform.SetParent(canvasGo.transform, false);
        Image distancePanelImage = distancePanelGo.AddComponent<Image>();
        distancePanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform distancePanelRt = distancePanelGo.GetComponent<RectTransform>();
        distancePanelRt.anchorMin = new Vector2(0f, 0f);
        distancePanelRt.anchorMax = new Vector2(0f, 0f);
        distancePanelRt.pivot = new Vector2(0f, 0f);
        distancePanelRt.sizeDelta = new Vector2(260f, 90f);
        distancePanelRt.anchoredPosition = speedPanelRt.anchoredPosition - new Vector2(0f, 10f + distancePanelRt.sizeDelta.y);

        CreatePanelLabel(distancePanelGo.transform, "ДИСТАНЦИЯ");

        var distanceTextGo = new GameObject("DistanceText");
        distanceTextGo.transform.SetParent(distancePanelGo.transform, false);
        Text distanceText = distanceTextGo.AddComponent<Text>();
        distanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        distanceText.fontSize = 40;
        distanceText.fontStyle = FontStyle.Bold;
        distanceText.alignment = TextAnchor.MiddleCenter;
        distanceText.color = new Color(0.7f, 1f, 0.7f);
        distanceText.text = "0 из 100";

        Outline distanceOutline = distanceTextGo.AddComponent<Outline>();
        distanceOutline.effectColor = Color.black;
        distanceOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform distanceTextRt = distanceTextGo.GetComponent<RectTransform>();
        distanceTextRt.anchorMin = new Vector2(0f, 0f);
        distanceTextRt.anchorMax = new Vector2(1f, 0.75f);
        distanceTextRt.offsetMin = Vector2.zero;
        distanceTextRt.offsetMax = Vector2.zero;

        var distanceManagerGo = new GameObject("DistanceIndicator");
        DistanceIndicator distanceIndicator = distanceManagerGo.AddComponent<DistanceIndicator>();
        SerializedObject distanceSo = new SerializedObject(distanceIndicator);
        distanceSo.FindProperty("distanceText").objectReferenceValue = distanceText;
        distanceSo.FindProperty("targetKm").floatValue = 10f; // TEMPORARY debug value — keep in sync with WinSequence.winDistanceKm, revert to 100f together
        distanceSo.ApplyModifiedPropertiesWithoutUndo();

        // Top-3 panel, to the left of the score+timer+speed+distance stack,
        // same total height.
        var topPanelGo = new GameObject("TopScoresPanel");
        topPanelGo.transform.SetParent(canvasGo.transform, false);
        Image topPanelImage = topPanelGo.AddComponent<Image>();
        topPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform topPanelRt = topPanelGo.GetComponent<RectTransform>();
        topPanelRt.anchorMin = new Vector2(0f, 0f);
        topPanelRt.anchorMax = new Vector2(0f, 0f);
        topPanelRt.pivot = new Vector2(0f, 0f);
        topPanelRt.sizeDelta = new Vector2(320f, 400f);
        topPanelRt.anchoredPosition = new Vector2(
            panelRt.anchoredPosition.x - 20f - topPanelRt.sizeDelta.x,
            distancePanelRt.anchoredPosition.y);

        var topTitleGo = new GameObject("TopScoresTitle");
        topTitleGo.transform.SetParent(topPanelGo.transform, false);
        Text topTitle = topTitleGo.AddComponent<Text>();
        topTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        topTitle.fontSize = 22;
        topTitle.fontStyle = FontStyle.Bold;
        topTitle.alignment = TextAnchor.MiddleCenter;
        topTitle.color = Color.white;
        topTitle.text = "ТОП-3: ВРЕМЯ";
        topTitleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform topTitleRt = topTitle.GetComponent<RectTransform>();
        topTitleRt.anchorMin = new Vector2(0.5f, 0.5f);
        topTitleRt.anchorMax = new Vector2(0.5f, 0.5f);
        topTitleRt.pivot = new Vector2(0.5f, 0.5f);
        topTitleRt.sizeDelta = new Vector2(300f, 40f);
        topTitleRt.anchoredPosition = new Vector2(0f, 160f);

        // One row per rank — each with its own photo (if that slot ever had
        // one attached), same idea as TopResultsPage on the start screen but
        // scaled down to fit this narrower in-game panel.
        var topRowTexts = new Text[3];
        var topRowPhotos = new RawImage[3];
        float[] topRowY = { 80f, -20f, -120f };

        for (int i = 0; i < 3; i++)
        {
            var rowTextGo = new GameObject("TopRowText" + i);
            rowTextGo.transform.SetParent(topPanelGo.transform, false);
            Text rowText = rowTextGo.AddComponent<Text>();
            rowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rowText.fontSize = 20;
            rowText.fontStyle = FontStyle.Bold;
            rowText.alignment = TextAnchor.MiddleLeft;
            rowText.color = Color.white;
            rowTextGo.AddComponent<Outline>().effectColor = Color.black;
            RectTransform rowTextRt = rowText.GetComponent<RectTransform>();
            rowTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowTextRt.pivot = new Vector2(0.5f, 0.5f);
            rowTextRt.sizeDelta = new Vector2(150f, 40f);
            rowTextRt.anchoredPosition = new Vector2(-75f, topRowY[i]);
            topRowTexts[i] = rowText;

            var rowPhotoGo = new GameObject("TopRowPhoto" + i);
            rowPhotoGo.transform.SetParent(topPanelGo.transform, false);
            RawImage rowPhoto = rowPhotoGo.AddComponent<RawImage>();
            rowPhoto.color = Color.white;
            RectTransform rowPhotoRt = rowPhoto.GetComponent<RectTransform>();
            rowPhotoRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowPhotoRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowPhotoRt.pivot = new Vector2(0.5f, 0.5f);
            rowPhotoRt.sizeDelta = new Vector2(70f, 70f);
            rowPhotoRt.anchoredPosition = new Vector2(115f, topRowY[i]);
            rowPhotoGo.SetActive(false);
            topRowPhotos[i] = rowPhoto;
        }

        var highScoreGo = new GameObject("HighScoreManager");
        HighScoreManager highScore = highScoreGo.AddComponent<HighScoreManager>();
        SerializedObject highScoreSo = new SerializedObject(highScore);
        highScoreSo.FindProperty("titleText").objectReferenceValue = topTitle;
        SerializedProperty topRowTextsProp = highScoreSo.FindProperty("rowTexts");
        topRowTextsProp.arraySize = topRowTexts.Length;
        for (int i = 0; i < topRowTexts.Length; i++)
            topRowTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = topRowTexts[i];
        SerializedProperty topRowPhotosProp = highScoreSo.FindProperty("rowPhotos");
        topRowPhotosProp.arraySize = topRowPhotos.Length;
        for (int i = 0; i < topRowPhotos.Length; i++)
            topRowPhotosProp.GetArrayElementAtIndex(i).objectReferenceValue = topRowPhotos[i];
        highScoreSo.ApplyModifiedPropertiesWithoutUndo();

        return panelRt;
    }

    static void CreateWinSequence(Canvas scoreCanvas)
    {
        var winTextGo = new GameObject("WinText");
        winTextGo.transform.SetParent(scoreCanvas.transform, false);

        Text winText = winTextGo.AddComponent<Text>();
        winText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        winText.fontSize = 90;
        winText.fontStyle = FontStyle.Bold;
        winText.alignment = TextAnchor.MiddleCenter;
        winText.color = new Color(1f, 0.85f, 0.15f);
        winText.text = "ВЫ ПОБЕДИЛИ!";

        Outline winOutline = winTextGo.AddComponent<Outline>();
        winOutline.effectColor = Color.black;
        winOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform winRt = winTextGo.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1100f, 200f);
        winRt.anchoredPosition = new Vector2(0f, 220f);
        winTextGo.SetActive(false);

        // Sequential "new record!" reveal — shown below the win text once
        // the run's final stats are checked against all 4 leaderboards.
        var recordTextGo = new GameObject("RecordText");
        recordTextGo.transform.SetParent(scoreCanvas.transform, false);

        Text recordText = recordTextGo.AddComponent<Text>();
        recordText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        recordText.fontSize = 54;
        recordText.fontStyle = FontStyle.Bold;
        recordText.alignment = TextAnchor.MiddleCenter;
        recordText.color = new Color(0.4f, 1f, 0.5f);
        recordText.text = "";

        Outline recordOutline = recordTextGo.AddComponent<Outline>();
        recordOutline.effectColor = Color.black;
        recordOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform recordRt = recordTextGo.GetComponent<RectTransform>();
        recordRt.anchorMin = new Vector2(0.5f, 0.5f);
        recordRt.anchorMax = new Vector2(0.5f, 0.5f);
        recordRt.pivot = new Vector2(0.5f, 0.5f);
        recordRt.sizeDelta = new Vector2(1000f, 150f);
        recordRt.anchoredPosition = new Vector2(0f, 60f);
        recordTextGo.SetActive(false);

        // Post-win achievements summary — cycles a few pages (totals,
        // collected, hit, tricks+rank), lower on screen than the record
        // reveal since both can briefly overlap in time.
        var achievementsGo = new GameObject("AchievementsText");
        achievementsGo.transform.SetParent(scoreCanvas.transform, false);

        Text achievementsText = achievementsGo.AddComponent<Text>();
        achievementsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        achievementsText.fontSize = 34;
        achievementsText.fontStyle = FontStyle.Bold;
        achievementsText.alignment = TextAnchor.MiddleCenter;
        achievementsText.color = Color.white;
        achievementsText.text = "";

        Outline achievementsOutline = achievementsGo.AddComponent<Outline>();
        achievementsOutline.effectColor = Color.black;
        achievementsOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform achievementsRt = achievementsGo.GetComponent<RectTransform>();
        achievementsRt.anchorMin = new Vector2(0.5f, 0.5f);
        achievementsRt.anchorMax = new Vector2(0.5f, 0.5f);
        achievementsRt.pivot = new Vector2(0.5f, 0.5f);
        achievementsRt.sizeDelta = new Vector2(1000f, 260f);
        achievementsRt.anchoredPosition = new Vector2(0f, -220f);
        achievementsGo.SetActive(false);

        // The finale of the post-win recap: the real per-category top-3
        // tables (same TopResultsPage the start screen carousel uses, photo
        // slots included) instead of a plain numeric rank line — right after
        // the photo capture (RevealRecords), so the just-taken photo is
        // visible immediately instead of only later on the start screen.
        var leaderboardRootGo = new GameObject("WinLeaderboardRoot");
        leaderboardRootGo.transform.SetParent(scoreCanvas.transform, false);
        RectTransform leaderboardRootRt = leaderboardRootGo.AddComponent<RectTransform>();
        leaderboardRootRt.anchorMin = new Vector2(0.5f, 0.5f);
        leaderboardRootRt.anchorMax = new Vector2(0.5f, 0.5f);
        leaderboardRootRt.pivot = new Vector2(0.5f, 0.5f);
        leaderboardRootRt.sizeDelta = new Vector2(1200f, 720f);
        leaderboardRootRt.anchoredPosition = Vector2.zero;

        var leaderboardPages = new GameObject[4];
        for (int category = 0; category < 4; category++)
        {
            GameObject page = CreateTopResultsPage(leaderboardRootRt, category);
            page.SetActive(false);
            leaderboardPages[category] = page;
        }

        var winGo = new GameObject("WinSequence");
        WinSequence win = winGo.AddComponent<WinSequence>();

        SerializedObject so = new SerializedObject(win);
        so.FindProperty("winTextRoot").objectReferenceValue = winRt;
        so.FindProperty("recordText").objectReferenceValue = recordText;
        so.FindProperty("achievementsText").objectReferenceValue = achievementsText;
        SerializedProperty leaderboardPagesProp = so.FindProperty("leaderboardPages");
        leaderboardPagesProp.arraySize = leaderboardPages.Length;
        for (int i = 0; i < leaderboardPages.Length; i++)
            leaderboardPagesProp.GetArrayElementAtIndex(i).objectReferenceValue = leaderboardPages[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Freestyle-tricks counter, right edge, vertically centered — display
    // only for now, no trick detection wired up yet.
    static void CreateTricksUI()
    {
        var canvasGo = new GameObject("TricksCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("TricksPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0.5f);
        panelRt.anchorMax = new Vector2(1f, 0.5f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.sizeDelta = new Vector2(220f, 150f);
        panelRt.anchoredPosition = new Vector2(-40f, 0f);

        CreatePanelLabel(panelGo.transform, "ТРЮКИ");

        var textGo = new GameObject("TricksText");
        textGo.transform.SetParent(panelGo.transform, false);
        Text tricksText = textGo.AddComponent<Text>();
        tricksText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tricksText.fontSize = 56;
        tricksText.fontStyle = FontStyle.Bold;
        tricksText.alignment = TextAnchor.MiddleCenter;
        tricksText.color = new Color(0.6f, 0.9f, 1f);
        tricksText.text = "0";

        Outline textOutline = textGo.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 0.75f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        // Marks the panel's own center in a (0,0)-anchored frame — popups
        // live in that same frame, so their anchoredPosition is directly
        // comparable/lerp-able against this point (mirrors ScoreManager's
        // counterAnchor setup).
        var counterAnchorGo = new GameObject("TricksCounterAnchor");
        counterAnchorGo.transform.SetParent(canvasGo.transform, false);
        RectTransform counterAnchor = counterAnchorGo.AddComponent<RectTransform>();
        counterAnchor.anchorMin = Vector2.zero;
        counterAnchor.anchorMax = Vector2.zero;
        counterAnchor.pivot = Vector2.zero;
        counterAnchor.anchoredPosition = new Vector2(
            1920f + panelRt.anchoredPosition.x - panelRt.sizeDelta.x / 2f,
            540f + panelRt.anchoredPosition.y);

        var managerGo = new GameObject("TricksManager");
        TricksManager manager = managerGo.AddComponent<TricksManager>();
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("tricksText").objectReferenceValue = tricksText;
        so.FindProperty("popupParent").objectReferenceValue = canvasGo.GetComponent<RectTransform>();
        so.FindProperty("counterAnchor").objectReferenceValue = counterAnchor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Bottom-left/bottom-right HUD per player, for the gesture simulator:
    // a legend (keys + gesture descriptions), a row of raw key-state
    // squares, and the interpreted-gesture arrows — sits dim/idle when that
    // player is on keyboard controls (GestureInput disabled). Returns both
    // canvases (left, right) so the start screen can hide them while its
    // own menu is up (they're gameplay HUD, not menu chrome) and reveal
    // them once the game actually begins.
    static (GameObject left, GameObject right) CreateGestureIndicators(GameObject playerRight, GameObject playerLeft)
    {
        GameObject leftCanvas = CreateGesturePanel(playerLeft, new Vector2(0f, 0f),
            "КЛАВИШИ: лев. рука Q/A верх/низ; прав. рука E/D верх/низ\n"
            + "ЖЕСТЫ: обе вниз — присесть, по диагонали — смещение, "
            + "Q+E быстро жать вместе — прыжок-полёт",
            new[] { "Q", "A", "E", "D" });

        GameObject rightCanvas = CreateGesturePanel(playerRight, new Vector2(1f, 0f),
            "КЛАВИШИ: лев. рука U/J верх/низ; прав. рука O/L верх/низ\n"
            + "ЖЕСТЫ: обе вниз — присесть, по диагонали — смещение, "
            + "U+O быстро жать вместе — прыжок-полёт",
            new[] { "U", "J", "O", "L" });

        return (leftCanvas, rightCanvas);
    }

    // Index order matches GestureKeyIndicator.KeyOrder(): leftUp, leftDown,
    // rightUp, rightDown. Column/row place each key into the physical 2x2
    // block it's bound to (column0 = left hand, column1 = right hand;
    // row0 = up, row1 = down).
    static readonly int[] GestureGridCol = { 0, 0, 1, 1 };
    static readonly int[] GestureGridRow = { 0, 1, 0, 1 };

    static GameObject CreateGesturePanel(GameObject player, Vector2 anchor, string legendText, string[] keyLabels)
    {
        bool leftSide = anchor.x < 0.5f;
        float sign = leftSide ? 1f : -1f;

        var canvasGo = new GameObject(player.name + "GestureCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // One-word current-action readout, bottom-most.
        const float actionY = 20f;
        const float actionHeight = 70f;
        var actionGo = new GameObject("GestureAction");
        actionGo.transform.SetParent(canvasGo.transform, false);
        Text actionText = actionGo.AddComponent<Text>();
        actionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        actionText.fontSize = 40;
        actionText.fontStyle = FontStyle.Bold;
        actionText.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        actionText.color = new Color(1f, 0.85f, 0.2f);
        actionText.text = "–";

        Outline actionOutline = actionGo.AddComponent<Outline>();
        actionOutline.effectColor = Color.black;
        actionOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform actionRt = actionText.GetComponent<RectTransform>();
        actionRt.anchorMin = anchor;
        actionRt.anchorMax = anchor;
        actionRt.pivot = anchor;
        actionRt.sizeDelta = new Vector2(340f, actionHeight);
        actionRt.anchoredPosition = new Vector2(sign * 20f, actionY);

        var actionIndicatorGo = new GameObject(player.name + "GestureActionIndicator");
        GestureActionIndicator actionIndicator = actionIndicatorGo.AddComponent<GestureActionIndicator>();
        SerializedObject actionSo = new SerializedObject(actionIndicator);
        actionSo.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        actionSo.FindProperty("actionText").objectReferenceValue = actionText;
        actionSo.ApplyModifiedPropertiesWithoutUndo();

        // Interpreted-gesture arrows (existing indicator), above the action readout.
        const float glyphY = actionY + actionHeight + 12f;
        var glyphGo = new GameObject("GestureGlyphs");
        glyphGo.transform.SetParent(canvasGo.transform, false);
        Text glyphText = glyphGo.AddComponent<Text>();
        glyphText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        glyphText.fontSize = 72;
        glyphText.fontStyle = FontStyle.Bold;
        glyphText.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        glyphText.supportRichText = true;
        glyphText.text = "–  –  –";

        Outline glyphOutline = glyphGo.AddComponent<Outline>();
        glyphOutline.effectColor = Color.black;
        glyphOutline.effectDistance = new Vector2(3f, -3f);

        RectTransform glyphRt = glyphText.GetComponent<RectTransform>();
        glyphRt.anchorMin = anchor;
        glyphRt.anchorMax = anchor;
        glyphRt.pivot = anchor;
        glyphRt.sizeDelta = new Vector2(340f, 100f);
        glyphRt.anchoredPosition = new Vector2(sign * 20f, glyphY);

        var indicatorGo = new GameObject(player.name + "GestureIndicator");
        GestureIndicator indicator = indicatorGo.AddComponent<GestureIndicator>();
        SerializedObject so = new SerializedObject(indicator);
        so.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        so.FindProperty("indicatorText").objectReferenceValue = glyphText;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Raw sensor numbers (millimetres) — between the arrows and the raw
        // key-state grid: what a real distance sensor would report, before
        // it gets thresholded into the Up/Down/braking state shown above.
        const float rawValueHeight = 56f;
        const float rawValueY = glyphY + 100f + 10f;
        var rawValueGo = new GameObject("GestureRawValues");
        rawValueGo.transform.SetParent(canvasGo.transform, false);
        Text rawValueText = rawValueGo.AddComponent<Text>();
        rawValueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rawValueText.fontSize = 40;
        rawValueText.fontStyle = FontStyle.Bold;
        rawValueText.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        rawValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        rawValueText.verticalOverflow = VerticalWrapMode.Overflow;
        rawValueText.color = new Color(0.6f, 0.85f, 1f);
        rawValueText.text = "Л:0мм  П:0мм";

        Outline rawValueOutline = rawValueGo.AddComponent<Outline>();
        rawValueOutline.effectColor = Color.black;
        rawValueOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform rawValueRt = rawValueText.GetComponent<RectTransform>();
        rawValueRt.anchorMin = anchor;
        rawValueRt.anchorMax = anchor;
        rawValueRt.pivot = anchor;
        rawValueRt.sizeDelta = new Vector2(440f, rawValueHeight);
        rawValueRt.anchoredPosition = new Vector2(sign * 20f, rawValueY);

        var rawValueIndicatorGo = new GameObject(player.name + "GestureRawValueIndicator");
        GestureRawValueIndicator rawValueIndicator = rawValueIndicatorGo.AddComponent<GestureRawValueIndicator>();
        SerializedObject rawValueSo = new SerializedObject(rawValueIndicator);
        rawValueSo.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        rawValueSo.FindProperty("valueText").objectReferenceValue = rawValueText;
        rawValueSo.ApplyModifiedPropertiesWithoutUndo();

        // Raw key-state squares — one per physical key, laid out as the
        // actual 2x2 keyboard block above the arrows (see GestureGridCol/Row).
        const float squareSize = 56f;
        const float squareStep = 66f; // size + gap
        const float gridBaseY = rawValueY + rawValueHeight + 12f;
        var squareImages = new Image[keyLabels.Length];
        for (int i = 0; i < keyLabels.Length; i++)
        {
            int col = GestureGridCol[i];
            int row = GestureGridRow[i];
            float colFromEdge = leftSide ? col : (1 - col); // mirrored so left-hand column reads left on screen either side
            float x = sign * (20f + colFromEdge * squareStep);
            float y = gridBaseY + (1 - row) * squareStep;
            squareImages[i] = CreateKeySquare(canvasGo.transform, anchor, new Vector2(x, y), squareSize, keyLabels[i]);
        }

        var keyIndicatorGo = new GameObject(player.name + "GestureKeyIndicator");
        GestureKeyIndicator keyIndicator = keyIndicatorGo.AddComponent<GestureKeyIndicator>();
        SerializedObject keySo = new SerializedObject(keyIndicator);
        keySo.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        SerializedProperty squaresProp = keySo.FindProperty("squares");
        squaresProp.arraySize = squareImages.Length;
        for (int i = 0; i < squareImages.Length; i++)
            squaresProp.GetArrayElementAtIndex(i).objectReferenceValue = squareImages[i];
        keySo.ApplyModifiedPropertiesWithoutUndo();

        // Legend text (keys + gesture descriptions), above the grid — bigger
        // than the rest of this HUD since it's meant to be read, not glanced at.
        var legendGo = new GameObject("GestureLegend");
        legendGo.transform.SetParent(canvasGo.transform, false);
        Text legend = legendGo.AddComponent<Text>();
        legend.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        legend.fontSize = 26;
        legend.fontStyle = FontStyle.Bold;
        legend.alignment = leftSide ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        legend.color = new Color(0.9f, 0.9f, 0.9f);
        legend.text = legendText;

        Outline legendOutline = legendGo.AddComponent<Outline>();
        legendOutline.effectColor = Color.black;
        legendOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform legendRt = legend.GetComponent<RectTransform>();
        legendRt.anchorMin = anchor;
        legendRt.anchorMax = anchor;
        legendRt.pivot = anchor;
        legendRt.sizeDelta = new Vector2(780f, 150f);
        float legendY = gridBaseY + 1f * squareStep + squareSize + 15f;
        legendRt.anchoredPosition = new Vector2(sign * 20f, legendY);

        return canvasGo;
    }

    // One labeled square for GestureKeyIndicator's raw key-press display.
    static Image CreateKeySquare(Transform parent, Vector2 anchor, Vector2 anchoredPosition, float size, string label)
    {
        var go = new GameObject("Key_" + label);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.75f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPosition;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return image;
    }

    // Used to be 6 color variants (random pick from badDuckPrefabs), tinted
    // at runtime via material.color — now a single real-construction-barrier
    // look (red/white hazard stripes baked into the texture itself), so no
    // tinting and just the one prefab.
    static GameObject CreateArchPrefab()
    {
        const string name = "Arch";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/SmallArch.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/SmallArch.png");
            return null;
        }

        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<DuckUnderObstacle>();

        // Same physical footprint the old primitive-built arch used — the
        // sprite is stretched to fit it rather than driven by its own
        // aspect ratio, so it still spans the lane the way a gate should.
        float spanWidth = LaneWidth - 0.3f;
        float spriteHeight = 1.6f;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localPosition = new Vector3(0f, spriteHeight / 2f, 0f);
        sprite.transform.localScale = new Vector3(spanWidth, spriteHeight, 1f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath); // safe to rerun Rebuild Scene
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        // Full-height trigger — ducking bypasses it via PlayerController's
        // DuckUnderObstacle check, not literal geometric clearance.
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(spanWidth, spriteHeight, 0.6f);
        box.center = new Vector3(0f, spriteHeight / 2f, 0f);

        AddStaticGroundShadow(root, spanWidth, 0.4f, name + "_Shadow");

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Same sprite-on-a-quad shape as CreateArchPrefab, but spans the whole
    // road (all lanes) and stands tall enough to walk under normally —
    // jumping into it is what counts as a hit, handled via TallArchObstacle
    // in PlayerController.OnTriggerEnter.
    static GameObject CreateBigArchPrefab()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/BigArchSign.png");
        if (tex == null)
        {
            Debug.LogWarning("Texture not found: Assets/Sprites/BigArchSign.png");
            return null;
        }

        var root = new GameObject("BigArch");
        root.AddComponent<MovingEntity>();
        root.AddComponent<TallArchObstacle>();

        float aspect = (float)tex.width / tex.height;
        // Wider than the road itself so the posts land out on the roadside,
        // not standing on the outer lanes — otherwise it visually reads as
        // if that lane specifically is what collides with the post, when
        // the actual pass/hit rule is purely about being airborne or not.
        // +4 puts them right where side scenery starts (SideScenerySpawner's
        // sideOffset), i.e. the actual shoulder of the road.
        float spanWidth = LaneCount * LaneWidth + 4f;
        // The image at full aspect-correct height (~8 units) put the
        // crossbar way above a player's jump peak (~2.4 units) — plenty of
        // apparent clearance, so jumping into it read as an arbitrary rule
        // rather than something the player could see coming. Squashed
        // vertically (posts still planted at road level, aspect ratio not
        // preserved) so the crossbar sits visibly closer to jump height.
        float spriteHeight = spanWidth / aspect * 0.78f;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);
        sprite.transform.localPosition = new Vector3(0f, spriteHeight / 2f, 0f);
        sprite.transform.localScale = new Vector3(spanWidth, spriteHeight, 1f);

        Renderer renderer = sprite.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { mainTexture = tex };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/BigArchSign.mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        // Full-frame trigger, same as CreateArchPrefab — pass/hit is decided
        // by the player's vertical state in PlayerController, not by literal
        // geometric clearance under the bar.
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(spanWidth, spriteHeight, 0.8f);
        box.center = new Vector3(0f, spriteHeight / 2f, 0f);

        AddStaticGroundShadow(root, spanWidth, 0.6f, "BigArch_Shadow");

        string savePath = "Assets/Prefabs/BigArch.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateBigArchSpawner()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        GameObject prefab = CreateBigArchPrefab();

        var spawnerGo = new GameObject("BigArchSpawner");
        BigArchSpawner spawner = spawnerGo.AddComponent<BigArchSpawner>();

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("prefab").objectReferenceValue = prefab;
        so.FindProperty("spawnZ").floatValue = RoadCenterZ + RoadLength / 2f - 5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateStartScreen(GameObject playerRight, GameObject playerLeft, GameObject gestureCanvasLeft, GameObject gestureCanvasRight)
    {
        var canvasGo = new GameObject("StartScreenCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the score/instructions/win canvases

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        // Energetic loop while the menu/instructions carousel is up —
        // stops the instant the game actually starts (BeginGame). Also why
        // SfxManager mutes pickup/hit one-shots until SpeedController says
        // the game is running — this music, not silence, is the intended
        // backdrop for the start screen.
        var musicGo = new GameObject("StartScreenMusic");
        musicGo.transform.SetParent(canvasGo.transform, false);
        AudioSource musicSource = musicGo.AddComponent<AudioSource>();
        musicSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/StartScreenMusic.mp3");
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0.5f;

        // Dim backdrop so the menu reads clearly over the (frozen) road.
        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(canvasGo.transform, false);
        Text title = titleGo.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 44;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(1f, 0.85f, 0.2f);
        title.text = "LADYBUG — HIT THE ROAD!";
        titleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(1400f, 70f);
        titleRt.anchoredPosition = new Vector2(0f, 490f);

        // Info carousel — cycles between rules/controls, trick diagrams and
        // gesture diagrams every few seconds, filling the gap between the
        // title and the menu. One shared-size container; each page is its
        // own child GameObject, only one active at a time (see
        // StartScreenController.UpdateCarousel) — lets some pages be plain
        // text and others be actual schematic diagrams built from UI
        // primitives instead of text. Grown taller (was 200) now that the
        // title is smaller/higher and the menu rows below are smaller/lower.
        var carouselGo = new GameObject("Carousel");
        carouselGo.transform.SetParent(canvasGo.transform, false);
        RectTransform carouselRt = carouselGo.AddComponent<RectTransform>();
        carouselRt.anchorMin = new Vector2(0.5f, 0.5f);
        carouselRt.anchorMax = new Vector2(0.5f, 0.5f);
        carouselRt.pivot = new Vector2(0.5f, 0.5f);
        // Fills essentially all the vertical space between the title and the
        // (now shorter, tighter-packed) button rows — top edge almost
        // touching the title, bottom edge almost touching the button rows —
        // so the winner tables (shown first) read as a real full page
        // instead of a small strip with empty space around it.
        carouselRt.sizeDelta = new Vector2(1200f, 720f);
        carouselRt.anchoredPosition = new Vector2(0f, 90f);

        // Background/frame so the carousel content (especially the winner
        // tables' text+photos) reads clearly against the road behind it,
        // same treatment as the button rows below — kept light (low alpha)
        // so it reads as a tint over the road, not a solid block hiding it.
        var carouselBgGo = new GameObject("CarouselBackground");
        carouselBgGo.transform.SetParent(carouselGo.transform, false);
        carouselBgGo.transform.SetAsFirstSibling(); // behind every page, not on top
        Image carouselBg = carouselBgGo.AddComponent<Image>();
        carouselBg.color = new Color(0f, 0f, 0f, 0.22f);
        Outline carouselBgOutline = carouselBgGo.AddComponent<Outline>();
        carouselBgOutline.effectColor = Color.gray;
        carouselBgOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform carouselBgRt = carouselBgGo.GetComponent<RectTransform>();
        carouselBgRt.anchorMin = Vector2.zero;
        carouselBgRt.anchorMax = Vector2.one;
        carouselBgRt.offsetMin = Vector2.zero;
        carouselBgRt.offsetMax = Vector2.zero;

        // Built separately (not inline in the list below) so the distance
        // line's Text can be grabbed afterward and wired to a live label —
        // "N км Проехат" needs to track WinSequence's actual win distance
        // (temporarily lowered for debug/test runs) instead of a hardcoded
        // number that'd lie while testing. Word order/spelling on this page
        // is deliberately mangled (meme reference, per the user) — verbatim
        // as given, not a typo to fix.
        var goalPage = CreateChecklistPage(carouselRt, "ЦЕЛЬ",
            "100 км Проехат",
            "время - уменьшат",
            "очки набират",
            "трюки выполнят",
            "скорость разгонят");
        GoalDistanceLabel goalDistanceLabel = goalPage.rowTexts[0].gameObject.AddComponent<GoalDistanceLabel>();
        SerializedObject goalDistanceSo = new SerializedObject(goalDistanceLabel);
        goalDistanceSo.FindProperty("label").objectReferenceValue = goalPage.rowTexts[0];
        goalDistanceSo.ApplyModifiedPropertiesWithoutUndo();

        var carouselPages = new System.Collections.Generic.List<GameObject>
        {
            // Leaderboards first, control/rules instructions after — see
            // each page's own photo row for the winners' pictures.
            CreateTopResultsPage(carouselRt, 0),
            CreateTopResultsPage(carouselRt, 1),
            CreateTopResultsPage(carouselRt, 2),
            CreateTopResultsPage(carouselRt, 3),

            // Word order/spelling on this page is deliberately mangled
            // (meme reference, per the user) — verbatim as given, not a
            // typo to fix.
            CreateChecklistPage(carouselRt, "СУТЬ ИГРЫ",
                "хорошее Собират",
                "плохое Избегат",
                "трюки Вдвоём делат",
                "Дорога сама разгонят").page,

            goalPage.page,

            CreateChecklistPage(carouselRt, "УПРАВЛЕНИЕ",
                "Правый: ← → полоса, ↑ прыжок, ↓ пригнуться",
                "Левый: A D полоса, W прыжок, S пригнуться").page,

            CreateTrickDiagramPage(carouselRt, "ТРЮК: АРКА", new Color(0.7f, 0.4f, 1f),
                "LadyBug1.png", new Vector2(-30f, -30f), "↓ приседает",
                "LadyBug2.png", new Vector2(30f, 30f), "↗ прыгает над ним",
                "Один игрок приседает под аркой, другой в этот момент перепрыгивает её вместе с ним — в одной полосе"),

            CreateTrickDiagramPage(carouselRt, "ТРЮК: КОЛЬЦО", new Color(0.7f, 0.4f, 1f),
                "LadyBug1.png", new Vector2(-160f, 25f), "→ по воздуху",
                "LadyBug2.png", new Vector2(160f, -25f), "← понизу",
                "Игроки одновременно меняются полосами — один в прыжке, другой понизу"),

            CreateGestureDiagramPage(carouselRt, "ПРИСЕСТЬ", false, false, "↓", "ПРИСЕСТЬ",
                "Обе руки вниз (клавиатура: J/L обе вниз)"),

            CreateGestureDiagramPage(carouselRt, "СМЕЩЕНИЕ В СТОРОНУ", false, true, "←", "СМЕЩЕНИЕ",
                "Одна рука вверх, другая вниз — полоса в сторону опущенной руки"),

            CreateGestureDiagramPage(carouselRt, "ПРЫЖОК-ПОЛЁТ", true, true, "↑", "ПРЫЖОК",
                "Обе руки вверх, часто (ритмично) — не статичная поза, а взмах"),
        };

        // Options row container — its Outline is the focus frame for row 0.
        // Lower and tighter to the other two rows than before — no bottom
        // hint text competing for space anymore (removed; the full control
        // scheme is already shown by InstructionsCanvas underneath).
        var rowGo = new GameObject("OptionsRow");
        rowGo.transform.SetParent(canvasGo.transform, false);
        Image rowBg = rowGo.AddComponent<Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.05f);
        Outline rowOutline = rowGo.AddComponent<Outline>();
        rowOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(700f, 80f);
        rowRt.anchoredPosition = new Vector2(0f, -320f);

        GameObject option1 = CreateMenuOption(rowGo.transform, "Option1", new Vector2(-180f, 0f), "[X] 1 ИГРОК", 280f, 32, 60f);
        GameObject option2 = CreateMenuOption(rowGo.transform, "Option2", new Vector2(180f, 0f), "[ ] 2 ИГРОКА", 280f, 32, 60f);

        // Controller-type row — its Outline is the focus frame for row 1.
        var controllerRowGo = new GameObject("ControllerRow");
        controllerRowGo.transform.SetParent(canvasGo.transform, false);
        Image controllerRowBg = controllerRowGo.AddComponent<Image>();
        controllerRowBg.color = new Color(1f, 1f, 1f, 0.05f);
        Outline controllerRowOutline = controllerRowGo.AddComponent<Outline>();
        controllerRowOutline.effectDistance = new Vector2(4f, -4f);
        RectTransform controllerRowRt = controllerRowGo.GetComponent<RectTransform>();
        controllerRowRt.anchorMin = new Vector2(0.5f, 0.5f);
        controllerRowRt.anchorMax = new Vector2(0.5f, 0.5f);
        controllerRowRt.pivot = new Vector2(0.5f, 0.5f);
        controllerRowRt.sizeDelta = new Vector2(900f, 80f);
        controllerRowRt.anchoredPosition = new Vector2(0f, -410f);

        // Narrower than the default 360px option box, and spaced a full box
        // width + gap apart, since three of these side by side would
        // otherwise overlap (360 wide but only 280 apart, in a prior version).
        GameObject controller1 = CreateMenuOption(controllerRowGo.transform, "Controller1", new Vector2(-300f, 0f), "[X] КЛАВИАТУРА", 260f, 26, 60f);
        GameObject controller2 = CreateMenuOption(controllerRowGo.transform, "Controller2", new Vector2(0f, 0f), "[ ] ДАТЧИКИ", 260f, 26, 60f);
        GameObject controller3 = CreateMenuOption(controllerRowGo.transform, "Controller3", new Vector2(300f, 0f), "[ ] ИМИТАТОР", 260f, 26, 60f);

        GameObject startBtn = CreateMenuOption(canvasGo.transform, "StartButton", new Vector2(0f, -500f), "СТАРТ", 300f, 32, 60f);
        Outline startOutline = startBtn.GetComponent<Outline>();

        // Shown only if Start is pressed while "Датчики расстояния" is selected.
        var notImplementedGo = new GameObject("NotImplemented");
        notImplementedGo.transform.SetParent(canvasGo.transform, false);
        Text notImplemented = notImplementedGo.AddComponent<Text>();
        notImplemented.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        notImplemented.fontSize = 24;
        notImplemented.fontStyle = FontStyle.Bold;
        notImplemented.alignment = TextAnchor.MiddleCenter;
        notImplemented.color = new Color(1f, 0.4f, 0.3f);
        notImplementedGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform notImplementedRt = notImplemented.GetComponent<RectTransform>();
        notImplementedRt.anchorMin = new Vector2(0.5f, 0.5f);
        notImplementedRt.anchorMax = new Vector2(0.5f, 0.5f);
        notImplementedRt.pivot = new Vector2(0.5f, 0.5f);
        notImplementedRt.sizeDelta = new Vector2(900f, 50f);
        notImplementedRt.anchoredPosition = new Vector2(0f, -460f);
        notImplementedGo.SetActive(false);

        // Bottom-left free space left behind once the per-player gesture
        // HUDs (КЛАВИШИ/ЖЕСТЫ panels) are hidden for the menu — a short,
        // input-agnostic reminder of how to actually drive THIS menu
        // (works from keyboard, the keyboard gesture simulator, or real
        // sensors alike, see StartScreenController.Update).
        var menuHelpGo = new GameObject("MenuHelpText");
        menuHelpGo.transform.SetParent(canvasGo.transform, false);
        Text menuHelp = menuHelpGo.AddComponent<Text>();
        menuHelp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        menuHelp.fontSize = 24;
        menuHelp.fontStyle = FontStyle.Bold;
        menuHelp.alignment = TextAnchor.LowerLeft;
        menuHelp.color = new Color(0.9f, 0.9f, 0.9f);
        menuHelp.text = "ВЫБОР: ← → / ↑ ↓\n"
            + "НАЧАЛО: выбрать СТАРТ и взмахнуть руками\n"
            + "(клавиши, имитатор жестов или датчики — любое)";
        menuHelpGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform menuHelpRt = menuHelp.GetComponent<RectTransform>();
        menuHelpRt.anchorMin = new Vector2(0f, 0f);
        menuHelpRt.anchorMax = new Vector2(0f, 0f);
        menuHelpRt.pivot = new Vector2(0f, 0f);
        menuHelpRt.sizeDelta = new Vector2(700f, 130f);
        menuHelpRt.anchoredPosition = new Vector2(30f, 30f);

        // Only the first page starts visible — StartScreenController swaps
        // active pages at runtime (see UpdateCarousel).
        for (int i = 1; i < carouselPages.Count; i++)
            carouselPages[i].SetActive(false);

        var controller = canvasGo.AddComponent<StartScreenController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
        SetPrefabArray(so, "carouselPages", carouselPages);
        so.FindProperty("carouselBackground").objectReferenceValue = carouselBgGo;
        so.FindProperty("option1Bg").objectReferenceValue = option1.GetComponent<Image>();
        so.FindProperty("option2Bg").objectReferenceValue = option2.GetComponent<Image>();
        so.FindProperty("option1Text").objectReferenceValue = option1.GetComponentInChildren<Text>();
        so.FindProperty("option2Text").objectReferenceValue = option2.GetComponentInChildren<Text>();
        so.FindProperty("optionsRowOutline").objectReferenceValue = rowOutline;
        so.FindProperty("controller1Bg").objectReferenceValue = controller1.GetComponent<Image>();
        so.FindProperty("controller2Bg").objectReferenceValue = controller2.GetComponent<Image>();
        so.FindProperty("controller3Bg").objectReferenceValue = controller3.GetComponent<Image>();
        so.FindProperty("controller1Text").objectReferenceValue = controller1.GetComponentInChildren<Text>();
        so.FindProperty("controller2Text").objectReferenceValue = controller2.GetComponentInChildren<Text>();
        so.FindProperty("controller3Text").objectReferenceValue = controller3.GetComponentInChildren<Text>();
        so.FindProperty("controllerRowOutline").objectReferenceValue = controllerRowOutline;
        so.FindProperty("notImplementedText").objectReferenceValue = notImplemented;
        so.FindProperty("startBg").objectReferenceValue = startBtn.GetComponent<Image>();
        so.FindProperty("startOutline").objectReferenceValue = startOutline;
        so.FindProperty("playerRight").objectReferenceValue = playerRight;
        so.FindProperty("playerLeft").objectReferenceValue = playerLeft;
        so.FindProperty("gestureRight").objectReferenceValue = playerRight.GetComponent<GestureInput>();
        so.FindProperty("gestureLeft").objectReferenceValue = playerLeft.GetComponent<GestureInput>();
        so.FindProperty("gestureCanvasRight").objectReferenceValue = gestureCanvasRight;
        so.FindProperty("gestureCanvasLeft").objectReferenceValue = gestureCanvasLeft;
        so.FindProperty("musicSource").objectReferenceValue = musicSource;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Shared visual style for the start-screen's option boxes and Start button.
    static GameObject CreateMenuOption(Transform parent, string name, Vector2 anchoredPos, string label, float width = 360f, int fontSize = 38, float height = 100f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.gray;
        outline.effectDistance = new Vector2(3f, -3f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return go;
    }

    // A page container that fills its carousel parent exactly — children
    // position themselves within it via their own anchored rects.
    static GameObject CreateFillPage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static void CreatePageTitle(Transform parent, string text, Color color)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(parent, false);
        Text title = go.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 40;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = color;
        title.text = text;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = title.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000f, 50f);
        rt.anchoredPosition = new Vector2(0f, 260f);
    }

    static void CreatePageCaption(Transform parent, string text)
    {
        var go = new GameObject("Caption");
        go.transform.SetParent(parent, false);
        Text caption = go.AddComponent<Text>();
        caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption.fontSize = 26;
        caption.fontStyle = FontStyle.Bold;
        caption.alignment = TextAnchor.MiddleCenter;
        caption.color = new Color(0.85f, 0.85f, 0.85f);
        caption.text = text;
        go.AddComponent<Outline>().effectColor = Color.black;
        RectTransform rt = caption.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1100f, 60f);
        rt.anchoredPosition = new Vector2(0f, -280f);
    }

    // An instructional carousel page: title up top (CreatePageTitle), then
    // each point as its own checklist row — a checked checkbox icon on the
    // left, the point's text on the right — instead of one plain block of
    // centered text. Rows are spread evenly between the title and the
    // bottom of the page regardless of how many points there are. Also
    // returns each row's Text component so a caller can swap one out for a
    // runtime-driven label (see the ЦЕЛЬ page's distance line) without
    // string-matching GameObject names.
    static (GameObject page, Text[] rowTexts) CreateChecklistPage(Transform parent, string title, params string[] lines)
    {
        GameObject page = CreateFillPage(parent, "Page_Checklist_" + title);

        CreatePageTitle(page.transform, title, new Color(1f, 0.85f, 0.2f));

        const float topY = 140f;
        const float bottomY = -300f;
        int n = lines.Length;
        var rowTexts = new Text[n];
        for (int i = 0; i < n; i++)
        {
            float t = n <= 1 ? 0.5f : (float)i / (n - 1);
            float y = Mathf.Lerp(topY, bottomY, t);
            rowTexts[i] = CreateChecklistRow(page.transform, y, lines[i]);
        }

        return (page, rowTexts);
    }

    // One checklist row: a small green "checked" box with a checkmark glyph
    // (left), the point's text (right, left-aligned so lines of different
    // length still start at the same spot). Returns the line's Text
    // component.
    static Text CreateChecklistRow(Transform parent, float y, string text)
    {
        const float checkSize = 60f;
        const float leftPadding = 50f;
        const float textGap = 30f;

        var checkGo = new GameObject("Check");
        checkGo.transform.SetParent(parent, false);
        Image checkImg = checkGo.AddComponent<Image>();
        checkImg.color = new Color(0.15f, 0.55f, 0.2f, 0.95f);
        Outline checkOutline = checkGo.AddComponent<Outline>();
        checkOutline.effectColor = Color.white;
        checkOutline.effectDistance = new Vector2(2f, -2f);
        RectTransform checkRt = checkImg.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0f, 0.5f);
        checkRt.anchorMax = new Vector2(0f, 0.5f);
        checkRt.pivot = new Vector2(0f, 0.5f);
        checkRt.sizeDelta = new Vector2(checkSize, checkSize);
        checkRt.anchoredPosition = new Vector2(leftPadding, y);

        var markGo = new GameObject("Mark");
        markGo.transform.SetParent(checkGo.transform, false);
        Text mark = markGo.AddComponent<Text>();
        mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mark.fontSize = 42;
        mark.fontStyle = FontStyle.Bold;
        mark.alignment = TextAnchor.MiddleCenter;
        mark.color = Color.white;
        mark.text = "✓"; // ✓
        RectTransform markRt = mark.GetComponent<RectTransform>();
        markRt.anchorMin = Vector2.zero;
        markRt.anchorMax = Vector2.one;
        markRt.offsetMin = Vector2.zero;
        markRt.offsetMax = Vector2.zero;

        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(parent, false);
        Text line = lineGo.AddComponent<Text>();
        line.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        line.fontSize = 34;
        line.fontStyle = FontStyle.Bold;
        line.alignment = TextAnchor.MiddleLeft;
        line.color = Color.white;
        line.text = text;
        lineGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform lineRt = line.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 0.5f);
        lineRt.anchorMax = new Vector2(0f, 0.5f);
        lineRt.pivot = new Vector2(0f, 0.5f);
        lineRt.sizeDelta = new Vector2(1000f, 70f);
        lineRt.anchoredPosition = new Vector2(leftPadding + checkSize + textGap, y);

        return line;
    }

    // One "distance sensor" glyph: a vertical laser line with a flat
    // rectangle marking where the palm sits — high on the line if the hand
    // reads "up", low if it reads "down", matching the real sensor's binary
    // near/far reading (see GestureInput.HandStateForDistance).
    static void CreateSensorGlyph(Transform parent, Vector2 anchoredPos, bool handUp)
    {
        var laserGo = new GameObject("Laser");
        laserGo.transform.SetParent(parent, false);
        Image laser = laserGo.AddComponent<Image>();
        laser.color = new Color(1f, 0.2f, 0.15f, 0.9f);
        RectTransform laserRt = laserGo.GetComponent<RectTransform>();
        laserRt.anchorMin = new Vector2(0.5f, 0.5f);
        laserRt.anchorMax = new Vector2(0.5f, 0.5f);
        laserRt.pivot = new Vector2(0.5f, 0.5f);
        laserRt.sizeDelta = new Vector2(5f, 110f);
        laserRt.anchoredPosition = anchoredPos;

        var palmGo = new GameObject("Palm");
        palmGo.transform.SetParent(parent, false);
        Image palm = palmGo.AddComponent<Image>();
        palm.color = Color.white;
        RectTransform palmRt = palmGo.GetComponent<RectTransform>();
        palmRt.anchorMin = new Vector2(0.5f, 0.5f);
        palmRt.anchorMax = new Vector2(0.5f, 0.5f);
        palmRt.pivot = new Vector2(0.5f, 0.5f);
        palmRt.sizeDelta = new Vector2(70f, 16f);
        palmRt.anchoredPosition = anchoredPos + new Vector2(0f, handUp ? 40f : -40f);
    }

    // One gesture, explained schematically: title up top, a little
    // 2-sensor diagram (laser + palm height per hand) with a big arrow and
    // the resulting action word beside it, one-line caption at the bottom.
    static GameObject CreateGestureDiagramPage(Transform parent, string title, bool leftHandUp, bool rightHandUp,
        string arrow, string actionLabel, string caption)
    {
        GameObject page = CreateFillPage(parent, "Page_Gesture_" + title);

        CreatePageTitle(page.transform, title, new Color(1f, 0.85f, 0.2f));

        CreateSensorGlyph(page.transform, new Vector2(-500f, 0f), leftHandUp);
        CreateSensorGlyph(page.transform, new Vector2(-380f, 0f), rightHandUp);

        var arrowGo = new GameObject("Arrow");
        arrowGo.transform.SetParent(page.transform, false);
        Text arrowText = arrowGo.AddComponent<Text>();
        arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        arrowText.fontSize = 84;
        arrowText.fontStyle = FontStyle.Bold;
        arrowText.alignment = TextAnchor.MiddleCenter;
        arrowText.color = new Color(1f, 0.85f, 0.2f);
        arrowText.text = arrow;
        RectTransform arrowRt = arrowText.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRt.pivot = new Vector2(0.5f, 0.5f);
        arrowRt.sizeDelta = new Vector2(160f, 110f);
        arrowRt.anchoredPosition = new Vector2(-60f, 0f);

        var labelGo = new GameObject("ActionLabel");
        labelGo.transform.SetParent(page.transform, false);
        Text label = labelGo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 32;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = actionLabel;
        labelGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(400f, 60f);
        labelRt.anchoredPosition = new Vector2(370f, 0f);

        CreatePageCaption(page.transform, caption);

        return page;
    }

    // One co-op trick, explained schematically: title, two player-colored
    // squares (numbered 1/2, matching PlayerRight=1/white, PlayerLeft=2/
    // light-blue) positioned/labelled per the trick's rule, caption below.
    static GameObject CreateTrickDiagramPage(Transform parent, string title, Color titleColor,
        string p1Sprite, Vector2 p1Pos, string p1Label,
        string p2Sprite, Vector2 p2Pos, string p2Label,
        string caption)
    {
        GameObject page = CreateFillPage(parent, "Page_Trick_" + title);

        CreatePageTitle(page.transform, title, titleColor);

        CreateTrickPlayerIcon(page.transform, p1Sprite, p1Pos, p1Label);
        CreateTrickPlayerIcon(page.transform, p2Sprite, p2Pos, p2Label);

        CreatePageCaption(page.transform, caption);

        return page;
    }

    // spriteFile: LadyBug1.png (player 1/right, white) or LadyBug2.png
    // (player 2/left, light blue) — the same textures the players actually
    // wear in-game, not a stand-in colored square.
    static void CreateTrickPlayerIcon(Transform parent, string spriteFile, Vector2 pos, string actionLabel)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);

        var iconGo = new GameObject("Player_" + spriteFile);
        iconGo.transform.SetParent(parent, false);
        RawImage icon = iconGo.AddComponent<RawImage>();
        icon.texture = tex;
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        float aspect = tex != null ? (float)tex.width / tex.height : 1f;
        const float iconHeight = 64f;
        iconRt.sizeDelta = new Vector2(iconHeight * aspect, iconHeight);
        iconRt.anchoredPosition = pos;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(parent, false);
        Text labelText = labelGo.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 20;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = pos.x < 0f ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.text = actionLabel;
        labelGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform labelRt = labelText.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(260f, 30f);
        // Sit just outside the square, on the side away from center so it
        // doesn't collide with the other player's icon in the middle.
        float xOffset = pos.x < 0f ? -160f : 160f;
        labelRt.anchoredPosition = pos + new Vector2(xOffset, 0f);
    }

    // Top-3 for one leaderboard category, with the #1 entry's photo (if any
    // was ever attached) — populated at runtime by TopResultsPage, since
    // the leaderboard itself only exists in PlayerPrefs, not at build time.
    // Same order as HighScoreManager's private Category enum/CategoryNames —
    // duplicated here only for the placeholder text below (SceneSetup can't
    // reach into that runtime-only private array at edit time).
    static readonly string[] TopCategoryNames = { "ВРЕМЯ", "ОЧКИ", "ТРЮКИ", "СКОРОСТЬ" };

    static GameObject CreateTopResultsPage(Transform parent, int category)
    {
        GameObject page = CreateFillPage(parent, "Page_TopResults_" + category);

        // Placeholder text set immediately (rather than leaving these blank
        // until TopResultsPage.Refresh runs) so the carousel background
        // never shows as an empty box for even a frame before real values
        // land — Refresh overwrites this the moment HighScoreManager has
        // real data.
        string categoryName = category >= 0 && category < TopCategoryNames.Length ? TopCategoryNames[category] : "?";

        // Three columns: title (left, vertically centered), rank+result
        // (middle, split into its own 2 sub-columns), photo (right, sized
        // to touch the carousel background's top and bottom edges) — not
        // all three crowded near the center.
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(page.transform, false);
        Text titleText = titleGo.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 40;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.85f, 0.2f);
        titleText.text = "ТОП-3: " + categoryName; // matches TopResultsPage.Refresh's format exactly, no visible jump once real data lands
        titleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform titleRt = titleText.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(380f, 200f);
        titleRt.anchoredPosition = new Vector2(-390f, 0f);

        // One row per rank — each with its own photo (if that slot ever had
        // one attached), not a single photo for #1 shown off to the side.
        // Photos are big enough that the top one's top edge and the bottom
        // one's bottom edge land exactly on the carousel background's own
        // edges (box half-height 360, photo half-size 115, 15px gaps).
        var rowRankTexts = new Text[3];
        var rowValueTexts = new Text[3];
        var rowPhotos = new RawImage[3];
        float[] rowY = { 245f, 0f, -245f };
        const float photoSize = 230f;
        const float photoPadding = 40f;

        for (int i = 0; i < 3; i++)
        {
            var rowRankGo = new GameObject("RowRank" + i);
            rowRankGo.transform.SetParent(page.transform, false);
            Text rowRank = rowRankGo.AddComponent<Text>();
            rowRank.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rowRank.fontSize = 44;
            rowRank.fontStyle = FontStyle.Bold;
            rowRank.alignment = TextAnchor.MiddleCenter;
            rowRank.color = Color.white;
            rowRank.text = (i + 1) + ".";
            rowRankGo.AddComponent<Outline>().effectColor = Color.black;
            RectTransform rowRankRt = rowRank.GetComponent<RectTransform>();
            rowRankRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRankRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRankRt.pivot = new Vector2(0.5f, 0.5f);
            rowRankRt.sizeDelta = new Vector2(140f, 80f);
            rowRankRt.anchoredPosition = new Vector2(-90f, rowY[i]);
            rowRankTexts[i] = rowRank;

            var rowValueGo = new GameObject("RowValue" + i);
            rowValueGo.transform.SetParent(page.transform, false);
            Text rowValue = rowValueGo.AddComponent<Text>();
            rowValue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rowValue.fontSize = 44;
            rowValue.fontStyle = FontStyle.Bold;
            rowValue.alignment = TextAnchor.MiddleLeft;
            rowValue.color = Color.white;
            rowValue.text = "--";
            rowValueGo.AddComponent<Outline>().effectColor = Color.black;
            RectTransform rowValueRt = rowValue.GetComponent<RectTransform>();
            rowValueRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowValueRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowValueRt.pivot = new Vector2(0.5f, 0.5f);
            rowValueRt.sizeDelta = new Vector2(200f, 80f);
            rowValueRt.anchoredPosition = new Vector2(70f, rowY[i]);
            rowValueTexts[i] = rowValue;

            var rowPhotoGo = new GameObject("RowPhoto" + i);
            rowPhotoGo.transform.SetParent(page.transform, false);
            RawImage rowPhoto = rowPhotoGo.AddComponent<RawImage>();
            rowPhoto.color = Color.white;
            RectTransform rowPhotoRt = rowPhoto.GetComponent<RectTransform>();
            rowPhotoRt.anchorMin = new Vector2(1f, 0.5f);
            rowPhotoRt.anchorMax = new Vector2(1f, 0.5f);
            rowPhotoRt.pivot = new Vector2(1f, 0.5f);
            rowPhotoRt.sizeDelta = new Vector2(photoSize, photoSize);
            rowPhotoRt.anchoredPosition = new Vector2(-photoPadding, rowY[i]);
            rowPhotoGo.SetActive(false);
            rowPhotos[i] = rowPhoto;
        }

        TopResultsPage pageComp = page.AddComponent<TopResultsPage>();
        SerializedObject so = new SerializedObject(pageComp);
        so.FindProperty("category").intValue = category;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        SerializedProperty rowRankTextsProp = so.FindProperty("rowRankTexts");
        rowRankTextsProp.arraySize = rowRankTexts.Length;
        for (int i = 0; i < rowRankTexts.Length; i++)
            rowRankTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowRankTexts[i];
        SerializedProperty rowValueTextsProp = so.FindProperty("rowValueTexts");
        rowValueTextsProp.arraySize = rowValueTexts.Length;
        for (int i = 0; i < rowValueTexts.Length; i++)
            rowValueTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowValueTexts[i];
        SerializedProperty rowPhotosProp = so.FindProperty("rowPhotos");
        rowPhotosProp.arraySize = rowPhotos.Length;
        for (int i = 0; i < rowPhotos.Length; i++)
            rowPhotosProp.GetArrayElementAtIndex(i).objectReferenceValue = rowPhotos[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        return page;
    }

    static void CreatePauseDialog()
    {
        var canvasGo = new GameObject("PauseCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // above the start screen too, just in case

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        var questionGo = new GameObject("Question");
        questionGo.transform.SetParent(canvasGo.transform, false);
        Text question = questionGo.AddComponent<Text>();
        question.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        question.fontSize = 54;
        question.fontStyle = FontStyle.Bold;
        question.alignment = TextAnchor.MiddleCenter;
        question.color = Color.white;
        question.text = "Закончить игру?";
        questionGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform questionRt = question.GetComponent<RectTransform>();
        questionRt.anchorMin = new Vector2(0.5f, 0.5f);
        questionRt.anchorMax = new Vector2(0.5f, 0.5f);
        questionRt.pivot = new Vector2(0.5f, 0.5f);
        questionRt.sizeDelta = new Vector2(1000f, 120f);
        questionRt.anchoredPosition = new Vector2(0f, 80f);

        Text yesText = CreateDialogOptionText(canvasGo.transform, "Yes", new Vector2(-160f, -40f), "ДА");
        Text noText = CreateDialogOptionText(canvasGo.transform, "No", new Vector2(160f, -40f), "НЕТ");

        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(canvasGo.transform, false);
        Text hint = hintGo.AddComponent<Text>();
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 24;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.85f, 0.85f, 0.85f);
        hint.text = "←→ выбор, Space — подтвердить";
        RectTransform hintRt = hint.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0.5f);
        hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintRt.pivot = new Vector2(0.5f, 0.5f);
        hintRt.sizeDelta = new Vector2(800f, 60f);
        hintRt.anchoredPosition = new Vector2(0f, -140f);

        canvasGo.SetActive(false);

        var controllerGo = new GameObject("PauseController");
        PauseController pause = controllerGo.AddComponent<PauseController>();
        SerializedObject so = new SerializedObject(pause);
        so.FindProperty("dialogRoot").objectReferenceValue = canvasGo;
        so.FindProperty("yesText").objectReferenceValue = yesText;
        so.FindProperty("noText").objectReferenceValue = noText;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Both hands down (or Down key, for the keyboard scheme) held on every
    // active player at once, for 10s total, opens the quit-confirm dialog —
    // see DuckToExitController for the 5s-silent / 5s-countdown timing.
    static void CreateExitGesture()
    {
        var canvasGo = new GameObject("ExitGestureCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140; // above gameplay HUD, below the help screen (150) / pause dialog (200)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("CountdownText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.3f, 0.2f);

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 140f);
        rt.anchoredPosition = new Vector2(0f, 250f);

        textGo.SetActive(false);

        var controllerGo = new GameObject("DuckToExitController");
        DuckToExitController controller = controllerGo.AddComponent<DuckToExitController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("countdownText").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Flowers used for the intro rain — reused art, not new assets, just
    // for visual variety ("разных цветов" — different colors/flowers).
    static readonly string[] IntroFlowerSprites =
    {
        "Flower.png", "FlowerPink.png", "FlowerYellow.png", "FlowerLavender.png",
        "DaisyWhite.png", "DaisyPink.png", "SunflowerYellow.png",
        "LotusYellow.png", "LotusBlue.png", "LotusPink.png",
    };

    // Very first thing the player sees: flowers rain from the top and pile
    // up until the whole screen is covered, then the canvas hides itself,
    // revealing the start menu that's been sitting ready underneath.
    // Highest sorting order of any canvas — this has to cover absolutely
    // everything, since it's the very first frame of the game.
    static void CreateIntroScreen()
    {
        var canvasGo = new GameObject("IntroCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Solid backdrop so gaps between not-yet-landed flowers show a
        // plain color instead of the 3D scene bleeding through underneath.
        var backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(canvasGo.transform, false);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0.55f, 0.78f, 0.95f);
        RectTransform backdropRt = backdropGo.GetComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        // Grid tiled a little past the reference screen's edges so full
        // coverage doesn't depend on exact rounding.
        const float cellSize = 140f;
        int columns = Mathf.CeilToInt(1920f / cellSize) + 1;
        int rows = Mathf.CeilToInt(1080f / cellSize) + 1;
        float gridWidth = columns * cellSize;
        float gridHeight = rows * cellSize;
        float startX = -gridWidth / 2f + cellSize / 2f;
        float startY = -gridHeight / 2f + cellSize / 2f;

        var rng = new System.Random();
        // Built row by row, bottom to top, columns shuffled within each row —
        // IntroSequence plays them back in this exact order, so the pile
        // visibly rises from the bottom until it reaches the top.
        var orderedFlowers = new System.Collections.Generic.List<RectTransform>();
        for (int row = 0; row < rows; row++)
        {
            var colOrder = new System.Collections.Generic.List<int>();
            for (int col = 0; col < columns; col++)
                colOrder.Add(col);
            for (int i = colOrder.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = colOrder[i];
                colOrder[i] = colOrder[j];
                colOrder[j] = tmp;
            }

            foreach (int col in colOrder)
            {
                string spriteFile = IntroFlowerSprites[rng.Next(IntroFlowerSprites.Length)];
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/" + spriteFile);

                var flowerGo = new GameObject("Flower_" + row + "_" + col);
                flowerGo.transform.SetParent(canvasGo.transform, false);
                RawImage img = flowerGo.AddComponent<RawImage>();
                img.texture = tex;
                RectTransform rt = img.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                const float sizeShrink = 0.9f; // slight gap so flowers read as individual pieces, not one solid mass
                rt.sizeDelta = new Vector2(cellSize * sizeShrink, cellSize * sizeShrink);
                rt.anchoredPosition = new Vector2(startX + col * cellSize, startY + row * cellSize);
                flowerGo.SetActive(false);

                orderedFlowers.Add(rt);
            }
        }

        var introGo = new GameObject("IntroSequence");
        IntroSequence intro = introGo.AddComponent<IntroSequence>();
        SerializedObject introSo = new SerializedObject(intro);
        introSo.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
        SerializedProperty flowersProp = introSo.FindProperty("flowers");
        flowersProp.arraySize = orderedFlowers.Count;
        for (int i = 0; i < orderedFlowers.Count; i++)
            flowersProp.GetArrayElementAtIndex(i).objectReferenceValue = orderedFlowers[i];
        introSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static Text CreateDialogOptionText(Transform parent, string name, Vector2 anchoredPos, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 46;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        go.AddComponent<Outline>().effectColor = Color.black;

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 80f);
        rt.anchoredPosition = anchoredPos;

        return text;
    }

    // Tileable asphalt grain — per-pixel brightness jitter around the base
    // road-gray so the surface reads as slightly uneven instead of a flat
    // block of color. Fixed seed so re-running Rebuild Scene is deterministic.
    static Texture2D CreateRoadTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color baseColor = new Color(0.25f, 0.25f, 0.25f);
        var rng = new System.Random(12345);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.12f;
                Color c = new Color(
                    Mathf.Clamp01(baseColor.r + jitter),
                    Mathf.Clamp01(baseColor.g + jitter),
                    Mathf.Clamp01(baseColor.b + jitter));
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    static Texture2D CreateDashTexture()
    {
        // 1px wide, 8px tall: first half white (dash), second half road-gray (gap).
        var tex = new Texture2D(1, 8, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        Color gap = new Color(0.25f, 0.25f, 0.25f);
        for (int y = 0; y < 8; y++)
            tex.SetPixel(0, y, y < 5 ? Color.white : gap);

        tex.Apply();
        return tex;
    }

    static void ApplyColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material material = new Material(shader) { color = color };
        renderer.sharedMaterial = material;
    }

    // Flat, static shadow baked into a prefab (unlike CreatePlayerShadow,
    // this never moves relative to its parent — fine since obstacle height
    // never changes at runtime). Local Y cancels out root's own height
    // offset so it always lands right at road level regardless of how each
    // builder positions its root (some use height/2, some leave it at 0).
    static void AddStaticGroundShadow(GameObject root, float width, float depth, string materialName)
    {
        const float groundY = 0.02f;

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "Shadow";
        Object.DestroyImmediate(shadow.GetComponent<Collider>());
        shadow.transform.SetParent(root.transform);
        shadow.transform.localScale = new Vector3(width, 0.01f, depth);
        shadow.transform.localPosition = new Vector3(0f, -root.transform.position.y + groundY, 0f);

        Renderer renderer = shadow.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { color = new Color(0f, 0f, 0f, 0.35f) };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + materialName + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;
    }
}
