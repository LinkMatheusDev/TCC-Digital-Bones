using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class AnatomyHudController : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.095f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.11f, 0.15f, 0.18f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.78f, 0.72f, 1f);
    private static readonly Color PrimaryTextColor = new Color(0.93f, 0.95f, 0.96f, 1f);
    private static readonly Color SecondaryTextColor = new Color(0.65f, 0.72f, 0.76f, 1f);

    private AnatomyViewer viewer;
    private AnatomyModelLoader modelLoader;
    private Canvas interfaceCanvas;
    private GraphicRaycaster graphicRaycaster;
    private Camera hudCamera;
    private GameObject modelPanel;
    private GameObject detailsPanel;
    private Text toggleLabel;
    private Text modelNameText;
    private Text speciesText;
    private Text boneText;
    private Text descriptionText;
    private Sprite panelSprite;
    private Font interfaceFont;
    private bool menuVisible = true;
    private bool hasSelectedModel;

    private void Awake()
    {
        viewer = GetComponent<AnatomyViewer>();
        modelLoader = FindObjectOfType<AnatomyModelLoader>();
    }

    private void Start()
    {
        if (viewer == null)
        {
            Debug.LogError("AnatomyHudController precisa de um AnatomyViewer.");
            enabled = false;
            return;
        }

        if (modelLoader == null)
        {
            modelLoader = FindObjectOfType<AnatomyModelLoader>();
        }

        if (modelLoader == null)
        {
            Debug.LogError("AnatomyHudController não encontrou um AnatomyModelLoader.");
            enabled = false;
            return;
        }

        hudCamera = viewer.ViewerCamera != null
            ? viewer.ViewerCamera
            : Camera.main;

        if (hudCamera == null)
        {
            Debug.LogError("AnatomyHudController não encontrou uma câmera para a interface.");
            enabled = false;
            return;
        }

        DisableLegacyCanvases();
        CreateInterface(hudCamera);
        PopulateModelButtons();
        SetMenuVisible(true);
    }

    private void DisableLegacyCanvases()
    {
        Canvas[] existingCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas existingCanvas in existingCanvases)
        {
            existingCanvas.gameObject.SetActive(false);
        }
    }

    private void CreateInterface(Camera hudCamera)
    {
        interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panelSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject canvasObject = new GameObject(
            "Digital Bones Interface",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(hudCamera.transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 0f, 0.9f);
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.001f;

        interfaceCanvas = canvasObject.GetComponent<Canvas>();
        interfaceCanvas.renderMode = RenderMode.WorldSpace;
        interfaceCanvas.worldCamera = hudCamera;
        interfaceCanvas.sortingOrder = 20;
        graphicRaycaster = canvasObject.GetComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = scaler.referenceResolution;

        modelPanel = CreatePanel(
            canvasRect,
            "Model Library",
            new Vector2(28f, -82f),
            new Vector2(390f, 540f),
            PanelColor
        ).gameObject;

        CreateText(
            modelPanel.transform,
            "DIGITAL BONES",
            13,
            FontStyle.Bold,
            AccentColor,
            new Vector2(24f, -22f),
            new Vector2(342f, 24f),
            TextAnchor.MiddleLeft
        );
        CreateText(
            modelPanel.transform,
            "Biblioteca de modelos",
            25,
            FontStyle.Bold,
            PrimaryTextColor,
            new Vector2(24f, -52f),
            new Vector2(342f, 42f),
            TextAnchor.MiddleLeft
        );
        CreateText(
            modelPanel.transform,
            "Escolha uma estrutura para visualizar",
            15,
            FontStyle.Normal,
            SecondaryTextColor,
            new Vector2(24f, -98f),
            new Vector2(342f, 36f),
            TextAnchor.MiddleLeft
        );
        CreateText(
            modelPanel.transform,
            "MODELOS DISPONÍVEIS",
            12,
            FontStyle.Bold,
            AccentColor,
            new Vector2(24f, -143f),
            new Vector2(342f, 22f),
            TextAnchor.MiddleLeft
        );

        CreateButton(
            canvasRect,
            "Menu Toggle",
            new Vector2(28f, -28f),
            new Vector2(154f, 42f),
            ButtonColor,
            ToggleMenu
        );
        toggleLabel = CreateText(
            canvasRect,
            "Fechar menu",
            15,
            FontStyle.Bold,
            PrimaryTextColor,
            new Vector2(44f, -28f),
            new Vector2(122f, 42f),
            TextAnchor.MiddleCenter
        );

        detailsPanel = CreatePanel(
            canvasRect,
            "Selected Model Details",
            new Vector2(28f, -82f),
            new Vector2(394f, 438f),
            PanelColor
        ).gameObject;

        CreateText(
            detailsPanel.transform,
            "ESTRUTURA SELECIONADA",
            12,
            FontStyle.Bold,
            AccentColor,
            new Vector2(24f, -24f),
            new Vector2(346f, 24f),
            TextAnchor.MiddleLeft
        );
        modelNameText = CreateText(
            detailsPanel.transform,
            string.Empty,
            27,
            FontStyle.Bold,
            PrimaryTextColor,
            new Vector2(24f, -58f),
            new Vector2(346f, 64f),
            TextAnchor.MiddleLeft
        );
        CreateText(
            detailsPanel.transform,
            "ESPÉCIE",
            11,
            FontStyle.Bold,
            SecondaryTextColor,
            new Vector2(24f, -142f),
            new Vector2(150f, 20f),
            TextAnchor.MiddleLeft
        );
        speciesText = CreateText(
            detailsPanel.transform,
            string.Empty,
            16,
            FontStyle.Normal,
            PrimaryTextColor,
            new Vector2(24f, -164f),
            new Vector2(346f, 30f),
            TextAnchor.MiddleLeft
        );
        CreateText(
            detailsPanel.transform,
            "ESTRUTURA ANATÔMICA",
            11,
            FontStyle.Bold,
            SecondaryTextColor,
            new Vector2(24f, -210f),
            new Vector2(346f, 20f),
            TextAnchor.MiddleLeft
        );
        boneText = CreateText(
            detailsPanel.transform,
            string.Empty,
            16,
            FontStyle.Normal,
            PrimaryTextColor,
            new Vector2(24f, -232f),
            new Vector2(346f, 30f),
            TextAnchor.MiddleLeft
        );
        CreateText(
            detailsPanel.transform,
            "SOBRE O MODELO",
            11,
            FontStyle.Bold,
            SecondaryTextColor,
            new Vector2(24f, -278f),
            new Vector2(346f, 20f),
            TextAnchor.MiddleLeft
        );
        descriptionText = CreateText(
            detailsPanel.transform,
            string.Empty,
            15,
            FontStyle.Normal,
            SecondaryTextColor,
            new Vector2(24f, -302f),
            new Vector2(346f, 112f),
            TextAnchor.UpperLeft
        );

        CreateText(
            canvasRect,
            "R restaura  •  botão direito do mouse gira",
            12,
            FontStyle.Normal,
            SecondaryTextColor,
            new Vector2(28f, -674f),
            new Vector2(500f, 26f),
            TextAnchor.MiddleLeft
        );

        detailsPanel.SetActive(false);
    }

    public bool TryHandleControllerRay(Ray ray)
    {
        if (interfaceCanvas == null ||
            graphicRaycaster == null ||
            EventSystem.current == null)
        {
            return false;
        }

        Plane interfacePlane = new Plane(
            interfaceCanvas.transform.forward,
            interfaceCanvas.transform.position
        );
        if (!interfacePlane.Raycast(ray, out float distance) || distance < 0f)
        {
            return false;
        }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            hudCamera,
            worldPoint
        );
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };
        var raycastResults = new System.Collections.Generic.List<RaycastResult>();
        graphicRaycaster.Raycast(pointerData, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            Button button = result.gameObject.GetComponentInParent<Button>();
            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
                return true;
            }
        }

        return false;
    }

    private void PopulateModelButtons()
    {
        AnatomyModelDefinition[] catalog = modelLoader.Catalog;
        if (catalog == null || catalog.Length == 0)
        {
            Debug.LogError("O catálogo de modelos está vazio; não foi possível criar o menu.");
            return;
        }

        int buttonIndex = 0;
        foreach (AnatomyModelDefinition definition in catalog)
        {
            if (definition == null || definition.ModelPrefab == null)
            {
                continue;
            }

            string modelId = definition.ModelId;
            string displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.ModelPrefab.name
                : definition.DisplayName;
            string subtitle = string.IsNullOrWhiteSpace(definition.Bone)
                ? "Modelo anatômico  •  selecionar"
                : definition.Bone + "  •  selecionar";

            CreateModelButton(
                modelPanel.transform,
                displayName,
                subtitle,
                new Vector2(22f, -174f - buttonIndex * 84f),
                () => SelectModel(modelId)
            );
            buttonIndex++;
        }

        if (buttonIndex == 0)
        {
            Debug.LogError("O catálogo não contém modelos com prefabs associados.");
        }
    }

    private void SelectModel(string modelId)
    {
        if (!modelLoader.TryLoadModel(modelId))
        {
            return;
        }

        AnatomyModelDefinition definition = System.Array.Find(
            modelLoader.Catalog,
            item => item != null && item.ModelId == modelId
        );

        if (definition == null)
        {
            Debug.LogError("O modelo carregado não foi encontrado no catálogo: " + modelId);
            return;
        }

        modelNameText.text = string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.ModelPrefab.name
            : definition.DisplayName;
        speciesText.text = string.IsNullOrWhiteSpace(definition.Species)
            ? "Não informada no catálogo"
            : definition.Species;
        boneText.text = string.IsNullOrWhiteSpace(definition.Bone)
            ? "Não informada no catálogo"
            : definition.Bone;
        descriptionText.text = string.IsNullOrWhiteSpace(definition.Description)
            ? "A descrição detalhada deste modelo ainda não foi cadastrada."
            : definition.Description;

        hasSelectedModel = true;
        SetMenuVisible(false);
    }

    private void ToggleMenu()
    {
        SetMenuVisible(!menuVisible);
    }

    private void SetMenuVisible(bool visible)
    {
        menuVisible = visible;
        modelPanel.SetActive(visible);
        detailsPanel.SetActive(!visible && hasSelectedModel);
        toggleLabel.text = visible ? "Fechar menu" : "Modelos";
    }

    private RectTransform CreatePanel(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject panelObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        SetTopLeftRect(rect, position, size);

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.sprite = panelSprite;
        image.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;

        return rect;
    }

    private void CreateModelButton(
        Transform parent,
        string title,
        string subtitle,
        Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        RectTransform buttonRect = CreateButton(
            parent,
            "Model " + title,
            position,
            new Vector2(346f, 72f),
            ButtonColor,
            onClick
        );

        GameObject accent = new GameObject(
            "Accent",
            typeof(RectTransform),
            typeof(Image)
        );
        accent.transform.SetParent(buttonRect, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        SetTopLeftRect(accentRect, new Vector2(0f, -12f), new Vector2(4f, 48f));
        accent.GetComponent<Image>().color = AccentColor;

        CreateText(
            buttonRect,
            title,
            17,
            FontStyle.Bold,
            PrimaryTextColor,
            new Vector2(18f, -11f),
            new Vector2(310f, 27f),
            TextAnchor.MiddleLeft
        );
        CreateText(
            buttonRect,
            subtitle,
            12,
            FontStyle.Normal,
            SecondaryTextColor,
            new Vector2(18f, -39f),
            new Vector2(310f, 22f),
            TextAnchor.MiddleLeft
        );
    }

    private RectTransform CreateButton(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetTopLeftRect(rect, position, size);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.sprite = panelSprite;
        image.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = new ColorBlock
        {
            normalColor = color,
            highlightedColor = new Color(0.16f, 0.23f, 0.25f, 1f),
            pressedColor = new Color(0.20f, 0.36f, 0.36f, 1f),
            selectedColor = color,
            disabledColor = new Color(color.r, color.g, color.b, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        button.onClick.AddListener(onClick);

        return rect;
    }

    private Text CreateText(
        Transform parent,
        string initialText,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        Vector2 position,
        Vector2 size,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(
            initialText,
            typeof(RectTransform),
            typeof(Text)
        );
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetTopLeftRect(rect, position, size);

        Text text = textObject.GetComponent<Text>();
        text.font = interfaceFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.text = initialText;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        return text;
    }

    private static void SetTopLeftRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
