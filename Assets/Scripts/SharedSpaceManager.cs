using UnityEngine;

public class SharedSpaceManager : MonoBehaviour
{
    public static SharedSpaceManager Instance;

    [SerializeField] private Transform makerspaceRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public Pose ConvertLocalToShared(Pose localPose, Matrix4x4 alignmentMatrix)
    {
        Matrix4x4 localMatrix = Matrix4x4.TRS(localPose.position, localPose.rotation, Vector3.one);
        Matrix4x4 sharedMatrix = alignmentMatrix * localMatrix;

        Vector3 pos = sharedMatrix.GetColumn(3);
        Quaternion rot = sharedMatrix.rotation;

        return new Pose(pos, rot);
    }

    public Transform MakerspaceRoot => makerspaceRoot;
}