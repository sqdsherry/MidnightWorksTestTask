using System;
using AutoService.Presentation.Characters;
using AutoService.Services.Core;
using AutoService.Services.Supplies;
using AutoService.Presentation.Player;

namespace AutoService.Presentation.Player
{
    public sealed class PlayerAnimatorPresenter : ITickable, IDisposable
    {
        private readonly CharacterAnimator _animator;
        private readonly IPlayerCarry _carry;

        public PlayerAnimatorPresenter(CharacterAnimator animator, IPlayerCarry carry)
        {
            _animator = animator;
            _carry = carry;
        }

        public void Tick(float deltaTime)
        {
            if (_animator != null)
            {
                _animator.Tick(deltaTime, _carry != null && _carry.HasBox);
            }
        }

        public void Dispose()
        {
        }
    }
}
