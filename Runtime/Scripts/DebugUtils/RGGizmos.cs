using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.RGBotConfigs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.DebugUtils
{
    
    /**
     * A set of debug utilities for drawing gizmos and text on top of entities in your scene.
     */
    // ReSharper disable once InconsistentNaming
    public class RGGizmos
    {
        private readonly object _lock = new object();
        
        const string BillboardTextAsset = "AgentBillboardText";
        
        private readonly Dictionary<object, (int, Vector3, Color)> _linesFromEntityToPosition = new();
        private readonly Dictionary<object, (int, int, Color)> _linesFromEntityToEntity = new();
        private readonly Dictionary<object, (Vector3, Vector3, Color)> _linesFromPositionToPosition = new();
        private readonly Dictionary<object, (Vector3, Color, float, bool)> _spheresAtPosition = new();
        private readonly Dictionary<object, (int, Color, float, bool)> _spheresAtEntity = new();
        private readonly Dictionary<int, (string, float)> _billboardsToDraw = new();
        private readonly Dictionary<int, GameObject> _drawnBillboards = new();
        private readonly Dictionary<object, List<object>> _lineGroups = new();

        // Billboard text objects
        private readonly GameObject _billboardAsset;

        public RGGizmos() => _billboardAsset = Resources.Load<GameObject>(BillboardTextAsset);

        /**
         * <summary>
         * Creates a line from the entity with the given id to a specific position. The line
         * will persist until removed using `DestroyLine(name)` or `DestroyAllLines()`.
         * </summary>
         * <param name="startEntityId">The id of the entity this line should start at.</param>
         * <param name="endPosition">The end position of the line.</param>
         * <param name="color">The color of the line.</param>
         * <param name="name">The name of the line. Used to remove the line later.</param>
         * <example>
         * <code>
         * // Create a line from the entity with id 1 to the position (0, 0, 0) with the color red.
         * RGGizmos.CreateLine(1, new Vector3(0, 0, 0), Color.red, "myLine");
         * </code>
         * </example>
         * <seealso cref="DestroyLine"/>
         * <seealso cref="DestroyAllLines"/>
         */
        public void CreateLine(int startEntityId, Vector3 endPosition, Color color, object name)
        {
            lock (_lock)
            {
                _linesFromEntityToPosition[name] = (startEntityId, endPosition, color);
            }
        }


        /**
         * <summary>
         * Creates a line from a position to an entity with the given id. The line
         * will persist until removed using `DestroyLine(name)` or `DestroyAllLines()`.
         * </summary>
         * <param name="startPosition">The start position of the line.</param>
         * <param name="endEntityId">The id of the entity this line should end at.</param>
         * <param name="color">The color of the line.</param>
         * <param name="name">The name of the line. Used to remove the line later.</param>
         * <example>
         * <code>
         * // Create a line from the position (0, 0, 0) to the entity with id 1 with the color red.
         * RGGizmos.CreateLine(new Vector3(0, 0, 0), 1, Color.red, "myLine");
         * </code>
         * </example>
         * <seealso cref="DestroyLine"/>
         * <seealso cref="DestroyAllLines"/>
         */
        public void CreateLine(Vector3 startPosition, int endEntityId, Color color, object name)
        {
            lock (_lock)
            {
                CreateLine(endEntityId, startPosition, color, name);
            }
        }

        /**
             * <summary>
             * Creates a line between two entities with the given ids. The line
             * will persist until removed using `DestroyLine(name)` or `DestroyAllLines()`.
             * </summary>
             * <param name="startEntityId">The id of the entity this line should start at.</param>
             * <param name="endEntityId">The id of the entity this line should end at.</param>
             * <param name="color">The color of the line.</param>
             * <param name="name">The name of the line. Used to remove the line later.</param>
             * <example>
             * <code>
             * // Create a line from an entity with id 5 to and entity with id 10 with the color green
             * RGGizmos.CreateLine(5, 10, Color.green, "myLine");
             * </code>
             * </example>
             * <seealso cref="DestroyLine"/>
             * <seealso cref="DestroyAllLines"/>
             */
        public void CreateLine(int startEntityId, int endEntityId, Color color, object name)
        {
            lock (_lock)
            {
                _linesFromEntityToEntity[name] = (startEntityId, endEntityId, color);
            }
        }

        /**
             * <summary>
             * Creates a line between two positions. The line will persist until removed using
             * `DestroyLine(name)` or `DestroyAllLines()` or `DestroyAllLines(groupName)`.
             * </summary>
             * <param name="startPosition">The start position of the line.</param>
             * <param name="endPosition">The end position of the line</param>
             * <param name="color">The color of the line.</param>
             * <param name="name">The name of the line. Used to remove the line later.</param>
             * <param name="groupId">The groupId with which to associate this line (optional). Used to remove the line later.</param>
             * <example>
             * <code>
             * // Create a line from the position (0, 0, 0) to position (1, 1, 1) with the color red.
             * RGGizmos.CreateLine(new Vector3(0, 0, 0), new Vector3(1, 1, 1), Color.red, "myLine");
             * </code>
             * </example>
             * <seealso cref="DestroyLine"/>
             * <seealso cref="DestroyAllLines"/>
             */
        public void CreateLine(Vector3 startPosition, Vector3 endPosition, Color color, object name,
            object groupId = null)
        {
            lock (_lock)
            {
                _linesFromPositionToPosition[name] = (startPosition, endPosition, color);
                if (groupId != null)
                {
                    if (!_lineGroups.TryGetValue(groupId, out var group))
                    {
                        group = new List<object>();
                        _lineGroups[groupId] = group;
                    }

                    group.Add(name);
                }
            }
        }

        /**
         * <summary>
         * Destroys a line with the given name. If no line with the given name exists, nothing happens.
         * </summary>
         * <param name="name">The name of the line to destroy.</param>
         * <example>
         * <code>
         * RGGizmos.DestroyLine("myLine");
         * </code>
         * </example>
         */
        public void DestroyLine(string name)
        {
            lock (_lock)
            {

                _linesFromEntityToPosition.Remove(name, out _);
                _linesFromEntityToEntity.Remove(name, out _);
                _linesFromPositionToPosition.Remove(name, out _);
            }
        }

        /**
         * <summary>
         * Destroys all lines created.
         * </summary>
         * <example>
         * <code>
         * RGGizmos.DestroyAllLines();
         * </code>
         * </example>
         * <seealso cref="DestroyLine"/>
         */
        public void DestroyAllLines()
        {
            lock (_lock)
            {
                _linesFromEntityToPosition.Clear();
                _linesFromEntityToEntity.Clear();
                _linesFromPositionToPosition.Clear();
            }
        }
        
        /**
         * <summary>
         * Destroys all lines created.
         * </summary>
         * <param name="groupId">The group of lines to destroy</param>
         * <example>
         * <code>
         * RGGizmos.DestroyAllLines();
         * </code>
         * </example>
         * <seealso cref="DestroyLine"/>
         */
        public void DestroyAllLines(object groupId)
        {
            lock (_lock)
            {
                if (_lineGroups.TryGetValue(groupId, out var lineNames))
                {
                    foreach (object lineName in lineNames)
                    {
                        _linesFromEntityToPosition.Remove(lineName, out _);
                        _linesFromEntityToEntity.Remove(lineName, out _);
                        _linesFromPositionToPosition.Remove(lineName, out _);
                    }
                    lineNames.Clear();
                }
            }
        }

        /**
         * <summary>
         * Creates a sphere at the given position. The sphere will persist until removed using
         * `DestroySphere(name)` or `DestroyAllSpheres()`.
         * </summary>
         * <param name="position">The position of the sphere.</param>
         * <param name="color">The color of the sphere.</param>
         * <param name="size">The size of the sphere.</param>
         * <param name="isWireframe">Whether the sphere should be rendered as a wireframe (true) or solid (false).</param>
         * <param name="name">The name of the sphere. Used to remove the sphere later.</param>
         * <example>
         * <code>
         * // Create a sphere at the position (0, 0, 0) with the color red and a size of 0.4.
         * RGGizmos.CreateSphere(new Vector3(0, 0, 0), Color.red, 0.4f, false, "mySphere");
         * </code>
         * </example>
         * <seealso cref="DestroySphere"/>
         * <seealso cref="DestroyAllSpheres"/>
         */
        public void CreateSphere(Vector3 position, Color color, float size, bool isWireframe, string name)
        {
            lock (_lock)
            {
                _spheresAtPosition[name] = (position, color, size, isWireframe);
            }
        }

        /**
             * <summary>
             * Creates a sphere at the origin of the entity with the given id. The sphere will persist until removed using
             * `DestroySphere(name)` or `DestroyAllSpheres()`.
             * </summary>
             * <param name="entityId">The entity of the id to place this sphere.</param>
             * <param name="color">The color of the sphere.</param>
             * <param name="size">The size of the sphere.</param>
             * <param name="isWireframe">Whether the sphere should be rendered as a wireframe (true) or solid (false).</param>
             * <param name="name">The name of the sphere. Used to remove the sphere later.</param>
             * <example>
             * <code>
             * // Create a sphere at the origin of an entity with id 1 with the color red and a size of 0.4.
             * RGGizmos.CreateSphere(1, Color.red, 0.4f, false, "mySphere");
             * </code>
             * </example>
             * <seealso cref="DestroySphere"/>
             * <seealso cref="DestroyAllSpheres"/>
             */
        public void CreateSphere(int entityId, Color color, float size, bool isWireframe, string name)
        {
            lock (_lock)
            {
                _spheresAtEntity[name] = (entityId, color, size, isWireframe);
            }
        }

        /**
             * <summary>
             * Destroys a sphere with the given name. If no sphere with the given name exists, nothing happens.
             * </summary>
             * <param name="name">The name of the sphere to destroy.</param>
             * <example>
             * <code>
             * RGGizmos.DestroySphere("mySphere");
             * </code>
             * </example>
             */
        public void DestroySphere(string name)
        {
            lock (_lock)
            {
                _spheresAtPosition.Remove(name, out _);
                _spheresAtEntity.Remove(name, out _);
            }
        }

        /**
         * <summary>
         * Destroys all spheres created.
         * </summary>
         * <example>
         * <code>
         * RGGizmos.DestroyAllSpheres();
         * </code>
         * </example>
         * <seealso cref="DestroySphere"/>
         */
        public void DestroyAllSpheres()
        {
            lock (_lock)
            {
                _spheresAtPosition.Clear();
                _spheresAtEntity.Clear();
            }
        }

        /**
         * <summary>
         * Creates a text billboard on an entity with the given id. The text will persist until removed using
         * `DestroyText(entityId)` or `DestroyAllTexts()`. Use the yOffset parameter to place the billboard text
         * a certain distance above the entity. An entity can only have one billboard text at a time.
         * </summary>
         * <remarks>
         * Note: Since the text billboard feature is not a real Gizmo, you need to make sure to actively
         * remove them if you want them to disappear after turning Gizmos off in your editor. These will
         * still appear even if Gizmos are off and you requested them before the Gizmos were turned off.
         * </remarks>
         * <param name="entityId">The entity of the id to place this text billboard.</param>
         * <param name="content">The content of the text billboard.</param>
         * <param name="yOffset">The y offset of the text billboard (defaults to 2.0).</param>
         * <example>
         * <code>
         * // Create a text billboard on an entity with id 1 with the content "Hello World!".
         * RGGizmos.CreateText(1, "Hello World!");
         * </code>
         * </example>
         * <seealso cref="DestroyText"/>
         * <seealso cref="DestroyAllTexts"/>
         */
        public void CreateText(int entityId, string content, float yOffset = 2.0f)
        {
            lock (_lock)
            {
                _billboardsToDraw[entityId] = (content, yOffset);
            }
        }

        /**
             * <summary>
             * Destroys the text billboard on an entity with the given id. If no text billboard exists on the entity, nothing
             * happens.
             * </summary>
             * <param name="entityId">The entity of the id whose text billboard should be destroyed.</param>
             * <example>
             * <code>
             * RGGizmos.DestroyText(1);
             * </code>
             * </example>
             * <seealso cref="CreateText"/>
             * <seealso cref="DestroyAllTexts"/>
             */
        public void DestroyText(int entityId)
        {
            lock (_lock)
            {
                _billboardsToDraw.Remove(entityId, out _);
            }
        }

        /**
             * <summary>
             * Destroys all text billboards created.
             * </summary>
             * <example>
             * <code>
             * RGGizmos.DestroyAllTexts();
             * </code>
             * </example>
             * <seealso cref="CreateText"/>
             * <seealso cref="DestroyText"/>
             */
        public void DestroyAllTexts()
        {
            lock (_lock)
            {
                _billboardsToDraw.Clear();
            }
        }

        /**
             * <summary>
             * Draws all Gizmos that have been set.
             * </summary>
             */
        protected internal void OnDrawGizmos()
        {
            lock (_lock)
            {

                var gizmosContainer = GameObject.Find("RGGizmosContainer");
                if (gizmosContainer == null)
                {
                    gizmosContainer = new GameObject("RGGizmosContainer");
                }

                // Draw lines
                foreach (var lineParams in _linesFromEntityToPosition.Values)
                {
                    var originInstance = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(lineParams.Item1);
                    if (originInstance != null)
                    {
                        var color = Gizmos.color;
                        Gizmos.color = lineParams.Item3;
                        Gizmos.DrawLine(originInstance.transform.position, lineParams.Item2);
                        Gizmos.color = color;
                    }
                }

                foreach (var lineParams in _linesFromEntityToEntity.Values)
                {
                    var originInstance = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(lineParams.Item1);
                    var endInstance = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(lineParams.Item2);
                    if (originInstance != null && endInstance != null)
                    {
                        var color = Gizmos.color;
                        Gizmos.color = lineParams.Item3;
                        Gizmos.DrawLine(originInstance.transform.position, endInstance.transform.position);
                        Gizmos.color = color;
                    }
                }

                foreach (var lineParams in _linesFromPositionToPosition.Values)
                {
                    var color = Gizmos.color;
                    Gizmos.color = lineParams.Item3;
                    Gizmos.DrawLine(lineParams.Item1, lineParams.Item2);
                    Gizmos.color = color;
                }

                // Draw spheres
                foreach (var sphereParams in _spheresAtPosition.Values)
                {
                    if (sphereParams.Item4)
                    {
                        Gizmos.color = sphereParams.Item2;
                        Gizmos.DrawWireSphere(sphereParams.Item1, sphereParams.Item3);
                    }
                    else
                    {
                        Gizmos.color = sphereParams.Item2;
                        Gizmos.DrawSphere(sphereParams.Item1, sphereParams.Item3);
                    }
                }

                foreach (var sphereParams in _spheresAtEntity.Values)
                {
                    var originInstance = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(sphereParams.Item1);
                    if (originInstance == null) continue;
                    if (sphereParams.Item4)
                    {
                        Gizmos.color = sphereParams.Item2;
                        Gizmos.DrawWireSphere(originInstance.transform.position, sphereParams.Item3);
                    }
                    else
                    {
                        Gizmos.color = sphereParams.Item2;
                        Gizmos.DrawSphere(originInstance.transform.position, sphereParams.Item3);
                    }
                }

                // Delete any billboards that are no longer being drawn
                var billboardsToRemove = _drawnBillboards.Where(b => !_billboardsToDraw.ContainsKey(b.Key));
                foreach (var billboard in billboardsToRemove)
                {
                    Object.Destroy(billboard.Value);
                    _drawnBillboards.Remove(billboard.Key, out _);
                }

                // Now update or create any billboards that are being drawn
                foreach (var billboardParams in _billboardsToDraw)
                {
                    try
                    {
                        // First, skip this billboard if the entity does not exist anymore
                        var entityGameObject = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(billboardParams.Key);
                        if (entityGameObject == null)
                        {
                            _drawnBillboards.Remove(billboardParams.Key, out _);
                            continue;
                        }

                        // If the billboard game object does not exist, create it
                        if (!_drawnBillboards.TryGetValue(billboardParams.Key, out var billboard))
                        {
                            billboard = Object.Instantiate(_billboardAsset, Vector3.zero, Quaternion.identity);
                            billboard.transform.parent = gizmosContainer.transform;
                            _drawnBillboards[billboardParams.Key] = billboard;
                        }

                        // If the billboard exist but is no longer active (i.e. destroyed), skip this
                        if (billboard == null || !billboard.activeInHierarchy)
                        {
                            _drawnBillboards.Remove(billboardParams.Key, out _);
                            continue;
                        }

                        ;

                        // Then set the parameters
                        var billboardText = billboard.GetComponent<BillboardText>();
                        billboardText.content = billboardParams.Value.Item1;
                        billboardText.yOffset = billboardParams.Value.Item2;
                        billboardText.target = entityGameObject.gameObject;
                    }
                    catch (MissingReferenceException e)
                    {
                        // If an exception occurred, it's likely that the existing entity was destroyed.
                        // In that case, just remove the billboard from the list of drawn billboards.
                        _drawnBillboards.Remove(billboardParams.Key, out _);
                    }

                }
            }

        }
        
    }
}