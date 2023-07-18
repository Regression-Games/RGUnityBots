using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames
{
    public static class RGDebug
    {
        public enum RGLogLevel {Verbose, Debug, Info, Warning, Error}

        /**
         * Logging of 'Verbose' logs. Only visible if Log Level is set to 'Verbose'
         * in the Regression Project Settings
         */
        public static void LogVerbose(string message)
        {
            LogToConsole(message, RGLogLevel.Verbose);
        }

        /**
         * Logging of 'Debug' logs. Only visible if Log Level is set to 'Debug' or
         * lower in the Regression Project Settings
         */
        public static void LogDebug(string message)
        {
            LogToConsole(message, RGLogLevel.Debug);
        }
        
        /**
         * Logging of 'Info' logs. Only visible if Log Level is set to 'Info' or
         * lower in the Regression Project Settings
         */
        public static void Log(string message)
        {
            LogToConsole(message, RGLogLevel.Info);
        }

        /**
         * Logging of 'Warning' logs. Only visible if Log Level is set to 'Warning' or
         * lower in the Regression Project Settings
         */
        public static void LogWarning(string message)
        {
            LogToConsole(message, RGLogLevel.Warning);
        }

        /**
         * Logging of 'Error' logs. Only visible if Log Level is set to 'Error' or
         * lower in the Regression Project Settings
         */
        public static void LogError(string message)
        {
            LogToConsole(message, RGLogLevel.Error);
        }

        /**
         * Logging of 'Exception' logs. Only visible if Log Level is set to 'Error' or
         * lower in the Regression Project Settings
         */
        public static void LogException(System.Exception exception)
        {
            if (!CheckLogLevel(RGLogLevel.Error))
            {
                return;
            }
            Debug.LogException(exception);
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
                case RGLogLevel.Verbose:
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
                case DebugLogLevel.Verbose:
                    return true;
                case DebugLogLevel.Debug:
                    if (logLevel >= RGLogLevel.Debug && logLevel <= RGLogLevel.Error)
                    {
                        return true;
                    }
                    break;
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