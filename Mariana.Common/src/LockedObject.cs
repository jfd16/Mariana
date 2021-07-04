using System;
using System.Threading;

namespace Mariana.Common {

    /// <summary>
    /// Provides thread-safe access to an object by guarding it with a lock.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public readonly struct LockedObject<T> : IDisposable where T : class {

        private readonly T m_value;
        private readonly object? m_lock;

        /// <summary>
        /// Creates a new <see cref="LockedObject{T}"/> instance.
        /// </summary>
        /// <param name="value">The object to be guarded by the lock.</param>
        /// <param name="lockObj">The object on which to take a lock to guard access to
        /// <paramref name="value"/>. If this is null, no lock is taken.</param>
        public LockedObject(T value, object? lockObj) {
            m_value = value;
            m_lock = lockObj;

            if (m_lock != null) {
                bool lockTaken = false;
                Monitor.Enter(m_lock, ref lockTaken);

                if (!lockTaken)
                    m_lock = null;
            }
        }

        /// <summary>
        /// Returns the object being guarded by the lock.
        /// </summary>
        public T value => m_value;

        /// <summary>
        /// Releases the lock held by this instance.
        /// </summary>
        public void Dispose() {
            if (m_lock != null)
                Monitor.Exit(m_lock);
        }

    }

}
