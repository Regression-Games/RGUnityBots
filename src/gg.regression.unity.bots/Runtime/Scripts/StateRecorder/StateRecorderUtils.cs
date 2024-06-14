using System.Collections.Generic;

namespace RegressionGames.StateRecorder
{
    public static class StateRecorderUtils
    {
        public static bool OptimizedContainsIntInList(List<int> list, int theScene)
        {
            var listCount = list.Count;
            for (var i = 0; i < listCount; i++)
            {
                if (list[i] == theScene)
                {
                    return true;
                }
            }

            return false;
        }


        public static bool OptimizedRemoveIntFromList(List<int> list, int theInt)
        {
            var listCount = list.Count;
            for (var i = 0; i < listCount; i++)
            {
                if (list[i] == theInt)
                {
                    list.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public static bool OptimizedContainsStringInArray(string[] list, string theString)
        {
            if (list != null)
            {
                var listCount = list.Length;
                for (var i = 0; i < listCount; i++)
                {
                    if (string.CompareOrdinal(list[i], theString) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool OptimizedStringStartsWithStringInList(List<string> list, string theString)
        {
            if (list != null)
            {
                var listCount = list.Count;
                for (var i = 0; i < listCount; i++)
                {
                    // if the string being checked starts with any of the strings in the list
                    if (theString.StartsWith(list[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool OptimizedContainsStringInList(List<string> list, string theString)
        {
            if (list != null)
            {
                var listCount = list.Count;
                for (var i = 0; i < listCount; i++)
                {
                    if (string.CompareOrdinal(list[i], theString) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool OptimizedRemoveStringFromList(List<string> list, string theString)
        {
            if (list != null)
            {
                var listCount = list.Count;
                for (var i = 0; i < listCount; i++)
                {
                    if (string.CompareOrdinal(list[i], theString) == 0)
                    {
                        list.RemoveAt(i);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
