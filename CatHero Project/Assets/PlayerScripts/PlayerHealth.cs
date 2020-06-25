using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public Animator animator;
    public int maxHealth = 100;
    public int currentHealth;
    public HealthBar healthBar;
    // Start is called before the first frame update
    public void Start()
    {
        currentHealth = maxHealth;
        healthBar.SetMaxHealth(maxHealth);
    }

    // Update is called once per frame
    public void Update()
    {
        // if (Input.GetKeyDown(KeyCode.X))
        // {
        //     TakeDamage(20);
        // }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        healthBar.SetHealth(currentHealth);
        animator.SetTrigger("Damaged");
            if(currentHealth <= 0)
            {
                FindObjectOfType<AudioManager>().Play("PlayerDeath");
            }
    }
}
