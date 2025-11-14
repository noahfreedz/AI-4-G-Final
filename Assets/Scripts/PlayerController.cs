using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float move_speed = 5f;

    [SerializeField] private float floor_y = -2f;

    [SerializeField] WaveFunctionCollapse WFC;

    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        handle_movement();
        clamp_to_floor();

        // Pass Position To WFC
        WFC.UpdateWFC(transform.position);
    }

    private void handle_movement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move_dir = new Vector3(x, 0f, z).normalized;

        if (move_dir.magnitude > 0.1f)
        {
            controller.Move(move_dir * move_speed * Time.deltaTime);
        }
    }

    private void clamp_to_floor()
    {
        Vector3 pos = transform.position;
        pos.y = floor_y;
        transform.position = pos;
    }
}
