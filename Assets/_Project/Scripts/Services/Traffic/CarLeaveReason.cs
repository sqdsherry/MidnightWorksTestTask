namespace AutoService.Services.Traffic
{
    /// <summary>Why a car left the location.</summary>
    public enum CarLeaveReason
    {
        /// <summary>The car was served and drove away.</summary>
        Served = 0,

        /// <summary>The car ran out of patience (module 14).</summary>
        Angry = 1,
    }
}
