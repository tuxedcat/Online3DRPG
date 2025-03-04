﻿using System;
using UnityEngine;

public class EnemyAttacking : StateMachineBehaviour
{
    Enemy _enemy;
    Collider _ardColl;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_enemy == null)
        {
            _enemy = animator.GetComponent<Enemy>();
            _ardColl = animator.GetComponentInChildren<AttackRangeDetector>().GetComponent<Collider>();
        }
        _ardColl.enabled = false;
    }

    //override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //}

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _enemy.OnAttackAnimationEnd(_ardColl);
    }

    // OnStateMove is called right after Animator.OnAnimatorMove(). Code that processes and affects root motion should be implemented here
    //override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
    //
    //}

    // OnStateIK is called right after Animator.OnAnimatorIK(). Code that sets up animation IK (inverse kinematics) should be implemented here.
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
    //
    //}
}