using System.Collections.Generic;
using UnityEngine;

/*
 * A component that can be inherited to relay game state information to
 * Regression Games. Includes a few default pieces of information that can
 * be enabled from the editor when attached to an object.
 *
 * TODO: Can we use a generic type instead of a dictionary? That way users can
 *       debug and use the states within their own code?
 */
public class RGState: MonoBehaviour
{

    [Header("General Information")]
    public string objectType;

    [Header("3D Positioning")]
    public bool syncPosition = true;
    public bool syncRotation = true;

    /**
     * A function that is overrided to provide the custom state of this specific GameObject.
     * For example, you may want to retrieve and set the health of a player on the returned
     * object, or their inventory information
     */
    public virtual Dictionary<string, object> GetState()
    {
        return new Dictionary<string, object>();
    }

    /**
     * Returns the entire internal state for this object, which consists of the default
     * states tracked by RG, and the result of any overridden GetState implementation.
     */
    public Dictionary<string, object> GetGameObjectState()
    {
        var state = new Dictionary<string, object>
        {
            ["id"] = this.transform.GetInstanceID(),
            ["type"] = objectType,
        };
        if (syncPosition) state["position"] = transform.position;
        if (syncRotation) state["rotation"] = transform.rotation;
        foreach (var entry in GetState())
        {
            state.Add(entry.Key, entry.Value);
        }

        return state;
    }

}

