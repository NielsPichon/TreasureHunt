using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TopDownController : MonoBehaviour
{
    public float speed = 5.0f;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Move the camera forward, backward, left, and right
        transform.position += (
            transform.forward * Input.GetAxis("Vertical")
            * speed * Time.deltaTime
        );
        transform.position += (
            transform.right * Input.GetAxis("Horizontal")
            * speed * Time.deltaTime
        );
    }
}
