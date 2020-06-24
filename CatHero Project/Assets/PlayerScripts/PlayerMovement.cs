using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController2D controller;

    public Animator animator;
    public float playerSpeed = 40f;
    private bool facingRIght = true;
    private float MoveX = 0f;

    bool jump = false;

    // Update is called once per frame
    void Update()
    {
        MoveX = Input.GetAxisRaw("Horizontal") * playerSpeed;

        animator.SetFloat("Speed", Mathf.Abs(MoveX));

        if (Input.GetButtonDown("Jump"))
        {
            jump = true;
            animator.SetBool("IsJumping", true);
        }
    }

    public void OnLanding()
    {
        animator.SetBool("IsJumping", false);
    }

    void FixedUpdate()
    {
        controller.Move(MoveX*Time.fixedDeltaTime, false, jump);
        jump = false;
    }
}
