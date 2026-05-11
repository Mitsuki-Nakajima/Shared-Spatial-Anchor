using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CalibrationValidator
{
    [Header("Validation Thresholds")]
    public float maxAverageErrorThreshold = 0.05f; 
    public float maxPointErrorThreshold = 0.10f;   
    public int minimumRequiredPoints = 3;
    
    public struct ValidationResult
    {
        public bool isValid;
        public float averageError;
        public float maxError;
        public List<float> pointErrors;
        public string errorMessage;
        public List<int> problematicPointIndices;
        
        public ValidationResult(bool valid, float avgErr, float maxErr, List<float> errors, string errorMsg = null)
        {
            isValid = valid;
            averageError = avgErr;
            maxError = maxErr;
            pointErrors = errors;
            errorMessage = errorMsg;
            problematicPointIndices = new List<int>();
            
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i] > maxPointErrorThreshold)
                {
                    problematicPointIndices.Add(i);
                }
            }
        }
    }
    
    public ValidationResult ValidateCalibration(CalibrationData calibrationData, Matrix4x4 transformationMatrix)
    {
        if (!calibrationData.HasMinimumPoints(minimumRequiredPoints))
        {
            return new ValidationResult(false, float.MaxValue, float.MaxValue, new List<float>(), 
                $"Insufficient calibration points. Minimum required: {minimumRequiredPoints}, Found: {calibrationData.CalibrationPoints.Count}");
        }
        
        List<float> errors = new List<float>();
        Vector3 totalError = Vector3.zero;
        
        foreach (var point in calibrationData.CalibrationPoints)
        {
            float error = CalculatePointError(point, transformationMatrix);
            errors.Add(error);
            totalError.x += error;
        }
        
        float averageError = totalError.x / errors.Count;
        float maxError = errors.Max();
        
        bool isValid = averageError <= maxAverageErrorThreshold && maxError <= maxPointErrorThreshold;
        
        return new ValidationResult(isValid, averageError, maxError, errors);
    }
    
    private float CalculatePointError(CalibrationData.CalibrationPoint point, Matrix4x4 transformationMatrix)
    {
        // Transform local position using the calculated matrix
        Vector3 transformedPosition = transformationMatrix.MultiplyPoint(point.localPosition);
        
        // Calculate position error
        Vector3 positionError = transformedPosition - point.targetPosition;
        float positionErrorMagnitude = positionError.magnitude;
        
        // Calculate rotation error
        Quaternion transformedRotation = transformationMatrix.rotation * point.localRotation;
        float rotationError = Quaternion.Angle(transformedRotation, point.targetRotation);
        
        // Weight position error more heavily (adjust weights as needed)
        float combinedError = positionErrorMagnitude + (rotationError * 0.01f); // Convert rotation to roughly equivalent distance
        
        return combinedError;
    }
    
    public bool CheckDegenerateConfiguration(CalibrationData calibrationData)
    {
        var points = calibrationData.CalibrationPoints;
        if (points.Count < 3) return true;
        
        // Check if points are collinear (for 3+ points)
        if (points.Count >= 3)
        {
            Vector3 v1 = points[1].localPosition - points[0].localPosition;
            Vector3 v2 = points[2].localPosition - points[0].localPosition;
            
            // Check if vectors are parallel (cross product near zero)
            Vector3 cross = Vector3.Cross(v1.normalized, v2.normalized);
            if (cross.magnitude < 0.01f)
            {
                return true; // Points are collinear
            }
        }
        
        // Check if points are too close together
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float distance = Vector3.Distance(points[i].localPosition, points[j].localPosition);
                if (distance < 0.1f) // 10cm minimum separation
                {
                    return true; // Points too close
                }
            }
        }
        
        return false;
    }
    
    public string GetQualityGrade(float averageError)
    {
        if (averageError <= 0.01f) return "Excellent";
        if (averageError <= 0.025f) return "Good";
        if (averageError <= 0.05f) return "Fair";
        return "Poor";
    }
}
