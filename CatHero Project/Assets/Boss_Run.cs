using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss_Run : StateMachineBehaviour
{
    public float speed = 10f;

    public float attackRange = 2f;

    public int nextAttacktime = 50;
    public int attackTimer = 0;

    public int distance = 20;
    

    Transform player;
    Rigidbody2D rb;
    Boss boss;


    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        rb = animator.GetComponent<Rigidbody2D>();
        boss = animator.GetComponent<Boss>();
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        boss.LookAtPlayer();
        Vector2 target = new Vector2(player.position.x, rb.position.y);
        if(Vector2.Distance(player.position, rb.position) <= distance)
        {
            if(!FindObjectOfType<AudioManager>().playing)
            {

                MusicCheck();
            }
            Vector2 newPos = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
        }


        if(Vector2.Distance(player.position, rb.position) <= attackRange)
        {
            attackTimer += 1;
            if(attackTimer == nextAttacktime)
            {
                animator.SetTrigger("StabAttack");
                attackTimer = 0;
            }

            }
        }

        void MusicCheck()
        {
            if(!FindObjectOfType<AudioManager>().playing)
            {
                FindObjectOfType<AudioManager>().Stop("BackgroundMusic");
                FindObjectOfType<AudioManager>().Play("MinotaurGrowl");
                FindObjectOfType<AudioManager>().Play("BossBattle");
            
                FindObjectOfType<AudioManager>().playing = true;

            }
        }
                
                
                    


    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.ResetTrigger("StabAttack");
    }
}

