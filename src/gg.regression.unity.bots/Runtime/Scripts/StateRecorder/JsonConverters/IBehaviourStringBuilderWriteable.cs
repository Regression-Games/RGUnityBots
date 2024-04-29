using System.Text;
using UnityEngine;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public interface IBehaviourStringBuilderWritable
    {
        public void WriteBehaviourToStringBuilder(StringBuilder stringBuilder, Behaviour behaviour);
    }
}
