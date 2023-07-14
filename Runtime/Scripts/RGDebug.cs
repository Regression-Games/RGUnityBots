using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames
{
    public static class RGDebug
    {
        public enum RGLogLevel {Info, Warning, Error}

        public static void Log(string message, RGLogLevel logLevel = RGLogLevel.Info)
        {
            if (!CheckLogLevel(logLevel))
            {
                return;
            }

            switch (logLevel)
            {
                case RGLogLevel.Info:
                    Debug.Log(message);
                    break;
                case RGLogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case RGLogLevel.Error:
                    Debug.LogError(message);
                    break;
            }
        }

        // Determine if the log message is within the log levels in the settings
        private static bool CheckLogLevel(RGLogLevel logLevel)
        {
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            DebugLogLevel rgLogLevel = rgSettings.GetLogLevel();

            if (rgLogLevel == DebugLogLevel.All)
            {
                return true;
            }

            return rgLogLevel switch
            {
                DebugLogLevel.Info when logLevel == RGLogLevel.Info => true,
                DebugLogLevel.Warning when logLevel == RGLogLevel.Warning => true,
                DebugLogLevel.Error when logLevel == RGLogLevel.Error => true,
                _ => false
            };
        }
    }
}