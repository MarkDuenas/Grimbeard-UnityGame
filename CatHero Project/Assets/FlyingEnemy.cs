using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyingEnemy : MonoBehaviour
{
    // public float downspeed = -5f;
    // public float upspeed = 5f;
    public Transform player;
    public bool isFlipped = false;
    public void LookAtPlayer()
    {
        Vector3 flipped = transform.localScale;
        flipped.z *= -1f;


        //RIGHT NOW HE IS MISTY STEPPING ALL OVER THE PLACE AND IT'S TERRIFYING...

        // if(transform.position.y > player.position.y)
        // {
        //     transform.Translate(0f, downspeed*Time.deltaTime, 0f);
        // }

        // if(transform.position.y < player.position.y)
        // {
        //     transform.Translate(0f, upspeed*Time.deltaTime, 0f);
        // }


        if(transform.position.x > player.position.x && isFlipped)
        {
            transform.localScale = flipped;
            transform.Rotate(2f, 180f, 0f);
            isFlipped = false;
        }
        else if (transform.position.x < player.position.x && !isFlipped)
        {
            transform.localScale = flipped;
            transform.Rotate(2f, 180f, 0f);
            isFlipped = true;
        }
    }

}
