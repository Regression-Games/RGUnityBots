using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.DefaultActions
{
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
                    var billboardText =
                        (GameObject) Instantiate(_billboardAsset, transform.position, Quaternion.identity);
                    billboardText.transform.SetParent(transform);
                    billboardText.GetComponent<BillboardText>().SetText(content);
                    _billboard = billboardText;
                }
                else
                {
                    _billboard.GetComponent<BillboardText>().SetText(content);
                }
            }

        }
        
    }
}