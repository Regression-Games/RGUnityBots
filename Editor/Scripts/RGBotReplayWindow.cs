using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateActionTypes;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
#endif

namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    public class RGBotReplayWindow : EditorWindow
    {
        public const string PREFAB_PATH = "Packages/gg.regression.unity.bots/Editor/Prefabs";
       
        //TODO: Get this from game engine
        private const int PHYSICS_TICK_RATE = 20; // 0.02 sec or 20 ms per physics tick
        private readonly Color _darkerColor = Color.white * 0.1f;

        private readonly JsonSerializerSettings _jsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full
        };

        private readonly Color _lighterColor = Color.white * 0.3f;
        private readonly Vector3 DESPAWN_TEXT_OFFSET = new(0, 2, 0);

        private readonly Vector3 SPAWN_TEXT_OFFSET = new(0, 5, 0);

        private readonly List<MultiColumnHeaderState.Column> _columns = new();
        private MultiColumnHeader _multiColumnHeader;

        private MultiColumnHeaderState _multiColumnHeaderState;
        private Vector2 _scrollPosition;

        private GameObject actionsObject;

        private int currentTick;
        private Object despawnPrefab;

        private bool fileLoaded;

        private string fileName = "";
        private bool groupEnabled;
        private int highestTick;

        private long lastTickPlayTime = -1;

        private int lowestTick;

        private Object objectPrefab;
        private int playbackRate = 50;

        private bool playing;

        private Object pPrefab;
        private int priorTick;

        private GameObject rootObject;

        private Object spawnPrefab;
        private GameObject spawnsObject;

        private Object targetPrefab;

        private readonly RGTickInfoActionManager tickInfoManager = new RGTickInfoActionManager();

        private int tickRate = 50;

        private void OnGUI()
        {
            if (_multiColumnHeader == null || !fileLoaded) CreateTimelineHeaders();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bot Replay Zip: ", GUILayout.ExpandWidth(false))) OpenFile();

            EditorGUILayout.LabelField(fileName, GUILayout.ExpandWidth(false));
            EditorGUI.BeginDisabledGroup(fileName == null || fileName.Length < 1);
            if (GUILayout.Button("Reload ...", GUILayout.ExpandWidth(false))) Reload();
            EditorGUI.EndDisabledGroup();
            
            GUILayout.FlexibleSpace();
                        

            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            if (!ReplayModelManager.GetInstance().HasEntries())
            {
                EditorGUILayout.HelpBox("Custom Replay Models have not been configured for this project.\nLoad your first replay zip to auto populate the entity types, then configure their model associations using the `Configure Custom Replay Models` button.\n(You can also manually add, edit, or remove associations at any time.)", MessageType.Warning, true);
            }
            
            // Button for registering custom replay models
            if (GUILayout.Button("Configure Custom Replay Models", new GUILayoutOption[] {GUILayout.ExpandWidth(false)}))
            {
                // create or load the prefabs list
                ShowReplayModelsInspector();
            };

            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(!fileLoaded);

            GUILayout.Space(5);

            var tickStyle = new GUIStyle(GUI.skin.textField);
            tickStyle.alignment = TextAnchor.MiddleRight;

            GUILayout.Label("Tick Inspector", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Recorded Tick Rate:", GUILayout.ExpandWidth(false));
            GUILayout.Label($"{tickRate}", GUILayout.ExpandWidth(false), GUILayout.MinWidth(20f));
            GUILayout.Label($"(every {Math.Round((float)tickRate * PHYSICS_TICK_RATE, 2)} ms)",
                GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Playback Tick Rate:", GUILayout.ExpandWidth(false));
            var pbText = GUILayout.TextField($"{playbackRate}", tickStyle, GUILayout.ExpandWidth(false),
                GUILayout.MinWidth(20f));
            pbText = Regex.Replace(pbText, @"[^0-9 ]", "");
            int.TryParse(pbText, out playbackRate);
            GUILayout.Label($"(every {Math.Round((float)playbackRate * PHYSICS_TICK_RATE, 2)} ms)",
                GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Tick:", GUILayout.ExpandWidth(false));

            var ctText = GUILayout.TextField($"{currentTick}", tickStyle, GUILayout.ExpandWidth(false),
                GUILayout.MinWidth(20f));
            ctText = Regex.Replace(ctText, @"[^0-9 ]", "");
            int.TryParse(ctText, out currentTick);

            GUILayout.Label("of ", GUILayout.ExpandWidth(false));
            GUILayout.Label($"{(highestTick - lowestTick) / tickRate + 1}", GUILayout.ExpandWidth(false),
                GUILayout.MinWidth(20f));

            // step back
            if (GUILayout.Button("|◀︎", GUILayout.ExpandWidth(false))) StepBack();
            // play
            if (GUILayout.Button("▶︎", GUILayout.ExpandWidth(false))) Play();
            // pause
            if (GUILayout.Button("||", GUILayout.ExpandWidth(false))) Pause();
            // step forward
            if (GUILayout.Button("︎▶︎|", GUILayout.ExpandWidth(false))) StepForward();
            EditorGUILayout.EndHorizontal();


            // Timeline window
            RenderTimelineView();


            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                UpdateForCurrentTick();
            }
            else
            {
                // do tick updates
                if (currentTick != priorTick) UpdateForCurrentTick();
            }
        }

        public void OnInspectorUpdate()
        {
            // 10 times a second force a repaint, thus when we're 'playing'.. it will show it smoothly
            Repaint();
        }

        private void CreateTimelineHeaders()
        {
            _columns.Clear();
            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = false,
                width = 30.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("□", "Whether to render this entity."),
                headerTextAlignment = TextAlignment.Center
            });
            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = false,
                width = 30.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("→", "Show path history for this entity?"),
                headerTextAlignment = TextAlignment.Center
            });
            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = false,
                width = 30.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("○", "Show highlight circle for this entity?"),
                headerTextAlignment = TextAlignment.Center
            });
            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = false,
                width = 30.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("≡", "Show actions for this entity?"),
                headerTextAlignment = TextAlignment.Center
            });
            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = false,
                width = 150.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("PlayerId", "Type and Id of the entity."),
                headerTextAlignment = TextAlignment.Center
            });
            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = false,
                width = 30.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("❤︎", "Is this entity a human/bot player?"),
                headerTextAlignment = TextAlignment.Center
            });

            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = true,
                minWidth = 150.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("Actions", "Actions entity took in the current tick."),
                headerTextAlignment = TextAlignment.Center
            });
            
            _columns.Add(new MultiColumnHeaderState.Column
            {
                allowToggleVisibility = false,
                autoResize = true,
                minWidth = 150.0f,
                canSort = false,
                sortingArrowAlignment = TextAlignment.Right,
                headerContent = new GUIContent("Validations", "Bot validations for entity in the current tick."),
                headerTextAlignment = TextAlignment.Center
            });

            _multiColumnHeaderState = new MultiColumnHeaderState(_columns.ToArray());

            _multiColumnHeader = new MultiColumnHeader(_multiColumnHeaderState);

            // When we change visibility of the column we resize columns to fit in the window.
            _multiColumnHeader.visibleColumnsChanged += multiColumnHeader => multiColumnHeader.ResizeToFit();

            // Initial resizing of the content.
            _multiColumnHeader.ResizeToFit();
        }

        private string getTimeStampForTick(int tickNumber)
        {
            var tickMs = (int)Math.Round((float)tickRate * PHYSICS_TICK_RATE, 2);

            long totalMs = tickNumber * tickMs;

            return $"{totalMs / 1000 / 60:00}:{totalMs / 1000 % 60:00}.{totalMs % 1000:000}";
        }

        private int compareListElements([CanBeNull] RGEntityTickInfo aTi, [CanBeNull] RGEntityTickInfo bTi,
            RGEntityReplayData a, RGEntityReplayData b)
        {
            if (aTi != null && bTi == null) return -1;
            if (bTi != null && aTi == null) return 1;

            // if same type, sort by id
            if (a.type == b.type)
            {
                // group the positive ids and negative ids, but show them both ascending as though unsigned
                if (a.id >= 0)
                    if (b.id < 0)
                        // put the positive a first
                        return -1;

                if (b.id >= 0)
                    if (a.id < 0)
                        // put the positive b first
                        return 1;
                return (int)(Math.Abs(a.id) - Math.Abs(b.id));
            }

            // sort Players to front
            if (a.isPlayer && !b.isPlayer) return -1;
            if (b.isPlayer && !a.isPlayer) return 1;

            // else sort by type
            if (a.type == null)
            {
                return 1;
            }

            return a.type.CompareTo(b.type);
        }

        private void RenderTimelineView()
        {
            const float SCROLL_BAR_OFFSET = 14.0f;
            var rowHeight = EditorGUIUtility.singleLineHeight;

            // This keeps the rect from overlapping the prior UI element in the window
            GUILayout.FlexibleSpace();

            var windowRect = GUILayoutUtility.GetLastRect();

            // BOUNDING RECT
            var boundingRect = new Rect(windowRect);
            boundingRect.width = position.width;

            // give room so scroll bar doesn't overlap content
            var rowWidth = boundingRect.width - SCROLL_BAR_OFFSET;

            if (_multiColumnHeader == null) CreateTimelineHeaders();

            // COLUMN HEADER RECT
            var columnHeaderRect = new Rect(boundingRect)
            {
                width = rowWidth,
                height = rowHeight
            };

            // Draw header for columns here.
            _multiColumnHeader.OnGUI(columnHeaderRect, 0.0f);

            // CONTENT AREA RECT 
            var contentRect = new Rect(boundingRect);
            contentRect.height = boundingRect.height - columnHeaderRect.height;
            contentRect.y = columnHeaderRect.y + columnHeaderRect.height;

            var rData = tickInfoManager.GetAllEntities();

            // INNER SCROLLABLE RECT
            var scrollableContentRect = new Rect(0, 0, rowWidth, rowHeight * rData.Count);

            _scrollPosition = GUI.BeginScrollView(
                contentRect,
                _scrollPosition,
                scrollableContentRect,
                false,
                false
            );
            {
                // Scroll View Scope.
                var a = 0;

                var toggleRectPadding = new Vector2(8.0f, 0f);

                var labelGUIStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter
                };

                var actionLabelGUIStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                actionLabelGUIStyle.normal.textColor = Color.white;

                var validationLabelGUIStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                };
                validationLabelGUIStyle.normal.textColor = Color.white;

                var nameFieldGUIStyle = new GUIStyle(GUI.skin.label)
                {
                    padding = new RectOffset(10, 10, 2, 2)
                };

                // sort rData based on active for current tick

                var tickInfos = new Dictionary<long, RGEntityTickInfo>();

                for (var i = 0; i < rData.Count; i++)
                    tickInfos[rData[i].id] = RGEntityDataForTick.FromReplayData(rData[i], currentTick)?.tickInfo;

                rData.Sort((a, b) => compareListElements(tickInfos[a.id], tickInfos[b.id], a, b));

                for (var i = 0; i < rData.Count; i++)
                {
                    var data = rData[i];
                    var dataTickInfo = tickInfos[data.id];
                    var isSpawned = dataTickInfo != null;

                    // should be similar size to column header rect, just different y offset
                    var rowRect = new Rect(columnHeaderRect);
                    rowRect.y = rowHeight * a;

                    // Draw a texture before drawing each of the fields for the whole row.
                    if (a % 2 == 0)
                        EditorGUI.DrawRect(rowRect, _darkerColor);
                    else
                        EditorGUI.DrawRect(rowRect, _lighterColor);

                    // all this.. just the 'center' the stupid toggle


                    var columnIndex = 0;
                    // Enable field
                    if (_multiColumnHeader.IsColumnVisible(columnIndex))
                    {
                        data.enabled = EditorGUI.Toggle(
                            CreateInsetRect(columnIndex, rowRect.y, toggleRectPadding),
                            data.enabled
                        );
                    }

                    // Path field
                    ++columnIndex;
                    if (_multiColumnHeader.IsColumnVisible(columnIndex))
                    {
                        data.showPath = EditorGUI.Toggle(
                            CreateInsetRect(columnIndex, rowRect.y, toggleRectPadding),
                            data.showPath
                        );
                    }

                    // Highlight field
                    ++columnIndex;
                    if (_multiColumnHeader.IsColumnVisible(columnIndex))
                    {
                        data.showHighlight = EditorGUI.Toggle(
                            CreateInsetRect(columnIndex, rowRect.y, toggleRectPadding),
                            data.showHighlight
                        );
                    }

                    // Actions field
                    ++columnIndex;
                    if (_multiColumnHeader.IsColumnVisible(columnIndex))
                    {
                        data.showActions = EditorGUI.Toggle(
                            CreateInsetRect(columnIndex, rowRect.y, toggleRectPadding),
                            data.showActions
                        );
                    }

                    EditorGUI.BeginDisabledGroup(!isSpawned); // use this to grey out the text for not-spawned 
                    // Name field.
                    ++columnIndex;
                    if (_multiColumnHeader.IsColumnVisible(columnIndex))
                    {
                        var visibleColumnIndex =
                            _multiColumnHeader.GetVisibleColumnIndex(columnIndex);
                        var columnRect =
                            _multiColumnHeader.GetColumnRect(visibleColumnIndex);
                        // This here basically is a row height
                        columnRect.y = rowRect.y;
                        EditorGUI.LabelField(
                            _multiColumnHeader.GetCellRect(visibleColumnIndex,
                                columnRect),
                            new GUIContent(data.objectName),
                            nameFieldGUIStyle
                        );
                    }

                    EditorGUI.EndDisabledGroup();

                    // Is Player field
                    ++columnIndex;
                    if (_multiColumnHeader.IsColumnVisible(columnIndex))
                    {
                        EditorGUI.LabelField(
                            CreateInsetRect(columnIndex, rowRect.y, null),
                            data.isPlayer ? "❤︎" : "",
                            labelGUIStyle
                        );
                    }

                    // Actions column
                    ++columnIndex;
                    if (_multiColumnHeader.IsColumnVisible(columnIndex))
                        if (isSpawned)
                        {
                            var actions = dataTickInfo.actions;
                       
                            // Fit each action equally into the space with text hover help
                            for (var j = 0; j < actions.Length; j++)
                            {
                                // draw a colored rect
                                var actionRect = CreateInfoRect(columnIndex, rowRect.y, actions.Length, j);
                                EditorGUI.DrawRect(actionRect, Color.gray * Color.yellow);
                                
                                // then put a label in it
                                var label = new GUIContent($"{actions[j].action}", $"{textForAction(j + 1, actions[j])}");
                                EditorGUI.DropShadowLabel(actionRect, label, actionLabelGUIStyle);
                            }
                        }
                    
                        // Validations column
                        ++columnIndex;
                        if (_multiColumnHeader.IsColumnVisible(columnIndex))
                            if (isSpawned)
                            {
                                var validationResults = dataTickInfo.validationResults;
                                
                                // Fit each validation equally into the space with text hover help
                                for (var j = 0; j < validationResults.Length; j++)
                                {
                                    var currentValidation = validationResults[j];

                                    // draw a colored rect
                                    var validationRect = CreateInfoRect(columnIndex, rowRect.y, validationResults.Length, j);
                                    var color = currentValidation.passed ? Color.green : Color.red;
                                    EditorGUI.DrawRect(validationRect, Color.gray * color);
                                    
                                    // then put a label in it
                                    var tooltip = (currentValidation.passed ? "[PASSED] " : "[FAILED] ") + currentValidation.message;
                                    var label = new GUIContent(currentValidation.message, tooltip);
                                    EditorGUI.DropShadowLabel(validationRect, label, validationLabelGUIStyle);
                                }
                            }

                    ++a;
                }
            }
            GUI.EndScrollView(true);
        }

        /**
         * Create a Rect for timeline components like checkboxes 
         */
        private Rect CreateInsetRect(int columnIndex, float height, Vector2? padding)
        {
            var visibleColumnIndex = _multiColumnHeader.GetVisibleColumnIndex(columnIndex);
            var columnRect = _multiColumnHeader.GetColumnRect(visibleColumnIndex);
            columnRect.y = height;

            var insetRect = new Rect(_multiColumnHeader.GetCellRect(
                visibleColumnIndex,
                columnRect));

            if (padding.HasValue)
            {
                insetRect.x += padding.Value.x;
                insetRect.width -= padding.Value.x * 2;

                insetRect.y += padding.Value.y;
                insetRect.height -= padding.Value.y * 2;
            }
            
            return insetRect;
        }

        /**
         * Create a Rect that is wide enough to contain text information.
         * For Actions and validationResults
         */
        private Rect CreateInfoRect(int columnIndex, float height, int componentsPerRow, int xOffset)
        {
            var insetRect = CreateInsetRect(columnIndex, height, null);
            
            const float minWidth = 5f;
            const float maxWidth = 150f;
            var width = insetRect.width / Math.Max(1, componentsPerRow);
            width = Math.Min(Math.Max(width, minWidth), maxWidth);
            
            // leave a 1 pixel gap .. this keeps many actions from flowing together
            insetRect.width = width - 1;
            insetRect.x += width * xOffset;
            return insetRect;
        }

        [MenuItem("Regression Games/Bot Replay")]
        public static void ShowWindow()
        {
            GetWindow(typeof(RGBotReplayWindow), false, "RG Bot Replay");
        }
        
        // Did not add to the top menu yet, but might in the future
        //[MenuItem("Regression Games/Configure Custom Replay Models")]
        public static void ShowReplayModelsInspector()
        {
            ReplayModelManager.GetInstance().OpenAssetInspector();
        }

        private void UpdateForCurrentTick()
        {
            if (currentTick > 0)
            {
                // populate our prefab into the scene
                if (pPrefab == null)
                    pPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{PREFAB_PATH}/RegressionGames_Bot_Replay.prefab");

                // pick the scene that matches the file or throw error
                if (rootObject == null)
                {
                    rootObject = Instantiate(pPrefab) as GameObject;
                    spawnsObject = rootObject.transform.GetChild(0).gameObject;
                    actionsObject = rootObject.transform.GetChild(1).gameObject;
                }
                // TODO: Set scene parent transform
                //rootObject.transform.SetParent();


                // cleanup all the spawn stuff from prior ticks
                if (spawnsObject != null)
                {
                    var childCount = spawnsObject.transform.childCount;
                    for (var i = childCount - 1; i >= 0; i--)
                    {
                        var child = spawnsObject.transform.GetChild(i).gameObject;
                        DestroyImmediate(child);
                    }
                }

                // cleanup all target info from prior ticks
                if (actionsObject != null)
                {
                    var childCount = actionsObject.transform.childCount;
                    for (var i = childCount - 1; i >= 0; i--)
                    {
                        var child = actionsObject.transform.GetChild(i).gameObject;
                        DestroyImmediate(child);
                    }
                }

                var namesInState = new HashSet<string>();

                var playerIds = tickInfoManager.GetAllEntityIds();
                foreach (var playerId in playerIds)
                {
                    var tickData = tickInfoManager.GetEntityInfoForTick(currentTick, playerId);
                    if (tickData != null && tickData.data.enabled)
                    {
                        var objectName = $"{tickData.data.type}_{tickData.data.id}";
                        if (tickData.tickInfo != null) namesInState.Add(objectName);

                        UpdatePlayerForGameState(tickData);
                        UpdatePlayerForActions(tickData);
                    }
                    else
                    {
                        RGDebug.LogWarning(
                            $"WARNING: No tickData available for tick: {currentTick}, rendering prior tick data.");
                    }

                    priorTick = currentTick;
                }

                // cleanup now 'dead' objects .. 
                var childCt = rootObject.transform.childCount;
                for (var i = childCt - 1; i >= 0; i--)
                {
                    var type = rootObject.transform.GetChild(i);
                    var typeChildCount = type.transform.childCount;
                    var typeName = type.gameObject.name;
                    if (typeName != "spawns" && typeName != "actions")
                        for (var j = typeChildCount - 1; j >= 0; j--)
                        {
                            var obj = type.transform.GetChild(j);
                            if (!namesInState.Contains(obj.gameObject.name)) DestroyImmediate(obj.gameObject);
                        }
                }
            }
        }

        private void UpdatePlayerForGameState(RGEntityDataForTick tickData)
        {
            // populate the 'state' into our scene 

            // id, type, position, rotation
            if (tickData.tickInfo != null)
            {
                var ti = tickData.tickInfo;

                if (ti.state != null)
                {
                    Vector3? position = null;
                    Quaternion? rotation = null;
                    string characterType = (string)ti?.state?.GetValueOrDefault("characterType", "");

                    if (ti?.state?.position != null)
                    {
                        position = ti.state.position;
                    }
                    
                    if (ti?.state?.rotation != null)
                    {
                        rotation = ti.state.rotation;
                    }
                    
                    var typeRootName = $"{tickData.data.type}s";
                    var typeRoot = findChildByName(rootObject.transform, typeRootName);
                    if (typeRoot == null)
                    {
                        typeRoot = new GameObject(typeRootName);
                        typeRoot.transform.parent = rootObject.transform;
                    }

                    var objectName = tickData.data.objectName;

                    var obj = findChildByName(typeRoot.transform, objectName);
                    if (obj == null)
                    {
                        // create the object
                        if (objectPrefab == null)
                            objectPrefab =
                                AssetDatabase.LoadAssetAtPath<GameObject>(
                                    $"{PREFAB_PATH}/RGReplayObject.prefab");
                        // do not do position rotation here as we do that on a child of the spawn
                        obj = Instantiate(objectPrefab, typeRoot.transform) as GameObject;

                        obj.name = objectName;
                        // set name tag
                        obj.transform.GetChild(0).GetChild(0).gameObject.GetComponent<TextMeshPro>().text =
                            objectName;

                        //SETUP THE MODEL
                        var rmm = ReplayModelManager.GetInstance();
                        var modelPrefab = rmm.getModelPrefabForType(tickData.data.type, characterType);
                        if (modelPrefab != null)
                        {
                            var model = Instantiate(modelPrefab, Vector3.zero, Quaternion.identity,
                                obj.transform.GetChild(0).GetChild(1));
                        }
                        else
                        {
                            // some things don't have models setup (because they are pre-populated into the scene
                            // remove their model/arrow altogether
                            DestroyImmediate(obj.transform.GetChild(0).GetChild(1).gameObject);
                        }
                    }

                    if (tickData.data.showHighlight)
                        // enable the 'my bot' showHighlight circle
                        obj.transform.GetChild(0).GetChild(2).gameObject.SetActive(true);
                    else
                        obj.transform.GetChild(0).GetChild(2).gameObject.SetActive(false);

                    // set/update the position/rotation on the internal object for the model
                    // we do this so that the the top level can manage breadcrumb trails/effect positions/etc
                    if (position != null)
                    {
                        obj.transform.GetChild(0).position = (Vector3)position;
                        if (tickData.justSpawned)
                        {
                            // create 'spawn' effect
                            if (spawnPrefab == null)
                                spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                                    $"{PREFAB_PATH}/SPAWN.prefab");

                            var spawn = Instantiate(spawnPrefab, (Vector3)position + SPAWN_TEXT_OFFSET,
                                Quaternion.identity,
                                spawnsObject.transform) as GameObject;
                        }
                    }

                    if (rotation != null) obj.transform.GetChild(0).rotation = (Quaternion)rotation;

                    // setup pathing lines for bots
                    if (tickData.data.showPath)
                    {
                        var points = tickInfoManager.GetPathForEntityId(currentTick, tickData.data.id);
                        if (points.Length > 0)
                        {
                            obj.transform.GetChild(1).gameObject.SetActive(true);
                            var lr = obj.transform.GetChild(1).GetComponent<LineRenderer>();
                            lr.positionCount = points.Length;
                            lr.SetPositions(points);
                        }
                        else
                        {
                            obj.transform.GetChild(1).gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        obj.transform.GetChild(1).gameObject.SetActive(false);
                    }
                }
            }

            // handle showing despawn indicator
            if (tickData.justDespawned)
            {
                var priorPosition = tickInfoManager.GetEntityInfoForTick(currentTick - 1, tickData.data.id)?.tickInfo
                    ?.state.position;
                var pos = (priorPosition ?? Vector3.zero) + DESPAWN_TEXT_OFFSET;
                // create 'de-spawn' effect
                if (despawnPrefab == null)
                    despawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{PREFAB_PATH}/DE-SPAWN.prefab");

                var despawn =
                    Instantiate(despawnPrefab, pos, Quaternion.identity,
                        spawnsObject.transform) as GameObject;
                despawn.transform.GetChild(0).GetComponent<TextMeshPro>().text = tickData.data.objectName;
            }
        }

        private void UpdatePlayerForActions(RGEntityDataForTick tickData)
        {
            if (tickData.data.showActions && tickData.tickInfo != null && tickData.tickInfo.actions.Length > 0)
            {
                var actionNumber = 1;
                foreach (var action in tickData.tickInfo.actions)
                {
                    var actionType = action.action;
                    if (targetPrefab == null)
                        targetPrefab =
                            AssetDatabase.LoadAssetAtPath<GameObject>(
                                $"{PREFAB_PATH}/Target.prefab");

                    var actionText = textForAction(actionNumber, action);

                    Vector3? position = null;
                    long? targetId = null;
                    if (action.Input.ContainsKey("targetId") && action.Input["targetId"] != null)
                        targetId = long.Parse(action.Input["targetId"].ToString());


                    switch (actionType)
                    {
                        case "PerformSkill":
                            var skillId = long.Parse(action.Input["skillId"].ToString());
                            if (action.Input.ContainsKey("xPosition") && action.Input["xPosition"] != null)
                            {
                                var xPosition = float.Parse(action.Input["xPosition"].ToString());
                                var yPosition = float.Parse(action.Input["yPosition"].ToString());
                                var zPosition = float.Parse(action.Input["zPosition"].ToString());
                                position = new Vector3(xPosition, yPosition, zPosition);
                            }

                            break;
                        case "FollowObject":
                            var range = float.Parse(action.Input["range"].ToString());
                            break;
                    }

                    if (position == null)
                    {
                        // get the position for the targetId
                        if (targetId != null)
                        {
                            var ti = tickInfoManager.GetEntityInfoForTick(currentTick, targetId.Value)
                                ?.tickInfo;
                            if (ti?.state.position != null) position = ti.state.position;
                        }

                        // if still null
                        if (position == null && tickData.tickInfo?.state.position != null)
                            // targeting the bot's self
                            position = tickData.tickInfo?.state.position.Value;
                    }

                    if (position == null) position = Vector3.zero;

                    var targetObject =
                        Instantiate(targetPrefab, (Vector3)position, Quaternion.identity,
                            actionsObject.transform) as GameObject;
                    targetObject.transform.GetChild(0).GetComponent<TextMeshPro>().text = actionText;

                    ++actionNumber;
                }
            }
        }

        private string textForAction(int actionNumber, RGActionRequest action)
        {
            var actionType = action.action;
            var actionText = $"Action: {actionNumber}";

            Vector3? position = null;
            long? targetId = null;
            if (action.Input.ContainsKey("targetId") && action.Input["targetId"] != null)
                targetId = long.Parse(action.Input["targetId"].ToString());
            actionText += $"\r\nTargetId: {(targetId != null ? targetId : "n/a")}";

            switch (actionType)
            {
                case "PerformSkill":
                    var skillId = long.Parse(action.Input["skillId"].ToString());
                    actionText += $"\r\n{actionType}: {skillId}";
                    if (action.Input.ContainsKey("xPosition") && action.Input["xPosition"] != null)
                    {
                        var xPosition = float.Parse(action.Input["xPosition"].ToString());
                        var yPosition = float.Parse(action.Input["yPosition"].ToString());
                        var zPosition = float.Parse(action.Input["zPosition"].ToString());
                        position = new Vector3(xPosition, yPosition, zPosition);
                        actionText += $"\r\nPosition: {xPosition}, {yPosition}, {zPosition}";
                    }

                    break;
                case "FollowObject":
                    var range = float.Parse(action.Input["range"].ToString());
                    actionText += $"\r\nRange: {range}";
                    break;
            }

            return actionText;
        }

        private void Reload()
        {
            if (fileName != null && fileName.Length > 0)
            {
                // LOAD THE FILE
                if (ParseFile(fileName))
                {
                    RGDebug.LogInfo("Bot Replay Zip Parsed... Populating Scene Assets");
                    fileLoaded = true;
                    CreateTimelineHeaders();
                    UpdateForCurrentTick();
                }
                else
                {
                    RGDebug.LogError("ERROR: Failed to parse RG Replay Zip File");
                }
            }
        }


        private void OpenFile()
        {
            var path = EditorUtility.OpenFilePanel("RG Bot Replay Zip", "", "zip");
            if (path.Length != 0)
            {
                // LOAD THE FILE
                if (ParseFile(path))
                {
                    RGDebug.LogInfo("Bot Replay Zip Parsed... Populating Scene Assets");
                    fileLoaded = true;
                    CreateTimelineHeaders();
                    UpdateForCurrentTick();
                }
                else
                {
                    RGDebug.LogError("ERROR: Failed to parse RG Replay Zip File");
                }
            }
        }

        private int getTickNumberFromFileName(string name)
        {
            return int.Parse(name.Split('.', '_')[3]);
        }

        private bool ParseFile(string filename)
        {
            fileName = filename;
            using (var zip = ZipFile.Open(filename, ZipArchiveMode.Read))
            {
                // get our ticks in order
                var entries = zip.Entries.ToList();
                entries.Sort((a, b) =>
                    getTickNumberFromFileName(a.Name) - getTickNumberFromFileName(b.Name)
                );

                var firstTick = entries.Find(entry => true);
                var lastTick = entries.FindLast(entry => true);
                if (firstTick != null && lastTick != null)
                {
                    // purge old stuff
                    DestroyOn.DestroyEverything();
                    tickInfoManager.Reset();

                    fileLoaded = false;
                    currentTick = 1;
                    tickRate = 50;
                    playbackRate = 50;
                    lowestTick = getTickNumberFromFileName(firstTick.Name);
                    highestTick = getTickNumberFromFileName(lastTick.Name);

                    var lastProcessedTick = -1;

                    var tickIndexNumber = 1; // 1->N
                    foreach (var entry in entries)
                    {
                        var tickNumber = getTickNumberFromFileName(entry.Name);
                        if (lastProcessedTick >= 0 && tickNumber != lastProcessedTick + tickRate)
                            RGDebug.LogWarning(
                                $"WARNING: Tick info missing for tick(s) {lastProcessedTick + 1} -> {tickNumber - 1}.  This normally means your RG bot was processing too slowly and skipped ticks.");
                        else
                            using (var sr = new StreamReader(entry.Open()))
                            {
                                var rData = JsonConvert.DeserializeObject<RGStateActionReplayData>(sr.ReadToEnd(),
                                    _jsonSettings);
                                tickInfoManager.processTick(tickIndexNumber, rData.tickInfo);
                                if (rData.playerId != null && rData.playerId != -1)
                                    tickInfoManager.processReplayData(tickIndexNumber, (long)rData.playerId, rData);
                                    
                                if (rData.tickRate != null)
                                    // get the right tickRate
                                    tickRate = (int)rData.tickRate;
                                lastProcessedTick = tickNumber;
                                ++tickIndexNumber;
                            }
                    }
                    
                    // save any assets we created, like replay model associations
                    // we do this once here instead of internally when adding the asset for
                    // performance reasons.. this is expensive
                    AssetDatabase.SaveAssets();

                    return true;
                }
            }

            return false;
        }

        [CanBeNull]
        private GameObject findChildByName(Transform parent, string name)
        {
            var childCount = parent.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var t = parent.GetChild(i);
                if (t.gameObject.name == name) return t.gameObject;
            }

            return null;
        }

        private void StepBack()
        {
            playing = false;
            var priorTick = currentTick;
            currentTick = currentTick > 1 ? currentTick - 1 : currentTick;
        }

        private void Play()
        {
            playing = true;
            lastTickPlayTime = -1;
            Task.Run(() =>
            {
                while (playing)
                {
                    var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (currentTime - lastTickPlayTime >= playbackRate * PHYSICS_TICK_RATE)
                    {
                        lastTickPlayTime = currentTime;
                        doStepForward();
                    }
                }
            });
        }

        private void Pause()
        {
            playing = false;
        }

        private void StepForward()
        {
            playing = false;
            doStepForward();
        }

        private void doStepForward()
        {
            var priorTick = currentTick;
            currentTick = (currentTick - 1) * tickRate + lowestTick < highestTick ? currentTick + 1 : currentTick;

            if (currentTick == priorTick) playing = false;
        }
    }
    #endif
}
