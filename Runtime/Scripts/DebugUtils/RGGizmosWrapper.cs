using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.RGBotConfigs;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace RegressionGames.DebugUtils
{
    
    /**
     * A set of debug utilities for drawing gizmos and text on top of entities in your scene.
     */
    // ReSharper disable once InconsistentNaming
    public class RGGizmos
    {
        
        private readonly Dictionary<string, Tuple<int, Vector3, Color>> _linesFromEntityToPosition = new();
        private readonly Dictionary<string, Tuple<int, int, Color>> _linesFromEntityToEntity = new();
        private readonly Dictionary<string, Tuple<Vector3, Vector3, Color>> _linesFromPositionToPosition = new();
        private readonly Dictionary<string, Tuple<Vector3, Color, float, bool>> _spheresAtPosition = new();
        private readonly Dictionary<string, Tuple<int, Color, float, bool>> _spheresAtEntity = new();
        private readonly Dictionary<int, Tuple<string, float>> _billboardsToDraw = new();
        private readonly Dictionary<int, GameObject> _drawnBillboards = new();
        
        // Billboard text objects
        private readonly GameObject _billboardAsset;

        public RGGizmos()
        {
            var path = "Packages/gg.regression.unity.bots/Runtime/Prefabs/AgentBillboardText.prefab";
            _billboardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /**
         * Creates a line from the entity with the given id to a specific position. The line
         * will persist until removed using `DestroyLine(name)` or `DestroyAllLines()`.
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
        public void CreateLine(int startEntityId, Vector3 endPosition, Color color, string name)
        {
            _linesFromEntityToPosition[name] = Tuple.Create(startEntityId, endPosition, color);
        }
        
        /**
         * Creates a line from a position to an entity with the given id. The line
         * will persist until removed using `DestroyLine(name)` or `DestroyAllLines()`.
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
        public void CreateLine(Vector3 startPosition, int endEntityId, Color color, string name)
        {
            CreateLine(endEntityId, startPosition, color, name);
        }
        
        /**
         * Creates a line between two entities with the given ids. The line
         * will persist until removed using `DestroyLine(name)` or `DestroyAllLines()`.
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
        public void CreateLine(int startEntityId, int endEntityId, Color color, string name)
        {
            _linesFromEntityToEntity[name] = Tuple.Create(startEntityId, endEntityId, color);
        }
        
        /**
         * Creates a line between two positions. The line will persist until removed using
         * `DestroyLine(name)` or `DestroyAllLines()`.
         * <param name="startPosition">The start position of the line.</param>
         * <param name="endPosition">The end position of the line</param>
         * <param name="color">The color of the line.</param>
         * <param name="name">The name of the line. Used to remove the line later.</param>
         * <example>
         * <code>
         * // Create a line from the position (0, 0, 0) to position (1, 1, 1) with the color red.
         * RGGizmos.CreateLine(new Vector3(0, 0, 0), new Vector3(1, 1, 1), Color.red, "myLine");
         * </code>
         * </example>
         * <seealso cref="DestroyLine"/>
         * <seealso cref="DestroyAllLines"/>
         */
        public void CreateLine(Vector3 startPosition, Vector3 endPosition, Color color, string name)
        {
            _linesFromPositionToPosition[name] = Tuple.Create(startPosition, endPosition, color);
        }

        /**
         * Destroys a line with the given name. If no line with the given name exists, nothing happens.
         * <param name="name">The name of the line to destroy.</param>
         * <example>
         * <code>
         * RGGizmos.DestroyLine("myLine");
         * </code>
         * </example>
         */
        public void DestroyLine(string name)
        {
            _linesFromEntityToPosition.Remove(name);
            _linesFromEntityToEntity.Remove(name);
            _linesFromPositionToPosition.Remove(name);
        }

        /**
         * Destroys all lines created.
         * <example>
         * <code>
         * RGGizmos.DestroyAllLines();
         * </code>
         * </example>
         * <seealso cref="DestroyLine"/>
         */
        public void DestroyAllLines()
        {
            _linesFromEntityToPosition.Clear();
            _linesFromEntityToEntity.Clear();
            _linesFromPositionToPosition.Clear();
        }
        
        /**
         * Creates a sphere at the given position. The sphere will persist until removed using
         * `DestroySphere(name)` or `DestroyAllSpheres()`.
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
            _spheresAtPosition[name] = Tuple.Create(position, color, size, isWireframe);
        }

        /**
         * Creates a sphere at the origin of the entity with the given id. The sphere will persist until removed using
         * `DestroySphere(name)` or `DestroyAllSpheres()`.
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
            _spheresAtEntity[name] = Tuple.Create(entityId, color, size, isWireframe);
        }

        /**
         * Destroys a sphere with the given name. If no sphere with the given name exists, nothing happens.
         * <param name="name">The name of the sphere to destroy.</param>
         * <example>
         * <code>
         * RGGizmos.DestroySphere("mySphere");
         * </code>
         * </example>
         */
        public void DestroySphere(string name)
        {
            _spheresAtPosition.Remove(name);
            _spheresAtEntity.Remove(name);
        }

        /**
         * Destroys all spheres created.
         * <example>
         * <code>
         * RGGizmos.DestroyAllSpheres();
         * </code>
         * </example>
         * <seealso cref="DestroySphere"/>
         */
        public void DestroyAllSpheres()
        {
            _spheresAtPosition.Clear();
            _spheresAtEntity.Clear();
        }
        
        /**
         * Creates a text billboard on an entity with the given id. The text will persist until removed using
         * `DestroyText(entityId)` or `DestroyAllTexts()`. Use the yOffset parameter to place the billboard text
         * a certain distance above the entity. An entity can only have one billboard text at a time.
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
            _billboardsToDraw[entityId] = Tuple.Create(content, yOffset);
        }

        /**
         * Destroys the text billboard on an entity with the given id. If no text billboard exists on the entity, nothing
         * happens.
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
            _billboardsToDraw.Remove(entityId);
        }

        /**
         * Destroys all text billboards created.
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
            _billboardsToDraw.Clear();
        }

        /**
         * Draws all Gizmos that have been set.
         */
        protected internal void OnDrawGizmos()
        {
            
            // Draw lines
            foreach (var lineParams in _linesFromEntityToPosition.Values)
            {
                var originInstance = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(lineParams.Item1);
                if (originInstance != null)
                {
                    Debug.DrawLine(originInstance.transform.position, lineParams.Item2, lineParams.Item3);
                }
            }
            foreach (var lineParams in _linesFromEntityToEntity.Values)
            {
                var originInstance = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(lineParams.Item1);
                var endInstance = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(lineParams.Item2);
                if (originInstance != null && endInstance != null)
                {
                    Debug.DrawLine(originInstance.transform.position, endInstance.transform.position, lineParams.Item3);
                }
            }
            foreach (var lineParams in _linesFromPositionToPosition.Values)
            {
                Debug.DrawLine(lineParams.Item1, lineParams.Item2, lineParams.Item3);
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
                _drawnBillboards.Remove(billboard.Key);
            }
            
            // Now update or create any billboards that are being drawn
            foreach (var billboardParams in _billboardsToDraw)
            {
                // If the billboard does not exist, create it
                if (!_drawnBillboards.ContainsKey(billboardParams.Key))
                {
                    var entityGameObject = RGFindUtils.Instance.FindOneByInstanceId<RGEntity>(billboardParams.Key);
                    var billboardObject = Object.Instantiate(_billboardAsset, entityGameObject.transform.position,
                        Quaternion.identity);
                    billboardObject.transform.SetParent(entityGameObject.transform);
                    _drawnBillboards[billboardParams.Key] = billboardObject;
                }
                
                // Then set the parameters
                var billboardText = _drawnBillboards[billboardParams.Key].GetComponent<BillboardText>();
                billboardText.SetText(billboardParams.Value.Item1);
                
                // If an offset is given, use that for placing it above the agent
                billboardText.SetYOffset(billboardParams.Value.Item2);
            }

        }
        
    }
}