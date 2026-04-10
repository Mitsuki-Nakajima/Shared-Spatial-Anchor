using UnityEngine;
using Unity.Netcode;

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

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

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
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (localHead == null || avatarHead == null) return;

        Matrix4x4 alignment = CalibrationManager.Instance != null
            ? CalibrationManager.Instance.GetAlignmentMatrix()
            : Matrix4x4.identity;

        UpdatePart(localHead, avatarHead, alignment);
        UpdatePart(localLeftHand, avatarLeftHand, alignment);
        UpdatePart(localRightHand, avatarRightHand, alignment);
    }

    private void UpdatePart(Transform localPart, Transform avatarPart, Matrix4x4 alignmentMatrix)
    {
        if (localPart == null || avatarPart == null) return;

        Pose localPose = new Pose(localPart.position, localPart.rotation);
        Pose sharedPose = SharedSpaceManager.Instance != null
            ? SharedSpaceManager.Instance.ConvertLocalToShared(localPose, alignmentMatrix)
            : localPose;

        avatarPart.position = sharedPose.position;
        avatarPart.rotation = sharedPose.rotation;
    }
}