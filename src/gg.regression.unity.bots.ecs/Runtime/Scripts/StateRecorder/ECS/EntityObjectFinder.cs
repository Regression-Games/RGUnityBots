using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RegressionGames.StateRecorder.ECS.Models;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace RegressionGames.StateRecorder.ECS
{
    public class EntityObjectFinder: ObjectFinder
    {
        private int _objectFrameNumber = -1;
        private int _stateFrameNumber = -1;

        // pre-allocate a big list we can re-use
        private Dictionary<long,RecordedGameObjectState> _priorStates = new (1000);
        private Dictionary<long,RecordedGameObjectState> _newStates = new(1000);
        private readonly List<RecordedGameObjectState> _fillInStates = new (1000);

        private Dictionary<long, ObjectStatus> _priorObjects = new(1000);
        private Dictionary<long, ObjectStatus> _newObjects = new(1000);

        private AllocatorManager.AllocatorHandle _handle = AllocatorManager.Temp;

        private List<IEntitySelector> _entitySelectors = new();

        public void Start()
        {
            // find and instantiate any IEntitySelectors in the runtime
            var type = typeof(IEntitySelector);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && !p.IsInterface);

            _entitySelectors = types.Select(a => Activator.CreateInstance(a) as IEntitySelector).ToList();

            // register our json converters
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(bool2?), new MathematicsBool2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(bool2), new MathematicsBool2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(bool3), new MathematicsBool3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(bool3?), new MathematicsBool3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(bool4), new MathematicsBool4JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(bool4?), new MathematicsBool4JsonConverter());

            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(double2?), new MathematicsDouble2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(double2), new MathematicsDouble2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(double3), new MathematicsDouble3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(double3?), new MathematicsDouble3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(double4), new MathematicsDouble4JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(double4?), new MathematicsDouble4JsonConverter());

            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(float2?), new MathematicsFloat2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(float2), new MathematicsFloat2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(float3), new MathematicsFloat3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(float3?), new MathematicsFloat3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(float4), new MathematicsFloat4JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(float4?), new MathematicsFloat4JsonConverter());

            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(half2?), new MathematicsHalf2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(half2), new MathematicsHalf2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(half3), new MathematicsHalf3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(half3?), new MathematicsHalf3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(half4), new MathematicsHalf4JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(half4?), new MathematicsHalf4JsonConverter());

            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(int2?), new MathematicsInt2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(int2), new MathematicsInt2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(int3), new MathematicsInt3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(int3?), new MathematicsInt3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(int4), new MathematicsInt4JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(int4?), new MathematicsInt4JsonConverter());

            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(quaternion), new MathematicsQuaternionJsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(quaternion?), new MathematicsQuaternionJsonConverter());

            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(uint2?), new MathematicsUint2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(uint2), new MathematicsUint2JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(uint3), new MathematicsUint3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(uint3?), new MathematicsUint3JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(uint4), new MathematicsUint4JsonConverter());
            JsonConverterContractResolver.Instance.RegisterJsonConverterForType(typeof(uint4?), new MathematicsUint4JsonConverter());
        }

        private (Vector3?, Quaternion?)? SelectPositionAndRotationForEntity(Entity entity, EntityManager entityManager)
        {
            if (_entitySelectors.Count > 0)
            {
                // alternative entity lookup for some games using custom selectors
                foreach (var entitySelector in _entitySelectors)
                {
                    var result = entitySelector.SelectPositionAndRotationForEntity(entity, entityManager);
                    if (result.HasValue)
                    {
                        return result.Value;
                    }
                }

                return null;
            }
            else
            {
                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    // TODO (REG-1832): Future handle querying ECS component data for position and rotation
                    //var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                }
            }

            return null;
        }

        private RecordedGameObjectState GetStateForEntityObject(EntityStatus eStatus)
        {
            var uiObjectTransformId = eStatus.Id;

            // We DO NOT filter entities to those that only have bounds as we support pulling in a custom list of entities by game
            var usingOldObject = _priorStates.TryGetValue(uiObjectTransformId, out var resultObject);
            if (!usingOldObject)
            {
                resultObject = new RecordedGameObjectState()
                {
                    id = uiObjectTransformId,
                    parentId = eStatus.ParentId,
                    type = ObjectType.Entity,
                    path = eStatus.Path,
                    normalizedPath = eStatus.NormalizedPath,
                    tag = eStatus.Tag,
                    layer = eStatus.LayerName,
                    scene = eStatus.Scene,
                    componentDataProviders = new List<IComponentDataProvider>()
                };
            }

            //Get entity transform information
            var positionRotation = SelectPositionAndRotationForEntity(eStatus.Entity, eStatus.EntityManager);
            if (positionRotation != null)
            {
                resultObject.position = positionRotation.Value.Item1;
                resultObject.rotation = positionRotation.Value.Item2;
            }
            else
            {
                resultObject.position = null;
                resultObject.rotation = null;
            }

            resultObject.screenSpaceZOffset = eStatus.screenSpaceZOffset;
            resultObject.screenSpaceBounds = eStatus.screenSpaceBounds;
            resultObject.worldSpaceBounds = eStatus.worldSpaceBounds;

            var dataProviders = resultObject.componentDataProviders;
            dataProviders.Clear();

            ProcessEntityComponents(eStatus, dataProviders);
            return resultObject;
        }

        private void ProcessEntityComponents(EntityStatus entityStatus, List<IComponentDataProvider> dataProviders)
        {
            foreach (var componentData in entityStatus.ComponentData)
            {
                dataProviders.Add( new EntityComponentDataProvider(componentData));
            }
        }

        public override (Dictionary<long, RecordedGameObjectState>, Dictionary<long, RecordedGameObjectState>) GetStateForCurrentFrame()
        {
            var frameCount = Time.frameCount;
            if (frameCount == _stateFrameNumber)
            {
                // we already processed this frame (happens when recording during replay and they both call this)
                return (_priorStates, _newStates);
            }

            _stateFrameNumber = frameCount;

            // switch the list references
            (_priorStates, _newStates) = (_newStates, _priorStates);
            _newStates.Clear();

            var statusList = GetObjectStatusForCurrentFrame().Item2;

            foreach (var oStatus in statusList.Values)
            {
                if (oStatus is EntityStatus eStatus)
                {
                    var entry = GetStateForEntityObject(eStatus);
                    // only visible entries get included by default
                    if (entry != null)
                    {
                        _newStates[eStatus.Id] = entry;
                    }
                }
            }

            _fillInStates.Clear();
            // now fill in any 'missing' parent objects in the state so that the UI can render the full tree
            foreach (var newStateEntry in _newStates.Values)
            {
                var parentId = newStateEntry.parentId;
                if (parentId.HasValue)
                {
                    if (statusList.TryGetValue(parentId.Value, out var parentStatus))
                    {
                        Entity? parentEntity = null;
                        EntityManager? parentEntityManager = null;
                        if (parentStatus is EntityStatus peStatus)
                        {
                            parentEntity = peStatus.Entity;
                            parentEntityManager = peStatus.EntityManager;
                        }

                        // go up the tree until we find something in our parent hierarchy existing..
                        // stop if we hit the top
                        while (parentId.HasValue && parentEntity.HasValue && parentEntityManager.HasValue && !_newStates.ContainsKey(parentId.Value))
                        {

                            var eStatus = EntityStatus.GetOrCreateEntityStatus(parentEntity.Value, parentEntityManager.Value);
                            var resultObject = GetStateForEntityObject(eStatus);

                            // don't update the _newStates dictionary while iterating
                            _fillInStates.Add(resultObject);

                            var parentData = GetParentEntity(parentEntity.Value, parentEntityManager.Value);
                            if (parentData.HasValue)
                            {
                                parentEntity = parentData.Value;
                                parentEntityManager = parentEntityManager.Value;
                                parentId = resultObject.parentId;
                            }
                            else
                            {
                                parentId = null;
                                parentEntity = null;
                                parentEntityManager = null;
                            }
                        }
                    }
                }
            }

            foreach (var recordedGameObjectState in _fillInStates)
            {
                _newStates[recordedGameObjectState.id] = recordedGameObjectState;
            }

            return (_priorStates, _newStates);
        }

        private Entity? GetParentEntity(Entity entity, EntityManager entityManager)
        {
            if (entityManager.HasComponent<Parent>(entity))
            {
                var parentData = entityManager.GetComponentData<Parent>(entity);
                return parentData.Value;
            }
            return null;
        }

        private IEnumerable<Entity> SelectEntities(EntityManager entityManager)
        {
            if (_entitySelectors.Count > 0)
            {
                // alternative entity lookup for some games using custom selectors
                var entities = new List<Entity>();
                foreach (var entitySelector in _entitySelectors)
                {
                    entities.AddRange(entitySelector.SelectEntities(entityManager));
                }
                return entities;
            }
            else
            {
                // ECS lookup query to find entities
                var queryDescription = new EntityQueryDesc
                {
                    //TODO (REG-1832): Current clients don't utilize hybrid renderer support for entities, so we didn't both to finish this filter for renderable entities code
                    //Any = new ComponentType[] { typeof(LocalToWorld), typeof(RenderMesh), typeof(RenderBounds) }
                };

                var entityQuery = entityManager.CreateEntityQuery(queryDescription);

                _handle.Dispose();
                _handle = AllocatorManager.Temp;

                var entities = entityQuery.ToEntityArray(_handle);
                return entities;
            }
        }

        public override (Dictionary<long, ObjectStatus>, Dictionary<long, ObjectStatus>) GetObjectStatusForCurrentFrame()
        {
            var frameCount = Time.frameCount;
            if (frameCount == _objectFrameNumber)
            {
                // we already processed this frame (happens when recording during replay and they both call this)
                return (_priorObjects, _newObjects);
            }

            _objectFrameNumber = frameCount;

            // switch the list references
            (_priorObjects, _newObjects) = (_newObjects, _priorObjects);
            _newObjects.Clear();

            var worlds = World.All;
            foreach (var world in worlds)
            {
                var entityManager = world.EntityManager;
                var entities = SelectEntities(entityManager);

                foreach (var entity in entities)
                {
                    var eStatus = EntityStatus.GetOrCreateEntityStatus(entity, entityManager);
                    _newObjects[eStatus.Id] = eStatus;
                    // update all the bounds stuff here on the status
                    if (_entitySelectors.Count > 0)
                    {
                        foreach (var entitySelector in _entitySelectors)
                        {
                            var bounds = entitySelector.SelectBounds(entity, entityManager);
                            if (bounds.HasValue)
                            {
                                eStatus.screenSpaceBounds = bounds.Value.Item1;
                                eStatus.screenSpaceZOffset = bounds.Value.Item2;
                                eStatus.worldSpaceBounds = bounds.Value.Item3;
                            }
                        }
                    }
                    else
                    {
                        //todo (reg-1832): implement getting the bounds for entities using the ecs hybrid renderer
                    }
                    // get component data
                    var cTypes = entityManager.GetComponentTypes(entity);
                    MethodInfo methodInfo = typeof(EntityManager).GetMethod("GetComponentData", new [] {typeof(Entity)});
                    using (cTypes)
                    {
                        var components = new List<IComponentData>();
                        foreach (var componentType in cTypes)
                        {
                            if (!componentType.IsZeroSized)
                            {
                                Type t = componentType.GetManagedType();
                                try
                                {
                                    //FIX ME Someday: This call is throwing ArgumentException: Invalid generic arguments when the type is nested like Unity.CharacterController.CharacterInterpolationRememberTransformSystem+Singleton
                                    MethodInfo genericMethodInfo = methodInfo.MakeGenericMethod(t);
                                    var parameters = new object[] { entity };
                                    var componentData = (IComponentData)genericMethodInfo.Invoke(entityManager, parameters);
                                    components.Add(componentData);
                                }
                                catch (ArgumentException)
                                {
                                    //FIX ME Someday: This next call is throwing ArgumentException: Invalid generic arguments when the type is nested like Unity.CharacterController.CharacterInterpolationRememberTransformSystem+Singleton
                                }
                            }
                        }

                        eStatus.ComponentData = components;
                    }
                }

            }

            return (_priorObjects, _newObjects);
        }

        public override void Cleanup()
        {
            _handle.Dispose();
        }


    }

}
