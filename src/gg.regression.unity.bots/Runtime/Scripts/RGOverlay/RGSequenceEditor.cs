using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateRecorder;
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

        private string _originalName = "";
        private bool _isOriginalTheRecordingPath;

        private bool _makingACopy;

        /**
         * <summary>
         * Ensure all required fields are provided, and set any event listening functions
         * </summary>
         * <param name="makingACopy">bool true if copying to a new file, or false if editing in place</param>
         * <param name="existingResourcePath">The resource path for an existing Sequence for editing</param>
         * <param name="existingFilePath">The path of the sequence that is being edited (optional)</param>
         */
        public void Initialize(bool makingACopy, string existingResourcePath, string existingFilePath = null)
        {
            SearchInput.onValueChanged.AddListener(OnSearchInputChange);
            NameInput.onValueChanged.AddListener(OnNameInputChange);
            DescriptionInput.onValueChanged.AddListener(OnDescriptionInputChange);
            _dropZone = DropZonePrefab.GetComponent<RGDropZone>();

            _makingACopy = makingACopy;

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
            _existingSequencePath = existingResourcePath;
            var isBeingEdited = !string.IsNullOrEmpty(_existingSequencePath);
            if (_makingACopy)
            {
                // populate the segment list from the contents of the loaded sequence
                CurrentSequence = SetEditingState(_existingSequencePath);
                titleComponent.text = "Copy Sequence";
            }
            else if (isBeingEdited)
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
            var saveButton = GetComponentInChildren<RGSaveSequenceButton>();
            if (saveButton != null)
            {
                saveButton.SetEditModeEnabled(isBeingEdited, makingACopy);
            }

            EnforceRequiredInputs();

            // load all segments and add them to the Available Segments list
            _segmentEntries = BotSegment
                .LoadAllSegments()
                .Values
                .Select(seg => seg.Item2)
                .ToList();
            CreateAvailableSegments(_segmentEntries);
        }

        public static bool IsRecordingSequencePath(string sequencePath)
        {
            if (sequencePath == null)
            {
                return false;
            }
            return sequencePath.EndsWith("/"+ScreenRecorder.RecordingPathName);
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
            if (sequenceToEdit != null)
            {
                NameInput.text = sequenceToEdit.name;
                _originalName = sequenceToEdit.name.Trim();
                _isOriginalTheRecordingPath = IsRecordingSequencePath(sequencePath);
                if (!_isOriginalTheRecordingPath)
                {
                    // copy the description
                    DescriptionInput.text = sequenceToEdit.description;
                }
                else
                {
                    // for recording copies, leave the description empty.. they should make their own
                }

                foreach (var entry in sequenceToEdit.segments)
                {
                    InstantiateDraggableSegmentCard(entry, _dropZone.Content.transform);
                }

                _dropZone.SetEmptyState(false);

                EnforceRequiredInputs();
            }
            else
            {
                // this is really here for unit testing ... shouldn't happen in prod
                sequenceToEdit = new BotSequence();
            }

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
                !_makingACopy && !string.IsNullOrEmpty(_existingSequencePath) && CurrentSequence.name != NameInput.text;

            var addedSegments = _dropZone.GetChildren();
            foreach (var segment in addedSegments)
            {
                var path = segment.payload["path"];
                CurrentSequence.AddSequenceEntryForPath(path);
            }

            CurrentSequence.name = NameInput.text;
            CurrentSequence.description = DescriptionInput.text;

            // special case for recordings... where we need to copy the underlying segments as well, not just the sequence file
            if (_makingACopy && _isOriginalTheRecordingPath)
            {
                CurrentSequence.CopySequenceSegmentsToNewPath();
            }

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
            UpdateColorForInputNameNeedsChanging();
            UpdateColorForInputDescriptionNeedsChanging();
        }

        /**
         * <summary>
         * Load all Segments from disk
         * </summary>
         */
        public void ReloadAvailableSegments()
        {
            SearchInput.text = string.Empty;
            _segmentEntries = BotSegment
                .LoadAllSegments()
                .Values
                .Select(seg => seg.Item2)
                .ToList();
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
         * Update the name input's text, and update the enabled state of the save/update Sequence button, and update the required color border
         * </summary>
         * <param name="text">Name input text value</param>
         */
        public void OnNameInputChange(string text)
        {
            NameInput.text = text;
            EnforceRequiredInputs();
        }

        /**
         * <summary>
         * Update the description input's text, and update the required color border
         * </summary>
         * <param name="text">description input text value</param>
         */
        public void OnDescriptionInputChange(string text)
        {
            DescriptionInput.text = text;
            EnforceRequiredInputs();
        }

        public void EnforceRequiredInputs()
        {
            SetCreateSequenceButtonEnabled(!_dropZone.IsEmpty() && NameInput.text.Trim().Length > 0 && (!_makingACopy || string.CompareOrdinal(NameInput.text.Trim(), _originalName) != 0));
            UpdateColorForInputNameNeedsChanging();
            UpdateColorForInputDescriptionNeedsChanging();
        }

        public void UpdateColorForInputDescriptionNeedsChanging()
        {
            var needsChanging = IsRecordingSequencePath(_existingSequencePath) && DescriptionInput.text.Trim().Length == 0;
            if (DescriptionInput != null && DescriptionInput.gameObject != null)
            {
                var image = DescriptionInput.gameObject.GetComponent<Image>();
                if (image != null)
                {
                    if (needsChanging)
                    {
                        // very light cyan - a suggestion color
                        image.color = (Color.cyan + Color.white *5 )/6;
                    }
                    else
                    {
                        image.color = Color.white;
                    }
                }
            }
        }

        public void UpdateColorForInputNameNeedsChanging()
        {
            var needsChanging = NameInput.text.Length == 0 || (_makingACopy && string.CompareOrdinal(NameInput.text.Trim(), _originalName) == 0);
            if (NameInput != null && NameInput.gameObject != null)
            {
                var image = NameInput.gameObject.GetComponent<Image>();
                if (image != null)
                {
                    if (needsChanging)
                    {
                        image.color = Color.red;
                    }
                    else
                    {
                        image.color = Color.white;
                    }
                }
            }
        }

        /**
         * <summary>Enabled or disable the given button and adjust alpha</summary>
         * <param name="isEnabled">Whether the save/update Sequence button should be enabled</param>
         * <param name="button">The button to enable/disable</param>
         */
        public static void SetButtonEnabled(bool isEnabled, Button button)
        {
            var alpha = isEnabled ? 1.0f : 0.1f;
            if (button != null)
            {
                if (button.interactable && isEnabled || !button.interactable && !isEnabled)
                {
                    // don't adjust anything, the requested change is redundant
                    return;
                }

                button.interactable = isEnabled;

                var imageChildren = button.gameObject.GetComponentsInChildren<Image>();
                foreach (var child in imageChildren)
                {
                    var color = child.color;
                    color.a = alpha;
                    child.color = color;
                }

                var textChildren = button.gameObject.GetComponentsInChildren<TMP_Text>();
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
         * Enable or disable the save/update Sequence button based on:
         * - If the current Sequence has a name
         * - If the current Sequence has at least 1 Segment
         * <param name="isEnabled">Whether the save/update Sequence button should be enabled</param>
         * </summary>
         */
        public void SetCreateSequenceButtonEnabled(bool isEnabled)
        {
            if (CreateSequenceButton != null)
            {
                var button = CreateSequenceButton.GetComponent<Button>();
                SetButtonEnabled(isEnabled, button);
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
