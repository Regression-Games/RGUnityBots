
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

namespace LegacyInputTests
{

    public class LegacyKeyHandlerUniRx: MonoBehaviour
    {
        public Image wIndicator;
        public Image aIndicator;
        public Image sIndicator;
        public Image dIndicator;

        public void Start()
        {

            gameObject.AddComponent<ObservableUpdateTrigger>()
                .UpdateAsObservable()
                .Subscribe(x =>
                {
                    if (Input.GetKeyDown(KeyCode.W))
                    {
                        wIndicator.color = Color.red;
                    }
                    if (Input.GetKeyUp(KeyCode.W))
                    {
                        wIndicator.color = (Color.white*2+Color.grey)/3;
                    }

                    if (Input.GetKeyDown(KeyCode.A))
                    {
                        aIndicator.color = Color.red;
                    }
                    if (Input.GetKeyUp(KeyCode.A))
                    {
                        aIndicator.color = (Color.white*2+Color.grey)/3;
                    }

                    if (Input.GetKeyDown(KeyCode.S))
                    {
                        sIndicator.color = Color.red;
                    }
                    if (Input.GetKeyUp(KeyCode.S))
                    {
                        sIndicator.color = (Color.white*2+Color.grey)/3;
                    }

                    if (Input.GetKeyDown(KeyCode.D))
                    {
                        dIndicator.color = Color.red;
                    }
                    if (Input.GetKeyUp(KeyCode.D))
                    {
                        dIndicator.color = (Color.white*2+Color.grey)/3;
                    }
                });

        }
    }
}
