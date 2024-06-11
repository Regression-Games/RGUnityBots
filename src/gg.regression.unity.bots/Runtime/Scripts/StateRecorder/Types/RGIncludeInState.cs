using UnityEngine;

namespace RegressionGames.StateRecorder.Types
{
    /**
     * <summary>Behaviour to indicate that this object and its children should be included in the state capture,
     * even if they do not have any key types like renderers, colliders, animators, rigidbodies, particle systems, etc.</summary>
     */
    public class RGIncludeInState : MonoBehaviour
    {
    }
}
