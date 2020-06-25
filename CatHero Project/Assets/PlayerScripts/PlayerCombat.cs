using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{

    public Animator animator;

    public Transform attackPoint;
    public float attackRange = .05f;
    public LayerMask enemyLayers;
    public int attackDamage = 20;
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
            }

            else if(Input.GetMouseButtonDown(1))
            {
                AttackTwo();
                nextAttacktime = Time.time + 1f/ attackRate;
            }
        }
    }

    void Attack()
    {
        animator.SetTrigger("Attack");

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach(Collider2D enemy in hitEnemies)
        {
            if(enemy.tag == "Boss")
            {
                enemy.GetComponent<BossHealth>().TakeDamage(attackDamage);
            }
            else if(enemy.tag == "Skeleton")
            {
                enemy.GetComponent<SkeletonHealth>().TakeDamage(attackDamage);
            }
            else if(enemy.tag == "Flyer")
            {
                enemy.GetComponent<FlyerHealth>().TakeDamage(attackDamage);
            }
        }
    }

    void AttackTwo()
    {
        animator.SetTrigger("AttackTwo");

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach(Collider2D enemy in hitEnemies)
        {
            if(enemy.tag == "Boss")
            {
                enemy.GetComponent<BossHealth>().TakeDamage(attackDamage);
            }
            else if(enemy.tag == "Skeleton")
            {
                enemy.GetComponent<SkeletonHealth>().TakeDamage(attackDamage);
            }
            else if(enemy.tag == "Flyer")
            {
                enemy.GetComponent<FlyerHealth>().TakeDamage(attackDamage);
            }
        }
    }
    void OnDrawGizmosSelected()
    {
        if(attackPoint == null)
            return;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

}
