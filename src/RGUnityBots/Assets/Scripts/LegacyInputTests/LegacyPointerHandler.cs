using System;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;

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

    public class LegacyPointerHandler: MonoBehaviour
    {
        void Update()
        {
            // if you call this like unity documents during startup, it gets removed again.. so dumb
            TouchSimulation.Enable();
        }

        public void Start()
        {

            gameObject.OnPointerDownAsObservable()
                .SelectMany(_ => this.gameObject.UpdateAsObservable())
                .TakeUntil(this.gameObject.OnPointerUpAsObservable())
                .Select(_ => Input.mousePosition)
                .RepeatUntilDestroy(this) // safety way
                .Subscribe(
                    x =>
                    {
                        Debug.Log($"{gameObject.name} OnPointerDownAsObservable()");
                    });
        }
    }
}
