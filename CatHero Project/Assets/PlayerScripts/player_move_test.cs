using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class player_move_test : MonoBehaviour
{
    public Animator animator;
    public int playerSpeed = 6;
    private bool facingRIght = true;
    public int playerJumpPower = 500;
    private float MoveX;
    

    // Update is called once per frame
    void Update()
    {
        PlayerMove();

    }

    void PlayerMove()
    {
    
        //CONTROLS
        MoveX = Input.GetAxis("Horizontal") * playerSpeed;

        animator.SetFloat("Speed", Mathf.Abs(MoveX));
        
        if(Input.GetButtonDown ("Jump"))
        {
            Jump();
        }
        //ANIAMTION
        //PLAYER DIRECTION
        if(MoveX > 0.0f && facingRIght == false)
        {
            FlipPlayer();
        }
        else if (MoveX < 0.0f && facingRIght == true)
        {
            FlipPlayer();
        }
        //PHYSICS
        gameObject.GetComponent<Rigidbody2D>().velocity = new Vector2 (MoveX, gameObject.GetComponent<Rigidbody2D>().velocity.y);
    }

    void Jump()
    {
        if(gameObject.GetComponent<Rigidbody2D>().velocity.y < .5 && gameObject.GetComponent<Rigidbody2D>().velocity.y > -.5)
        {
            GetComponent<Rigidbody2D>().AddForce (Vector2.up * playerJumpPower);
        }
        //Jumping Code
    }

    void FlipPlayer()
    {
        facingRIght = !facingRIght;
        Vector2 localScale = gameObject.transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

}