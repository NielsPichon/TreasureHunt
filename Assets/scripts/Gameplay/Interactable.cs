using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    //Basic class which implements the property of interactable that they can
    // only be interacted with by a single user at once

    [Tooltip("The duration of the highlight when the object"
             + "stops being interacted with")]
    public float highlightDuration = 0.0f;
    [Tooltip("The debug material to use when the object is highlighted")]
    public Material debugMaterial;

    [System.NonSerialized]
    public bool isInteractable = true;
    private float highlightTime = 0.0f;
    private Material originalMaterial;

    void Start()
    {
        gameObject.layer = LayerMask.NameToLayer("Interactable");
        originalMaterial = gameObject.GetComponent<Renderer>().material;
    }

    public bool StartInteraction()
    {
        if (isInteractable)
        {
            Debug.Log(gameObject.name + " is being interacted with");
            isInteractable = false;
            return true;
        }
        return false;
    }

    public void StopInteraction()
    {
        isInteractable = true;
    }

    public void ToggleHighlight(bool highlight)
    {
        if (highlight)
        {
            highlightTime = highlightDuration;
            gameObject.GetComponent<Renderer>().material = debugMaterial;

        }
        else
        {
            gameObject.GetComponent<Renderer>().material = originalMaterial;
        }
    }

    void Update() {
        if (highlightTime > 0.0f) {
            highlightTime -= Time.deltaTime;
            if (highlightTime <= 0.0f) {
                ToggleHighlight(false);
            }
        }
    }
}
