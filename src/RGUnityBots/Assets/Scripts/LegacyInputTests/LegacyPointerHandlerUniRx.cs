using System;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LegacyInputTests
{

    public static partial class ObservableTriggerExtensions
    {

        public static IObservable<PointerEventData> OnPointerDownAsObservable(this GameObject gameObject)
        {
            if (gameObject == null) return Observable.Empty<PointerEventData>();
            return GetOrAddComponent<ObservablePointerDownTrigger>(gameObject).OnPointerDownAsObservable();
        }

        public static IObservable<PointerEventData> OnPointerUpAsObservable(this GameObject gameObject)
        {
            if (gameObject == null) return Observable.Empty<PointerEventData>();
            return GetOrAddComponent<ObservablePointerUpTrigger>(gameObject).OnPointerUpAsObservable();
        }


        static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }
    }

    public class LegacyPointerHandlerUniRx: MonoBehaviour
    {
        public GameObject clickedIndicator;

        public void Start()
        {

            gameObject.OnPointerDownAsObservable()
                .Subscribe(
                    x =>
                    {
                        Debug.Log($"{gameObject.name} OnPointerDownAsObservable()");
                        clickedIndicator.SetActive(true);
                    });

            gameObject.OnPointerUpAsObservable()
                .Subscribe(
                    x =>
                    {
                        Debug.Log($"{gameObject.name} OnPointerUpAsObservable()");
                        clickedIndicator.SetActive(false);
                    });
        }
    }
}
