using UnityEngine;

public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance;

    private Matrix4x4 alignmentMatrix = Matrix4x4.identity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Calibrate(Transform headsetTransform, Transform targetSpawnInMakerspace)
    {
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

        alignmentMatrix = target * localHead.inverse;
    }

    public Matrix4x4 GetAlignmentMatrix()
    {
        return alignmentMatrix;
    }
}