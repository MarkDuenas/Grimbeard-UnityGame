using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkeletonRun : StateMachineBehaviour
{
    public float speed = 10f;

    public float attackRange = 1f;

    public int nextAttacktime = 200;
    public int attackTimer = 0;

    public float distanceX = 10f;
    public float distanceY = 5f;

    Transform player;
    Rigidbody2D rb;
    Skeleton skeleton;
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        rb = animator.GetComponent<Rigidbody2D>();
        skeleton = animator.GetComponent<Skeleton>();
    
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        skeleton.LookAtPlayer();

        Vector2 target = new Vector2(player.position.x, rb.position.y);
        if(Vector2.Distance(player.position, rb.position) <= distanceX && rb.position.y - player.position.y <= distanceY)     
        {
            Vector2 newPos = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
        }

        if(Vector2.Distance(player.position, rb.position) <= attackRange)
        {
            attackTimer += 1;
            if(attackTimer == nextAttacktime)
            {
                animator.SetTrigger("SkeletonAttack");
                attackTimer = 0;
            }

        }
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.ResetTrigger("SkeletonAttack");
    }

}
