using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.RemoteOrchestration.Types
{
    [Serializable]
    public class SDKClientRegistrationResponse
    {
        public long id;
    }
}
