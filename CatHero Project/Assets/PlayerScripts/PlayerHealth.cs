using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public Animator animator;
    public int maxHealth = 100;
    public int currentHealth;
    public HealthBar healthBar;
    public bool PlayerDeath = false;
    // Start is called before the first frame update
    public void Start()
    {
        currentHealth = maxHealth;
        healthBar.SetMaxHealth(maxHealth);
    }

    // Update is called once per frame
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            TakeDamage(20);
        }
        // if (PlayerDeath)
        // {
        //     FindObjectOfType<GameManager>().Loss();
        // }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        healthBar.SetHealth(currentHealth);
        animator.SetTrigger("Damaged");
        
        if(currentHealth <= 0)
        {
            Die();
            // PlayerDeath = true;
        }
    }

    public void Die()
    {
        PlayerDeath = true;
        animator.SetBool("IsDead", true);
        // GetComponent<BoxCollider2D>().enabled = false;
        // GetComponent<CircleCollider2D>().enabled = false;


        this.enabled = false;

    }
        
}
