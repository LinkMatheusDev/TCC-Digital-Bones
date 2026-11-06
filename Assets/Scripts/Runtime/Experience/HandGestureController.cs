using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

/// <summary>
/// Interação com o modelo 3D por gestos das mãos (hand tracking via pacote XR Hands / OpenXR).
///
/// Gestos:
///   Pinça (uma mão)            -> girar o modelo (arraste a mão no ar)
///   Pinça (duas mãos)          -> zoom / tamanho (afaste = aumenta, aproxime = diminui) + girar torcendo as mãos
///   Punho fechado (uma mão)    -> mover o modelo (puxar para perto, empurrar para longe, lados)
///   Dois punhos por ~1 s       -> restaurar a visualização
///
/// Funciona em conjunto com o AnatomyViewer (mouse e controles continuam funcionando).
/// </summary>
[DisallowMultipleComponent]
public sealed class HandGestureController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private AnatomyViewer viewer;
    [SerializeField] private Text statusText;

    [Header("Detecção de pinça (metros, entre ponta do polegar e do indicador)")]
    [SerializeField] private float pinchOnDistance = 0.020f;
    [SerializeField] private float pinchOffDistance = 0.035f;

    [Header("Detecção de punho (metros, distância média das pontas dos dedos até a palma)")]
    [SerializeField] private float fistOnDistance = 0.055f;
    [SerializeField] private float fistOffDistance = 0.075f;

    [Header("Girar (pinça com uma mão)")]
    [SerializeField] private float rotationDegreesPerMeter = 500f;
    [SerializeField] private float rotationDeadzone = 0.0006f;

    [Header("Zoom / tamanho (pinça com duas mãos)")]
    [SerializeField] private float minScale = 0.35f;
    [SerializeField] private float maxScale = 2.5f;
    [SerializeField] private bool twistWithTwoHands = true;

    [Header("Mover (punho)")]
    [SerializeField] private float moveGain = 2.5f;
    [SerializeField] private float minDistanceFromCamera = 0.35f;
    [SerializeField] private float maxDistanceFromCamera = 5f;

    [Header("Restaurar (dois punhos)")]
    [SerializeField] private float resetHoldSeconds = 1.0f;

    [Header("Suavização e depuração")]
    [Tooltip("Quanto maior, menos suavização (e menos atraso). Valores típicos: 15 a 40.")]
    [SerializeField] private float smoothingRate = 25f;
    [Tooltip("Mostra no HUD as distâncias medidas, útil para calibrar os limiares no headset.")]
    [SerializeField] private bool showDebugMetrics;

    private enum Gesture
    {
        None,
        Rotate,
        Scale,
        Move,
        ResetHold
    }

    private sealed class HandTracker
    {
        public bool Tracked;
        public bool Pinching;
        public bool Fist;
        public bool HasSmoothedPoints;
        public Vector3 PinchPoint; // mundo, suavizado
        public Vector3 PalmPoint;  // mundo, suavizado
        public float PinchDistance; // espaço de tracking (metros)
        public float CurlDistance;  // espaço de tracking (metros)

        public void Clear()
        {
            Tracked = false;
            Pinching = false;
            Fist = false;
            HasSmoothedPoints = false;
        }
    }

    private static readonly List<XRHandSubsystem> SubsystemBuffer = new List<XRHandSubsystem>();

    private readonly HandTracker leftHand = new HandTracker();
    private readonly HandTracker rightHand = new HandTracker();

    private XRHandSubsystem handSubsystem;
    private Camera viewerCamera;
    private float nextSubsystemSearchTime;
    private float nextStatusTime;

    private Gesture currentGesture = Gesture.None;
    private HandTracker activeHand;
    private Vector3 lastPoint;
    private float startDistance;
    private float startScale;
    private Quaternion startRotation;
    private Vector3 startFlatVector;
    private float resetHoldTimer;
    private bool resetLatched;

    public void Configure(AnatomyViewer anatomyViewer, Text status)
    {
        viewer = anatomyViewer;
        statusText = status;
    }

    private void OnDisable()
    {
        leftHand.Clear();
        rightHand.Clear();
        currentGesture = Gesture.None;
        activeHand = null;
    }

    private void Update()
    {
        if (!ResolveReferences())
        {
            return;
        }

        if (!EnsureSubsystem())
        {
            SetStatus("Mãos: rastreamento indisponível (use mouse ou controles)");
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        ReadHand(handSubsystem.leftHand, leftHand, deltaTime);
        ReadHand(handSubsystem.rightHand, rightHand, deltaTime);

        UpdateGesture(deltaTime);
        RefreshStatus();
    }

    // ---------------------------------------------------------------- referências

    private bool ResolveReferences()
    {
        if (viewer == null)
        {
            viewer = FindFirstObjectByType<AnatomyViewer>();
        }

        if (viewer == null || viewer.ModelRoot == null)
        {
            return false;
        }

        if (viewerCamera == null)
        {
            viewerCamera = viewer.ViewerCamera != null ? viewer.ViewerCamera : Camera.main;
        }

        return viewerCamera != null;
    }

    private bool EnsureSubsystem()
    {
        if (handSubsystem != null && handSubsystem.running)
        {
            return true;
        }

        if (Time.unscaledTime < nextSubsystemSearchTime)
        {
            return false;
        }

        nextSubsystemSearchTime = Time.unscaledTime + 0.5f;
        handSubsystem = null;
        SubsystemManager.GetSubsystems(SubsystemBuffer);
        foreach (XRHandSubsystem subsystem in SubsystemBuffer)
        {
            if (subsystem.running)
            {
                handSubsystem = subsystem;
                break;
            }
        }

        return handSubsystem != null;
    }

    // ---------------------------------------------------------------- leitura das mãos

    private void ReadHand(XRHand hand, HandTracker tracker, float deltaTime)
    {
        if (!hand.isTracked ||
            !TryGetJointPosition(hand, XRHandJointID.ThumbTip, out Vector3 thumbTip) ||
            !TryGetJointPosition(hand, XRHandJointID.IndexTip, out Vector3 indexTip) ||
            !TryGetJointPosition(hand, XRHandJointID.MiddleTip, out Vector3 middleTip) ||
            !TryGetJointPosition(hand, XRHandJointID.RingTip, out Vector3 ringTip) ||
            !TryGetJointPosition(hand, XRHandJointID.LittleTip, out Vector3 littleTip) ||
            !TryGetJointPosition(hand, XRHandJointID.Palm, out Vector3 palm))
        {
            tracker.Clear();
            return;
        }

        // Punho primeiro: com o punho fechado o polegar fica perto do indicador e
        // seria confundido com pinça. Usamos só médio, anelar e mindinho.
        tracker.CurlDistance = (Vector3.Distance(middleTip, palm) +
                                Vector3.Distance(ringTip, palm) +
                                Vector3.Distance(littleTip, palm)) / 3f;
        tracker.Fist = tracker.CurlDistance < (tracker.Fist ? fistOffDistance : fistOnDistance);

        tracker.PinchDistance = Vector3.Distance(thumbTip, indexTip);
        tracker.Pinching = !tracker.Fist &&
                           tracker.PinchDistance < (tracker.Pinching ? pinchOffDistance : pinchOnDistance);

        Vector3 pinchWorld = ToWorld((thumbTip + indexTip) * 0.5f);
        Vector3 palmWorld = ToWorld(palm);

        if (tracker.HasSmoothedPoints)
        {
            float blend = 1f - Mathf.Exp(-smoothingRate * deltaTime);
            tracker.PinchPoint = Vector3.Lerp(tracker.PinchPoint, pinchWorld, blend);
            tracker.PalmPoint = Vector3.Lerp(tracker.PalmPoint, palmWorld, blend);
        }
        else
        {
            tracker.PinchPoint = pinchWorld;
            tracker.PalmPoint = palmWorld;
            tracker.HasSmoothedPoints = true;
        }

        tracker.Tracked = true;
    }

    private static bool TryGetJointPosition(XRHand hand, XRHandJointID jointId, out Vector3 position)
    {
        if (hand.GetJoint(jointId).TryGetPose(out Pose pose))
        {
            position = pose.position;
            return true;
        }

        position = default;
        return false;
    }

    /// <summary>As poses do XR Hands vêm no espaço de tracking; convertemos para o mundo.</summary>
    private Vector3 ToWorld(Vector3 trackingSpacePosition)
    {
        Transform origin = viewer.TrackingOrigin;
        return origin != null ? origin.TransformPoint(trackingSpacePosition) : trackingSpacePosition;
    }

    // ---------------------------------------------------------------- máquina de gestos

    private void UpdateGesture(float deltaTime)
    {
        Gesture desired;
        HandTracker desiredHand = null;

        if (leftHand.Pinching && rightHand.Pinching)
        {
            desired = Gesture.Scale;
        }
        else if (leftHand.Pinching || rightHand.Pinching)
        {
            desired = Gesture.Rotate;
            desiredHand = leftHand.Pinching ? leftHand : rightHand;
        }
        else if (leftHand.Fist && rightHand.Fist)
        {
            desired = Gesture.ResetHold;
        }
        else if (leftHand.Fist || rightHand.Fist)
        {
            desired = Gesture.Move;
            desiredHand = leftHand.Fist ? leftHand : rightHand;
        }
        else
        {
            desired = Gesture.None;
        }

        if (desired != currentGesture || desiredHand != activeHand)
        {
            currentGesture = desired;
            activeHand = desiredHand;
            BeginGesture();
        }

        switch (currentGesture)
        {
            case Gesture.Rotate:
                ApplyRotate();
                break;
            case Gesture.Scale:
                ApplyScale();
                break;
            case Gesture.Move:
                ApplyMove();
                break;
            case Gesture.ResetHold:
                ApplyResetHold(deltaTime);
                break;
        }
    }

    /// <summary>Guarda o ponto de partida do gesto para evitar saltos ao trocar de gesto.</summary>
    private void BeginGesture()
    {
        Transform model = viewer.ModelRoot;

        switch (currentGesture)
        {
            case Gesture.Rotate:
                lastPoint = activeHand.PinchPoint;
                break;

            case Gesture.Move:
                lastPoint = activeHand.PalmPoint;
                break;

            case Gesture.Scale:
                Vector3 between = rightHand.PinchPoint - leftHand.PinchPoint;
                startDistance = Mathf.Max(between.magnitude, 0.05f);
                startScale = model.localScale.x;
                startRotation = model.rotation;
                startFlatVector = Vector3.ProjectOnPlane(between, Vector3.up);
                break;

            case Gesture.ResetHold:
                resetHoldTimer = 0f;
                resetLatched = false;
                break;
        }
    }

    private void ApplyRotate()
    {
        Vector3 delta = activeHand.PinchPoint - lastPoint;
        if (delta.magnitude < rotationDeadzone)
        {
            return; // ignora tremor da mão; o movimento lento se acumula até passar o limite
        }

        lastPoint = activeHand.PinchPoint;

        Vector3 cameraRight = Vector3.ProjectOnPlane(viewerCamera.transform.right, Vector3.up);
        cameraRight = cameraRight.sqrMagnitude > 0.0001f ? cameraRight.normalized : Vector3.right;

        // Mesma convenção do mouse: mão para a direita gira a face visível para a direita;
        // mão para cima inclina a face visível para cima.
        float yaw = -Vector3.Dot(delta, cameraRight) * rotationDegreesPerMeter;
        float pitch = Vector3.Dot(delta, Vector3.up) * rotationDegreesPerMeter;

        Transform model = viewer.ModelRoot;
        model.Rotate(Vector3.up, yaw, Space.World);
        model.Rotate(cameraRight, pitch, Space.World);
    }

    private void ApplyScale()
    {
        Transform model = viewer.ModelRoot;
        Vector3 between = rightHand.PinchPoint - leftHand.PinchPoint;

        float scale = Mathf.Clamp(startScale * (between.magnitude / startDistance), minScale, maxScale);
        model.localScale = Vector3.one * scale;

        if (!twistWithTwoHands)
        {
            return;
        }

        Vector3 flat = Vector3.ProjectOnPlane(between, Vector3.up);
        if (startFlatVector.sqrMagnitude > 0.0004f && flat.sqrMagnitude > 0.0004f)
        {
            float twist = Vector3.SignedAngle(startFlatVector, flat, Vector3.up);
            model.rotation = Quaternion.AngleAxis(twist, Vector3.up) * startRotation;
        }
    }

    private void ApplyMove()
    {
        Vector3 delta = activeHand.PalmPoint - lastPoint;
        lastPoint = activeHand.PalmPoint;

        Transform model = viewer.ModelRoot;
        Vector3 position = model.position + delta * moveGain;

        Vector3 cameraPosition = viewerCamera.transform.position;
        Vector3 offset = position - cameraPosition;
        float distance = offset.magnitude;
        if (distance > 0.001f)
        {
            position = cameraPosition + offset / distance * Mathf.Clamp(distance, minDistanceFromCamera, maxDistanceFromCamera);
        }

        model.position = position;
    }

    private void ApplyResetHold(float deltaTime)
    {
        if (resetLatched)
        {
            return;
        }

        resetHoldTimer += deltaTime;
        if (resetHoldTimer >= resetHoldSeconds)
        {
            viewer.ResetView();
            resetLatched = true;
        }
    }

    // ---------------------------------------------------------------- feedback no HUD

    private void RefreshStatus()
    {
        if (statusText == null || Time.unscaledTime < nextStatusTime)
        {
            return;
        }

        nextStatusTime = Time.unscaledTime + 0.1f;

        string message;
        if (!leftHand.Tracked && !rightHand.Tracked)
        {
            message = "Mãos: mostre as mãos ao headset";
        }
        else
        {
            switch (currentGesture)
            {
                case Gesture.Rotate:
                    message = "Girando";
                    break;
                case Gesture.Scale:
                    message = $"Zoom: {viewer.ModelRoot.localScale.x:0.00}x";
                    break;
                case Gesture.Move:
                    message = "Movendo";
                    break;
                case Gesture.ResetHold:
                    message = resetLatched
                        ? "Visualização restaurada"
                        : $"Restaurando... {Mathf.Clamp01(resetHoldTimer / resetHoldSeconds) * 100f:0}%";
                    break;
                default:
                    message = "Mãos: pinça = girar | 2 pinças = zoom | punho = mover | 2 punhos = restaurar";
                    break;
            }
        }

        if (showDebugMetrics)
        {
            message += $"\nE: pinça {leftHand.PinchDistance * 100f:0.0}cm curl {leftHand.CurlDistance * 100f:0.0}cm" +
                       $" | D: pinça {rightHand.PinchDistance * 100f:0.0}cm curl {rightHand.CurlDistance * 100f:0.0}cm";
        }

        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null && statusText.text != message)
        {
            statusText.text = message;
        }
    }
}
