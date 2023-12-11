using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetFramerate : MonoBehaviour
{
    [SerializeField] private int targetFramerate = 60;
    
    void Start()
    {
        Application.targetFrameRate = targetFramerate;
    }
}
