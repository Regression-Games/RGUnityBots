using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace RegressionGames
{
    public static class RGUtils
    {
        public static bool IsCSharpPrimitive(string typeName)
        {
            HashSet<string> primitiveTypes = new HashSet<string>
            {
                "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong", "short", "ushort",
                "string"
            };

            return primitiveTypes.Contains(typeName);
        }

        /// <summary>
        /// Gets the latest write date for any file at the specified path OR any file in the specified directory.
        /// </summary>
        /// <remarks>
        /// Normally, fetching the last write date for a directory will only show you when the directory metadata was changed.
        /// However, if given a directory, this method will recursively search that directory for the latest write date of any file in the directory.
        /// </remarks>
        /// <param name="path">The path to fetch the last updated date for. Can be a file or directory.</param>
        /// <returns>The latest write date for any file in the specified path. Or, <c>null</c> if the path does not exist.</returns>
        public static DateTimeOffset? GetLatestWriteDate(string path)
        {
            // If it's a file, just fetch it's last write time.
            if (File.Exists(path))
            {
                return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            }

            // If it's not a file and it's not a directory, it doesn't exist!
            if (!Directory.Exists(path))
            {
                return null;
            }

            // Start with the latest write time of the directory itself.
            var currentDate = new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero);

            // Iterate through all the directories and files below
            // Recursively call GetLatestWriteDate to fetch their last write time.
            // Track the maximum value, and that's our answer.
            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                var entryDate = GetLatestWriteDate(entry);
                if(entryDate > currentDate)
                {
                    currentDate = entryDate.Value;
                }
            }
            return currentDate;
        }
    }
}
