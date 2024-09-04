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
        
        public GameObject AvailableSegmentsList;

        public GameObject DropZonePrefab;

        public GameObject SegmentCardPrefab;

        public Sprite SegmentIcon;

        public Sprite SegmentListIcon;

        public RGDropZone _dropZone;
        
        private IList<BotSequenceEntry> _segmentEntries;

        public void Initialize()
        {
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
            
            ClearSegments();
            
            _segmentEntries = LoadAllSegments();
            
            foreach (var segment in _segmentEntries)
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
                    segmentCard.icon = segment.type == BotSequenceEntryType.Segment ? SegmentIcon : SegmentListIcon;
                }
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

        private void ClearSegments()
        {
            var childCount = AvailableSegmentsList.transform.childCount - 1;
            for (var i = childCount; i >= 0; i--)
            {
                Destroy(AvailableSegmentsList.transform.GetChild(i).gameObject);
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
                    entry.entryName = segment.name;
                    entry.type = BotSequenceEntryType.SegmentList;
                }
                catch
                {
                    try
                    {
                        var segmentList = JsonConvert.DeserializeObject<BotSegment>(result.Item2);
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