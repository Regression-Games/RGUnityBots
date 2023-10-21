using UnityEngine;
using Cinemachine;
using RGThirdPersonDemo;

public class LockOnCamera : MonoBehaviour
{
    [Tooltip("Reference to the player's transform")]
    public Transform player; 
    [Tooltip("Reference to the target's transform")]
    public Transform target;
    [Tooltip("Minimum Follow Offset Z value when zoomed in")]
    public float minFollowOffsetZ = -6f; 
    [Tooltip("Maximum Follow Offset Z value when zoomed out")]
    public float maxFollowOffsetZ = -12f; 
    [Tooltip("Speed of zooming in/out")]
    public float zoomSpeed = 1f;
    [Tooltip("Height offset from the player's position")]
    public float heightOffset = .6f; 
    [Tooltip("Minimum distance between player and target to start zooming in")]
    public float minDistance = 6f;
    [Tooltip("Maximum distance between player and target to start zooming out")]
    public float maxDistance = 12f;

    private CinemachineVirtualCamera vCam;
    private GameObject lookAtTarget; // Empty GameObject to use as the LookAt target
    private float initialFollowOffsetZ; // Initial Follow Offset Z value

    private void Start()
    {
        vCam = GetComponent<CinemachineVirtualCamera>();
        if (vCam == null)
        {
            Debug.LogError("CinemachineVirtualCamera component not found on this GameObject!");
            return;
        }

        if (player == null)
        {
            Debug.LogError("Player Transform not assigned in the Inspector!");
            return;
        }

        // Create an empty GameObject as the look-at target
        lookAtTarget = new GameObject("LookAtTarget");
        var lookAtTargetPos = lookAtTarget.transform.position;
        lookAtTargetPos.y = 1.3f;
        lookAtTarget.transform.position = lookAtTargetPos;
        initialFollowOffsetZ = vCam.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset.z;
    }

    private void Update()
    {
        if (target == null)
        {
            ResetCamera();
            return;
        }

        Vector3 targetPosition = target.position + Vector3.up * heightOffset;
        Vector3 playerToTarget = targetPosition - player.position;
        float distanceToTarget = playerToTarget.magnitude;
        Vector3 directionToTarget = playerToTarget.normalized;

        // Calculate the midpoint between the player and the target
        Vector3 midpoint = player.position + playerToTarget * 0.5f;

        // Calculate the direction from the midpoint to the camera
        Vector3 directionToCamera = vCam.State.FinalPosition - midpoint;
        directionToCamera.Normalize();

        // Calculate the desired camera position based on distance and height offset
        Vector3 desiredCameraPosition = midpoint + directionToCamera * distanceToTarget + Vector3.up * heightOffset;

        // Update the CinemachineVirtualCamera follow position
        CinemachineTransposer transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            float newFollowOffsetZ;
            if (distanceToTarget <= minDistance)
            {
                newFollowOffsetZ = minFollowOffsetZ;
            }
            else
            {
                newFollowOffsetZ = Mathf.Lerp(minFollowOffsetZ, maxFollowOffsetZ, Mathf.InverseLerp(minDistance, maxDistance, distanceToTarget));
                newFollowOffsetZ = Mathf.Clamp(newFollowOffsetZ, maxFollowOffsetZ, minFollowOffsetZ);
            }
            transposer.m_FollowOffset.z = Mathf.Lerp(transposer.m_FollowOffset.z, newFollowOffsetZ, Time.deltaTime * zoomSpeed);
        }

        // Update the CinemachineVirtualCamera lookAt position
        vCam.transform.position = desiredCameraPosition;
        vCam.LookAt = lookAtTarget.transform;

        // Set the look-at target's position to the midpoint between the player and the target
        lookAtTarget.transform.position = midpoint;
    }

    public void SetLockOnTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void ClearLockOnTarget()
    {
        target = null;
        ResetCamera();
    }

    public void SelectEnemy(EnemyController enemyController)
    {
        SetLockOnTarget(enemyController.transform);
    }

    private void ResetCamera()
    {
        CinemachineTransposer transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            transposer.m_FollowOffset.z = initialFollowOffsetZ;
        }

        vCam.LookAt = player;
    }
}
