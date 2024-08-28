using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RegressionGames
{
    public class RGDraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool IsReordering;

        public string draggableCardName;

        public TMP_Text namePrefab;

        public GameObject restingStatePrefab;

        public GameObject draggingStatePrefab;

        private GameObject _draggingStateInstance;

        private RGDropZone _dropZone;

        public void Start()
        {
            IsReordering = false;

            if (namePrefab != null)
            {
                namePrefab.text = draggableCardName;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var position = new Vector3
            {
                x = eventData.position.x,
                y = eventData.position.y,
                z = 0
            };

            var instance = Instantiate(draggingStatePrefab, position, draggingStatePrefab.transform.rotation);
            if (instance != null)
            {
                var dragged = instance.GetComponent<RGDraggedCard>();
                if (dragged != null)
                {
                    dragged.draggedCardName = draggableCardName;
                }

                instance.transform.SetParent(transform.root, false);
                _draggingStateInstance = instance;
            }

            Vector2 mouseScreenSpacePosition = Input.mousePosition;

            var potentialDropZones = GameObject.FindObjectsOfType<RGDropZone>();
            foreach (var dz in potentialDropZones)
            {
                var dropZoneRectTransform = dz.GetComponent<RectTransform>();
                var localMousePos = dropZoneRectTransform.InverseTransformPoint(mouseScreenSpacePosition);

                if (dropZoneRectTransform.rect.Contains(localMousePos))
                {
                    var ev = new PointerEventData(EventSystem.current)
                    {
                        pointerDrag = this.gameObject
                    };
                    dz.OnPointerEnter(ev);
                    IsReordering = true;
                    _dropZone = dz;
                    break;
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_draggingStateInstance != null)
            {
                _draggingStateInstance.transform.position = eventData.position;
            }

            if (_dropZone == null)
            {
                _dropZone = eventData.pointerEnter?.GetComponent<RGDropZone>();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_draggingStateInstance != null)
            {
                Destroy(_draggingStateInstance);
            }

            if (_dropZone == null && IsReordering)
            {
                var potentialDropZones = GameObject.FindObjectsOfType<RGDropZone>();
                foreach (var dz in potentialDropZones)
                {
                    if (dz.Contains(this.gameObject))
                    {
                        dz.RemoveChild(this.gameObject);
                        break;
                    }
                }

                return;
            }

            if (_dropZone == null)
            {
                return;
            }

            if (IsReordering)
            {
                IsReordering = false;
                _dropZone.CompleteReordering();
                _dropZone = null;
                return;
            }

            var newCard = Instantiate(restingStatePrefab);
            _dropZone.AddChild(newCard);
            _dropZone = null;
        }

        public void OnExitDropZone()
        {
            _dropZone = null;
        }
    }
}