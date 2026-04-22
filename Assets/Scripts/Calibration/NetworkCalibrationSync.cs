using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

public class NetworkCalibrationSync : NetworkBehaviour
{
    public static NetworkCalibrationSync Instance { get; private set; }
    
    [Header("Network Settings")]
    [SerializeField] private bool syncCalibrationOnConnect = true;
    [SerializeField] private bool allowHostCalibrationOnly = true;
    [SerializeField] private float syncInterval = 5.0f;
    
    [Header("Calibration Data")]
    [SerializeField] private CalibrationData sharedCalibrationData;
    
    // Network variables
    private NetworkVariable<CalibrationNetworkData> networkCalibrationData = new NetworkVariable<CalibrationNetworkData>();
    private NetworkVariable<bool> isCalibrationValid = new NetworkVariable<bool>(false);
    private NetworkVariable<float> calibrationQuality = new NetworkVariable<float>(0f);
    
    // Events
    public System.Action<CalibrationNetworkData> OnCalibrationSynced;
    public System.Action<string> OnSyncError;
    public System.Action OnCalibrationInvalidated;
    
    private float lastSyncTime = 0f;
    private CalibrationNetworkData localCalibrationData;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes
        networkCalibrationData.OnValueChanged += OnCalibrationDataChanged;
        isCalibrationValid.OnValueChanged += OnCalibrationValidityChanged;
        calibrationQuality.OnValueChanged += OnCalibrationQualityChanged;
        
        // Request calibration sync if client
        if (IsClient && !IsHost && syncCalibrationOnConnect)
        {
            RequestCalibrationSyncServerRpc();
        }
        
        // Start periodic sync for host
        if (IsHost)
        {
            InvokeRepeating(nameof(PeriodicSync), syncInterval, syncInterval);
        }
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Unsubscribe from events
        networkCalibrationData.OnValueChanged -= OnCalibrationDataChanged;
        isCalibrationValid.OnValueChanged -= OnCalibrationValidityChanged;
        calibrationQuality.OnValueChanged -= OnCalibrationQualityChanged;
        
        // Cancel periodic sync
        if (IsHost)
        {
            CancelInvoke(nameof(PeriodicSync));
        }
    }
    
    #region Server RPCs
    
    [ServerRpc]
    public void SubmitCalibrationServerRpc(CalibrationNetworkData calibrationData, ServerRpcParams rpcParams = default)
    {
        // Check if host-only calibration is enabled
        if (allowHostCalibrationOnly && rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("Non-host attempted to submit calibration. Rejecting.");
            return;
        }
        
        // Validate calibration data
        if (!ValidateCalibrationData(calibrationData))
        {
            Debug.LogError("Invalid calibration data received from client.");
            return;
        }
        
        // Update network variables
        networkCalibrationData.Value = calibrationData;
        isCalibrationValid.Value = calibrationData.isValid;
        calibrationQuality.Value = calibrationData.averageError;
        
        // Apply to shared calibration data
        ApplyNetworkCalibration(calibrationData);
        
        Debug.Log($"Calibration synced from client {rpcParams.Receive.SenderClientId}");
    }
    
    [ServerRpc]
    public void RequestCalibrationSyncServerRpc(ServerRpcParams rpcParams = default)
    {
        if (networkCalibrationData.Value.isValid)
        {
            // Send current calibration to requesting client
            SendCalibrationToClientClientRpc(networkCalibrationData.Value, rpcParams.Receive.SenderClientId);
        }
    }
    
    [ServerRpc]
    public void InvalidateCalibrationServerRpc(ServerRpcParams rpcParams = default)
    {
        // Check if host-only invalidation is enabled
        if (allowHostCalibrationOnly && rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("Non-host attempted to invalidate calibration. Rejecting.");
            return;
        }
        
        // Invalidate calibration
        networkCalibrationData.Value = new CalibrationNetworkData();
        isCalibrationValid.Value = false;
        calibrationQuality.Value = 0f;
        
        Debug.Log("Calibration invalidated by host");
    }
    
    #endregion
    
    #region Client RPCs
    
    [ClientRpc]
    private void SendCalibrationToClientClientRpc(CalibrationNetworkData calibrationData, ulong targetClientId)
    {
        // Only apply to the target client
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            ApplyNetworkCalibration(calibrationData);
            OnCalibrationSynced?.Invoke(calibrationData);
            
            Debug.Log("Received calibration sync from host");
        }
    }
    
    [ClientRpc]
    private void NotifyCalibrationInvalidatedClientRpc()
    {
        OnCalibrationInvalidated?.Invoke();
        Debug.Log("Calibration invalidated by host");
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Submit local calibration to network (host only)
    /// </summary>
    public bool SubmitCalibration(CalibrationData localData)
    {
        if (!IsHost && allowHostCalibrationOnly)
        {
            OnSyncError?.Invoke("Only host can submit calibration");
            return false;
        }
        
        if (!IsSpawned)
        {
            OnSyncError?.Invoke("Network object not spawned");
            return false;
        }
        
        // Convert local calibration data to network format
        CalibrationNetworkData networkData = ConvertToNetworkData(localData);
        
        // Submit to server
        SubmitCalibrationServerRpc(networkData);
        
        return true;
    }
    
    /// <summary>
    /// Force calibration sync from host
    /// </summary>
    public void ForceSync()
    {
        if (IsClient && !IsHost)
        {
            RequestCalibrationSyncServerRpc();
        }
    }
    
    /// <summary>
    /// Invalidate calibration across all clients
    /// </summary>
    public void InvalidateCalibration()
    {
        if (!IsHost && allowHostCalibrationOnly)
        {
            OnSyncError?.Invoke("Only host can invalidate calibration");
            return;
        }
        
        InvalidateCalibrationServerRpc();
    }
    
    /// <summary>
    /// Check if network calibration is valid
    /// </summary>
    public bool IsNetworkCalibrationValid()
    {
        return isCalibrationValid.Value;
    }
    
    /// <summary>
    /// Get network calibration quality
    /// </summary>
    public float GetNetworkCalibrationQuality()
    {
        return calibrationQuality.Value;
    }
    
    #endregion
    
    #region Private Methods
    
    private void PeriodicSync()
    {
        if (!IsHost || !isCalibrationValid.Value) return;
        
        // Periodic sync to ensure all clients have latest calibration
        lastSyncTime = Time.time;
        
        // Broadcast to all clients
        SendCalibrationToAllClientsClientRpc(networkCalibrationData.Value);
    }
    
    [ClientRpc]
    private void SendCalibrationToAllClientsClientRpc(CalibrationNetworkData calibrationData)
    {
        if (!IsHost) // Don't apply to host
        {
            ApplyNetworkCalibration(calibrationData);
            OnCalibrationSynced?.Invoke(calibrationData);
        }
    }
    
    private void OnCalibrationDataChanged(CalibrationNetworkData previousValue, CalibrationNetworkData newValue)
    {
        if (!IsHost) // Only clients need to respond to changes
        {
            ApplyNetworkCalibration(newValue);
            OnCalibrationSynced?.Invoke(newValue);
        }
    }
    
    private void OnCalibrationValidityChanged(bool previousValue, bool newValue)
    {
        if (!newValue && previousValue)
        {
            OnCalibrationInvalidated?.Invoke();
        }
    }
    
    private void OnCalibrationQualityChanged(float previousValue, float newValue)
    {
        // Could be used for UI updates or quality monitoring
    }
    
    private bool ValidateCalibrationData(CalibrationNetworkData data)
    {
        // Basic validation
        if (data.calibrationPoints == null || data.calibrationPoints.Count < 3)
            return false;
            
        if (data.averageError > 0.1f) // More than 10cm error is too high
            return false;
            
        return true;
    }
    
    private void ApplyNetworkCalibration(CalibrationNetworkData networkData)
    {
        if (sharedCalibrationData == null)
        {
            Debug.LogError("Shared calibration data not assigned");
            return;
        }
        
        // Clear existing points
        sharedCalibrationData.ClearCalibrationPoints();
        
        // Add network points
        foreach (var networkPoint in networkData.calibrationPoints)
        {
            var point = new CalibrationData.CalibrationPoint(
                networkPoint.localPosition,
                networkPoint.localRotation,
                networkPoint.targetPosition,
                networkPoint.targetRotation,
                networkPoint.confidence
            );
            sharedCalibrationData.AddCalibrationPoint(point);
        }
        
        // Apply transformation matrix
        sharedCalibrationData.SetTransformationMatrix(networkData.transformationMatrix);
        sharedCalibrationData.SetQualityMetrics(
            networkData.averageError,
            networkData.maxError,
            networkData.isValid
        );
        
        localCalibrationData = networkData;
    }
    
    private CalibrationNetworkData ConvertToNetworkData(CalibrationData localData)
    {
        CalibrationNetworkData networkData = new CalibrationNetworkData();
        
        // Convert calibration points
        networkData.calibrationPoints = new List<CalibrationPointNetworkData>();
        foreach (var point in localData.CalibrationPoints)
        {
            networkData.calibrationPoints.Add(new CalibrationPointNetworkData
            {
                localPosition = point.localPosition,
                localRotation = point.localRotation,
                targetPosition = point.targetPosition,
                targetRotation = point.targetRotation,
                confidence = point.confidence,
                timestamp = point.timestamp
            });
        }
        
        // Copy other data
        networkData.transformationMatrix = localData.TransformationMatrix;
        networkData.averageError = localData.AverageError;
        networkData.maxError = localData.MaxError;
        networkData.isValid = localData.IsValid;
        networkData.calibrationVersion = localData.CalibrationVersion;
        networkData.timestamp = localData.Timestamp;
        networkData.deviceId = localData.DeviceId;
        
        return networkData;
    }
    
    #endregion
    
    #region Network Data Structures
    
    [System.Serializable]
    public struct CalibrationNetworkData : INetworkSerializable
    {
        public List<CalibrationPointNetworkData> calibrationPoints;
        public Matrix4x4 transformationMatrix;
        public float averageError;
        public float maxError;
        public bool isValid;
        public string calibrationVersion;
        public long timestamp;
        public string deviceId;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref calibrationPoints);
            serializer.SerializeValue(ref transformationMatrix);
            serializer.SerializeValue(ref averageError);
            serializer.SerializeValue(ref maxError);
            serializer.SerializeValue(ref isValid);
            serializer.SerializeValue(ref calibrationVersion);
            serializer.SerializeValue(ref timestamp);
            serializer.SerializeValue(ref deviceId);
        }
    }
    
    [System.Serializable]
    public struct CalibrationPointNetworkData : INetworkSerializable
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public float confidence;
        public long timestamp;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref localPosition);
            serializer.SerializeValue(ref localRotation);
            serializer.SerializeValue(ref targetPosition);
            serializer.SerializeValue(ref targetRotation);
            serializer.SerializeValue(ref confidence);
            serializer.SerializeValue(ref timestamp);
        }
    }
    
    #endregion
    
    #region Properties
    
    public bool IsHostCalibrationOnly => allowHostCalibrationOnly;
    public CalibrationData SharedCalibrationData => sharedCalibrationData;
    public CalibrationNetworkData LocalCalibrationData => localCalibrationData;
    
    #endregion
}
