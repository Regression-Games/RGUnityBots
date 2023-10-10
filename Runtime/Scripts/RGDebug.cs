using System;
using System.Threading;
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
         * lower in the Regression Project Settings.
         * This is an alias for LogInfo
         */
        public static void Log(string message)
        {
            LogToConsole(message, RGLogLevel.Info);
        }
        
        /**
         * Logging of 'Info' logs. Only visible if Log Level is set to 'Info' or
         * lower in the Regression Project Settings
         */
        public static void LogInfo(string message)
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
        public static void LogException(Exception exception)
        {
            if (!CheckLogLevel(RGLogLevel.Error))
            {
                return;
            }
            Debug.LogException(exception);
        }
        
        public static bool IsVerboseEnabled => CheckLogLevel(RGLogLevel.Verbose);
        public static bool IsDebugEnabled => CheckLogLevel(RGLogLevel.Debug);
        public static bool IsInfoEnabled => CheckLogLevel(RGLogLevel.Info);
        public static bool IsWarningEnabled => CheckLogLevel(RGLogLevel.Warning);
        public static bool IsErrorEnabled => CheckLogLevel(RGLogLevel.Error);

        private static string buildPrefix(RGLogLevel logLevel)
        {
            return $"{{RG}} {DateTime.Now:yyyy-MM-ddTHH:mm:ss:ffff}  {logLevel.ToString().ToUpperInvariant()} [{Thread.CurrentThread.ManagedThreadId}] --- ";
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
                    Debug.Log(buildPrefix(logLevel) + message);
                    break;
                case RGLogLevel.Warning:
                    Debug.LogWarning(buildPrefix(logLevel) + message);
                    break;
                case RGLogLevel.Error:
                    Debug.LogError(buildPrefix(logLevel) + message);
                    break;
                default:
                    Debug.Log(buildPrefix(logLevel) + message);
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