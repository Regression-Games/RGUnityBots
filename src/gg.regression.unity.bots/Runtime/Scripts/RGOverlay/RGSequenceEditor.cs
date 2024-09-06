using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class RGSequenceEditor : MonoBehaviour
    {
        public BotSequence CurrentSequence;

        public TMP_InputField NameInput;

        public TMP_InputField DescriptionInput;
        
        public TMP_InputField SearchInput;
        
        public GameObject AvailableSegmentsList;

        public GameObject CreateSequenceButton;
        
        public GameObject DropZonePrefab;

        public GameObject SegmentCardPrefab;

        public Sprite SegmentIcon;

        public Sprite SegmentListIcon;

        public RGDropZone _dropZone;
        
        private IList<BotSequenceEntry> _segmentEntries;

        private IList<BotSequenceEntry> _filteredSegmentEntries;

        public void Initialize()
        {
            if (SearchInput == null)
            {
                Debug.LogError("RGSequenceEditor is missing its SearchInput");
            }
            else
            {
                SearchInput.onValueChanged.AddListener(OnSearchInputChange);
            }
            
            if (NameInput == null)
            {
                Debug.LogError("RGSequenceEditor is missing its NameInput");
            }
            else
            {
                NameInput.onValueChanged.AddListener(OnNameInputChange);
            }

            if (DescriptionInput == null)
            {
                Debug.LogError("RGSequenceEditor is missing its DescriptionInput");
            }
            
            if (DropZonePrefab == null)
            {
                Debug.LogError("RGSequenceEditor is missing its DropZone");
            }
            else
            {
                _dropZone = DropZonePrefab.GetComponent<RGDropZone>();
            }

            if (SegmentCardPrefab == null)
            {   
                Debug.LogError("RGSequenceEditor is missing its SegmentCardPrefab");
            }
            
            if (AvailableSegmentsList == null)
            {
                Debug.LogError($"RGSequenceEditor is missing its AvailableSegmentsList");
                var layoutGroups = this.GetComponents<VerticalLayoutGroup>();
                foreach (var layout in layoutGroups)
                {
                    if (layout.transform.parent.name == "Available Segments List")
                    {
                        AvailableSegmentsList = layout.gameObject;
                        break;
                    }
                }

                if (AvailableSegmentsList == null)
                {
                    Debug.LogError("RGSequenceEditor is missing its AvailableSegmentsList, and could not find one");
                }
            }
            
            CurrentSequence = new BotSequence();
            
            ResetEditor();
            
            _segmentEntries = BotSegment.LoadAllSegments();
            CreateAvailableSegments(_segmentEntries);
        }

        public void CreateAvailableSegments(IList<BotSequenceEntry> segments)
        {
            ClearAvailableSegments();
            
            foreach (var segment in segments)
            {
                var prefab = Instantiate(SegmentCardPrefab, AvailableSegmentsList.transform, false);
                var segmentCard = prefab.GetComponent<RGDraggableCard>();
                if (segmentCard != null)
                {
                    segmentCard.payload = new Dictionary<string, string>
                    {
                        { "path", segment.path },
                        { "type", segment.type.ToString() }
                    };
                    segmentCard.draggableCardName = segment.entryName;
                    segmentCard.draggableCardDescription = segment.description;
                    segmentCard.icon = segment.type == BotSequenceEntryType.Segment ? SegmentIcon : SegmentListIcon;
                }
            }
        }

        public void ClearAvailableSegments()
        {
            var childCount = AvailableSegmentsList.transform.childCount - 1;
            for (var i = childCount; i >= 0; i--)
            {
                Destroy(AvailableSegmentsList.transform.GetChild(i).gameObject);
            }
        }

        public void SaveSequence()
        {
            var addedSegments = _dropZone.GetChildren();
            
            foreach (var segment in addedSegments)
            {
                var path = segment.payload["path"];
                Enum.TryParse(segment.payload["type"], out BotSequenceEntryType type);
                
                if (type == BotSequenceEntryType.Segment)
                {
                    CurrentSequence.AddBotSegmentPath(path);
                }
                else if (type == BotSequenceEntryType.SegmentList)
                {
                    CurrentSequence.AddBotSegmentListPath(path);
                }
                else
                {
                    Debug.LogError($"RGSequenceEditor could not add Bot Segment of type {type}");
                }
            }

            CurrentSequence.name = NameInput.text;
            CurrentSequence.description = DescriptionInput.text;
            CurrentSequence.SaveSequenceAsJson();
        }

        public void ResetEditor()
        {
            if (NameInput != null)
            {
                NameInput.text = string.Empty;
            }

            if (DescriptionInput != null)
            {
                DescriptionInput.text = string.Empty;
            }
            
            ClearAvailableSegments();
            
            _dropZone.ClearChildren();

            SetCreateSequenceButtonEnabled(false);
        }

        public void ReloadAvailableSegments()
        {
            SearchInput.text = string.Empty;
            _segmentEntries = BotSegment.LoadAllSegments();
            CreateAvailableSegments(_segmentEntries);
        }

        public void OnSearchInputChange(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                CreateAvailableSegments(_segmentEntries);
            }
            else
            {
                _filteredSegmentEntries = _segmentEntries.Where(s => s.entryName.Contains(text.Trim())).ToList();
                CreateAvailableSegments(_filteredSegmentEntries);
            }
        }

        public void OnNameInputChange(string text)
        {
            NameInput.text = text;
            SetCreateSequenceButtonEnabled(!_dropZone.IsEmpty() && text.Length > 0);
        }

        public void SetCreateSequenceButtonEnabled(bool isEnabled)
        {
            if (CreateSequenceButton != null)
            {
                var alpha = isEnabled ? 1.0f : 0.1f;
                var button = CreateSequenceButton.GetComponent<Button>();
                if (button != null)
                {
                    if (button.interactable && isEnabled || !button.interactable && !isEnabled)
                    {
                        // don't adjust anything, the requested change is redundant
                        return;
                    }
                    
                    button.interactable = isEnabled;
                }
                
                var imageChildren = CreateSequenceButton.GetComponentsInChildren<Image>();
                foreach (var child in imageChildren)
                {
                    var color = child.color;
                    color.a = alpha;
                    child.color = color;
                }
                
                var textChildren = CreateSequenceButton.GetComponentsInChildren<TMP_Text>();
                foreach (var child in textChildren)
                {
                    var color = child.color;
                    color.a = alpha;
                    child.color = color;
                }
            }
        }
    }
}