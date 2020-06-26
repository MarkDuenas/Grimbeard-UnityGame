using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossHealth : MonoBehaviour
{
    public Animator animator;
    public int maxHealth = 100;
    public int currentHealth;
    public bool BossDeath = false;
    Transform minotaur;    
    // Start is called before the first frame update
    void Start()
    {
        currentHealth = maxHealth;

    }

    // Update is called once per frame
    void Update()
    {
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        animator.SetTrigger("TakeDamage");

        if (currentHealth <= 160)
        {
            animator.SetBool("IsEnraged", true);
            FindObjectOfType<AudioManager>().Stop("MinotaurGrowl");
            FindObjectOfType<AudioManager>().Play("MinotaurRoar");
        }

        if(currentHealth <= 0)
        {
            Die();
        }

    }

    public void Die()
    {
        BossDeath = true;
        animator.SetBool("Dead", true);
        GetComponent<Collider2D>().enabled = false;
        GetComponent<CircleCollider2D>().enabled = false;
        

        // Vector3 newPosition = minotaur.transform.position;
        // newPosition.y = 3f;
        // minotaur.localPosition = new Vector2(237f, -3f);

        this.enabled = false;
    }


}
