using UnityEngine;
using UnityEngine.AI;
using AutoService.Services.Core;

namespace AutoService.Presentation.Characters
{
    public sealed class CharacterAnimator
    {
        private static readonly int SpeedHash = Animator.StringToHash(""Speed"");
        private static readonly int CarryingHash = Animator.StringToHash(""Carrying"");

        private readonly Animator _animator;
        private readonly NavMeshAgent _agent;

        public CharacterAnimator(Animator animator, NavMeshAgent agent)
        {
            _animator = animator;
            _agent = agent;
        }

        public void Tick(float deltaTime, bool isCarrying)
        {
            if (_animator != null && _agent != null)
            {
                _animator.SetFloat(SpeedHash, _agent.velocity.magnitude);
                _animator.SetBool(CarryingHash, isCarrying);
            }
        }
    }
}
