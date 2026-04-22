using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance;
    
    [Header("Calibration Configuration")]
    [SerializeField] private CalibrationData calibrationData;
    [SerializeField] private CalibrationValidator validator;
    [SerializeField] private bool useMultiPointCalibration = true;
    [SerializeField] private int minimumCalibrationPoints = 3;
    [SerializeField] private bool enablePersistence = true;
    
    [Header("Legacy Support")]
    [SerializeField] private bool enableBackwardCompatibility = true;
    
    // Legacy single-point calibration (for backward compatibility)
    private Matrix4x4 legacyAlignmentMatrix = Matrix4x4.identity;
    
    // Multi-point calibration state
    private bool isMultiPointCalibrated = false;
    private CalibrationValidator.ValidationResult lastValidationResult;
    
    public System.Action<CalibrationValidator.ValidationResult> OnCalibrationValidated;
    public System.Action<string> OnCalibrationError;
    public System.Action OnCalibrationReset;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        if (validator == null)
            validator = new CalibrationValidator();
            
        if (enablePersistence)
            LoadCalibration();
    }
    
    #region Legacy Single-Point Calibration (Backward Compatibility)
    
    /// <summary>
    /// Legacy single-point calibration for backward compatibility
    /// Only considers Y-axis rotation as per original implementation
    /// </summary>
    public void Calibrate(Transform headsetTransform, Transform targetSpawnInMakerspace)
    {
        if (!enableBackwardCompatibility)
        {
            Debug.LogWarning("Legacy calibration is disabled. Use multi-point calibration instead.");
            return;
        }
        
        Matrix4x4 localHead = Matrix4x4.TRS(
            headsetTransform.position,
            Quaternion.Euler(0f, headsetTransform.eulerAngles.y, 0f),
            Vector3.one
        );

        Matrix4x4 target = Matrix4x4.TRS(
            targetSpawnInMakerspace.position,
            targetSpawnInMakerspace.rotation,
            Vector3.one
        );

        legacyAlignmentMatrix = target * localHead.inverse;
        isMultiPointCalibrated = false;
        
        Debug.Log("Legacy single-point calibration completed");
    }
    
    #endregion
    
    #region Multi-Point Calibration System
    
    /// <summary>
    /// Add a calibration point for multi-point calibration
    /// </summary>
    public bool CollectCalibrationPoint(Transform localTransform, Transform targetTransform, float confidence = 1.0f)
    {
        if (!useMultiPointCalibration)
        {
            OnCalibrationError?.Invoke("Multi-point calibration is disabled");
            return false;
        }
        
        if (calibrationData == null)
        {
            OnCalibrationError?.Invoke("CalibrationData not assigned");
            return false;
        }
        
        var point = new CalibrationData.CalibrationPoint(
            localTransform.position,
            localTransform.rotation,
            targetTransform.position,
            targetTransform.rotation,
            confidence
        );
        
        calibrationData.AddCalibrationPoint(point);
        
        Debug.Log($"Added calibration point {calibrationData.CalibrationPoints.Count}: {point.localPosition} -> {point.targetPosition}");
        
        return true;
    }
    
    /// <summary>
    /// Compute transformation matrix using Kabsch algorithm for optimal alignment
    /// </summary>
    public bool ComputeTransformation()
    {
        if (!useMultiPointCalibration || calibrationData == null)
            return false;
            
        if (!calibrationData.HasMinimumPoints(minimumCalibrationPoints))
        {
            OnCalibrationError?.Invoke($"Insufficient calibration points. Need at least {minimumRequiredPoints}, have {calibrationData.CalibrationPoints.Count}");
            return false;
        }
        
        // Check for degenerate configuration
        if (validator.CheckDegenerateConfiguration(calibrationData))
        {
            OnCalibrationError?.Invoke("Degenerate calibration configuration detected. Points are collinear or too close together.");
            return false;
        }
        
        // Compute optimal transformation using Kabsch algorithm
        Matrix4x4 transformationMatrix = ComputeKabschTransformation(calibrationData.CalibrationPoints);
        
        // Validate the computed transformation
        lastValidationResult = validator.ValidateCalibration(calibrationData, transformationMatrix);
        
        if (lastValidationResult.isValid)
        {
            calibrationData.SetTransformationMatrix(transformationMatrix);
            calibrationData.SetQualityMetrics(
                lastValidationResult.averageError,
                lastValidationResult.maxError,
                true
            );
            
            isMultiPointCalibrated = true;
            
            if (enablePersistence)
                SaveCalibration();
                
            OnCalibrationValidated?.Invoke(lastValidationResult);
            
            Debug.Log($"Multi-point calibration completed successfully. Average error: {lastValidationResult.averageError:F3}m");
            return true;
        }
        else
        {
            OnCalibrationError?.Invoke($"Calibration validation failed. Average error: {lastValidationResult.averageError:F3}m (threshold: {validator.maxAverageErrorThreshold:F3}m)");
            return false;
        }
    }
    
    /// <summary>
    /// Kabsch algorithm implementation for optimal rigid transformation
    /// </summary>
    private Matrix4x4 ComputeKabschTransformation(List<CalibrationData.CalibrationPoint> points)
    {
        int n = points.Count;
        
        // Calculate centroids
        Vector3 localCentroid = Vector3.zero;
        Vector3 targetCentroid = Vector3.zero;
        
        foreach (var point in points)
        {
            localCentroid += point.localPosition;
            targetCentroid += point.targetPosition;
        }
        
        localCentroid /= n;
        targetCentroid /= n;
        
        // Compute covariance matrix
        Matrix3x3 H = Matrix3x3.zero;
        
        for (int i = 0; i < n; i++)
        {
            Vector3 p = points[i].localPosition - localCentroid;
            Vector3 q = points[i].targetPosition - targetCentroid;
            
            H.m00 += p.x * q.x; H.m01 += p.x * q.y; H.m02 += p.x * q.z;
            H.m10 += p.y * q.x; H.m11 += p.y * q.y; H.m12 += p.y * q.z;
            H.m20 += p.z * q.x; H.m21 += p.z * q.y; H.m22 += p.z * q.z;
        }
        
        // Singular value decomposition
        var svd = SingularValueDecomposition(H);
        
        // Calculate rotation matrix
        Matrix3x3 R = svd.V.transpose * svd.U.transpose;
        
        // Ensure proper rotation (determinant = 1)
        if (R.determinant < 0)
        {
            var V = svd.V;
            V.SetColumn(2, -V.GetColumn(2));
            R = V.transpose * svd.U.transpose;
        }
        
        // Calculate translation
        Vector3 translation = targetCentroid - R.MultiplyPoint(localCentroid);
        
        // Create transformation matrix
        Matrix4x4 transformation = Matrix4x4.identity;
        transformation.SetTRS(translation, Quaternion.LookRotation(R.GetColumn(2), R.GetColumn(1)), Vector3.one);
        
        return transformation;
    }
    
    /// <summary>
    /// Simple SVD implementation for 3x3 matrices
    /// </summary>
    private (Matrix3x3 U, Matrix3x3 V) SingularValueDecomposition(Matrix3x3 M)
    {
        // For simplicity, using Unity's built-in matrix operations
        // In production, consider using a more robust SVD implementation
        Matrix4x4 M4 = Matrix4x4.identity;
        M4.SetRow(0, new Vector4(M.m00, M.m01, M.m02, 0));
        M4.SetRow(1, new Vector4(M.m10, M.m11, M.m12, 0));
        M4.SetRow(2, new Vector4(M.m20, M.m21, M.m22, 0));
        
        // Use QR decomposition as approximation (simplified approach)
        // In production, replace with proper SVD algorithm
        var (Q, R) = QRDecomposition(M4);
        
        Matrix3x3 U = Matrix3x3.identity;
        Matrix3x3 V = Matrix3x3.identity;
        
        // Extract 3x3 parts
        for (int i = 0; i < 3; i++)
        {
            U.SetRow(i, Q.GetRow(i));
            V.SetRow(i, R.GetRow(i));
        }
        
        return (U, V);
    }
    
    private (Matrix4x4 Q, Matrix4x4 R) QRDecomposition(Matrix4x4 M)
    {
        // Simplified QR decomposition using Gram-Schmidt
        Vector3[] cols = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            cols[i] = M.GetColumn(i);
        }
        
        // Gram-Schmidt orthogonalization
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < i; j++)
            {
                float dot = Vector3.Dot(cols[i], cols[j]);
                cols[i] -= dot * cols[j];
            }
            cols[i] = cols[i].normalized;
        }
        
        Matrix4x4 Q = Matrix4x4.identity;
        for (int i = 0; i < 3; i++)
        {
            Q.SetColumn(i, cols[i]);
        }
        
        Matrix4x4 R = Q.transpose * M;
        
        return (Q, R);
    }
    
    #endregion
    
    #region Calibration Management
    
    /// <summary>
    /// Reset all calibration data
    /// </summary>
    public void ResetCalibration()
    {
        if (calibrationData != null)
            calibrationData.ClearCalibrationPoints();
            
        legacyAlignmentMatrix = Matrix4x4.identity;
        isMultiPointCalibrated = false;
        lastValidationResult = new CalibrationValidator.ValidationResult(false, 0, 0, new List<float>());
        
        if (enablePersistence)
            SaveCalibration();
            
        OnCalibrationReset?.Invoke();
        
        Debug.Log("Calibration reset");
    }
    
    /// <summary>
    /// Apply calibration transformation to a pose
    /// </summary>
    public Pose ApplyCalibration(Pose localPose)
    {
        Matrix4x4 alignmentMatrix = GetAlignmentMatrix();
        
        if (alignmentMatrix == Matrix4x4.identity)
            return localPose;
            
        Matrix4x4 localMatrix = Matrix4x4.TRS(localPose.position, localPose.rotation, Vector3.one);
        Matrix4x4 calibratedMatrix = alignmentMatrix * localMatrix;
        
        return new Pose(calibratedMatrix.GetColumn(3), calibratedMatrix.rotation);
    }
    
    /// <summary>
    /// Get the appropriate alignment matrix based on calibration state
    /// </summary>
    public Matrix4x4 GetAlignmentMatrix()
    {
        if (useMultiPointCalibration && isMultiPointCalibrated && calibrationData != null)
            return calibrationData.TransformationMatrix;
        else if (enableBackwardCompatibility)
            return legacyAlignmentMatrix;
        else
            return Matrix4x4.identity;
    }
    
    /// <summary>
    /// Check if calibration is valid and ready to use
    /// </summary>
    public bool IsCalibrationValid()
    {
        if (useMultiPointCalibration)
            return isMultiPointCalibrated && lastValidationResult.isValid;
        else if (enableBackwardCompatibility)
            return legacyAlignmentMatrix != Matrix4x4.identity;
        else
            return false;
    }
    
    /// <summary>
    /// Get calibration quality information
    /// </summary>
    public string GetCalibrationInfo()
    {
        if (useMultiPointCalibration && isMultiPointCalibrated)
        {
            return calibrationData?.GetSummary() ?? "No calibration data";
        }
        else if (enableBackwardCompatibility && legacyAlignmentMatrix != Matrix4x4.identity)
        {
            return "Legacy single-point calibration active";
        }
        else
        {
            return "No calibration active";
        }
    }
    
    #endregion
    
    #region Persistence
    
    private const string CALIBRATION_KEY = "SpatialAnchor_Calibration";
    
    private void SaveCalibration()
    {
        if (calibrationData == null) return;
        
        try
        {
            string jsonData = JsonUtility.ToJson(calibrationData, true);
            PlayerPrefs.SetString(CALIBRATION_KEY, jsonData);
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save calibration: {e.Message}");
        }
    }
    
    private void LoadCalibration()
    {
        if (calibrationData == null) return;
        
        try
        {
            string jsonData = PlayerPrefs.GetString(CALIBRATION_KEY, "");
            if (!string.IsNullOrEmpty(jsonData))
            {
                JsonUtility.FromJsonOverwrite(jsonData, calibrationData);
                
                if (calibrationData.IsValid)
                {
                    isMultiPointCalibrated = true;
                    lastValidationResult = new CalibrationValidator.ValidationResult(
                        true, 
                        calibrationData.AverageError, 
                        calibrationData.MaxError, 
                        new List<float>()
                    );
                    Debug.Log("Loaded saved calibration: " + calibrationData.GetSummary());
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load calibration: {e.Message}");
        }
    }
    
    #endregion
    
    #region Properties
    
    public CalibrationData CalibrationData => calibrationData;
    public CalibrationValidator Validator => validator;
    public bool UseMultiPointCalibration => useMultiPointCalibration;
    public bool IsMultiPointCalibrated => isMultiPointCalibrated;
    public CalibrationValidator.ValidationResult LastValidationResult => lastValidationResult;
    public int MinimumCalibrationPoints => minimumCalibrationPoints;
    
    #endregion
}