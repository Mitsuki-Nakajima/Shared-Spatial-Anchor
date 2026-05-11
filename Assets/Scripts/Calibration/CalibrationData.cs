using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CalibrationData", menuName = "Spatial Anchor/Calibration Data")]
public class CalibrationData : ScriptableObject
{
    [Header("Calibration Points")]
    [SerializeField] private List<CalibrationPoint> calibrationPoints = new List<CalibrationPoint>();
    
    [Header("Transformation Matrix")]
    [SerializeField] private Matrix4x4 transformationMatrix = Matrix4x4.identity;
    
    [Header("Quality Metrics")]
    [SerializeField] private float averageError = 0f;
    [SerializeField] private float maxError = 0f;
    [SerializeField] private bool isValid = false;
    
    [Header("Metadata")]
    [SerializeField] private string calibrationVersion = "1.0";
    [SerializeField] private long timestamp;
    [SerializeField] private string deviceId;
    
    [Serializable]
    public class CalibrationPoint
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public float confidence;
        public long timestamp;
        
        public CalibrationPoint(Vector3 localPos, Quaternion localRot, Vector3 targetPos, Quaternion targetRot, float conf = 1.0f)
        {
            localPosition = localPos;
            localRotation = localRot;
            targetPosition = targetPos;
            targetRotation = targetRot;
            confidence = conf;
            timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }
    
    public List<CalibrationPoint> CalibrationPoints => calibrationPoints;
    public Matrix4x4 TransformationMatrix => transformationMatrix;
    public float AverageError => averageError;
    public float MaxError => maxError;
    public bool IsValid => isValid;
    public string CalibrationVersion => calibrationVersion;
    public long Timestamp => timestamp;
    public string DeviceId => deviceId;
    
    public void AddCalibrationPoint(CalibrationPoint point)
    {
        calibrationPoints.Add(point);
    }
    
    public void ClearCalibrationPoints()
    {
        calibrationPoints.Clear();
    }
    
    public void SetTransformationMatrix(Matrix4x4 matrix)
    {
        transformationMatrix = matrix;
    }
    
    public void SetQualityMetrics(float avgError, float maxErr, bool valid)
    {
        averageError = avgError;
        maxError = maxErr;
        isValid = valid;
        timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        deviceId = SystemInfo.deviceUniqueIdentifier;
    }
    
    public bool HasMinimumPoints(int minimumRequired = 3)
    {
        return calibrationPoints.Count >= minimumRequired;
    }
    
    public string GetSummary()
    {
        return $"Calibration: {calibrationPoints.Count} points, " +
               $"Avg Error: {averageError:F3}m, " +
               $"Valid: {isValid}, " +
               $"Date: {DateTimeOffset.FromUnixTimeMilliseconds(timestamp):yyyy-MM-dd HH:mm}";
    }
}
