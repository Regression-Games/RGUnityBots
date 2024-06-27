using System.Text;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    public class TransformComponentDataProvider : IComponentDataProvider
    {
        public Transform Transform;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            throw new System.NotImplementedException();
        }
    }
}
