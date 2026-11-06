using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public sealed class AnatomyViewer : MonoBehaviour
{
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Transform trackingOrigin;
    [SerializeField] private Camera viewerCamera;
    [SerializeField] private Text selectedPartText;
    [SerializeField] private Text detailsText;
    [SerializeField] private Text modeText;

    private AnatomicalPart[] parts;
    private AnatomicalPart selectedPart;
    private InputDevice rightController;
    private InputDevice leftController;
    private InputDevice headDevice;
    private Vector3 initialScale;
    private Quaternion initialRotation;
    private Vector3 previousMousePosition;
    private bool dissected;
    private bool isolated;
    private bool previousTrigger;
    private bool previousRightPrimary;
    private bool previousLeftPrimary;
    private Vector3 initialPosition;

    public Transform ModelRoot => modelRoot;
    public Transform TrackingOrigin => trackingOrigin;
    public Camera ViewerCamera => viewerCamera;

    public void Configure(Transform root, Transform origin, Camera camera, Text selectedText, Text description, Text mode)
    {
        modelRoot = root;
        trackingOrigin = origin;
        viewerCamera = camera;
        selectedPartText = selectedText;
        detailsText = description;
        modeText = mode;
    }

    private void Start()
    {
        if (viewerCamera == null)
        {
            viewerCamera = Camera.main;
        }

        parts = modelRoot != null ? modelRoot.GetComponentsInChildren<AnatomicalPart>() : new AnatomicalPart[0];
        if (modelRoot != null)
        {
            initialScale = modelRoot.localScale;
            initialRotation = modelRoot.localRotation;
            initialPosition = modelRoot.localPosition;
        }

        if (parts.Length > 0)
        {
            SelectPart(parts[0]);
        }

        RefreshModeText();
    }

    private void Update()
    {
        HandleHeadTracking();
        HandleDesktopInput();
        HandleControllerInput();
    }

    private void HandleHeadTracking()
    {
        if (!headDevice.isValid)
        {
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }

        if (viewerCamera != null &&
            headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPosition) &&
            headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRotation))
        {
            viewerCamera.transform.localPosition = headPosition;
            viewerCamera.transform.localRotation = headRotation;
        }
    }

    private void HandleDesktopInput()
    {
        if (viewerCamera == null || modelRoot == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            SelectFromRay(viewerCamera.ScreenPointToRay(Input.mousePosition));
        }

        if (Input.GetMouseButtonDown(1))
        {
            previousMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 mouseDelta = Input.mousePosition - previousMousePosition;
            modelRoot.Rotate(Vector3.up, -mouseDelta.x * 0.35f, Space.World);
            modelRoot.Rotate(Vector3.right, mouseDelta.y * 0.35f, Space.Self);
            previousMousePosition = Input.mousePosition;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            ScaleModel(scroll * 0.08f);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleDissection();
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleIsolation();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetView();
        }

        for (int index = 0; index < parts.Length && index < 3; index++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + index)))
            {
                SelectPart(parts[index]);
            }
        }
    }

    private void HandleControllerInput()
    {
        if (!rightController.isValid)
        {
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        if (!leftController.isValid)
        {
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }

        bool triggerPressed = rightController.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton) && triggerButton;
        if (triggerPressed && !previousTrigger && TryGetControllerRay(rightController, out Ray controllerRay))
        {
            SelectFromRay(controllerRay);
        }
        previousTrigger = triggerPressed;

        bool rightPrimaryPressed = rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool rightPrimary) && rightPrimary;
        if (rightPrimaryPressed && !previousRightPrimary)
        {
            ToggleDissection();
        }
        previousRightPrimary = rightPrimaryPressed;

        bool leftPrimaryPressed = leftController.TryGetFeatureValue(CommonUsages.primaryButton, out bool leftPrimary) && leftPrimary;
        if (leftPrimaryPressed && !previousLeftPrimary)
        {
            ToggleIsolation();
        }
        previousLeftPrimary = leftPrimaryPressed;

        if (leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick) && stick.sqrMagnitude > 0.04f && modelRoot != null)
        {
            modelRoot.Rotate(Vector3.up, -stick.x * 90f * Time.deltaTime, Space.World);
            modelRoot.Rotate(Vector3.right, stick.y * 90f * Time.deltaTime, Space.Self);
        }
    }

    private bool TryGetControllerRay(InputDevice controller, out Ray controllerRay)
    {
        if (controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
            controller.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
        {
            Vector3 worldPosition = trackingOrigin != null ? trackingOrigin.TransformPoint(position) : position;
            Quaternion worldRotation = trackingOrigin != null ? trackingOrigin.rotation * rotation : rotation;
            controllerRay = new Ray(worldPosition, worldRotation * Vector3.forward);
            return true;
        }

        controllerRay = default;
        return false;
    }

    private void SelectFromRay(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, 10f) && hit.collider.GetComponentInParent<AnatomicalPart>() is AnatomicalPart part)
        {
            SelectPart(part);
        }
    }

    private void SelectPart(AnatomicalPart part)
    {
        if (selectedPart != null)
        {
            selectedPart.SetSelected(false);
        }

        selectedPart = part;
        if (selectedPart != null)
        {
            selectedPart.SetSelected(true);
        }

        ApplyIsolation();
        RefreshInformation();
    }

    private void ToggleDissection()
    {
        dissected = !dissected;
        foreach (AnatomicalPart part in parts)
        {
            part.SetDissected(dissected);
        }

        RefreshModeText();
    }

    private void ToggleIsolation()
    {
        isolated = !isolated;
        ApplyIsolation();
        RefreshModeText();
    }

    private void ApplyIsolation()
    {
        foreach (AnatomicalPart part in parts)
        {
            part.SetVisible(!isolated || part == selectedPart);
        }
    }

    private void ScaleModel(float amount)
    {
        if (modelRoot == null)
        {
            return;
        }

        float scale = Mathf.Clamp(modelRoot.localScale.x + amount, 0.35f, 2.5f);
        modelRoot.localScale = Vector3.one * scale;
    }

    public void ResetView()
    {
        dissected = false;
        isolated = false;
        if (modelRoot != null)
        {
            modelRoot.localScale = initialScale;
            modelRoot.localRotation = initialRotation;
            modelRoot.localPosition = initialPosition;
        }

        foreach (AnatomicalPart part in parts)
        {
            part.SetDissected(false);
            part.SetVisible(true);
        }

        RefreshModeText();
    }

    private void RefreshInformation()
    {
        if (selectedPartText != null)
        {
            selectedPartText.text = selectedPart != null ? selectedPart.DisplayName : "Nenhuma estrutura selecionada";
        }

        if (detailsText != null)
        {
            detailsText.text = selectedPart != null ? selectedPart.Details : "Aponte para uma estrutura e pressione o gatilho.";
        }
    }

    private void RefreshModeText()
    {
        if (modeText != null)
        {
            string dissectionLabel = dissected ? "Disseccao: ativa" : "Disseccao: inativa";
            string isolationLabel = isolated ? "Isolamento: ativo" : "Isolamento: inativo";
            modeText.text = $"{dissectionLabel}  |  {isolationLabel}";
        }
    }
}