using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.DefaultActions
{
    
    /**
     * This action uses Gizmos to draw text above the agent.
     * Takes the following parameters:
     *  If adding or updating the text:
     *    - content (string): The text to display
     *    - yOffset (float): The distance above the agent's origin to display the text
     *  If removing the text:
     *    - remove (boolean): Set to true
     */
    public class DrawText: RGAction
    {

        private GameObject _billboard;
        private Object _billboardAsset;

        void Start()
        {
            var path = "Packages/gg.regression.unity.bots/Runtime/Prefabs/AgentBillboardText.prefab";
            _billboardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        
        public override string GetActionName()
        {
            return "DrawText";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            
            if ((bool) input.GetValueOrDefault("remove", false))
            {
                Destroy(_billboard);
                _billboard = null;
            }
            else
            {
                var content = (string) input["content"];
                if (_billboard == null)
                {
                    var billboardObject =
                        (GameObject) Instantiate(_billboardAsset, transform.position, Quaternion.identity);
                    billboardObject.transform.SetParent(transform);
                    _billboard = billboardObject;
                }
                
                var billboardText = _billboard.GetComponent<BillboardText>();
                billboardText.SetText(content);
                
                // If an offset is given, use that for placing it above the agent
                var yOffset = (float) input.GetValueOrDefault("yOffset", 2f);
                billboardText.SetYOffset(yOffset);
            }
        }
    }
}