using UnityEngine;

namespace RegressionGames
{
    public class RGSettingsDynamicEnabler: MonoBehaviour
    {
        public bool disableIfUsingGlobalSettings = false;
        public void OnEnable()
        {
            OptionsUpdated();
        }

        public void OptionsUpdated()
        {
            if (disableIfUsingGlobalSettings)
            {
                if (RGSettings.GetOrCreateSettings().GetUseSystemSettings())
                {
                    this.gameObject.SetActive(false);
                }
                else
                {
                    this.gameObject.SetActive(true);
                }
            }
            // else leave it alone
        }
    }
}
