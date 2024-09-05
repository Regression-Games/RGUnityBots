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

        public void ReloadAvailableSegments()
        {
            SearchInput.text = string.Empty;
            _segmentEntries = LoadAllSegments();
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

        private BotSequenceEntry ParseSegment(string json, string fileName)
        {
            try
            {
                var entry = new BotSequenceEntry
                {
                    path = fileName,
                };

                try
                {
                    var segment = JsonConvert.DeserializeObject<BotSegmentList>(json);
                    entry.description = segment.description;
                    entry.entryName = segment.name;
                    entry.type = BotSequenceEntryType.SegmentList;
                }
                catch
                {
                    try
                    {
                        var segmentList = JsonConvert.DeserializeObject<BotSegment>(json);
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
                Debug.Log($"RGSequenceEditor could not open Bot Sequence Entry: {exception}");
            }

            return null;
        }

        private List<BotSequenceEntry> LoadSegmentsInDirectory(string path)
        {
            var results = new List<BotSequenceEntry>();
            var directories = Directory.EnumerateDirectories(path);
            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory, "*.json");
                foreach (var fileName in files)
                {
                    using var sr = new StreamReader(File.OpenRead(fileName));
                    var result = (fileName, sr.ReadToEnd());
                    var segment = ParseSegment(result.Item2, fileName);
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

        private List<BotSequenceEntry> LoadSegmentsInDirectoryForRuntime(string path)
        {
            var results = new List<BotSequenceEntry>();
            var directories = Directory.EnumerateDirectories(path);
            foreach (var directory in directories)
            {
                var jsons = Resources.LoadAll(directory, typeof(TextAsset));
                foreach (var jsonObject in jsons)
                {
                    try
                    {
                        var json = (jsonObject as TextAsset)?.text ?? "";
                        var segment = ParseSegment(json, jsonObject.name);

                        // don't add segments with duplicate names
                        if (results.Any(s => s.entryName == segment.entryName))
                        {
                            continue;
                        }
                        
                        results.Add(segment);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Exception reading a Segment json file from resource path: {directory}", e);
                    }
                }

                results = results.Concat(
                    LoadSegmentsInDirectoryForRuntime(directory)
                ).ToList();
            }

            return results;
        }

        private List<BotSequenceEntry> LoadAllSegments()
        {
            var segments = new List<BotSequenceEntry>();
            
#if UNITY_EDITOR
            const string sequencePath = "Assets/RegressionGames/Resources/BotSegments";
            if (!Directory.Exists(sequencePath))
            {
                return new List<BotSequenceEntry>();
            }

            segments = LoadSegmentsInDirectory(sequencePath);
#else
            // 1. check the persistentDataPath for segments
            var persistentDataPath = Application.persistentDataPath + "/BotSegments";
            if (Directory.Exists(persistentDataPath))
            {
                segments = LoadSegmentsInDirectoryForRuntime(persistentDataPath);
            }
        
            // 2. load Segments from Resources, and ensure that no Segments have duplicate names
            const string runtimePath = "BotSegments";
            segments = segments.Concat(LoadSegmentsInDirectoryForRuntime(runtimePath)).ToList();
#endif

            return segments;
        }
    }
}