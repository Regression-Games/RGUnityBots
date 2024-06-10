using System;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    public abstract class RGGameAction
    {
        /**
         * The path of the action, typically derived from
         * the location where the associated input handling logic takes place.
         * 
         * This serves as an identifier for the action. It is expected that
         * upon changes to the code, the path will remain the same if the
         * location of the associated input-handling logic has not changed.
         */
        public string Path { get; private set; }
        
        /**
         * The type of object that the action is associated with.
         * The object must be derived from UnityEngine.Object, and
         * all instances of the object should be retrievable via
         * UnityEngine.Object.FindObjectsOfType(ObjectType).
         */
        public abstract Type ObjectType { get; }
        
        public RGGameAction(string path)
        {
            Path = path;
        }
        
    }
}