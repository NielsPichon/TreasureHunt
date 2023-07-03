using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowCam : MonoBehaviour
{
    public GameObject target;
    public float smoothness = 0.5f;
    public float height = 5.0f;
    public float distance = 2.0f;

    Vector3 GetTargetLocation() {
        return this.target.transform.position
            + Vector3.up * this.height
            - this.target.transform.forward * this.distance;
    }

    Vector3 GetTargetLookAt() {
        return this.target.transform.position
            + Vector3.up * this.height;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 targetLocation = GetTargetLocation();
        Vector3 targetLookAt = GetTargetLookAt();
        Vector3 currentLocation = this.transform.position;
        Vector3 currentLookAt = this.transform.position
            + this.transform.forward * this.distance;

        this.transform.position = Vector3.Lerp(
            currentLocation,
            targetLocation,
            (1 - this.smoothness)
        );
        this.transform.LookAt(Vector3.Lerp(
            currentLookAt,
            targetLookAt,
            (1 - this.smoothness)
        ));
    }
}
