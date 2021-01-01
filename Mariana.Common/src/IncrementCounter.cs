using System.Threading;

namespace Mariana.Common {

    /// <summary>
    /// An incrementing counter for generating unique identifiers.
    /// </summary>
    public sealed class IncrementCounter {

        private int m_value;

        /// <summary>
        /// Creates a new <see cref="IncrementCounter"/> instance.
        /// </summary>
        /// <param name="initialValue">The initial value of the counter.</param>
        public IncrementCounter(int initialValue = 0) {
            m_value = initialValue;
        }

        /// <summary>
        /// Returns the current value of the counter without incrementing.
        /// </summary>
        /// <returns>The current value of the counter.</returns>
        public int current => m_value;

        /// <summary>
        /// Returns the current value of the counter and increments it.
        /// </summary>
        /// <returns>The current value of the counter.</returns>
        public int next() => m_value++;

        /// <summary>
        /// Returns the current value of the counter and increments it. The increment is done
        /// using interlocked operations for thread safety.
        /// </summary>
        /// <returns>The current value of the counter.</returns>
        public int atomicNext() => Interlocked.Increment(ref m_value) - 1;

    }

}
