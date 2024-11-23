using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.GenericBots.Experimental.Rewards
{
    /// <summary>
    /// Generic exploration reward module.
    /// This implements a novelty reward based on the camera position,
    /// Other reward modules in the future this could include screenshot novelty, code coverage, state coverage, etc.
    /// </summary>
    public class CameraPositionRewardModule : IRewardModule
    {
        // Size of each discretized camera position cell
        private const int CameraCellSize = 1;

        // Stores a mapping from discretized camera position to the number of times that position has been seen before
        private readonly Dictionary<(int, int), int> _cameraPositionVisits = new ();

        // Reward the agent for the novelty of the camera position
        // This is 1/N, where N is the number of times the current (discretized) camera position has been seen previously
        // This reward has a range of 0 to 1.
        public float GetRewardForLastAction()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return 0.0f; // no main camera, don't use this reward
            }

            var camTransformPosition = camera.gameObject.transform.position;

            var pos = ((int)camTransformPosition.x/CameraCellSize,
                (int)camTransformPosition.y/CameraCellSize);
            int numVisits = _cameraPositionVisits.GetValueOrDefault(pos, 0);
            numVisits += 1;
            _cameraPositionVisits[pos] = numVisits;
            return 1.0f / numVisits;
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
