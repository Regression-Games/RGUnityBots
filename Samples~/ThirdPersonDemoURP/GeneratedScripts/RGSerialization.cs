/*
* This file has been automatically generated. Do not modify.
*/

using System;
using Newtonsoft.Json;

namespace RGThirdPersonDemo
{
    using UnityEngine;

    public static class RGSerialization
    {
        public static Vector2 Deserialize_Vector2(string paramJson)
        {
            return JsonConvert.DeserializeObject<Vector2>(paramJson);
        }
    }
}