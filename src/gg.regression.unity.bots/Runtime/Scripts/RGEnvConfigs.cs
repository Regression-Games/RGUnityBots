using System;
using JetBrains.Annotations;

namespace RegressionGames
{

    /**
     * Environment variable and command line argument utilities for configuring Regression Games
     */
    public class RGEnvConfigs
    {
        public static readonly string RG_API_KEY = "RG_API_KEY";
        public static readonly string RG_HOST = "RG_HOST";
        public static readonly string RG_AI_HOST = "RG_AI_HOST";
        public static readonly string RG_BOT = "RG_BOT";

        /**
         * Reads the RG_API_KEY from either the environment variable or command
         * line arguments. This is useful for automated CI builds, where the
         * values may be passed in as command line args and not as env vars
         * necessarily.
         */
        [CanBeNull]
        public static string ReadAPIKey() => ReadEnvVarOrCommandLine(RG_API_KEY);

        /**
         * Reads the RG_HOST from either the environment variable or command
         * line arguments. This is useful for automated CI builds, where the
         * values may be passed in as command line args and not as env vars
         * necessarily.
         */
        [CanBeNull]
        public static string ReadHost() => ReadEnvVarOrCommandLine(RG_HOST);

        /**
         * Reads the RG_CV_HOST from either the environment variable or command
         * line arguments. This is useful for automated CI builds, where the
         * values may be passed in as command line args and not as env vars
         * necessarily.
         */
        [CanBeNull]
        public static string ReadAiHost() => ReadEnvVarOrCommandLine(RG_AI_HOST);

        /**
         * Reads the BOT from either the environment variable or command
         * line arguments. This is useful for automated CI builds, where the
         * values may be passed in as command line args and not as env vars
         * necessarily.
         */
        [CanBeNull]
        public static string ReadBotId() => ReadEnvVarOrCommandLine(RG_BOT);

        [CanBeNull]
        private static string ReadEnvVarOrCommandLine(string key)
        {
            // First, try to read the env var
            var value = Environment.GetEnvironmentVariable(key);
            if (value != null)
            {
                return value;
            }
            // Otherwise, read the command line args
            var commandLineArgs = Environment.GetCommandLineArgs();
            var index = Array.FindIndex(commandLineArgs, s => s.Equals("-" + key)) + 1;
            if (index == 0 || index >= commandLineArgs.Length)
            {
                return null;
            }
            return commandLineArgs[index];
        }

    }
}
