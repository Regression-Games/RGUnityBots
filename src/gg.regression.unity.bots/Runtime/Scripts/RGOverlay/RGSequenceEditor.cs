using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
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

        public GameObject DropZonePrefab;

        public GameObject SegmentCardPrefab;

        public Sprite SegmentIcon;

        public Sprite SegmentListIcon;

        public RGDropZone _dropZone;
        
        private IList<BotSequenceEntry> _segmentEntries;

        private IList<BotSequenceEntry> _filteredSegmentEntries;

        public void Initialize()
        {
            if (SearchInput != null)
            {
                SearchInput.onValueChanged.AddListener(OnSearchInputChange);
            }
            
            if (NameInput == null)
            {
                Debug.LogError("RGSequenceEditor is missing its NameInput");
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
            
            _segmentEntries = LoadAllSegments();
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

        private BotSequenceEntry ParseSegment(string path, string fileName)
        {
            try
            {
                using var sr = new StreamReader(File.OpenRead(fileName));
                var result = (fileName, sr.ReadToEnd());
                        
                var entry = new BotSequenceEntry
                {
                    path = fileName,
                };

                try
                {
                    var segment = JsonConvert.DeserializeObject<BotSegmentList>(result.Item2);
                    entry.description = segment.description;
                    entry.entryName = segment.name;
                    entry.type = BotSequenceEntryType.SegmentList;
                }
                catch
                {
                    try
                    {
                        var segmentList = JsonConvert.DeserializeObject<BotSegment>(result.Item2);
                        entry.description = segmentList.description;
                        entry.entryName = segmentList.name;
                        entry.type = BotSequenceEntryType.Segment;
                    }
                    catch
                    {
                        Debug.LogError($"RGSequenceEditor Could not parse Bot Segment file: {fileName}");
                        throw;
                    }
                }

                return entry;
            }
            catch (Exception exception)
            {
                Debug.Log($"Error reading Bot Sequence Entry: {exception}");
            }

            return null;
        }

        private IList<BotSequenceEntry> LoadSegmentsInDirectory(string path)
        {
            var results = new List<BotSequenceEntry>();
            var directories = Directory.EnumerateDirectories(path);
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory, "*.json");
                foreach (var fileName in files)
                {
                    var segment = ParseSegment(path, fileName);
                    if (segment != null)
                    {
                        results.Add(segment);
                    }   
                }

                results = results.Concat(
                    LoadSegmentsInDirectory(directory)
                ).ToList();
            }

            return results;
        }

        private IList<BotSequenceEntry> LoadAllSegments()
        {
            const string sequencePath = "Assets/RegressionGames/Resources/BotSegments";
            if (!Directory.Exists(sequencePath))
            {
                return new List<BotSequenceEntry>();
            }

            return LoadSegmentsInDirectory(sequencePath);
        }
    }
}