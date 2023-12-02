using UnityEngine;

namespace RegressionGames.Editor
{
    [ExecuteInEditMode]
    public class LookAtCameraInEditor : MonoBehaviour
    {
       

        // Update is called once per frame
        public void Update()
        {
#if UNITY_EDITOR
            Transform camTransform = Camera.current != null ? Camera.current.transform : null;
            if (camTransform != null)
            {
                // point of focus of the camera
                Vector3 lookPoint = camTransform.position +
                                    ( camTransform.forward)*100_000;
                this.transform.LookAt(lookPoint);
            }
#endif
        }
    }

}
