using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AnatomySceneSetup
{
    private const string ScenePath = "Assets/Scenes/AnatomyLab.unity";
    private const string MaterialPath = "Assets/Materials/Bone.mat";
    private static readonly Color BoneColor = new Color(0.82f, 0.79f, 0.69f);

    [MenuItem("Digital Bones/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material boneMaterial = CreateBoneMaterial();

        GameObject trackingOrigin = new GameObject("XR Tracking Origin");
        trackingOrigin.transform.position = new Vector3(0f, 0f, -2.5f);

        GameObject cameraObject = new GameObject("Quest Camera");
        cameraObject.transform.SetParent(trackingOrigin.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.07f);
        camera.nearClipPlane = 0.03f;
        GameObject lightObject = new GameObject("Key Light");
        Light keyLight = lightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

        GameObject modelRoot = new GameObject("Modelo Anatomico - Femur demonstrativo");
        modelRoot.transform.position = new Vector3(0f, 1f, 0f);

        CreatePart(modelRoot.transform, "Epifise proximal", "Extremidade superior do osso longo. Nesta base, o modelo e apenas demonstrativo; substitua-o pelo modelo anatomico validado.", new Vector3(0f, 0.54f, 0f), new Vector3(0.42f, 0.28f, 0.36f), boneMaterial, new Vector3(0f, 0.18f, 0f));
        CreatePart(modelRoot.transform, "Diafise", "Corpo do osso longo. Use esta estrutura para apresentar a regiao cortical e relacionar a anatomia com o planejamento de procedimentos.", Vector3.zero, new Vector3(0.16f, 0.63f, 0.16f), boneMaterial, Vector3.zero, PrimitiveType.Capsule);
        CreatePart(modelRoot.transform, "Epifise distal", "Extremidade inferior do osso longo. A malha placeholder pode ser trocada por um arquivo exportado do fluxo de segmentacao DICOM.", new Vector3(0f, -0.54f, 0f), new Vector3(0.34f, 0.25f, 0.33f), boneMaterial, new Vector3(0f, -0.18f, 0f));

        Canvas hudCanvas = CreateHud(cameraObject.transform, out Text selectedText, out Text detailsText, out Text modeText);
        GameObject viewerObject = new GameObject("Anatomy Viewer");
        AnatomyViewer viewer = viewerObject.AddComponent<AnatomyViewer>();
        viewer.Configure(modelRoot.transform, trackingOrigin.transform, camera, selectedText, detailsText, modeText);

        HandGestureController handGestures = viewerObject.AddComponent<HandGestureController>();
        handGestures.Configure(viewer, CreateGestureStatusText(hudCanvas.transform));

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("Digital Bones demo scene created at " + ScenePath);
    }

    [MenuItem("Digital Bones/Add Hand Gestures To Open Scene")]
    public static void AddHandGesturesToOpenScene()
    {
        AnatomyViewer viewer = UnityEngine.Object.FindFirstObjectByType<AnatomyViewer>();
        if (viewer == null)
        {
            EditorUtility.DisplayDialog("Digital Bones", "Nenhum AnatomyViewer encontrado na cena aberta. Abra Assets/Scenes/AnatomyLab.unity.", "OK");
            return;
        }

        if (viewer.GetComponent<HandGestureController>() != null)
        {
            EditorUtility.DisplayDialog("Digital Bones", "A cena já possui o HandGestureController.", "OK");
            return;
        }

        Text statusText = null;
        GameObject hud = GameObject.Find("Anatomy HUD");
        if (hud != null)
        {
            statusText = CreateGestureStatusText(hud.transform);
            Undo.RegisterCreatedObjectUndo(statusText.gameObject, "Add Gesture Status Text");
        }

        HandGestureController controller = Undo.AddComponent<HandGestureController>(viewer.gameObject);
        controller.Configure(viewer, statusText);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(viewer.gameObject.scene);
        Debug.Log("HandGestureController adicionado. Salve a cena (Ctrl+S).");
    }

    [MenuItem("Digital Bones/Play Demo Scene")]
    public static void PlayDemoScene()
    {
        if (!File.Exists(ScenePath))
        {
            CreateDemoScene();
        }
        else
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
    }

    private static Material CreateBoneMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader)
        {
            name = "Bone",
            color = BoneColor
        };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static void CreatePart(Transform parent, string partName, string details, Vector3 localPosition, Vector3 localScale, Material material, Vector3 dissectionOffset, PrimitiveType primitive = PrimitiveType.Sphere)
    {
        GameObject partObject = GameObject.CreatePrimitive(primitive);
        partObject.name = partName;
        partObject.transform.SetParent(parent, false);
        partObject.transform.localPosition = localPosition;
        partObject.transform.localScale = localScale;
        partObject.GetComponent<Renderer>().sharedMaterial = material;

        AnatomicalPart part = partObject.AddComponent<AnatomicalPart>();
        part.Configure(partName, details, BoneColor, dissectionOffset);
    }

    private static Canvas CreateHud(Transform cameraTransform, out Text selectedText, out Text detailsText, out Text modeText)
    {
        GameObject canvasObject = new GameObject("Anatomy HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(cameraTransform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 0f, 0.9f);
        canvasObject.transform.localScale = Vector3.one * 0.001f;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.GetComponent<RectTransform>().sizeDelta = scaler.referenceResolution;

        GameObject panelObject = new GameObject("Info Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(440f, 260f);
        panelObject.GetComponent<Image>().color = new Color(0.025f, 0.07f, 0.09f, 0.82f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateText(panelObject.transform, "Digital Bones", font, 28, FontStyle.Bold, new Vector2(18f, -18f), new Vector2(404f, 42f), TextAnchor.MiddleLeft, out _);
        CreateText(panelObject.transform, "Selected Structure", font, 22, FontStyle.Bold, new Vector2(18f, -68f), new Vector2(404f, 34f), TextAnchor.MiddleLeft, out selectedText);
        CreateText(panelObject.transform, "Structure Details", font, 16, FontStyle.Normal, new Vector2(18f, -106f), new Vector2(404f, 74f), TextAnchor.UpperLeft, out detailsText);
        CreateText(panelObject.transform, "View Modes", font, 15, FontStyle.Italic, new Vector2(18f, -184f), new Vector2(404f, 28f), TextAnchor.MiddleLeft, out modeText);
        CreateText(panelObject.transform, "Trigger: select  |  A: dissection  |  X: isolate\nMouse: select / rotate / zoom  |  Space: dissection  |  I: isolate  |  R: reset", font, 13, FontStyle.Normal, new Vector2(18f, -214f), new Vector2(404f, 40f), TextAnchor.UpperLeft, out _);
        return canvas;
    }

    private static Text CreateGestureStatusText(Transform hudCanvas)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateText(hudCanvas, "Gesture Status", font, 16, FontStyle.Bold, new Vector2(24f, -292f), new Vector2(560f, 64f), TextAnchor.UpperLeft, out Text statusText);
        return statusText;
    }

    private static void CreateText(Transform parent, string initialText, Font font, int fontSize, FontStyle fontStyle, Vector2 position, Vector2 size, TextAnchor alignment, out Text text)
    {
        GameObject textObject = new GameObject(initialText, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = initialText;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}