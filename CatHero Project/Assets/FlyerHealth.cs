using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyerHealth : MonoBehaviour
{
    public int flyerHealth = 50;
    public GameObject deatheffect;
    public void TakeDamage(int damage)
    {
            flyerHealth -= damage;
        
        if(flyerHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Instantiate(deatheffect, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
