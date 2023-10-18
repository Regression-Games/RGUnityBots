using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.DefaultActions
{
    
    /**
     * This action uses Gizmos to draw a line from a given agent to a position in a scene.
     * Takes the following parameters:
     *  If drawing a line (either new or old)
     *    - name (string): And identifier for the line
     *    - position (Vector3): The position to draw the line to (from the agent)
     *    - color (Color): The color of the line to draw
     *  If removing the line:
     *    - name (string): The line to remove
     *    - remove (boolean): Set to true
     *  If removing all lines:
     *    - removeAll (bool): Set to true
     */
    public class DrawLineToAction: RGAction
    {

        private Dictionary<string, Tuple<Vector3, Color>> _lines = new ();

        public override string GetActionName()
        {
            return "DrawLineTo";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            
            if ((bool) input.GetValueOrDefault("removeAll", false))
            {
                _lines.Clear();
                return;
            }

            var lineName = (string) input["name"];
            if ((bool) input.GetValueOrDefault("remove", false))
            {
                _lines.Remove(lineName);
            }
            else
            {
                var targetPosition = (Vector3) input["position"];
                var color = (Color) input["color"];
                var lineParams = Tuple.Create(targetPosition, color);
                _lines[lineName] = lineParams;
            }

        }

        private void OnDrawGizmos()
        {
            foreach (var lineParams in _lines.Values)
            {
                Debug.DrawLine(transform.position, lineParams.Item1, lineParams.Item2);
            }
        }
    }
}