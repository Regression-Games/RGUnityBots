﻿namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public enum KeyFrameCriteriaType
    {
        Or,
        And,
        UIPixelHash,
        NormalizedPath,
        PartialNormalizedPath,
        CVText,
        CVImage,
        ActionComplete,
        CVObjectDetection,
        ValidationsComplete

        //FUTURE
        //Path,
        //XPath
    }
}
