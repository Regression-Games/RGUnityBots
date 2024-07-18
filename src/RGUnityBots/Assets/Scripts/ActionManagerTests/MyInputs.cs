//------------------------------------------------------------------------------
// <auto-generated>
//     This code was auto-generated by com.unity.inputsystem:InputActionCodeGenerator
//     version 1.5.1
//     from Assets/Scripts/ActionManagerTests/MyInputs.inputactions
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public partial class @MyInputs: IInputActionCollection2, IDisposable
{
    public InputActionAsset asset { get; }
    public @MyInputs()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""MyInputs"",
    ""maps"": [
        {
            ""name"": ""ActionMap1"",
            ""id"": ""a7bf1aec-dc26-4b44-91ae-fb17c3cf0a81"",
            ""actions"": [
                {
                    ""name"": ""Move"",
                    ""type"": ""Value"",
                    ""id"": ""7ce4e894-5799-45a7-bb66-6f883cf3bdd2"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Jump"",
                    ""type"": ""Button"",
                    ""id"": ""5d5b6520-318b-4c14-ae4f-53841541ec74"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Aim"",
                    ""type"": ""Value"",
                    ""id"": ""2495bebb-372e-4719-bfdd-09be37cf37ee"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""2D Vector"",
                    ""id"": ""2e3303f8-3ed7-45c0-8ed8-dd3f4302c058"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""7348a9b7-2ad9-4d83-8c5b-e3eeaca65c9a"",
                    ""path"": ""<Keyboard>/upArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""3b87d7ad-5ef4-467b-88cc-8f7260ca6241"",
                    ""path"": ""<Keyboard>/downArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""f7ea3188-8f86-4e53-b6df-a81656bdaa02"",
                    ""path"": ""<Keyboard>/leftArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""37af3eda-2586-43ff-80a6-10cf10b5d5b6"",
                    ""path"": ""<Keyboard>/rightArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""3437f24c-46b2-49d7-9f47-4896580693a7"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d3cc978a-1402-44fc-88d2-c9832cdd68b7"",
                    ""path"": ""<Mouse>/position"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Aim"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        // ActionMap1
        m_ActionMap1 = asset.FindActionMap("ActionMap1", throwIfNotFound: true);
        m_ActionMap1_Move = m_ActionMap1.FindAction("Move", throwIfNotFound: true);
        m_ActionMap1_Jump = m_ActionMap1.FindAction("Jump", throwIfNotFound: true);
        m_ActionMap1_Aim = m_ActionMap1.FindAction("Aim", throwIfNotFound: true);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Enable()
    {
        asset.Enable();
    }

    public void Disable()
    {
        asset.Disable();
    }

    public IEnumerable<InputBinding> bindings => asset.bindings;

    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    public int FindBinding(InputBinding bindingMask, out InputAction action)
    {
        return asset.FindBinding(bindingMask, out action);
    }

    // ActionMap1
    private readonly InputActionMap m_ActionMap1;
    private List<IActionMap1Actions> m_ActionMap1ActionsCallbackInterfaces = new List<IActionMap1Actions>();
    private readonly InputAction m_ActionMap1_Move;
    private readonly InputAction m_ActionMap1_Jump;
    private readonly InputAction m_ActionMap1_Aim;
    public struct ActionMap1Actions
    {
        private @MyInputs m_Wrapper;
        public ActionMap1Actions(@MyInputs wrapper) { m_Wrapper = wrapper; }
        public InputAction @Move => m_Wrapper.m_ActionMap1_Move;
        public InputAction @Jump => m_Wrapper.m_ActionMap1_Jump;
        public InputAction @Aim => m_Wrapper.m_ActionMap1_Aim;
        public InputActionMap Get() { return m_Wrapper.m_ActionMap1; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(ActionMap1Actions set) { return set.Get(); }
        public void AddCallbacks(IActionMap1Actions instance)
        {
            if (instance == null || m_Wrapper.m_ActionMap1ActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_ActionMap1ActionsCallbackInterfaces.Add(instance);
            @Move.started += instance.OnMove;
            @Move.performed += instance.OnMove;
            @Move.canceled += instance.OnMove;
            @Jump.started += instance.OnJump;
            @Jump.performed += instance.OnJump;
            @Jump.canceled += instance.OnJump;
            @Aim.started += instance.OnAim;
            @Aim.performed += instance.OnAim;
            @Aim.canceled += instance.OnAim;
        }

        private void UnregisterCallbacks(IActionMap1Actions instance)
        {
            @Move.started -= instance.OnMove;
            @Move.performed -= instance.OnMove;
            @Move.canceled -= instance.OnMove;
            @Jump.started -= instance.OnJump;
            @Jump.performed -= instance.OnJump;
            @Jump.canceled -= instance.OnJump;
            @Aim.started -= instance.OnAim;
            @Aim.performed -= instance.OnAim;
            @Aim.canceled -= instance.OnAim;
        }

        public void RemoveCallbacks(IActionMap1Actions instance)
        {
            if (m_Wrapper.m_ActionMap1ActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        public void SetCallbacks(IActionMap1Actions instance)
        {
            foreach (var item in m_Wrapper.m_ActionMap1ActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_ActionMap1ActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    public ActionMap1Actions @ActionMap1 => new ActionMap1Actions(this);
    public interface IActionMap1Actions
    {
        void OnMove(InputAction.CallbackContext context);
        void OnJump(InputAction.CallbackContext context);
        void OnAim(InputAction.CallbackContext context);
    }
}
