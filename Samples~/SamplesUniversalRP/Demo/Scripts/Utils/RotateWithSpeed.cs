using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo
{
    public class RotateWithSpeed : MonoBehaviour
    {
        enum Axis {X, Y, Z}

        [SerializeField] private Axis _rotationAxis;
        [SerializeField] private float _rotationSpeed;

        private Vector3 _rotationVector;
        
        void Start()
        {
            switch (_rotationAxis)
            {
                case Axis.X:
                    _rotationVector = Vector3.right;
                    break;
                case Axis.Y:
                    _rotationVector = Vector3.up;
                    break;
                case Axis.Z:
                    _rotationVector = Vector3.forward;
                    break;
            }
        }

        void Update()
        {
            transform.RotateAround(transform.position, _rotationVector, _rotationSpeed * Time.deltaTime);
        }
    }
}