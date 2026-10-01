namespace AutoService.Presentation.Player
{
    /// <summary>States of the character's movement FSM, driven by <see cref="PlayerMotor"/>.</summary>
    public enum PlayerMotionState
    {
        /// <summary>Standing still, no target.</summary>
        Idle = 0,

        /// <summary>Walking to a point on the ground.</summary>
        MovingToPoint = 1,

        /// <summary>Walking to an interactable's approach position.</summary>
        MovingToTarget = 2,

        /// <summary>Arrived; turning to the interactable's approach rotation.</summary>
        Turning = 3,

        /// <summary>Interacting with the target until the next command.</summary>
        Interacting = 4,
    }
}
