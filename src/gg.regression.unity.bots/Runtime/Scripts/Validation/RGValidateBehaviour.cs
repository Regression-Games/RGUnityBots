using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateRecorder.BotSegments.Models.SegmentValidations;
using UnityEngine;

namespace RegressionGames.Validation
{
    public class RGValidateBehaviour: MonoBehaviour
    {

        private bool _isPaused;
        
        public class ValidatorData
        {
            public MethodInfo Method { get; set; }
            public int Frequency { get; set; }
            
            public ValidationMode Mode { get; set; }
            
            public SegmentValidationStatus Status { get; set; } = SegmentValidationStatus.UNKNOWN;
            
            public System.Exception ThrownException { get; set; }
        }

        public List<ValidatorData> Validators = new ();
        
        /**
         * Called when any validation here changes
         */
        public delegate void ValidationsUpdated();
        public event ValidationsUpdated OnValidationsUpdated;

        private int frame = 0;
        private ValidatorData currentValidator;
        private SegmentValidationStatus currentStatus = SegmentValidationStatus.UNKNOWN;

        public void Awake()
        {
            // Cache all methods with UpdateMethod attribute
            Validators = new List<ValidatorData>();
            var methods = GetType().GetMethods(BindingFlags.Instance | 
                                               BindingFlags.NonPublic | 
                                               BindingFlags.Public);
        
            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<RGValidate>();
                if (attribute != null)
                {
                    Validators.Add(new ValidatorData 
                    { 
                        Method = method,
                        Frequency = attribute.Frequency,
                        Mode = attribute.Mode
                    });
                    RGDebug.Log("[RGValidator] Activated " + method.Name);
                } 
            }
            
        }

        public void AssertAsTrue(string message = null)
        {
            currentStatus = SegmentValidationStatus.PASSED;
        }

        public void AssertAsFalse(string message = null)
        {
            currentStatus = SegmentValidationStatus.FAILED;
        }

        /**
         * <summary>
         * Returns a collection of all the results for the validations so far.
         * This does not necessarily mean the final result - it is the result
         * for this moment in time.
         * </summary>
         */
        public SegmentValidationResultSetContainer GetResults()
        {
            var resultSet = new SegmentValidationResultSetContainer
            {
                name = "SET THE CLASS HERE",
                validationResults = Validators.Select(v => new SegmentValidationResultContainer(v.Method.Name, null, v.Status)).ToList()
            };
            return resultSet;
        }

        public void ResetValidationStates()
        {
            // The base implementation of this doesn't do anything - it's up to the validation script to implement this
        }

        /**
         * <summary>
         * Resets all results for this script.
         * </summary>
         */
        public void ResetResults()
        {
            // First, reset all the stored results
            foreach (var validator in Validators)
            {
                validator.Status = SegmentValidationStatus.UNKNOWN;
                validator.ThrownException = null;
            }
            
            // Then call the reset function on the script, in case they need to reset some intermediate state
            ResetValidationStates();
        }

        private void Update()
        {

            // If paused, don't do anything now
            if (_isPaused) return;
            
            // Call tagged methods based on their frequency
            foreach (var validator in Validators)
            {

                // Check that we should run on this frame
                if (frame % validator.Frequency != 0)
                {
                    continue;
                }
                
                // Skip over validators that are already passed for failed
                if (validator.Status == SegmentValidationStatus.FAILED)
                {
                    continue;
                }
                if (validator.Status == SegmentValidationStatus.PASSED && validator.Mode != ValidationMode.ONCE_TRUE_ALWAYS_TRUE)
                {
                    continue;
                }

                // Run the validator
                try
                {
                    currentStatus = SegmentValidationStatus.UNKNOWN;
                    currentValidator = validator;
                    validator.Method.Invoke(this, null);
                } 
                catch (Exception e)
                {
                    Debug.LogError($"Error running validation method {validator.Method.Name}: {e.Message}");
                    validator.ThrownException = e;
                }
                
                // After the method is called, we can figure out what to do with the validators that should immediately
                // fail or pass
                if (currentValidator.Mode == ValidationMode.NEVER_TRUE && currentStatus == SegmentValidationStatus.PASSED)
                {
                    // Debug.LogError($"Validation method {validator.Method.Name} should never pass, but it did.");
                    currentValidator.Status = SegmentValidationStatus.FAILED;
                    OnValidationsUpdated?.Invoke();
                }
                else if (currentValidator.Mode == ValidationMode.ALWAYS_TRUE && currentStatus is SegmentValidationStatus.FAILED or SegmentValidationStatus.UNKNOWN)
                {
                    // Debug.LogError($"Validation method {validator.Method.Name} should always pass, but it did not.");
                    currentValidator.Status = SegmentValidationStatus.FAILED;
                    OnValidationsUpdated?.Invoke();
                } 
                else if (currentValidator.Mode == ValidationMode.EVENTUALLY_TRUE && currentStatus == SegmentValidationStatus.PASSED)
                {
                    // Debug.LogError($"Validation method {validator.Method.Name} finally passed.");
                    currentValidator.Status = SegmentValidationStatus.PASSED;
                    OnValidationsUpdated?.Invoke();
                }
                else if (currentValidator.Mode == ValidationMode.ONCE_TRUE_ALWAYS_TRUE &&
                         currentValidator.Status == SegmentValidationStatus.PASSED &&
                         currentStatus != SegmentValidationStatus.PASSED)
                {
                    // Debug.LogError($"Validation method {validator.Method.Name} passed before but is no longer passing.");
                    currentValidator.Status = SegmentValidationStatus.FAILED;
                    OnValidationsUpdated?.Invoke();
                }
                else if (currentValidator.Mode == ValidationMode.ONCE_TRUE_ALWAYS_TRUE &&
                         currentValidator.Status == SegmentValidationStatus.UNKNOWN &&
                         currentStatus == SegmentValidationStatus.PASSED)
                {
                    // Debug.LogError($"Validation method {validator.Method.Name} is now passing.");
                    currentValidator.Status = SegmentValidationStatus.PASSED;
                    OnValidationsUpdated?.Invoke();
                } 
                else if (currentValidator.Mode == ValidationMode.ONCE_TRUE_ALWAYS_TRUE &&
                         currentValidator.Status == SegmentValidationStatus.UNKNOWN &&
                         currentStatus == SegmentValidationStatus.FAILED)
                {
                    // Debug.LogError($"Validation method {validator.Method.Name} has now failed.");
                    currentValidator.Status = SegmentValidationStatus.FAILED;
                    OnValidationsUpdated?.Invoke();
                }

            }

            frame++;
        }

        public void PauseValidation()
        {
            _isPaused = true;
        }

        public void UnPauseValidation()
        {
            _isPaused = false;
        }

        /**
         * When the tests end, we need to mark all tests that were expected to be finished but aren't as failed
         */
        private void OnDestroy()
        {
            foreach (var validator in Validators)
            {
                if (validator.Mode == ValidationMode.ALWAYS_TRUE && validator.Status == SegmentValidationStatus.UNKNOWN)
                {
                    //Debug.LogError($"Validation method {validator.Method.Name} should always pass, but it never did.");
                    validator.Status = SegmentValidationStatus.FAILED;
                    OnValidationsUpdated?.Invoke();
                }
                else if (validator.Mode == ValidationMode.NEVER_TRUE && validator.Status == SegmentValidationStatus.UNKNOWN)
                {
                    //Debug.LogError($"Validation method {validator.Method.Name} should never pass, and it never did!");
                    validator.Status = SegmentValidationStatus.PASSED;
                    OnValidationsUpdated?.Invoke();
                }
            }
        }
    }
}