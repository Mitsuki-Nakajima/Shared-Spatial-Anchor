using UnityEngine;
using UnityEngine.UI;

public class CalibrationPointVisual : MonoBehaviour
{
    public enum PointState
    {
        Inactive,
        Active,
        Captured,
        Error
    }
    
    [Header("Visual Components")]
    [SerializeField] private MeshRenderer pointRenderer;
    [SerializeField] private Light pointLight;
    [SerializeField] private ParticleSystem captureEffect;
    [SerializeField] private Canvas infoCanvas;
    [SerializeField] private TextMesh pointLabel;
    
    [Header("Materials")]
    [SerializeField] private Material inactiveMaterial;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private Material capturedMaterial;
    [SerializeField] private Material errorMaterial;
    
    [Header("Settings")]
    [SerializeField] private float pulseSpeed = 2.0f;
    [SerializeField] private float pulseIntensity = 0.3f;
    [SerializeField] private bool showLabel = true;
    
    private PointState currentState = PointState.Inactive;
    private int pointIndex = 0;
    private float confidence = 0f;
    private float pulseTimer = 0f;
    
    // Colors for different states
    private Color inactiveColor = Color.gray;
    private Color activeColor = Color.yellow;
    private Color capturedColor = Color.green;
    private Color errorColor = Color.red;
    
    public void Initialize(int index)
    {
        pointIndex = index;
        
        // Set up label
        if (pointLabel != null)
        {
            pointLabel.text = $"Point {index + 1}";
            pointLabel.gameObject.SetActive(showLabel);
        }
        
        // Set initial state
        SetState(PointState.Inactive);
    }
    
    public void SetState(PointState state)
    {
        currentState = state;
        
        UpdateVisuals();
        UpdateLight();
        UpdateEffects();
    }
    
    public void SetConfidence(float confidence)
    {
        this.confidence = Mathf.Clamp01(confidence);
        
        // Update visual based on confidence
        if (currentState == PointState.Captured)
        {
            UpdateVisuals();
        }
    }
    
    private void Update()
    {
        // Pulse effect for active state
        if (currentState == PointState.Active)
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float pulse = Mathf.Sin(pulseTimer) * pulseIntensity + 1.0f;
            
            if (pointRenderer != null)
            {
                Color baseColor = activeColor;
                pointRenderer.material.color = Color.Lerp(baseColor * 0.5f, baseColor, pulse);
            }
            
            if (pointLight != null)
            {
                pointLight.intensity = pulse;
            }
        }
    }
    
    private void UpdateVisuals()
    {
        if (pointRenderer == null) return;
        
        Material targetMaterial = null;
        Color targetColor = inactiveColor;
        
        switch (currentState)
        {
            case PointState.Inactive:
                targetMaterial = inactiveMaterial;
                targetColor = inactiveColor;
                break;
                
            case PointState.Active:
                targetMaterial = activeMaterial;
                targetColor = activeColor;
                break;
                
            case PointState.Captured:
                targetMaterial = capturedMaterial;
                // Blend color based on confidence
                targetColor = Color.Lerp(errorColor, capturedColor, confidence);
                break;
                
            case PointState.Error:
                targetMaterial = errorMaterial;
                targetColor = errorColor;
                break;
        }
        
        // Apply material if available
        if (targetMaterial != null)
        {
            pointRenderer.material = targetMaterial;
        }
        else
        {
            // Fallback to color change
            if (pointRenderer.material != null)
            {
                pointRenderer.material.color = targetColor;
            }
        }
        
        // Update label color
        if (pointLabel != null)
        {
            pointLabel.color = targetColor;
        }
    }
    
    private void UpdateLight()
    {
        if (pointLight == null) return;
        
        Color lightColor = inactiveColor;
        float intensity = 0f;
        
        switch (currentState)
        {
            case PointState.Inactive:
                lightColor = inactiveColor;
                intensity = 0.2f;
                break;
                
            case PointState.Active:
                lightColor = activeColor;
                intensity = 1.0f;
                break;
                
            case PointState.Captured:
                lightColor = Color.Lerp(errorColor, capturedColor, confidence);
                intensity = 0.5f;
                break;
                
            case PointState.Error:
                lightColor = errorColor;
                intensity = 0.8f;
                break;
        }
        
        pointLight.color = lightColor;
        pointLight.intensity = intensity;
    }
    
    private void UpdateEffects()
    {
        if (captureEffect == null) return;
        
        switch (currentState)
        {
            case PointState.Active:
                if (!captureEffect.isPlaying)
                    captureEffect.Play();
                break;
                
            case PointState.Captured:
                // Play burst effect then stop
                if (!captureEffect.isPlaying)
                    captureEffect.Play();
                    
                // Auto-stop after a short duration
                Invoke(nameof(StopCaptureEffect), 1.0f);
                break;
                
            default:
                if (captureEffect.isPlaying)
                    captureEffect.Stop();
                break;
        }
    }
    
    private void StopCaptureEffect()
    {
        if (captureEffect != null && captureEffect.isPlaying)
        {
            captureEffect.Stop();
        }
    }
    
    // Public methods for external interaction
    public void OnHoverEnter()
    {
        if (currentState == PointState.Inactive)
        {
            // Slightly brighten on hover
            if (pointRenderer != null && pointRenderer.material != null)
            {
                Color currentColor = pointRenderer.material.color;
                pointRenderer.material.color = currentColor * 1.2f;
            }
        }
    }
    
    public void OnHoverExit()
    {
        // Restore normal color
        UpdateVisuals();
    }
    
    public void OnSelected()
    {
        // Visual feedback when selected
        if (captureEffect != null)
        {
            captureEffect.Play();
        }
    }
    
    // Properties
    public PointState State => currentState;
    public int Index => pointIndex;
    public float Confidence => confidence;
}
