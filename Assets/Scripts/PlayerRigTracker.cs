using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerRigTracker : NetworkBehaviour
{
    [Header("Local XR tracked transforms")]
    [SerializeField] private Transform localHead;
    [SerializeField] private Transform localLeftHand;
    [SerializeField] private Transform localRightHand;

    [Header("Avatar visuals")]
    [SerializeField] private Transform avatarHead;
    [SerializeField] private Transform avatarLeftHand;
    [SerializeField] private Transform avatarRightHand;
    
    [Header("Calibration Settings")]
    [SerializeField] private bool useEnhancedCalibration = true;
    [SerializeField] private bool fallbackToLegacy = true;
    [SerializeField] private float updateRate = 90f;
    [SerializeField] private bool enableCalibrationWarnings = true;
    
    [Header("Network Sync")]
    [SerializeField] private bool networkSyncTransforms = true;
    [SerializeField] private float networkUpdateRate = 30f;
    
    // Network variables for transform synchronization
    private NetworkVariable<NetworkTransformData> headTransform = new NetworkVariable<NetworkTransformData>();
    private NetworkVariable<NetworkTransformData> leftHandTransform = new NetworkVariable<NetworkTransformData>();
    private NetworkVariable<NetworkTransformData> rightHandTransform = new NetworkVariable<NetworkTransformData>();
    
    // State tracking
    private float lastUpdateTime = 0f;
    private float lastNetworkUpdateTime = 0f;
    private bool isCalibrationValid = false;
    private CalibrationManager calibrationManager;
    private SharedSpaceManager sharedSpaceManager;
    
    // Events
    public System.Action<string> OnCalibrationWarning;
    public System.Action OnCalibrationRestored;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsOwner) return;

        InitializeComponents();
        SetupXRTracking();
        SetupAvatarTransforms();
        SubscribeToCalibrationEvents();
        
        Debug.Log("PlayerRigTracker initialized for owner");
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        UnsubscribeFromCalibrationEvents();
    }
    
    private void InitializeComponents()
    {
        calibrationManager = CalibrationManager.Instance;
        sharedSpaceManager = SharedSpaceManager.Instance;
        
        if (calibrationManager == null)
        {
            Debug.LogWarning("CalibrationManager not found. Calibration will not work.");
        }
        
        if (sharedSpaceManager == null)
        {
            Debug.LogWarning("SharedSpaceManager not found. Using direct transformation.");
        }
        
        // Check calibration status
        CheckCalibrationStatus();
    }
    
    private void SetupXRTracking()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (VR)");
        if (xrOrigin == null)
        {
            Debug.LogError("XR Origin (VR) not found in scene.");
            return;
        }

        Transform mainCamera = xrOrigin.transform.Find("Camera Offset/Main Camera");
        Transform leftHand = xrOrigin.transform.Find("Camera Offset/LeftHand");
        Transform rightHand = xrOrigin.transform.Find("Camera Offset/RightHand");

        if (mainCamera == null || leftHand == null || rightHand == null)
        {
            Debug.LogError("Could not find Main Camera / LeftHand / RightHand under XR Origin (VR).");
            return;
        }

        localHead = mainCamera;
        localLeftHand = leftHand;
        localRightHand = rightHand;
        
        Debug.Log("XR tracking transforms assigned successfully");
    }
    
    private void SetupAvatarTransforms()
    {
        Transform head = transform.Find("Head");
        Transform lHand = transform.Find("LeftHand");
        Transform rHand = transform.Find("RightHand");

        if (head == null || lHand == null || rHand == null)
        {
            Debug.LogError("Could not find Head / LeftHand / RightHand under PlayerAvatar.");
            return;
        }

        avatarHead = head;
        avatarLeftHand = lHand;
        avatarRightHand = rHand;
        
        Debug.Log("Avatar transforms assigned successfully");
    }
    
    private void SubscribeToCalibrationEvents()
    {
        if (calibrationManager != null)
        {
            calibrationManager.OnCalibrationValidated += OnCalibrationValidated;
            calibrationManager.OnCalibrationError += OnCalibrationError;
            calibrationManager.OnCalibrationReset += OnCalibrationReset;
        }
    }
    
    private void UnsubscribeFromCalibrationEvents()
    {
        if (calibrationManager != null)
        {
            calibrationManager.OnCalibrationValidated -= OnCalibrationValidated;
            calibrationManager.OnCalibrationError -= OnCalibrationError;
            calibrationManager.OnCalibrationReset -= OnCalibrationReset;
        }
    }
    
    private void Update()
    {
        if (!IsOwner) return;
        
        // Rate limiting for local updates
        if (Time.time - lastUpdateTime < 1f / updateRate)
            return;
            
        lastUpdateTime = Time.time;
        
        // Check if we have valid tracking transforms
        if (localHead == null || avatarHead == null)
        {
            Debug.LogWarning("Missing tracking transforms, skipping update");
            return;
        }
        
        // Update all tracked parts
        UpdateTrackedPart(localHead, avatarHead, TrackedPartType.Head);
        UpdateTrackedPart(localLeftHand, avatarLeftHand, TrackedPartType.LeftHand);
        UpdateTrackedPart(localRightHand, avatarRightHand, TrackedPartType.RightHand);
        
        // Network sync at lower rate
        if (networkSyncTransforms && Time.time - lastNetworkUpdateTime >= 1f / networkUpdateRate)
        {
            UpdateNetworkTransforms();
            lastNetworkUpdateTime = Time.time;
        }
    }
    
    private void UpdateTrackedPart(Transform localPart, Transform avatarPart, TrackedPartType partType)
    {
        if (localPart == null || avatarPart == null) return;

        Pose localPose = new Pose(localPart.position, localPart.rotation);
        Pose calibratedPose = ApplyCalibration(localPose);
        
        avatarPart.position = calibratedPose.position;
        avatarPart.rotation = calibratedPose.rotation;
    }
    
    private Pose ApplyCalibration(Pose localPose)
    {
        // Use enhanced calibration if available and enabled
        if (useEnhancedCalibration && calibrationManager != null && calibrationManager.IsCalibrationValid())
        {
            return calibrationManager.ApplyCalibration(localPose);
        }
        
        // Fallback to legacy calibration
        if (fallbackToLegacy && calibrationManager != null)
        {
            Matrix4x4 legacyAlignment = calibrationManager.GetAlignmentMatrix();
            if (legacyAlignment != Matrix4x4.identity)
            {
                Matrix4x4 localMatrix = Matrix4x4.TRS(localPose.position, localPose.rotation, Vector3.one);
                Matrix4x4 calibratedMatrix = legacyAlignment * localMatrix;
                return new Pose(calibratedMatrix.GetColumn(3), calibratedMatrix.rotation);
            }
        }
        
        // Fallback to SharedSpaceManager (original behavior)
        if (sharedSpaceManager != null)
        {
            Matrix4x4 alignment = calibrationManager?.GetAlignmentMatrix() ?? Matrix4x4.identity;
            return sharedSpaceManager.ConvertLocalToShared(localPose, alignment);
        }
        
        // No calibration available
        if (!isCalibrationValid && enableCalibrationWarnings)
        {
            Debug.LogWarning("No valid calibration available. Using uncalibrated transforms.");
            isCalibrationValid = true; // Prevent spamming warnings
        }
        
        return localPose;
    }
    
    private void UpdateNetworkTransforms()
    {
        if (!IsSpawned) return;
        
        // Update network variables
        headTransform.Value = new NetworkTransformData(avatarHead.position, avatarHead.rotation);
        leftHandTransform.Value = new NetworkTransformData(avatarLeftHand.position, avatarLeftHand.rotation);
        rightHandTransform.Value = new NetworkTransformData(avatarRightHand.position, avatarRightHand.rotation);
    }
    
    private void CheckCalibrationStatus()
    {
        if (calibrationManager != null)
        {
            isCalibrationValid = calibrationManager.IsCalibrationValid();
            
            if (!isCalibrationValid && enableCalibrationWarnings)
            {
                string warning = "Calibration is not valid. Avatar positioning may be inaccurate.";
                Debug.LogWarning(warning);
                OnCalibrationWarning?.Invoke(warning);
            }
        }
    }
    
    #region Calibration Event Handlers
    
    private void OnCalibrationValidated(CalibrationValidator.ValidationResult result)
    {
        isCalibrationValid = true;
        Debug.Log($"Calibration validated with {result.averageError:F3}m average error");
        OnCalibrationRestored?.Invoke();
    }
    
    private void OnCalibrationError(string errorMessage)
    {
        isCalibrationValid = false;
        Debug.LogError($"Calibration error: {errorMessage}");
        OnCalibrationWarning?.Invoke($"Calibration error: {errorMessage}");
    }
    
    private void OnCalibrationReset()
    {
        isCalibrationValid = false;
        Debug.Log("Calibration reset - avatar positioning may be inaccurate");
        OnCalibrationWarning?.Invoke("Calibration reset - please recalibrate");
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Force refresh of calibration status
    /// </summary>
    public void RefreshCalibrationStatus()
    {
        CheckCalibrationStatus();
    }
    
    /// <summary>
    /// Get current calibration status
    /// </summary>
    public bool GetCalibrationStatus()
    {
        return isCalibrationValid;
    }
    
    /// <summary>
    /// Get calibration information
    /// </summary>
    public string GetCalibrationInfo()
    {
        if (calibrationManager != null)
        {
            return calibrationManager.GetCalibrationInfo();
        }
        return "CalibrationManager not available";
    }
    
    #endregion
    
    #region Network Data Structures
    
    private enum TrackedPartType
    {
        Head,
        LeftHand,
        RightHand
    }
    
    [System.Serializable]
    public struct NetworkTransformData : INetworkSerializable
    {
        public Vector3 position;
        public Quaternion rotation;
        
        public NetworkTransformData(Vector3 pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
        }
    }
    
    #endregion
    
    #region Properties
    
    public bool UseEnhancedCalibration => useEnhancedCalibration;
    public bool FallbackToLegacy => fallbackToLegacy;
    public bool IsCalibrationValid => isCalibrationValid;
    public float UpdateRate => updateRate;
    public float NetworkUpdateRate => networkUpdateRate;
    
    #endregion
}