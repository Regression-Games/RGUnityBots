using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames
{
    public static class RGDebug
    {
        public enum RGLogLevel {Debug, Info, Warning, Error}

        /**
         * Replaces Unity's Debug.Log method. Adds the ability to add a custom log level, which is
         * controlled through Regression Project Settings
         */
        public static void Log(string message, RGLogLevel logLevel = RGLogLevel.Info)
        {
            LogToConsole(message, logLevel);
        }

        /**
         * Replaces Unity's Debug.LogWarning method
         */
        public static void LogWarning(string message)
        {
            LogToConsole(message, RGLogLevel.Warning);
        }

        /**
         * Replaces Unity's Debug.LogError method
         */
        public static void LogError(string message)
        {
            LogToConsole(message, RGLogLevel.Error);
        }

        /**
         * Replaces Unity's Debug.LogException method
         */
        public static void LogException(System.Exception exception)
        {
            if (!CheckLogLevel(RGLogLevel.Error))
            {
                return;
            }
            RGDebug.LogException(exception);
        }

        // Log the given message to the console
        private static void LogToConsole(string message, RGLogLevel logLevel)
        {
            if (!CheckLogLevel(logLevel))
            {
                return;
            }
            
            switch (logLevel)
            {
                case RGLogLevel.Debug:
                case RGLogLevel.Info:
                    Debug.Log(message);
                    break;
                case RGLogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case RGLogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
        
        
        
        // Determine if the log message is within the log levels in the settings
        private static bool CheckLogLevel(RGLogLevel logLevel)
        {
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            DebugLogLevel rgLogLevel = rgSettings.GetLogLevel();

            if (rgLogLevel == DebugLogLevel.Off)
            {
                return false;
            }

            switch (rgLogLevel)
            {
                case DebugLogLevel.Debug:
                    return true;
                case DebugLogLevel.Info:
                    if (logLevel >= RGLogLevel.Info && logLevel <= RGLogLevel.Error)
                    {
                        return true;
                    }
                    break;
                case DebugLogLevel.Warning:
                    if (logLevel >= RGLogLevel.Warning && logLevel <= RGLogLevel.Error)
                    {
                        return true;
                    }
                    break;
                case DebugLogLevel.Error:
                    if (logLevel == RGLogLevel.Error)
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

    }
}