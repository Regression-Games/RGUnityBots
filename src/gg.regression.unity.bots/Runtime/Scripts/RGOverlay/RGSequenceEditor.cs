using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class RGSequenceEditor : MonoBehaviour
    {
        public BotSequence CurrentSequence;

        public GameObject AvailableSegmentsList;

        public GameObject SegmentCardPrefab;

        public Sprite SegmentIcon;

        public Sprite SegmentListIcon;

        private IList<BotSequenceEntry> _segmentEntries;

        public void Initialize()
        {
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
            
            for (var i = 0; i < AvailableSegmentsList.transform.childCount; ++i)
            {
                Destroy(AvailableSegmentsList.transform.GetChild(i));
            }
            
            _segmentEntries = LoadAllSegments();
            
            foreach (var segment in _segmentEntries)
            {
                var prefab = Instantiate(SegmentCardPrefab, AvailableSegmentsList.transform, false);
                var segmentCard = prefab.GetComponent<RGDraggableCard>();
                if (segmentCard != null)
                {
                    segmentCard.draggableCardName = segment.displayPath;
                    segmentCard.icon = segment.type == BotSequenceEntryType.Segment ? SegmentIcon : SegmentListIcon;
                }
            }
        }
        
        private IList<BotSequenceEntry> EnumerateSegmentsInDirectory(string path)
        {
            var results = new List<BotSequenceEntry>();
            var segmentDirectories = Directory.EnumerateDirectories(path);

            foreach (var directory in segmentDirectories)
            {
                var segmentFiles = Directory.EnumerateFiles(directory, "*.json");
                foreach (var fileName in segmentFiles)
                {
                    try
                    {
                        using var sr = new StreamReader(File.OpenRead(fileName));
                        var result = (fileName, sr.ReadToEnd());
                        
                        var entry = new BotSequenceEntry
                        {
                            path = path + "/" + fileName,
                            displayPath =  Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar)) + "/" + Path.GetFileNameWithoutExtension(fileName)
                        };

                        try
                        {
                            JsonConvert.DeserializeObject<BotSegmentList>(result.Item2);
                            entry.type = BotSequenceEntryType.SegmentList;
                        }
                        catch
                        {
                            try
                            {
                                JsonConvert.DeserializeObject<BotSegment>(result.Item2);
                                entry.type = BotSequenceEntryType.Segment;
                            }
                            catch
                            {
                                Debug.LogError($"RGSequenceEditor Could not parse Bot Segment file: {fileName}");
                                continue;
                            }
                        }
                        
                        results.Add(entry);
                    }
                    catch (Exception exception)
                    {
                        Debug.Log($"Error reading Bot Sequence Entry {fileName}: {exception}");
                    }
                }
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

            return EnumerateSegmentsInDirectory(sequencePath);
        }
        
        private void LoadSegmentsForSequence(IList<BotSequenceEntry> segmentEntries)
        {
            // TODO
        }
    }
}