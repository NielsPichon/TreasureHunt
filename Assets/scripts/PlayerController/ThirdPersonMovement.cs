using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonMovement : MonoBehaviour
{
    // How fast the character will move
    public float speed = 6.0f;
    // How fast the character will rotate to face the direction of movement
    public float rotationSpeed = 0.15f;

    private CharacterController controller;

    // Start is called before the first frame update
    void Start()
    {
        controller = this.GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(
            horizontal,
            0.0f,
            vertical
        );

        if (direction.magnitude >= 0.1f) {
            controller.Move(direction.normalized * speed * Time.deltaTime);

            this.transform.rotation = Quaternion.Slerp(
                this.transform.rotation,
                Quaternion.LookRotation(direction),
                rotationSpeed
            );
        }
    }
}
