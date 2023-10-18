using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.DefaultActions
{
    
    /**
     * This action uses Gizmos to draw an indicator at a specific position in a scene.
     * Takes the following parameters:
     *  If modifying an indicator (either new or old)
     *    - name (string): And identifier for the indicator
     *    - position (Vector3): The position to place the indicator
     *    - color (Color): The color of the indicator
     *    - size (float): The size of the indicator
     *  If removing the indicator:
     *    - name (string): The indicator to remove
     *    - remove (boolean): Set to true
     *  If removing all indicators:
     *    - removeAll (bool): Set to true
     */
    public class DrawIndicator: RGAction
    {

        private Dictionary<string, Tuple<Vector3, Color, float>> _indicators = new ();

        public override string GetActionName()
        {
            return "DrawIndicator";
        }

        public override void StartAction(Dictionary<string, object> input)
        {

            if ((bool) input.GetValueOrDefault("removeAll", false))
            {
                _indicators.Clear();
                return;
            }

            var indicatorName = (string) input["name"];
            if ((bool) input.GetValueOrDefault("remove", false))
            {
                _indicators.Remove(indicatorName);
            }
            else
            {
                var targetPosition = (Vector3) input["position"];
                var color = (Color) input["color"];
                var size = (float) input["size"];
                var indicatorParams = Tuple.Create(targetPosition, color, size);
                _indicators[indicatorName] = indicatorParams;
            }

        }

        private void OnDrawGizmos()
        {
            foreach (var indicatorParams in _indicators.Values)
            {
                Gizmos.color = indicatorParams.Item2;
                Gizmos.DrawSphere(indicatorParams.Item1, indicatorParams.Item3);
            }
        }
    }
}