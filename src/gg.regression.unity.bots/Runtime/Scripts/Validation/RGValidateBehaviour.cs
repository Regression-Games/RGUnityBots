using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RegressionGames.Validation
{
    public class RGValidateBehaviour: MonoBehaviour
    {
        
        private class ValidatorData
        {
            public MethodInfo Method { get; set; }
            public int Frequency { get; set; }
            public RGCondition Condition { get; set; }
            public RGValidatorResult Result { get; set; } = RGValidatorResult.NOT_SET;
            public System.Exception ThrownException { get; set; }
        }

        private List<ValidatorData> validators;

        private int frame = 0;
        private ValidatorData currentValidator;
        private RGValidatorResult currentStatus = RGValidatorResult.NOT_SET;

        private void Awake()
        {
            // Cache all methods with UpdateMethod attribute
            validators = new List<ValidatorData>();
            var methods = GetType().GetMethods(BindingFlags.Instance | 
                                               BindingFlags.NonPublic | 
                                               BindingFlags.Public);
        
            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<RGValidate>();
                if (attribute != null)
                {
                    validators.Add(new ValidatorData 
                    { 
                        Method = method,
                        Frequency = attribute.Frequency,
                        Condition = attribute.Condition
                    });
                    Debug.Log("[RGValidator] Activated " + method.Name);
                } 
            }
        }

        public void AssertAsTrue(string message = null)
        {
            currentStatus = RGValidatorResult.PASSED;
        }

        public void AssertAsFalse(string message = null)
        {
            currentStatus = RGValidatorResult.FAILED;
        }

        private void Update()
        {
            
            // Call tagged methods based on their frequency
            foreach (var validator in validators)
            {

                // Check that we should run on this frame
                if (frame % validator.Frequency != 0)
                {
                    continue;
                }
                
                // Skip over validators that are already passed for failed
                if (validator.Result is RGValidatorResult.PASSED or RGValidatorResult.FAILED)
                {
                    continue;
                }

                // Run the validator
                try
                {
                    currentStatus = RGValidatorResult.NOT_SET;
                    currentValidator = validator;
                    validator.Method.Invoke(this, null);
                } 
                catch (System.Exception e)
                {
                    Debug.LogError($"Error running validation method {validator.Method.Name}: {e.Message}");
                    validator.ThrownException = e;
                }
                
                // After the method is called, we can figure out what to do with the validators that should immediately
                // fail or pass
                if (currentValidator.Condition == RGCondition.NEVER_TRUE && currentStatus == RGValidatorResult.PASSED)
                {
                    Debug.LogError($"Validation method {validator.Method.Name} should never pass, but it did.");
                    currentValidator.Result = RGValidatorResult.FAILED;
                }
                else if (currentValidator.Condition == RGCondition.ALWAYS_TRUE && currentStatus is RGValidatorResult.FAILED or RGValidatorResult.NOT_SET)
                {
                    Debug.LogError($"Validation method {validator.Method.Name} should always pass, but it did not.");
                    currentValidator.Result = RGValidatorResult.FAILED;
                } 
                else if (currentValidator.Condition == RGCondition.EVENTUALLY_TRUE && currentStatus == RGValidatorResult.PASSED)
                {
                    Debug.LogError($"Validation method {validator.Method.Name} finally passed.");
                    currentValidator.Result = RGValidatorResult.PASSED;
                }

            }

            frame++;
        }
        
    }
}