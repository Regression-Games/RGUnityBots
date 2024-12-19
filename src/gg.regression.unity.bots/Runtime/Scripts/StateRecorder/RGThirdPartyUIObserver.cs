using UnityEngine;
// ReSharper disable InconsistentNaming

namespace RegressionGames.StateRecorder
{
    /**
     * Whenever we enable a new third party UI library (such as coherent gameface), there must be a MonoBehaviour in that package that implements this interface.
     * ScreenRecorded utilizes these implementations to detect 3rd party UI changes.
     */
    public abstract class RGThirdPartyUIObserver : MonoBehaviour
    {
        public abstract void SetActive(bool active);
        public abstract bool HasUIChanged();

    }
}
