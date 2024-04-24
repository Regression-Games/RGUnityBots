using System;
using System.Threading;
using UnityEngine;

namespace RegressionGames
{
    public static class RGDebug
    {
        // ReSharper disable once InconsistentNaming
        private enum RGLogLevel { Verbose, Debug, Info, Warning, Error }

        /**
         * Logging of 'Verbose' logs. Only visible if Log Level is set to 'Verbose'
         * in the Regression Project Settings
         */
        public static void LogVerbose(string message, UnityEngine.Object context = null)
        {
            LogToConsole(message, RGLogLevel.Verbose, context);
        }

        /**
         * Logging of 'Debug' logs. Only visible if Log Level is set to 'Debug' or
         * lower in the Regression Project Settings
         */
        public static void LogDebug(string message, UnityEngine.Object context = null)
        {
            LogToConsole(message, RGLogLevel.Debug, context);
        }

        /**
         * Logging of 'Info' logs. Only visible if Log Level is set to 'Info' or
         * lower in the Regression Project Settings.
         * This is an alias for LogInfo
         */
        public static void Log(string message, UnityEngine.Object context = null)
        {
            LogToConsole(message, RGLogLevel.Info, context);
        }

        /**
         * Logging of 'Info' logs. Only visible if Log Level is set to 'Info' or
         * lower in the Regression Project Settings
         */
        public static void LogInfo(string message, UnityEngine.Object context = null)
        {
            LogToConsole(message, RGLogLevel.Info, context);
        }

        /**
         * Logging of 'Warning' logs. Only visible if Log Level is set to 'Warning' or
         * lower in the Regression Project Settings
         */
        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
            LogToConsole(message, RGLogLevel.Warning, context);
        }

        /**
         * Logging of 'Error' logs. Only visible if Log Level is set to 'Error' or
         * lower in the Regression Project Settings
         */
        public static void LogError(string message, UnityEngine.Object context = null)
        {
            LogToConsole(message, RGLogLevel.Error, context);
        }

        /**
         * Logging of 'Exception' logs. Only visible if Log Level is set to 'Error' or
         * lower in the Regression Project Settings
         */
        public static void LogException(Exception exception, string message = default, UnityEngine.Object context = null)
        {
            if (!CheckLogLevel(RGLogLevel.Error))
            {
                return;
            }

            if (!string.IsNullOrEmpty(message))
            {
                if (context != null)
                {
                    Debug.LogWarning(message, context);
                }
                else
                {
                    Debug.LogWarning(message);
                }
            }

            if (context != null)
            {
                Debug.LogException(exception, context);
            }
            else
            {
                Debug.LogException(exception);
            }
        }

        public static bool IsVerboseEnabled => CheckLogLevel(RGLogLevel.Verbose);
        public static bool IsDebugEnabled => CheckLogLevel(RGLogLevel.Debug);
        public static bool IsInfoEnabled => CheckLogLevel(RGLogLevel.Info);
        public static bool IsWarningEnabled => CheckLogLevel(RGLogLevel.Warning);
        public static bool IsErrorEnabled => CheckLogLevel(RGLogLevel.Error);

        private static string GetDateString()
        {
            // ReSharper disable once StringLiteralTypo
            return DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss:ffff ");
        }

        private static string CreateLogMessageWithPrefix(RGLogLevel logLevel, string message)
        {
            return "{{RG}} " + GetDateString() + logLevel.ToString().ToUpperInvariant() + " [" + Thread.CurrentThread.ManagedThreadId + "] --- " + message;
        }

        // Log the given message to the console
        private static void LogToConsole(string message, RGLogLevel logLevel, UnityEngine.Object context = null)
        {
            if (!CheckLogLevel(logLevel))
            {
                return;
            }

            var logMessage = CreateLogMessageWithPrefix(logLevel, message);

            switch (logLevel)
            {
                case RGLogLevel.Warning:
                    if (context != null)
                    {
                        Debug.LogWarning(logMessage, context);
                    }
                    else
                    {
                        Debug.LogWarning(logMessage);
                    }
                    break;
                case RGLogLevel.Error:
                    if (context != null)
                    {
                        Debug.LogError(logMessage, context);
                    }
                    else
                    {
                        Debug.LogError(logMessage);
                    }
                    break;
                case RGLogLevel.Verbose:
                case RGLogLevel.Debug:
                case RGLogLevel.Info:
                default:
                    if (context != null)
                    {
                        Debug.Log(logMessage, context);
                    }
                    else
                    {
                        Debug.Log(logMessage);
                    }
                    break;
            }
        }

        // Determine if the log message is within the log levels in the settings
        private static bool CheckLogLevel(RGLogLevel logLevel)
        {
            var rgSettings = RGSettings.GetOrCreateSettings();
            var rgLogLevel = rgSettings.GetLogLevel();

            if (rgLogLevel == DebugLogLevel.Off)
            {
                return false;
            }

            switch (rgLogLevel)
            {
                case DebugLogLevel.Verbose:
                    return true;
                case DebugLogLevel.Debug:
                    if (logLevel is >= RGLogLevel.Debug and <= RGLogLevel.Error)
                    {
                        return true;
                    }
                    break;
                case DebugLogLevel.Info:
                    if (logLevel is >= RGLogLevel.Info and <= RGLogLevel.Error)
                    {
                        return true;
                    }
                    break;
                case DebugLogLevel.Warning:
                    if (logLevel is >= RGLogLevel.Warning and <= RGLogLevel.Error)
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
