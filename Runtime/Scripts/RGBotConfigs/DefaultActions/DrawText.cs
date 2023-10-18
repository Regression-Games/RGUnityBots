using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.RGBotConfigs.DefaultActions
{
    public class DrawText: RGAction
    {

        private Dictionary<string, GameObject> _billboards = new ();
        private Object _billboardAsset;

        void Start()
        {
            var path = "Packages/gg.regression.unity.bots/Runtime/Prefabs/AgentBillboardText.prefab";
            _billboardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log($"Grabbed asset from {path}: {_billboardAsset}");
        }
        
        public override string GetActionName()
        {
            return "DrawText";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            
            var textName = (string) input["name"];
            if ((bool) input.GetValueOrDefault("remove", false))
            {
                _billboards.Remove(textName);
            }
            else
            {
                var content = (string) input["content"];
                if (!_billboards.ContainsKey(name))
                {
                    var billboardText =
                        (GameObject) Instantiate(_billboardAsset, transform.position, Quaternion.identity);
                    billboardText.transform.SetParent(transform);
                    billboardText.GetComponent<BillboardText>().SetText(content);
                    _billboards[name] = billboardText;
                }
                else
                {
                    var billboard = _billboards[name];
                    billboard.GetComponent<BillboardText>().SetText(content);
                }
            }

        }
        
    }
}