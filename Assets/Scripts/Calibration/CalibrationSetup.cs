using UnityEngine;

/// <summary>
/// Helper script to set up calibration system in the scene
/// Attach this to a GameObject to automatically configure the calibration system
/// </summary>
public class CalibrationSetup : MonoBehaviour
{
    [Header("Auto-Setup Configuration")]
    [SerializeField] private bool setupOnAwake = true;
    [SerializeField] private bool createCalibrationDataAsset = true;
    [SerializeField] private bool setupNetworkSync = true;
    [SerializeField] private bool setupUI = true;
    
    [Header("Calibration Points")]
    [SerializeField] private int numberOfPoints = 5;
    [SerializeField] private float pointSpacing = 2.0f;
    [SerializeField] private Vector3 startPoint = Vector3.zero;
    [SerializeField] private bool arrangeInCircle = true;
    [SerializeField] private float circleRadius = 2.0f;
    
    private void Awake()
    {
        if (setupOnAwake)
        {
            SetupCalibrationSystem();
        }
    }
    
    [ContextMenu("Setup Calibration System")]
    public void SetupCalibrationSystem()
    {
        Debug.Log("Setting up calibration system...");
        
        // Create or find CalibrationManager
        SetupCalibrationManager();
        
        // Create CalibrationData asset if needed
        if (createCalibrationDataAsset)
        {
            CreateCalibrationData();
        }
        
        // Setup network sync
        if (setupNetworkSync)
        {
            SetupNetworkSync();
        }
        
        // Setup UI
        if (setupUI)
        {
            SetupCalibrationUI();
        }
        
        // Create calibration points
        CreateCalibrationPoints();
        
        Debug.Log("Calibration system setup complete!");
    }
    
    private void SetupCalibrationManager()
    {
        CalibrationManager manager = FindObjectOfType<CalibrationManager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("CalibrationManager");
            manager = managerObj.AddComponent<CalibrationManager>();
            Debug.Log("Created CalibrationManager");
        }
        else
        {
            Debug.Log("Found existing CalibrationManager");
        }
    }
    
    private void CreateCalibrationData()
    {
        CalibrationManager manager = FindObjectOfType<CalibrationManager>();
        if (manager != null && manager.CalibrationData == null)
        {
            // Create ScriptableObject asset
            CalibrationData data = ScriptableObject.CreateInstance<CalibrationData>();
            
            #if UNITY_EDITOR
            string path = "Assets/Calibration/CalibrationData.asset";
            if (!System.IO.Directory.Exists("Assets/Calibration"))
            {
                System.IO.Directory.CreateDirectory("Assets/Calibration");
            }
            
            UnityEditor.AssetDatabase.CreateAsset(data, path);
            UnityEditor.AssetDatabase.SaveAssets();
            
            // Assign to manager
            var managerScript = manager as CalibrationManager;
            var field = typeof(CalibrationManager).GetField("calibrationData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(managerScript, data);
            
            Debug.Log($"Created CalibrationData asset at {path}");
            #else
            Debug.LogWarning("Cannot create CalibrationData asset in build mode. Please create manually in editor.");
            #endif
        }
    }
    
    private void SetupNetworkSync()
    {
        NetworkCalibrationSync sync = FindObjectOfType<NetworkCalibrationSync>();
        if (sync == null)
        {
            GameObject syncObj = new GameObject("NetworkCalibrationSync");
            sync = syncObj.AddComponent<NetworkCalibrationSync>();
            Debug.Log("Created NetworkCalibrationSync");
        }
        else
        {
            Debug.Log("Found existing NetworkCalibrationSync");
        }
    }
    
    private void SetupCalibrationUI()
    {
        CalibrationUI ui = FindObjectOfType<CalibrationUI>();
        if (ui == null)
        {
            GameObject uiObj = new GameObject("CalibrationUI");
            ui = uiObj.AddComponent<CalibrationUI>();
            
            // Setup basic UI components
            SetupBasicUI(uiObj);
            
            Debug.Log("Created CalibrationUI");
        }
        else
        {
            Debug.Log("Found existing CalibrationUI");
        }
    }
    
    private void SetupBasicUI(GameObject uiObject)
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(uiObject.transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create panel
        GameObject panelObj = new GameObject("CalibrationPanel");
        panelObj.transform.SetParent(canvasObj.transform);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(400, 300);
        panelRect.anchoredPosition = Vector2.zero;
        
        UnityEngine.UI.Image panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        
        // Add basic UI components (simplified setup)
        // In a real implementation, you'd want to create proper prefabs
    }
    
    private void CreateCalibrationPoints()
    {
        CalibrationUI ui = FindObjectOfType<CalibrationUI>();
        if (ui == null) return;
        
        // Create parent object for calibration points
        GameObject pointsParent = new GameObject("CalibrationPoints");
        pointsParent.transform.SetParent(transform);
        
        for (int i = 0; i < numberOfPoints; i++)
        {
            Vector3 position = CalculatePointPosition(i);
            GameObject pointObj = CreateCalibrationPointObject(i, position);
            pointObj.transform.SetParent(pointsParent.transform);
        }
        
        Debug.Log($"Created {numberOfPoints} calibration points");
    }
    
    private Vector3 CalculatePointPosition(int index)
    {
        if (arrangeInCircle)
        {
            float angle = (float)index / numberOfPoints * 2f * Mathf.PI;
            return new Vector3(
                startPoint.x + Mathf.Cos(angle) * circleRadius,
                startPoint.y,
                startPoint.z + Mathf.Sin(angle) * circleRadius
            );
        }
        else
        {
            return startPoint + Vector3.forward * index * pointSpacing;
        }
    }
    
    private GameObject CreateCalibrationPointObject(int index, Vector3 position)
    {
        GameObject pointObj = new GameObject($"CalibrationPoint_{index}");
        pointObj.transform.position = position;
        
        // Add visual components
        CreatePointVisual(pointObj, index);
        
        // Add CalibrationPointVisual component
        CalibrationPointVisual visual = pointObj.AddComponent<CalibrationPointVisual>();
        visual.Initialize(index);
        
        return pointObj;
    }
    
    private void CreatePointVisual(GameObject pointObj, int index)
    {
        // Create sphere mesh
        MeshFilter meshFilter = pointObj.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateSphereMesh();
        
        MeshRenderer meshRenderer = pointObj.AddComponent<MeshRenderer>();
        
        // Create material
        Material material = new Material(Shader.Find("Standard"));
        material.color = Color.gray;
        meshRenderer.material = material;
        
        // Add light for visibility
        Light pointLight = pointObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.range = 2f;
        pointLight.intensity = 0.5f;
        pointLight.color = Color.gray;
        
        // Add particle system for effects
        ParticleSystem particles = pointObj.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startLifetime = 2.0f;
        main.startSize = 0.1f;
        main.startColor = Color.white;
        main.emissionRate = 10f;
        
        // Add label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(pointObj.transform);
        labelObj.transform.localPosition = Vector3.up * 0.5f;
        
        TextMesh label = labelObj.AddComponent<TextMesh>();
        label.text = $"Point {index + 1}";
        label.fontSize = 20;
        label.color = Color.white;
        label.anchor = TextAnchor.MiddleCenter;
        
        // Make label face camera
        BillboardEffect billboard = labelObj.AddComponent<BillboardEffect>();
    }
    
    private Mesh CreateSphereMesh()
    {
        // Simple sphere creation (or use Unity's built-in sphere)
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh mesh = sphere.GetComponent<MeshFilter>().mesh;
        DestroyImmediate(sphere);
        return mesh;
    }
    
    [ContextMenu("Cleanup Calibration System")]
    public void CleanupCalibrationSystem()
    {
        Debug.Log("Cleaning up calibration system...");
        
        // Find and destroy calibration objects
        CalibrationManager manager = FindObjectOfType<CalibrationManager>();
        if (manager != null)
        {
            DestroyImmediate(manager.gameObject);
        }
        
        NetworkCalibrationSync sync = FindObjectOfType<NetworkCalibrationSync>();
        if (sync != null)
        {
            DestroyImmediate(sync.gameObject);
        }
        
        CalibrationUI ui = FindObjectOfType<CalibrationUI>();
        if (ui != null)
        {
            DestroyImmediate(ui.gameObject);
        }
        
        // Clean up calibration points
        CalibrationPointVisual[] points = FindObjectsOfType<CalibrationPointVisual>();
        foreach (var point in points)
        {
            DestroyImmediate(point.gameObject);
        }
        
        Debug.Log("Calibration system cleanup complete!");
    }
}

/// <summary>
/// Simple billboard effect to make objects face the camera
/// </summary>
public class BillboardEffect : MonoBehaviour
{
    private Camera targetCamera;
    
    private void Start()
    {
        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            targetCamera = FindObjectOfType<Camera>();
        }
    }
    
    private void LateUpdate()
    {
        if (targetCamera != null)
        {
            transform.LookAt(targetCamera.transform);
            transform.Rotate(0, 180, 0); // Flip to face forward
        }
    }
}
