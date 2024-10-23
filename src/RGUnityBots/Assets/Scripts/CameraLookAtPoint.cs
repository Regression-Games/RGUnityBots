using UnityEngine;
// ReSharper disable Unity.InefficientPropertyAccess

public class CameraLookAtPoint : MonoBehaviour
{

    [Range(0.01f, 1.0f)]
    public float moveSpeed = 0.1f;

    public Transform lookTarget;

    public LineRenderer path;

    private Camera myCamera;

    private int nextIndex = 0;

    private Vector3[] positions;

    public bool movementActive = false;

    public void StartCameraMovement()
    {
        movementActive = true;
    }

    public void StopCameraMovement()
    {
        movementActive = false;
    }

    public void ResetCamera()
    {
        StopCameraMovement();
        nextIndex = 0;
        myCamera.transform.position = positions[0];
    }

    private void Start()
    {
        myCamera = GetComponent<Camera>();
        positions = new Vector3[path.positionCount];
        path.GetPositions(positions);

        for (var i = 0; i < positions.Length; i++)
        {
            positions[i] = positions[i] + path.transform.position;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (movementActive)
        {
            var nextPosition = myCamera.transform.position;
            if ((myCamera.transform.position - positions[nextIndex]).sqrMagnitude < 0.02f)
            {
                // make sure we compute the new position cleanly on the line between points regardless of camera move speed
                nextPosition = positions[nextIndex];

                // close enough to the point
                ++nextIndex;
                if (nextIndex > positions.Length - 1)
                {
                    nextIndex = 0;
                }
            }

            var direction = (positions[nextIndex] - myCamera.transform.position).normalized;

            myCamera.transform.position = nextPosition + (direction * moveSpeed);
        }
    }

    private void LateUpdate()
    {
        myCamera.transform.LookAt(lookTarget);
    }
}
