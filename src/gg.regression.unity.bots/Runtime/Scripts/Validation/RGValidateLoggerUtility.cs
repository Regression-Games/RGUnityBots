using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using StateRecorder.BotSegments.Models.SegmentValidations;

namespace RegressionGames.Validation
{
    
    /**
     * A set of utilities for logging results related to validations
     */
    public class RGValidateLoggerUtility
    {

        /**
         * <summary>Prints out validation results in a clean way in the logs</summary>
         */
        public static void LogValidationResults([CanBeNull] List<SegmentValidationResultSetContainer> results)
        {

            if (results == null)
            {
                // For now, if there are no validations, we just print a small message
                RGDebug.LogInfo("No validations were provided as part of this run");
                return;
            }
            
            // Print out results while also collecting total results
            var passed = 0;
            var failed = 0;
            var unknown = 0;

            var logBuilder = new StringBuilder(100_000);

            logBuilder.Append("<b>--------------- VALIDATION RESULTS ---------------</b> (If in the editor, click this to view more)\n\n");
            foreach (var resultSet in results)
            {
                logBuilder.Append("<b>" + resultSet.name + "</b>\n");
                foreach (var validation in resultSet.validationResults)
                {
                    switch (validation.result)
                    {
                        case SegmentValidationStatus.PASSED:
                            logBuilder.Append("    <b><color=green>[PASS]</color></b> ");
                            passed++;
                            break;
                        case SegmentValidationStatus.FAILED:
                            logBuilder.Append("    <b><color=red>[FAIL]</color></b> ");
                            failed++;
                            break;
                        case SegmentValidationStatus.UNKNOWN:
                            logBuilder.Append("    <b>[UNKNOWN]</b> ");
                            unknown++;
                            break;
                    }
                    logBuilder.Append(validation.name + "\n");
                }

                logBuilder.Append("\n");
            }

            if (failed > 0)
            {
                logBuilder.Append("<b><color=red>VALIDATIONS FAILED</color> - ");
            }
            else
            {
                logBuilder.Append("<b><color=green>VALIDATIONS PASSED</color> - ");
            }

            logBuilder.Append($"<color=red>{failed} FAILED</color>, <color=green>{passed} PASSED</color> <i>({unknown} UNKNOWN)</i></b>\n\n");
            
            // Finally log the results
            if (failed > 0)
            {
                RGDebug.LogError(logBuilder.ToString());
            }
            else
            {
                RGDebug.LogInfo(logBuilder.ToString());
            }

        }
        
    }
}