using System;
using System.Collections.Generic;
using RegressionGames.RGBotConfigs;
using UnityEngine;

namespace RegressionGames.DebugUtils
{
    
    public class RGGizmos
    {
        
        private Dictionary<string, Tuple<int, Vector3, Color>> _linesFromEntityToPosition = new ();
        private Dictionary<string, Tuple<int, int, Color>> _linesFromEntityToEntity = new ();
        private Dictionary<string, Tuple<Vector3, Vector3, Color>> _linesFromPositionToPosition = new ();
        

        public void CreateLine(int startEntityId, Vector3 end, Color color, string name)
        {
            _linesFromEntityToPosition[name] = Tuple.Create(startEntityId, end, color);
        }
        
        public void CreateLine(Vector3 start, int endEntityId, Color color, string name)
        {
            CreateLine(endEntityId, start, color, name);
        }
        
        public void CreateLine(int startEntityId, int endEntityId, Color color, string name)
        {
            _linesFromEntityToEntity[name] = Tuple.Create(startEntityId, endEntityId, color);
        }
        
        public void CreateLine(Vector3 start, Vector3 end, Color color, string name)
        {
            _linesFromPositionToPosition[name] = Tuple.Create(start, end, color);
        }

        public void DestroyLine(string name)
        {
            _linesFromEntityToPosition.Remove(name);
            _linesFromEntityToEntity.Remove(name);
            _linesFromPositionToPosition.Remove(name);
        }

        public void DestroyAllLines()
        {
            _linesFromEntityToPosition.Clear();
            _linesFromEntityToEntity.Clear();
            _linesFromPositionToPosition.Clear();
        }

        /**
         * Draws all Gizmos that have been set
         */
        public void OnDrawGizmos()
        {
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
        }
        
    }
}