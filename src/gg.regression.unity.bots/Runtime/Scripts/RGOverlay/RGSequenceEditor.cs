using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder.BotSegments.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * Provides a method of constructing, or editing, Sequences by altering the Segments comprising the Sequence. We
     * can also give the Sequence a name and description
     * </summary>
     *
     */
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

        public TMP_Text titleComponent;

        public RGDropZone _dropZone;

        private IList<BotSequenceEntry> _segmentEntries;

        private IList<BotSequenceEntry> _filteredSegmentEntries;

        private string _existingSequencePath;

        /**
         * <summary>
         * Ensure all required fields are provided, and set any event listening functions
         * </summary>
         * <param name="existingSequencePath">The path of the sequence that is being edited (optional)</param>
         */
        public void Initialize(string existingSequencePath = null)
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

            // if the Available Segment List cannot be found, we will try to find it somewhere in the scene
            if (AvailableSegmentsList == null)
            {
                Debug.LogError($"RGSequenceEditor is missing its AvailableSegmentsList");
                var layoutGroups = GetComponents<VerticalLayoutGroup>();
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
                    Debug.LogError("RGSequenceEditor is missing its AvailableSegmentsList, and could not find one in the scene");
                }
            }

            // reset the editor to its default values
            ResetEditor();

            // set the editor to either be in an editing or creating state
            _existingSequencePath = existingSequencePath;
            var isBeingEdited = !string.IsNullOrEmpty(_existingSequencePath);
            if (isBeingEdited)
            {
                // populate the segment list from the contents of the loaded sequence
                CurrentSequence = SetEditingState(_existingSequencePath);
                titleComponent.text = "Edit Sequence";
            }
            else
            {
                CurrentSequence = new BotSequence();
                titleComponent.text = "Create Sequence";
            }

            // ensure that the create/update button is in the correct default state:
            // - creating -> disabled
            // - editing -> enabled
            SetCreateSequenceButtonEnabled(isBeingEdited);
            var saveButton = GetComponentInChildren<RGSaveSequenceButton>();
            if (saveButton != null)
            {
                saveButton.SetEditModeEnabled(isBeingEdited);
            }

            // load all segments and add them to the Available Segments list
            _segmentEntries = BotSegment.LoadAllSegments().Values.ToList();
            CreateAvailableSegments(_segmentEntries);
        }

        /**
         * <summary>
         * Load an existing Sequence and set its name, description, and segments.
         * </summary>
         * <param name="sequencePath">The existing Sequence to load</param>
         */
        public BotSequence SetEditingState(string sequencePath)
        {
            var sequenceToEdit = BotSequence.LoadSequenceJsonFromPath(sequencePath).Item3;
            NameInput.text = sequenceToEdit.name;
            DescriptionInput.text = sequenceToEdit.description;
            foreach (var entry in sequenceToEdit.segments)
            {
                InstantiateDraggableSegmentCard(entry, _dropZone.Content.transform);
            }

            _dropZone.SetEmptyState(false);

            SetCreateSequenceButtonEnabled(true);

            return sequenceToEdit;
        }

        /**
         * <summary>
         * Create UI components for each Segment in the Available Segments List
         * </summary>
         * <param name="segments">
         * Bot Sequence Entries to turn into UI components
         * </param>
         */
        public void CreateAvailableSegments(IList<BotSequenceEntry> segments)
        {
            ClearAvailableSegments();

            foreach (var segment in segments)
            {
                InstantiateDraggableSegmentCard(segment, AvailableSegmentsList.transform);
            }
        }

        /**
         * <summary>
         * Destroy the Segments in the Available Segments List
         * </summary>
         * <returns>
         * The number of child Segments destroyed
         * </returns>
         */
        public int ClearAvailableSegments()
        {
            var childCount = AvailableSegmentsList.transform.childCount;
            for (var i = 0; i < childCount; ++i)
            {
                Destroy(AvailableSegmentsList.transform.GetChild(i).gameObject);
            }

            return childCount;
        }

        /**
         * <summary>
         * Save or update the current Sequence
         * </summary>
         */
        public void SaveSequence()
        {
            CurrentSequence.segments.Clear();

            // If the Sequence being edited is renamed, we must delete the original Sequence
            // after saving due to the new file name being based on the Sequence name
            var shouldDeleteOriginal =
                !string.IsNullOrEmpty(_existingSequencePath) && CurrentSequence.name != NameInput.text;

            var addedSegments = _dropZone.GetChildren();
            foreach (var segment in addedSegments)
            {
                var path = segment.payload["path"];
                CurrentSequence.AddSequenceEntryForPath(path);
            }

            CurrentSequence.name = NameInput.text;
            CurrentSequence.description = DescriptionInput.text;
            CurrentSequence.SaveSequenceAsJson();

            if (shouldDeleteOriginal)
            {
                BotSequence.DeleteSequenceAtPath(_existingSequencePath);
            }
        }

        /**
         * <summary>
         * Reset the `name`, `description`, and Available Segments to their defaults
         * </summary>
         */
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

        /**
         * <summary>
         * Load all Segments from disk
         * </summary>
         */
        public void ReloadAvailableSegments()
        {
            SearchInput.text = string.Empty;
            _segmentEntries = BotSegment.LoadAllSegments().Values.ToList();
            CreateAvailableSegments(_segmentEntries);
        }

        /**
         * <summary>
         * When the search input's value changes, filter the Segments. If the search input is empty, show all Segments
         * </summary>
         * <param name="text">Search input text value</param>
         */
        public void OnSearchInputChange(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                CreateAvailableSegments(_segmentEntries);
            }
            else
            {
                var comparisonText = text.Trim().ToLower();
                _filteredSegmentEntries = _segmentEntries.Where(s =>
                    s.name.ToLower().Contains(comparisonText)
                ).ToList();
                CreateAvailableSegments(_filteredSegmentEntries);
            }
        }

        /**
         * <summary>
         * Update the name input's text, and update the enabled state of the save/update Sequence button
         * </summary>
         * <param name="text">Name input text value</param>
         */
        public void OnNameInputChange(string text)
        {
            NameInput.text = text;
            SetCreateSequenceButtonEnabled(!_dropZone.IsEmpty() && text.Length > 0);
        }

        /**
         * <summary>
         * Enable or disable the save/update Sequence button based on:
         * - If the current Sequence has a name
         * - If the current Sequence has at least 1 Segment
         * </summary>
         * <param name="isEnabled">
         * Whether the save/update Sequence button should be enabled
         * </param>
         */
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

        /**
         * <summary>
         * Instantiate a Segment Card prefab and attach it to a parent
         * </summary>
         * <param name="entry">The Bot Sequence Entry to use as the source for prefab instantiation</param>
         * <param name="parentTransform">The transform to attach to newly instantiated Segment Card to</param>
         */
        private void InstantiateDraggableSegmentCard(BotSequenceEntry entry, Transform parentTransform)
        {
            var prefab = Instantiate(SegmentCardPrefab, parentTransform, false);
            var segmentCard = prefab.GetComponent<RGDraggableCard>();
            if (segmentCard != null)
            {
                // load the card's payload
                segmentCard.payload = new Dictionary<string, string>
                {
                    { "path", entry.path },
                    { "type", entry.type.ToString() }
                };
                segmentCard.draggableCardName = entry.name;
                segmentCard.draggableCardDescription = entry.description;
                segmentCard.icon = entry.type == BotSequenceEntryType.Segment ? SegmentIcon : SegmentListIcon;
            }
        }
    }
}
