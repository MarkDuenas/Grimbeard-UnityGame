using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyerHealth : MonoBehaviour
{
    public Animator animator;
    public int maxHealth = 50;
    public int currentHealth;
    Transform MonEnemy;    
    // Start is called before the first frame update
    void Start()
    {
        currentHealth = maxHealth;

    }

    // Update is called once per frame
    void Update()
    {}

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }

    }

    public void Die()
    {
        animator.SetBool("isDead", true);
        this.enabled = false;
        GetComponent<CircleCollider2D>().enabled = false;
        // FindObjectOfType<AudioManager>().Play("FlyingDeath");

    }
}
