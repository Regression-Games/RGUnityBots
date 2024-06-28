using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.ECS.Models;
using RegressionGames.StateRecorder.Models;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace RegressionGames.StateRecorder.ECS
{
    public class EntityFinder: ObjectFinder
    {
        private int _objectFrameNumber = -1;
        private int _stateFrameNumber = -1;

        // pre-allocate a big list we can re-use
        private Dictionary<long,RecordedGameObjectState> _priorStates = new (1000);
        private Dictionary<long,RecordedGameObjectState> _newStates = new(1000);
        private readonly List<RecordedGameObjectState> _fillInStates = new (1000);

        private Dictionary<long, ObjectStatus> _priorObjects = new(1000);
        private Dictionary<long, ObjectStatus> _newObjects = new(1000);

        private List<EntityStatus> _latestFoundEntities = new(1000);

        private AllocatorManager.AllocatorHandle handle = new AllocatorManager.AllocatorHandle();

        public void Start()
        {
            // register our json converters
            //JsonConverterContractResolver.Instance.RegisterJsonConverterForType();
        }

        private RecordedGameObjectState GetStateForEntityObject(EntityStatus eStatus)
        {
            var uiObjectTransformId = eStatus.Id;
            // only process visible objects into the state
            // TODO: Until entity statuses are properly populated with their bounds... nothing will show in the state
            if (eStatus.screenSpaceBounds.HasValue)
            {
                var usingOldObject = _priorStates.TryGetValue(uiObjectTransformId, out var resultObject);

                if (!usingOldObject)
                {
                    resultObject = new RecordedGameObjectState()
                    {
                        id = uiObjectTransformId,
                        parentId = eStatus.ParentId,
                        path = eStatus.Path,
                        normalizedPath = eStatus.NormalizedPath,
                        tag = eStatus.Tag,
                        layer = eStatus.LayerName,
                        scene = eStatus.Scene,
                        componentDataProviders = new List<IComponentDataProvider>()
                        {
                            new TransformComponentDataProvider()
                            {
                                Transform = transform
                            }
                        }
                    };
                }

                //TODO: Get entity transform information
                //resultObject.position = eStatus.Transform.position;
                //resultObject.rotation = eStatus.Transform.rotation;

                resultObject.screenSpaceZOffset = eStatus.screenSpaceZOffset;
                resultObject.screenSpaceBounds = eStatus.screenSpaceBounds.Value;
                resultObject.worldSpaceBounds = eStatus.worldSpaceBounds;

                var dataProviders = resultObject.componentDataProviders;
                dataProviders.Clear();

                ProcessEntityComponents(eStatus, dataProviders);
                return resultObject;

            }

            return null;
        }

        private void ProcessEntityComponents(EntityStatus entityStatus, List<IComponentDataProvider> dataProviders)
        {

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

                            var parentData = GetParentEntity(parentEntity.Value);
                            parentEntity = parentData.Item1;
                            parentEntityManager = parentData.Item2;
                            parentId = resultObject.parentId;
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

        private (Entity?, EntityManager?) GetParentEntity(Entity entity)
        {
            // todo: lookup parent component if exists and return parent
            return (null, null);
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

                var entityQuery = entityManager.CreateEntityQuery(new EntityQueryDesc()
                {
                    //TODO: Only get ANY renderable object here has (LocalToWorld, RenderMesh, and RenderBounds)
                });

                handle.Dispose();
                handle = AllocatorManager.Temp;

                var entities = entityQuery.ToEntityArray(handle);

                foreach (var entity in entities)
                {
                    var eStatus = EntityStatus.GetOrCreateEntityStatus(entity, entityManager);
                    _newObjects[eStatus.Id] = eStatus;
                    var cTypes = entityManager.GetComponentTypes(entity);
                    MethodInfo methodInfo = typeof(EntityManager).GetMethod("GetComponentData", new Type[] {typeof(Entity)});
                    using (cTypes)
                    {
                        // todo: update all the bounds stuff here on the status
                        var components = new List<IComponentData>();
                        foreach (var componentType in cTypes)
                        {
                            if (!componentType.IsZeroSized)
                            {
                                Type t = componentType.GetManagedType();
                                try
                                {
                                    //TODO: FIX ME:  This call is throwing ArgumentException: Invalid generic arguments when the type is nested like Unity.CharacterController.CharacterInterpolationRememberTransformSystem+Singleton
                                    MethodInfo genericMethodInfo = methodInfo.MakeGenericMethod(t);
                                    var parameters = new object[] { entity };
                                    var componentData = (IComponentData)genericMethodInfo.Invoke(entityManager, parameters);
                                    components.Add(componentData);
                                }
                                catch (ArgumentException)
                                {
                                    //TODO: FIX ME:  This next call is throwing ArgumentException: Invalid generic arguments when the type is nested like Unity.CharacterController.CharacterInterpolationRememberTransformSystem+Singleton
                                }
                            }
                        }
                    }
                }

            }

            return (_priorObjects, _newObjects);
        }

        public override void Cleanup()
        {
            handle.Dispose();
        }


    }

}
