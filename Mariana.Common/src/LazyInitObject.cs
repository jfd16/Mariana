using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Mariana.Common {

    /// <summary>
    /// The initialization state of a <see cref="LazyInitObject{T}"/>.
    /// </summary>
    public enum LazyInitObjectState : byte {

        /// <summary>
        /// The object initializer has not bet been called.
        /// </summary>
        UNINITIALIZED,

        /// <summary>
        /// The object's initializer is currently running.
        /// </summary>
        IN_INITIALIZER,

        /// <summary>
        /// The object's initializer has completed and the object is available.
        /// </summary>
        COMPLETE,

        /// <summary>
        /// An exception was thrown by the object's initializer.
        /// </summary>
        FAILED,

    }

    /// <summary>
    /// Specifies how accesses to the value of a <see cref="LazyInitObject{T}"/> from within its
    /// initializer must be handled.
    /// </summary>
    public enum LazyInitRecursionHandling : byte {

        /// <summary>
        /// Access to <see cref="LazyInitObject{T}.value" qualifyHint="true"/> from within the
        /// initializer will throw an exception.
        /// </summary>
        THROW,

        /// <summary>
        /// Access to <see cref="LazyInitObject{T}.value" qualifyHint="true"/> from within the
        /// initializer will result in the default value of the object's type being returned.
        /// </summary>
        RETURN_DEFAULT,

        /// <summary>
        /// Access to <see cref="LazyInitObject{T}.value" qualifyHint="true"/> from within the
        /// initializer will recursively call the initializer function. The initializer function is
        /// responsible for handling such calls.
        /// </summary>
        RECURSIVE_CALL,

    }

    /// <summary>
    /// A lazy initialized object.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    ///
    /// <remarks>
    /// <para>This struct provides thread-safe lazy initialization.</para>
    /// <para>
    /// The <see cref="LazyInitObject{T}"/> type is a value type intended for internal use
    /// within a class. It should not be returned from a function or passed by value to a
    /// function, as making copies of a <see cref="LazyInitObject{T}"/> may result in unintended
    /// behaviour, such as the initialization function being called more than once. (In almost all
    /// these cases, it is the object, i.e. the <see cref="value"/> property which must be used
    /// and not the <see cref="LazyInitObject{T}"/> instance itself).
    /// </para>
    /// <para>Do not use this type as the type of a readonly field, as defensive copying
    /// may result in more than one initialization.</para>
    /// </remarks>
    public struct LazyInitObject<T> {

        private T? m_value;
        private Func<T> m_initFunc;
        private readonly object m_initLock;
        private readonly LazyInitRecursionHandling m_recursionHandler;
        private volatile LazyInitObjectState m_state;

        /// <summary>
        /// Constructs a new instance of <see cref="LazyInitObject{T}"/>.
        /// </summary>
        ///
        /// <param name="initFunc">A delegate that must be called during initialization to obtain the
        /// object's value.</param>
        /// <param name="initLock">An object that will be used as a lock to ensure thread safe
        /// initialization. If this is null, a lock object is created internally and used.</param>
        /// <param name="recursionHandling">Specifies how accesses to the object within the
        /// initialization function are handled.</param>
        public LazyInitObject(
            Func<T> initFunc,
            object? initLock = null,
            LazyInitRecursionHandling recursionHandling = LazyInitRecursionHandling.THROW
        ) {
            if (initFunc == null)
                throw new ArgumentNullException(nameof(initFunc));

            if (recursionHandling > LazyInitRecursionHandling.RECURSIVE_CALL)
                throw new ArgumentOutOfRangeException(nameof(recursionHandling));

            if (initLock == null)
                initLock = new object();

            m_value = default(T);
            m_initFunc = initFunc;
            m_initLock = initLock;
            m_recursionHandler = recursionHandling;
            m_state = LazyInitObjectState.UNINITIALIZED;
        }

        /// <summary>
        /// Returns the current initialization state of the object.
        /// </summary>
        public readonly LazyInitObjectState currentState => m_state;

        /// <summary>
        /// Returns the object value, initializing it if it has not yet been initialized.
        /// </summary>
        ///
        /// <remarks>
        /// If the recursion handling policy of this <see cref="LazyInitObject{T}"/> instance is
        /// <see cref="LazyInitRecursionHandling.RETURN_DEFAULT"/>, this property will return
        /// null when accessed inside the initializer function when <typeparamref name="T"/>
        /// is a (possibly non-nullable) reference type.
        /// </remarks>
        ///
        /// <exception cref="InvalidOperationException">
        /// <list type="bullet">
        /// <item><description>This property is accessed on a default-initialized instance of type
        /// <see cref="LazyInitObject{T}"/></description></item>
        /// <item><description>This property is accessed within the initialization function of the object and
        /// recursion handling is set to
        /// <see cref="LazyInitRecursionHandling.THROW" qualifyHint="true"/></description></item>
        /// <item><description>This property is accessed after an exception was thrown by the initialization
        /// function.</description></item>
        /// </list>
        /// </exception>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T value {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (m_state == LazyInitObjectState.COMPLETE)
                    return m_value!;

                return _internalGetValue();
            }
        }

        private T _internalGetValue() {
            if (m_initLock == null)
                throw new InvalidOperationException("Cannot access the value of a default-initialized LazyInitObject.");

            lock (m_initLock) {
                LazyInitObjectState state = m_state;
                if (state == LazyInitObjectState.COMPLETE)
                    return m_value!;

                if (state == LazyInitObjectState.FAILED)
                    throw new InvalidOperationException("Cannot access lazy object after initialization failure.");

                if (state == LazyInitObjectState.IN_INITIALIZER) {
                    if (m_recursionHandler == LazyInitRecursionHandling.THROW)
                        throw new InvalidOperationException("Cannot access lazy object in initializer.");

                    if (m_recursionHandler == LazyInitRecursionHandling.RETURN_DEFAULT)
                        return default(T)!;
                }

                m_state = LazyInitObjectState.IN_INITIALIZER;
                T value;
                try {
                    value = m_initFunc();
                }
                catch {
                    m_state = LazyInitObjectState.FAILED;
                    m_initFunc = null!;    // This will never be called again
                    throw;
                }

                m_value = value;
                m_state = LazyInitObjectState.COMPLETE;
                m_initFunc = null!;    // This will never be called again

                return value;
            }
        }

    }

}
