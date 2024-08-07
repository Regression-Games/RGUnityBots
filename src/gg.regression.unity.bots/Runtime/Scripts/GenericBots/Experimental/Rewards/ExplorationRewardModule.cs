using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.GenericBots.Experimental.Rewards
{
    /// <summary>
    /// Generic exploration reward module.
    /// Currently this just implements a novelty reward based on the camera position,
    /// but in the future this could include screenshot novelty, code coverage, state coverage, etc.
    /// </summary>
    public class ExplorationRewardModule : IRewardModule
    {
        // Size of each discretized camera position cell
        private const int CameraCellSize = 1;
        
        // Stores a mapping from discretized camera position to the number of times that position has been seen before
        private Dictionary<(int, int), int> _cameraPositionVisits;

        public ExplorationRewardModule()
        {
            _cameraPositionVisits = new Dictionary<(int, int), int>();
        }
        
        // Reward the agent for the novelty of the camera position
        // This is 1/N, where N is the number of times the current (discretized) camera position has been seen previously
        // This reward has a range of 0 to 1.
        private float GetCameraPositionReward()
        {
            var camera = Camera.main;
            if (camera == null)
                return 0.0f; // no main camera, don't use this reward

            var pos = ((int)camera.gameObject.transform.position.x/CameraCellSize, 
                (int)camera.gameObject.transform.position.y/CameraCellSize);
            if (!_cameraPositionVisits.TryGetValue(pos, out int numVisits))
            {
                numVisits = 0;
            }
            numVisits += 1;
            _cameraPositionVisits[pos] = numVisits;
            return 1.0f / numVisits;
        }
        
        public float GetRewardForLastAction()
        {
            return GetCameraPositionReward();
        }

        public void Reset()
        {
            _cameraPositionVisits.Clear();
        }

        public void Dispose()
        {
        }
    }
}