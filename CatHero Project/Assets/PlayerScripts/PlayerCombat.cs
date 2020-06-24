using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{

    public Animator animator;

    public Transform attackPoint;
    public float attackRange = .05f;
    public LayerMask enemyLayers;

    public float attackRate = 2f;
    float nextAttacktime = 0f;
    // Update is called once per frame
    void Update()
    {
        if(Time.time >= nextAttacktime) 
        {

            if (Input.GetMouseButtonDown(0))
            {
                Attack();
                nextAttacktime = Time.time + 1f/ attackRate;
                FindObjectOfType<AudioManager>().Play("AxeSwing");
            }

            else if(Input.GetMouseButtonDown(1))
            {
                AttackTwo();
                nextAttacktime = Time.time + 1f/ attackRate;
                FindObjectOfType<AudioManager>().Play("AxeSwing");
            }
        }
    }

    void Attack()
    {
        animator.SetTrigger("Attack");

        // Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRangem, enemyLayers);

        // foreach(Collider2D enemy in hitEnemies)
        // {
        //     Debug.Log("We Hit");
        // }
    }

    void AttackTwo()
    {
        animator.SetTrigger("AttackTwo");
    }

    void OnDrawGizmosSelected()
    {
        if(attackPoint == null)
            return;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
