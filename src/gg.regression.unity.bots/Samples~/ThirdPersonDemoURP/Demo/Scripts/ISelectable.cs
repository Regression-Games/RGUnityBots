using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo
{
    public interface ISelectable
    {
        public void Select();

        public void Deselect();
    }
}