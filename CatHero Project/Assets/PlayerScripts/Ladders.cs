using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ladders : MonoBehaviour
{

    public float speed = 6;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.tag == "Player" && Input.GetKey(KeyCode.UpArrow))
        {
            other.GetComponent<Rigidbody2D> ().velocity = new Vector2(0, speed);
        }
        else if (other.tag == "Player" && Input.GetKey(KeyCode.DownArrow))
        {
            other.GetComponent<Rigidbody2D> ().velocity = new Vector2(0, -speed);
        }
        else
        {
            other.GetComponent<Rigidbody2D> ().velocity = new Vector2 (0,1);
        }

    }




}
