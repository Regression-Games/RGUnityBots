﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Collider2DRecordState : ColliderRecordState
    {
        private static readonly string TypeName = typeof(Collider2D).FullName;

        public new int apiVersion = SdkApiVersion.VERSION_4;

        public new int ApiVersion()
        {
            return apiVersion;
        }

        public new Collider2D collider;

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {

            stringBuilder.Append("{\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, TypeName);
            stringBuilder.Append(",\"is2D\":true");
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"bounds\":");
            BoundsJsonConverter.WriteToStringBuilder(stringBuilder, collider.bounds);
            stringBuilder.Append(",\"isTrigger\":");
            stringBuilder.Append((collider.isTrigger ? "true" : "false"));
            stringBuilder.Append("}");
        }

    }
}
