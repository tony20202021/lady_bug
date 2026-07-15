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

    [MenuItem("Tools/Rebuild Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateLight();
        CreateSpeedController();

        // Two players sharing one road/score/speed — co-op, not split-screen.
        // Right player: arrows for lane/jump/duck, I/K for accel/brake (IJKL layout).
        // Left player: WASD for lane/jump/duck, Shift/Ctrl for accel/brake.
        // Accel/brake are independent per player and summed in SpeedController.
        // Each also gets a gesture-sensor simulator (disabled by default), one
        // 3-key column per hand (top/middle/bottom = near/mid/far reading):
        // left player's left hand Q/A/Z, right hand W/S/X;
        // right player's left hand O/L/., right hand P/;//  — mirrors their seating.
        GameObject playerRight = CreatePlayer("PlayerRight", KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.I, KeyCode.K,
            KeyCode.O, KeyCode.L, KeyCode.Period, KeyCode.P, KeyCode.Semicolon, KeyCode.Slash, LaneCount - 1, Color.white);
        GameObject playerLeft = CreatePlayer("PlayerLeft", KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.LeftShift, KeyCode.LeftControl,
            KeyCode.Q, KeyCode.A, KeyCode.Z, KeyCode.W, KeyCode.S, KeyCode.X, 0, new Color(0.55f, 0.75f, 1f));

        CreateCamera(playerRight.transform);
        CreateRoad();
        CreateSpawner();
        CreateSideScenery();
        CreateInstructionsUI();
        CreateHelpScreen();
        RectTransform scorePanel = CreateScoreUI(out Canvas scoreCanvas);
        CreateWinSequence(scorePanel, scoreCanvas);
        CreateTricksUI();
        CreateGestureIndicators(playerRight, playerLeft);
        CreateStartScreen(playerRight, playerLeft);
        CreatePauseDialog();

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
        so.FindProperty("maxSpeed").floatValue = 20f;
        so.FindProperty("accelerationRate").floatValue = 8f;
        so.FindProperty("brakeRate").floatValue = 15f;
        so.FindProperty("dragRate").floatValue = 1.5f;
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

    static GameObject CreatePlayer(string name, KeyCode left, KeyCode right, KeyCode up, KeyCode down, KeyCode accel, KeyCode brake,
        KeyCode leftHandUp, KeyCode leftHandMiddle, KeyCode leftHandDown,
        KeyCode rightHandUp, KeyCode rightHandMiddle, KeyCode rightHandDown, int startLane, Color tint)
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
        playerSo.FindProperty("accelKey").intValue = (int)accel;
        playerSo.FindProperty("brakeKey").intValue = (int)brake;
        playerSo.ApplyModifiedPropertiesWithoutUndo();

        // Gesture (distance-sensor) input — keyboard-simulated for now, since
        // no hardware is connected. Disabled by default; the start screen
        // enables it only if the sensor simulator is the chosen controller.
        GestureInput gesture = player.AddComponent<GestureInput>();
        SerializedObject gestureSo = new SerializedObject(gesture);
        gestureSo.FindProperty("leftHandUpKey").intValue = (int)leftHandUp;
        gestureSo.FindProperty("leftHandMiddleKey").intValue = (int)leftHandMiddle;
        gestureSo.FindProperty("leftHandDownKey").intValue = (int)leftHandDown;
        gestureSo.FindProperty("rightHandUpKey").intValue = (int)rightHandUp;
        gestureSo.FindProperty("rightHandMiddleKey").intValue = (int)rightHandMiddle;
        gestureSo.FindProperty("rightHandDownKey").intValue = (int)rightHandDown;
        gestureSo.ApplyModifiedPropertiesWithoutUndo();
        gesture.enabled = false;

        // Trigger detection against the spawned entities' colliders needs a
        // Rigidbody on at least one side of the pair; the player moves via
        // transform, not physics, so it stays kinematic with gravity off.
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        CreatePlayerSprite(player.transform, tint);
        CreatePlayerShadow(player.transform);

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

    static void CreatePlayerSprite(Transform parent, Color tint)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/LadyBug.png");
        if (tex == null)
        {
            Debug.LogWarning("LadyBug sprite not found at Assets/Sprites/LadyBug.png");
            return;
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
        ApplyColor(surface, new Color(0.25f, 0.25f, 0.25f));

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
        ("Cactus", "Cactus.png", 1.2f, -1),
        ("Flower", "Flower.png", 0.8f, 1),
        ("Heart", "Heart.png", 1.2f, 1),
        ("Cherry", "Cherry.png", 1.2f, 1),
        ("Mosquito", "Mosquito.png", 0.8f, -1),
        ("Shield", "Shield.png", 0.9f, 1),
        ("FlowerPink", "FlowerPink.png", 0.8f, 1),
        ("FlowerYellow", "FlowerYellow.png", 0.8f, 1),
        ("DaisyWhite", "DaisyWhite.png", 0.8f, 1),
        ("DaisyPink", "DaisyPink.png", 0.8f, 1),
        ("SunflowerYellow", "SunflowerYellow.png", 0.8f, 1),
        ("LotusYellow", "LotusYellow.png", 0.8f, 1),
        ("LotusBlue", "LotusBlue.png", 0.8f, 1),
        ("LotusPink", "LotusPink.png", 0.8f, 1),
        ("Star", "Star.png", 1.1f, 1),
        ("TrafficCone", "TrafficCone.png", 1.1f, -1),
        ("Wheel", "Wheel.png", 1f, -1),
        ("Bicycle", "Bicycle.png", 1.2f, -1),
        ("Motorbike", "Motorbike.png", 1.3f, -1),
        ("Motorcycle", "Motorcycle.png", 1.3f, -1),
        ("Dog", "Dog.png", 1f, -1),
        ("Cat", "Cat.png", 1f, -1),
        ("RabbitSign", "RabbitSign.png", 1f, -1),
        ("Crow", "Crow.png", 0.9f, -1),
    };

    // (name, texture file, roadside height)
    static readonly (string, string, float)[] SceneryObjects =
    {
        ("PalmTree", "PalmTree.png", 3.5f),
        ("BigCactus", "BigCactus.png", 2f),
        ("PineForest", "PineForest.png", 4f),
        ("Mountain", "Mountain.png", 5f),
        ("GreenHill", "GreenHill.png", 3.5f),
        ("CactusFlowerOrange", "CactusFlowerOrange.png", 1.3f),
    };

    // Things we couldn't find a decent freely-licensed image for — built from
    // primitives instead. (name, color, piece shape, piece count, piece size)
    static readonly (string, Color, PrimitiveType, int, float)[] ProceduralPiles =
    {
        ("SandPile", new Color(0.87f, 0.75f, 0.45f), PrimitiveType.Sphere, 1, 1.4f),
        ("BrickPile", new Color(0.55f, 0.22f, 0.15f), PrimitiveType.Cube, 6, 0.4f),
        ("WoodPile", new Color(0.45f, 0.3f, 0.15f), PrimitiveType.Cylinder, 5, 0.35f),
        ("RockPile", new Color(0.5f, 0.5f, 0.5f), PrimitiveType.Sphere, 5, 0.45f),
    };

    static readonly (string, Color)[] ProceduralPuddles =
    {
        ("PuddleBlue", new Color(0.2f, 0.5f, 0.9f)),
        ("PuddleGreen", new Color(0.3f, 0.7f, 0.3f)),
        ("PuddleBrown", new Color(0.4f, 0.3f, 0.15f)),
        ("PuddlePurple", new Color(0.6f, 0.3f, 0.8f)),
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
            GameObject prefab = CreateEntityPrefab(name, "Assets/Sprites/" + file, height, "Assets/Prefabs/" + name + ".prefab", score);
            if (prefab == null)
                continue;
            (score > 0 ? goodPrefabs : badJumpPrefabs).Add(prefab);
        }

        foreach (var (name, color, shape, count, size) in ProceduralPiles)
            badJumpPrefabs.Add(CreatePilePrefab(name, color, shape, count, size));

        foreach (var (name, color) in ProceduralPuddles)
            badJumpPrefabs.Add(CreatePuddlePrefab(name, color));

        var badDuckPrefabs = new System.Collections.Generic.List<GameObject>();
        foreach (var (name, color) in ArchColors)
            badDuckPrefabs.Add(CreateArchPrefab(name, color));

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

    static GameObject CreateEntityPrefab(string name, string texturePath, float height, string savePath, int? score = null)
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

        float aspect = (float)tex.width / tex.height;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(height * aspect, height, 0.3f);

        // Bad obstacles get a ground shadow so their lane position is easy
        // to judge when jumping — good pickups don't need it.
        if (score.HasValue && score.Value < 0)
            AddStaticGroundShadow(root, height * aspect * 0.7f, height * 0.35f, name + "_Shadow");

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = "Sprite";
        Object.DestroyImmediate(sprite.GetComponent<Collider>());
        sprite.transform.SetParent(root.transform);

        sprite.transform.localScale = new Vector3(height * aspect, height, 1f);
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

    static GameObject CreatePilePrefab(string name, Color color, PrimitiveType shape, int pieceCount, float pieceSize)
    {
        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<ScoreValue>().value = -1;

        var rng = new System.Random(name.GetHashCode());
        float spread = pieceSize * 1.2f;

        for (int i = 0; i < pieceCount; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(shape);
            piece.name = "Piece" + i;
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.transform.SetParent(root.transform);

            float x = ((float)rng.NextDouble() - 0.5f) * spread;
            float z = ((float)rng.NextDouble() - 0.5f) * spread;
            float y = pieceSize / 2f + i * pieceSize * 0.25f;
            piece.transform.localPosition = new Vector3(x, y, z);
            piece.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);

            float scale = pieceSize * (0.8f + (float)rng.NextDouble() * 0.5f);
            piece.transform.localScale = Vector3.one * scale;

            ApplyPersistentColor(piece, color, name + "_Piece" + i);
        }

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        float pileHeight = pieceSize + pieceCount * pieceSize * 0.25f;
        box.size = new Vector3(spread + pieceSize, pileHeight, spread + pieceSize);
        box.center = new Vector3(0f, pileHeight / 2f, 0f);

        AddStaticGroundShadow(root, (spread + pieceSize) * 0.7f, (spread + pieceSize) * 0.7f, name + "_Shadow");

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject CreatePuddlePrefab(string name, Color color)
    {
        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<ScoreValue>().value = -1;

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Puddle";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(root.transform);
        disc.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        disc.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);

        Color translucent = color;
        translucent.a = 0.75f;
        Renderer renderer = disc.GetComponent<Renderer>();
        Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Standard");
        Material material = new Material(shader) { color = translucent };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + name + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
        renderer.sharedMaterial = material;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.6f, 0.1f, 1.6f);

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateInstructionsUI()
    {
        var canvasGo = new GameObject("InstructionsCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        // Just a small reminder that help exists — the full controls text
        // lives in the F1 help overlay instead of sitting on screen always.
        var textGo = new GameObject("Instructions");
        textGo.transform.SetParent(canvasGo.transform, false);

        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.text = "F1 — помощь";

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(300f, 50f);
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
                  + "Ехать вперёд, собирать хорошее, избегать плохое, вдвоём — делать трюки\n\n"
                  + "ЦЕЛЬ\n"
                  + "Набрать 100 очков за самое короткое время, дополнительно выполняя трюки\n\n"
                  + "УПРАВЛЕНИЕ\n"
                  + "Правый: ← → полоса, ↑ прыжок, ↓ пригнуться, I газ, K тормоз\n"
                  + "Левый: A D полоса, W прыжок, S пригнуться, Shift газ, Ctrl тормоз\n"
                  + "Газ/тормоз у каждого свои и складываются: оба жмут газ — разгон вдвое,\n"
                  + "один газ + один тормоз — эффекта нет\n"
                  + "Держать тормоз 5 сек всем игрокам сразу — пауза и вопрос закончить игру\n\n"
                  + "ТРЮКИ\n"
                  + "АРКА: один приседает под аркой, другой в этот момент перепрыгивает её вместе с ним\n"
                  + "КОЛЬЦО: игроки одновременно меняются полосами — один в прыжке, другой понизу\n\n"
                  + "ДАТЧИКИ РАССТОЯНИЯ (ИМИТАТОР)\n"
                  + "По датчику на каждую руку, смотрят вниз: обе руки вверх — прыжок, обе вниз — пригнуться,\n"
                  + "одна вверх/другая вниз — полоса в сторону опущенной руки,\n"
                  + "тронуть среднее положение обеих рук разом — газ, по очереди — тормоз\n"
                  + "Имитатор (верх, середина, низ): левый Q,A,Z и W,S,X — правый O,L,. и P,;,/";

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
        hint.text = "F1 — закрыть";
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

        var textGo = new GameObject("ScoreText");
        textGo.transform.SetParent(panelGo.transform, false);
        Text scoreText = textGo.AddComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 56;
        scoreText.fontStyle = FontStyle.Bold;
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.color = new Color(1f, 0.85f, 0.2f);
        scoreText.text = "0";

        Outline textOutline = textGo.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
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

        var timerTextGo = new GameObject("TimerText");
        timerTextGo.transform.SetParent(timerPanelGo.transform, false);
        Text timerText = timerTextGo.AddComponent<Text>();
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.fontSize = 36;
        timerText.fontStyle = FontStyle.Bold;
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.color = Color.white;
        timerText.text = "00:00";

        Outline timerOutline = timerTextGo.AddComponent<Outline>();
        timerOutline.effectColor = Color.black;
        timerOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform timerTextRt = timerTextGo.GetComponent<RectTransform>();
        timerTextRt.anchorMin = Vector2.zero;
        timerTextRt.anchorMax = Vector2.one;
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
        speedPanelRt.sizeDelta = new Vector2(260f, 70f);
        speedPanelRt.anchoredPosition = timerPanelRt.anchoredPosition - new Vector2(0f, 10f + speedPanelRt.sizeDelta.y);

        var speedTextGo = new GameObject("SpeedText");
        speedTextGo.transform.SetParent(speedPanelGo.transform, false);
        Text speedText = speedTextGo.AddComponent<Text>();
        speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        speedText.fontSize = 30;
        speedText.fontStyle = FontStyle.Bold;
        speedText.alignment = TextAnchor.MiddleCenter;
        speedText.color = new Color(0.6f, 0.9f, 1f);
        speedText.text = "0 км/ч";

        Outline speedOutline = speedTextGo.AddComponent<Outline>();
        speedOutline.effectColor = Color.black;
        speedOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform speedTextRt = speedTextGo.GetComponent<RectTransform>();
        speedTextRt.anchorMin = Vector2.zero;
        speedTextRt.anchorMax = Vector2.one;
        speedTextRt.offsetMin = Vector2.zero;
        speedTextRt.offsetMax = Vector2.zero;

        var speedManagerGo = new GameObject("SpeedIndicator");
        SpeedIndicator speedIndicator = speedManagerGo.AddComponent<SpeedIndicator>();
        SerializedObject speedSo = new SerializedObject(speedIndicator);
        speedSo.FindProperty("speedText").objectReferenceValue = speedText;
        speedSo.ApplyModifiedPropertiesWithoutUndo();

        // Top-3 panel, to the left of the score+timer+speed stack, same total height.
        var topPanelGo = new GameObject("TopScoresPanel");
        topPanelGo.transform.SetParent(canvasGo.transform, false);
        Image topPanelImage = topPanelGo.AddComponent<Image>();
        topPanelImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform topPanelRt = topPanelGo.GetComponent<RectTransform>();
        topPanelRt.anchorMin = new Vector2(0f, 0f);
        topPanelRt.anchorMax = new Vector2(0f, 0f);
        topPanelRt.pivot = new Vector2(0f, 0f);
        topPanelRt.sizeDelta = new Vector2(320f, 290f);
        topPanelRt.anchoredPosition = new Vector2(
            panelRt.anchoredPosition.x - 20f - topPanelRt.sizeDelta.x,
            speedPanelRt.anchoredPosition.y);

        var topTextGo = new GameObject("TopScoresText");
        topTextGo.transform.SetParent(topPanelGo.transform, false);
        Text topText = topTextGo.AddComponent<Text>();
        topText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        topText.fontSize = 20;
        topText.fontStyle = FontStyle.Bold;
        topText.alignment = TextAnchor.MiddleCenter;
        topText.color = Color.white;
        topText.text = "ТОП-3\nвремя · очки за трюки\n1. --:--\n2. --:--\n3. --:--";

        Outline topOutline = topTextGo.AddComponent<Outline>();
        topOutline.effectColor = Color.black;
        topOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform topTextRt = topTextGo.GetComponent<RectTransform>();
        topTextRt.anchorMin = Vector2.zero;
        topTextRt.anchorMax = Vector2.one;
        topTextRt.offsetMin = Vector2.zero;
        topTextRt.offsetMax = Vector2.zero;

        var highScoreGo = new GameObject("HighScoreManager");
        HighScoreManager highScore = highScoreGo.AddComponent<HighScoreManager>();
        SerializedObject highScoreSo = new SerializedObject(highScore);
        highScoreSo.FindProperty("listText").objectReferenceValue = topText;
        highScoreSo.ApplyModifiedPropertiesWithoutUndo();

        return panelRt;
    }

    static void CreateWinSequence(RectTransform scorePanel, Canvas scoreCanvas)
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

        var winGo = new GameObject("WinSequence");
        WinSequence win = winGo.AddComponent<WinSequence>();

        SerializedObject so = new SerializedObject(win);
        so.FindProperty("scorePanel").objectReferenceValue = scorePanel;
        so.FindProperty("winTextRoot").objectReferenceValue = winRt;
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

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(panelGo.transform, false);
        Text label = labelGo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.color = new Color(0.85f, 0.85f, 0.85f);
        label.text = "ТРЮКИ";
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.sizeDelta = new Vector2(0f, 40f);
        labelRt.anchoredPosition = new Vector2(0f, -10f);

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

    // Bottom-left/bottom-right readouts of each player's current two-hand
    // gesture reading (real sensors once connected, or the keyboard
    // simulator) — sits dim/idle when that player is on keyboard controls.
    static void CreateGestureIndicators(GameObject playerRight, GameObject playerLeft)
    {
        CreateGestureIndicator(playerLeft, new Vector2(0f, 0f), new Vector2(20f, 20f));
        CreateGestureIndicator(playerRight, new Vector2(1f, 0f), new Vector2(-20f, 20f));
    }

    static void CreateGestureIndicator(GameObject player, Vector2 anchor, Vector2 anchoredPosition)
    {
        var canvasGo = new GameObject(player.name + "GestureCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("GestureText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 40;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor.x < 0.5f ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        text.supportRichText = true;
        text.text = "–  –";

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(200f, 60f);
        rt.anchoredPosition = anchoredPosition;

        var indicatorGo = new GameObject(player.name + "GestureIndicator");
        GestureIndicator indicator = indicatorGo.AddComponent<GestureIndicator>();
        SerializedObject so = new SerializedObject(indicator);
        so.FindProperty("gestureInput").objectReferenceValue = player.GetComponent<GestureInput>();
        so.FindProperty("indicatorText").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Several color variants, spawned via random pick from badDuckPrefabs —
    // that's what makes the arch appear in a random color per spawn.
    static readonly (string, Color)[] ArchColors =
    {
        ("ArchBrown", new Color(0.6f, 0.4f, 0.2f)),
        ("ArchRed", new Color(0.75f, 0.2f, 0.15f)),
        ("ArchBlue", new Color(0.2f, 0.4f, 0.75f)),
        ("ArchGreen", new Color(0.2f, 0.6f, 0.25f)),
        ("ArchPurple", new Color(0.55f, 0.25f, 0.65f)),
        ("ArchOrange", new Color(0.85f, 0.5f, 0.1f)),
    };

    static GameObject CreateArchPrefab(string name, Color archColor)
    {
        var root = new GameObject(name);
        root.AddComponent<MovingEntity>();
        root.AddComponent<DuckUnderObstacle>();

        float postHeight = 1.3f;
        float postThickness = 0.3f;
        float span = LaneWidth - 0.6f;

        GameObject postLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        postLeft.name = "PostLeft";
        Object.DestroyImmediate(postLeft.GetComponent<Collider>());
        postLeft.transform.SetParent(root.transform);
        postLeft.transform.localPosition = new Vector3(-span / 2f, postHeight / 2f, 0f);
        postLeft.transform.localScale = new Vector3(postThickness, postHeight, postThickness);
        ApplyPersistentColor(postLeft, archColor, name + "_PostLeft");

        GameObject postRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        postRight.name = "PostRight";
        Object.DestroyImmediate(postRight.GetComponent<Collider>());
        postRight.transform.SetParent(root.transform);
        postRight.transform.localPosition = new Vector3(span / 2f, postHeight / 2f, 0f);
        postRight.transform.localScale = new Vector3(postThickness, postHeight, postThickness);
        ApplyPersistentColor(postRight, archColor, name + "_PostRight");

        GameObject topBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topBar.name = "TopBar";
        Object.DestroyImmediate(topBar.GetComponent<Collider>());
        topBar.transform.SetParent(root.transform);
        topBar.transform.localPosition = new Vector3(0f, postHeight + postThickness / 2f, 0f);
        topBar.transform.localScale = new Vector3(span + postThickness, postThickness, postThickness);
        ApplyPersistentColor(topBar, archColor, name + "_TopBar");

        // One trigger covering the whole frame — ducking bypasses it via
        // PlayerController's DuckUnderObstacle check, so it doesn't need to
        // be split into "post" vs "bar" colliders.
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(span + postThickness, postHeight + postThickness, postThickness * 2f);
        box.center = new Vector3(0f, (postHeight + postThickness) / 2f, 0f);

        AddStaticGroundShadow(root, span + postThickness, postThickness * 3f, name + "_Shadow");

        string savePath = "Assets/Prefabs/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CreateStartScreen(GameObject playerRight, GameObject playerLeft)
    {
        var canvasGo = new GameObject("StartScreenCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the score/instructions/win canvases

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

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
        title.fontSize = 64;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(1f, 0.85f, 0.2f);
        title.text = "LADYBUG — HIT THE ROAD!";
        titleGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(1400f, 100f);
        titleRt.anchoredPosition = new Vector2(0f, 440f);

        // Info carousel — cycles between controls and the trick list every
        // few seconds, filling the gap between the title and the menu.
        var carouselGo = new GameObject("Carousel");
        carouselGo.transform.SetParent(canvasGo.transform, false);
        Text carousel = carouselGo.AddComponent<Text>();
        carousel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        carousel.fontSize = 28;
        carousel.fontStyle = FontStyle.Bold;
        carousel.alignment = TextAnchor.MiddleCenter;
        carousel.color = Color.white;
        carouselGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform carouselRt = carousel.GetComponent<RectTransform>();
        carouselRt.anchorMin = new Vector2(0.5f, 0.5f);
        carouselRt.anchorMax = new Vector2(0.5f, 0.5f);
        carouselRt.pivot = new Vector2(0.5f, 0.5f);
        carouselRt.sizeDelta = new Vector2(1500f, 200f);
        carouselRt.anchoredPosition = new Vector2(0f, 255f);

        // Menu-navigation hint only — the full control scheme is already shown
        // by InstructionsCanvas underneath, so this stays short and sits at
        // the bottom to avoid overlapping that top-left text.
        var instructionsGo = new GameObject("Instructions");
        instructionsGo.transform.SetParent(canvasGo.transform, false);
        Text instructions = instructionsGo.AddComponent<Text>();
        instructions.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instructions.fontSize = 28;
        instructions.fontStyle = FontStyle.Bold;
        instructions.alignment = TextAnchor.LowerCenter;
        instructions.color = Color.white;
        instructions.text = "Выбор в строке: любое ←→   Смена строки: любое ↑↓   Начать игру: любой газ";
        instructionsGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform instructionsRt = instructions.GetComponent<RectTransform>();
        instructionsRt.anchorMin = new Vector2(0.5f, 0f);
        instructionsRt.anchorMax = new Vector2(0.5f, 0f);
        instructionsRt.pivot = new Vector2(0.5f, 0f);
        instructionsRt.sizeDelta = new Vector2(1600f, 80f);
        instructionsRt.anchoredPosition = new Vector2(0f, 40f);

        // Options row container — its Outline is the focus frame for row 0.
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
        rowRt.sizeDelta = new Vector2(800f, 160f);
        rowRt.anchoredPosition = new Vector2(0f, 40f);

        GameObject option1 = CreateMenuOption(rowGo.transform, "Option1", new Vector2(-210f, 0f), "[X] 1 ИГРОК");
        GameObject option2 = CreateMenuOption(rowGo.transform, "Option2", new Vector2(210f, 0f), "[ ] 2 ИГРОКА");

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
        controllerRowRt.sizeDelta = new Vector2(1000f, 160f);
        controllerRowRt.anchoredPosition = new Vector2(0f, -155f);

        // Narrower than the default 360px option box, and spaced a full box
        // width + gap apart, since three of these side by side would
        // otherwise overlap (360 wide but only 280 apart, in a prior version).
        GameObject controller1 = CreateMenuOption(controllerRowGo.transform, "Controller1", new Vector2(-330f, 0f), "[X] КЛАВИАТУРА", 300f, 30);
        GameObject controller2 = CreateMenuOption(controllerRowGo.transform, "Controller2", new Vector2(0f, 0f), "[ ] ДАТЧИКИ", 300f, 30);
        GameObject controller3 = CreateMenuOption(controllerRowGo.transform, "Controller3", new Vector2(330f, 0f), "[ ] ИМИТАТОР", 300f, 30);

        GameObject startBtn = CreateMenuOption(canvasGo.transform, "StartButton", new Vector2(0f, -320f), "СТАРТ");
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
        notImplementedRt.anchoredPosition = new Vector2(0f, -400f);
        notImplementedGo.SetActive(false);

        var controller = canvasGo.AddComponent<StartScreenController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("canvasRoot").objectReferenceValue = canvasGo;
        so.FindProperty("carouselText").objectReferenceValue = carousel;
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
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Shared visual style for the start-screen's option boxes and Start button.
    static GameObject CreateMenuOption(Transform parent, string name, Vector2 anchoredPos, string label, float width = 360f, int fontSize = 38)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 100f);
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
        hint.text = "←→ выбор, газ — подтвердить";
        RectTransform hintRt = hint.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0.5f);
        hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintRt.pivot = new Vector2(0.5f, 0.5f);
        hintRt.sizeDelta = new Vector2(800f, 60f);
        hintRt.anchoredPosition = new Vector2(0f, -140f);

        canvasGo.SetActive(false);

        // Countdown warning while the brake is being held — lives on its own
        // always-active canvas (below the main controls text, top-left) since
        // it needs to show well before the dialog itself opens.
        var warningCanvasGo = new GameObject("HoldWarningCanvas");
        Canvas warningCanvas = warningCanvasGo.AddComponent<Canvas>();
        warningCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        warningCanvas.sortingOrder = 50;

        CanvasScaler warningScaler = warningCanvasGo.AddComponent<CanvasScaler>();
        warningScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        warningScaler.referenceResolution = new Vector2(1920f, 1080f);
        warningCanvasGo.AddComponent<GraphicRaycaster>();

        var warningGo = new GameObject("HoldWarningText");
        warningGo.transform.SetParent(warningCanvasGo.transform, false);
        Text warningText = warningGo.AddComponent<Text>();
        warningText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        warningText.fontSize = 28;
        warningText.fontStyle = FontStyle.Bold;
        warningText.alignment = TextAnchor.UpperLeft;
        warningText.color = new Color(1f, 0.4f, 0.3f);
        warningGo.AddComponent<Outline>().effectColor = Color.black;
        RectTransform warningRt = warningText.GetComponent<RectTransform>();
        warningRt.anchorMin = new Vector2(0f, 1f);
        warningRt.anchorMax = new Vector2(0f, 1f);
        warningRt.pivot = new Vector2(0f, 1f);
        warningRt.anchoredPosition = new Vector2(20f, -360f);
        warningRt.sizeDelta = new Vector2(700f, 60f);
        warningGo.SetActive(false);

        var controllerGo = new GameObject("PauseController");
        PauseController pause = controllerGo.AddComponent<PauseController>();
        SerializedObject so = new SerializedObject(pause);
        so.FindProperty("dialogRoot").objectReferenceValue = canvasGo;
        so.FindProperty("yesText").objectReferenceValue = yesText;
        so.FindProperty("noText").objectReferenceValue = noText;
        so.FindProperty("holdWarningText").objectReferenceValue = warningText;
        so.ApplyModifiedPropertiesWithoutUndo();
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

    // Same as ApplyColor, but saves the material as its own asset first.
    // Required for anything that ends up inside a saved Prefab asset — an
    // in-memory Material there serializes as a broken (magenta) reference.
    static void ApplyPersistentColor(GameObject go, Color color, string materialName)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material material = new Material(shader) { color = color };

        System.IO.Directory.CreateDirectory("Assets/Materials");
        string materialPath = "Assets/Materials/" + materialName + ".mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(material, materialPath);
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
