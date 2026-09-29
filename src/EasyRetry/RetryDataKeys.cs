namespace EasyRetry
{
    /// <summary>
    /// Keys under which <see cref="EasyRetry"/> stores retry details in <see cref="System.Exception.Data"/>
    /// of the exception that finally propagates.
    /// </summary>
    public static class RetryDataKeys
    {
        /// <summary>
        /// Number of invocations made before giving up, stored as <see cref="int"/>.
        /// </summary>
        public const string Attempts = "EasyRetry.Attempts";

        /// <summary>
        /// Every exception thrown by the attempts in order, including the final one,
        /// stored as <see cref="System.Collections.Generic.IReadOnlyList{T}"/> of <see cref="System.Exception"/>.
        /// </summary>
        public const string Exceptions = "EasyRetry.Exceptions";
    }
}
