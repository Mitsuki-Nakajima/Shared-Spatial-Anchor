using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class CalibrationUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private GameObject calibrationPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Button captureButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button finishButton;
    [SerializeField] private Slider confidenceSlider;
    [SerializeField] private TextMeshProUGUI confidenceText;
    
    [Header("Calibration Points")]
    [SerializeField] private List<CalibrationPointVisual> calibrationPointVisuals;
    [SerializeField] private Transform calibrationPointParent;
    [SerializeField] private GameObject calibrationPointPrefab;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject alignmentIndicator;
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private float alignmentThreshold = 0.1f;
    
    [Header("Calibration Settings")]
    [SerializeField] private int requiredCalibrationPoints = 5;
    [SerializeField] private float captureDelay = 2.0f;
    [SerializeField] private bool showTrajectory = true;
    
    // State
    private int currentPointIndex = 0;
    private bool isCalibrating = false;
    private bool isCapturing = false;
    private float captureTimer = 0f;
    private List<Vector3> captureTrajectory = new List<Vector3>();
    
    // Events
    public System.Action<int> OnPointCaptured;
    public System.Action OnCalibrationCompleted;
    public System.Action OnCalibrationCancelled;
    
    private void Start()
    {
        InitializeUI();
        HideCalibrationUI();
    }
    
    private void Update()
    {
        if (isCapturing)
        {
            UpdateCaptureProcess();
            UpdateAlignmentIndicator();
        }
    }
    
    private void InitializeUI()
    {
        // Button listeners
        captureButton.onClick.AddListener(StartCapture);
        retryButton.onClick.AddListener(RetryCurrentPoint);
        resetButton.onClick.AddListener(ResetCalibration);
        finishButton.onClick.AddListener(FinishCalibration);
        
        // Confidence slider
        confidenceSlider.onValueChanged.AddListener(UpdateConfidenceText);
        confidenceSlider.minValue = 0.1f;
        confidenceSlider.maxValue = 1.0f;
        confidenceSlider.value = 0.8f;
        
        // Initialize calibration point visuals
        if (calibrationPointVisuals == null || calibrationPointVisuals.Count == 0)
        {
            CreateCalibrationPointVisuals();
        }
        
        UpdateUIState();
    }
    
    private void CreateCalibrationPointVisuals()
    {
        calibrationPointVisuals = new List<CalibrationPointVisual>();
        
        for (int i = 0; i < requiredCalibrationPoints; i++)
        {
            if (calibrationPointPrefab != null)
            {
                GameObject pointObj = Instantiate(calibrationPointPrefab, calibrationPointParent);
                CalibrationPointVisual visual = pointObj.GetComponent<CalibrationPointVisual>();
                if (visual == null)
                    visual = pointObj.AddComponent<CalibrationPointVisual>();
                    
                visual.Initialize(i);
                calibrationPointVisuals.Add(visual);
            }
        }
    }
    
    #region Public Methods
    
    public void StartCalibration()
    {
        if (isCalibrating) return;
        
        isCalibrating = true;
        currentPointIndex = 0;
        captureTrajectory.Clear();
        
        // Reset all point visuals
        foreach (var visual in calibrationPointVisuals)
        {
            visual.SetState(CalibrationPointVisual.PointState.Inactive);
        }
        
        ShowCalibrationUI();
        UpdateUIState();
        UpdateProgressText();
        
        Debug.Log("Calibration started");
    }
    
    public void StopCalibration()
    {
        isCalibrating = false;
        isCapturing = false;
        captureTimer = 0f;
        
        HideCalibrationUI();
        OnCalibrationCancelled?.Invoke();
        
        Debug.Log("Calibration cancelled");
    }
    
    #endregion
    
    #region Calibration Process
    
    private void StartCapture()
    {
        if (isCapturing) return;
        
        isCapturing = true;
        captureTimer = 0f;
        captureTrajectory.Clear();
        
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = showTrajectory;
            trajectoryLine.positionCount = 0;
        }
        
        captureButton.interactable = false;
        retryButton.interactable = false;
        
        statusText.text = "Capturing calibration point... Hold steady!";
        
        Debug.Log($"Starting capture for point {currentPointIndex + 1}");
    }
    
    private void UpdateCaptureProcess()
    {
        captureTimer += Time.deltaTime;
        
        // Add current position to trajectory
        if (showTrajectory && Camera.main != null)
        {
            Vector3 currentPos = Camera.main.transform.position;
            captureTrajectory.Add(currentPos);
            UpdateTrajectoryLine();
        }
        
        // Check if capture is complete
        if (captureTimer >= captureDelay)
        {
            CompleteCapture();
        }
        else
        {
            // Update progress indicator
            float progress = captureTimer / captureDelay;
            statusText.text = $"Capturing... {Mathf.RoundToInt(progress * 100)}%";
        }
    }
    
    private void CompleteCapture()
    {
        isCapturing = false;
        captureTimer = 0f;
        
        // Calculate confidence based on stability
        float stability = CalculateStability();
        float userConfidence = confidenceSlider.value;
        float finalConfidence = Mathf.Min(stability, userConfidence);
        
        // Get current transforms
        Transform headsetTransform = GetHeadsetTransform();
        Transform targetTransform = GetCurrentTargetTransform();
        
        if (headsetTransform != null && targetTransform != null)
        {
            // Capture the calibration point
            bool success = CalibrationManager.Instance.CollectCalibrationPoint(
                headsetTransform, 
                targetTransform, 
                finalConfidence
            );
            
            if (success)
            {
                // Update visual state
                calibrationPointVisuals[currentPointIndex].SetState(CalibrationPointVisual.PointState.Captured);
                calibrationPointVisuals[currentPointIndex].SetConfidence(finalConfidence);
                
                currentPointIndex++;
                
                // Check if calibration is complete
                if (currentPointIndex >= requiredCalibrationPoints)
                {
                    CompleteCalibration();
                }
                else
                {
                    PrepareNextPoint();
                }
                
                OnPointCaptured?.Invoke(currentPointIndex - 1);
            }
            else
            {
                statusText.text = "Capture failed! Please try again.";
                captureButton.interactable = true;
                retryButton.interactable = true;
            }
        }
        else
        {
            statusText.text = "Unable to locate transforms! Check setup.";
            captureButton.interactable = true;
            retryButton.interactable = true;
        }
        
        // Hide trajectory
        if (trajectoryLine != null)
            trajectoryLine.enabled = false;
    }
    
    private void PrepareNextPoint()
    {
        // Activate next point visual
        calibrationPointVisuals[currentPointIndex].SetState(CalibrationPointVisual.PointState.Active);
        
        captureButton.interactable = true;
        retryButton.interactable = true;
        
        UpdateProgressText();
        statusText.text = $"Move to calibration point {currentPointIndex + 1} and press Capture";
        
        Debug.Log($"Ready for point {currentPointIndex + 1}");
    }
    
    private void CompleteCalibration()
    {
        statusText.text = "Computing calibration transformation...";
        
        // Compute the transformation
        bool success = CalibrationManager.Instance.ComputeTransformation();
        
        if (success)
        {
            var result = CalibrationManager.Instance.LastValidationResult;
            statusText.text = $"Calibration complete! Average error: {result.averageError:F3}m ({CalibrationManager.Instance.Validator.GetQualityGrade(result.averageError)})";
            
            finishButton.interactable = true;
            captureButton.interactable = false;
            retryButton.interactable = false;
            
            OnCalibrationCompleted?.Invoke();
            
            Debug.Log("Calibration completed successfully");
        }
        else
        {
            statusText.text = "Calibration failed! Please retry.";
            captureButton.interactable = true;
            retryButton.interactable = true;
            
            Debug.LogError("Calibration failed");
        }
    }
    
    #endregion
    
    #region UI Management
    
    private void ShowCalibrationUI()
    {
        if (calibrationPanel != null)
            calibrationPanel.SetActive(true);
    }
    
    private void HideCalibrationUI()
    {
        if (calibrationPanel != null)
            calibrationPanel.SetActive(false);
    }
    
    private void UpdateUIState()
    {
        bool canCapture = !isCapturing && isCalibrating;
        bool canRetry = !isCapturing && isCalibrating && currentPointIndex > 0;
        bool canReset = isCalibrating;
        bool canFinish = currentPointIndex >= requiredCalibrationPoints;
        
        captureButton.interactable = canCapture;
        retryButton.interactable = canRetry;
        resetButton.interactable = canReset;
        finishButton.interactable = canFinish;
        
        // Update point visual states
        for (int i = 0; i < calibrationPointVisuals.Count; i++)
        {
            if (i < currentPointIndex)
                calibrationPointVisuals[i].SetState(CalibrationPointVisual.PointState.Captured);
            else if (i == currentPointIndex && isCalibrating)
                calibrationPointVisuals[i].SetState(CalibrationPointVisual.PointState.Active);
            else
                calibrationPointVisuals[i].SetState(CalibrationPointVisual.PointState.Inactive);
        }
    }
    
    private void UpdateProgressText()
    {
        if (progressText != null)
        {
            progressText.text = $"Point {currentPointIndex + 1} of {requiredCalibrationPoints}";
        }
    }
    
    private void UpdateConfidenceText(float value)
    {
        if (confidenceText != null)
        {
            confidenceText.text = $"Confidence: {value:P0}";
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private Transform GetHeadsetTransform()
    {
        // Find XR Origin and get main camera
        GameObject xrOrigin = GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null)
        {
            Transform mainCamera = xrOrigin.transform.Find("Camera Offset/Main Camera");
            return mainCamera;
        }
        
        // Fallback to main camera
        return Camera.main?.transform;
    }
    
    private Transform GetCurrentTargetTransform()
    {
        // This should be implemented based on your calibration point setup
        // For now, return a default target or null
        if (currentPointIndex < calibrationPointVisuals.Count)
        {
            return calibrationPointVisuals[currentPointIndex].transform;
        }
        
        return null;
    }
    
    private float CalculateStability()
    {
        if (captureTrajectory.Count < 2) return 0.5f;
        
        // Calculate variance in positions during capture
        Vector3 sum = Vector3.zero;
        foreach (Vector3 pos in captureTrajectory)
        {
            sum += pos;
        }
        
        Vector3 mean = sum / captureTrajectory.Count;
        
        float variance = 0f;
        foreach (Vector3 pos in captureTrajectory)
        {
            variance += Vector3.SqrMagnitude(pos - mean);
        }
        
        variance /= captureTrajectory.Count;
        
        // Convert variance to confidence (lower variance = higher confidence)
        float stability = Mathf.Clamp01(1.0f - (variance * 100f));
        return stability;
    }
    
    private void UpdateTrajectoryLine()
    {
        if (trajectoryLine != null && captureTrajectory.Count > 1)
        {
            trajectoryLine.positionCount = captureTrajectory.Count;
            trajectoryLine.SetPositions(captureTrajectory.ToArray());
        }
    }
    
    private void UpdateAlignmentIndicator()
    {
        if (alignmentIndicator != null && Camera.main != null)
        {
            Transform headset = GetHeadsetTransform();
            Transform target = GetCurrentTargetTransform();
            
            if (headset != null && target != null)
            {
                float distance = Vector3.Distance(headset.position, target.position);
                bool isAligned = distance <= alignmentThreshold;
                
                alignmentIndicator.SetActive(isAligned);
                
                if (isAligned)
                {
                    alignmentIndicator.transform.position = target.position;
                    alignmentIndicator.transform.rotation = target.rotation;
                }
            }
        }
    }
    
    #endregion
    
    #region Button Handlers
    
    private void RetryCurrentPoint()
    {
        if (currentPointIndex > 0)
        {
            currentPointIndex--;
            
            // Remove the last captured point
            if (CalibrationManager.Instance.CalibrationData != null)
            {
                var points = CalibrationManager.Instance.CalibrationData.CalibrationPoints;
                if (points.Count > 0)
                {
                    points.RemoveAt(points.Count - 1);
                }
            }
            
            // Reset visual state
            calibrationPointVisuals[currentPointIndex].SetState(CalibrationPointVisual.PointState.Active);
            
            UpdateProgressText();
            statusText.text = $"Retrying point {currentPointIndex + 1}";
        }
    }
    
    private void ResetCalibration()
    {
        CalibrationManager.Instance.ResetCalibration();
        
        currentPointIndex = 0;
        isCapturing = false;
        captureTimer = 0f;
        captureTrajectory.Clear();
        
        // Reset all visuals
        foreach (var visual in calibrationPointVisuals)
        {
            visual.SetState(CalibrationPointVisual.PointState.Inactive);
        }
        
        UpdateUIState();
        UpdateProgressText();
        statusText.text = "Calibration reset. Ready to start.";
        
        if (trajectoryLine != null)
            trajectoryLine.enabled = false;
    }
    
    private void FinishCalibration()
    {
        StopCalibration();
    }
    
    #endregion
}
