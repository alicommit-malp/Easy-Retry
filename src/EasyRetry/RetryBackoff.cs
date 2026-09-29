namespace EasyRetry
{
    /// <summary>
    /// Strategy used to grow <see cref="RetryOptions.DelayBetweenRetries"/> across attempts.
    /// </summary>
    public enum RetryBackoff
    {
        /// <summary>
        /// Every retry waits the same base delay.
        /// </summary>
        Constant,

        /// <summary>
        /// Retry <c>n</c> waits <c>n</c> times the base delay.
        /// </summary>
        Linear,

        /// <summary>
        /// Retry <c>n</c> waits <c>2^(n-1)</c> times the base delay.
        /// </summary>
        Exponential
    }
}
