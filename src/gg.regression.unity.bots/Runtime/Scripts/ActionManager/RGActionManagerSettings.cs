using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    public class RGActionManagerSettings : ScriptableObject
    {
        public ISet<string> DisabledActionPaths = new HashSet<string>();
    }
}