using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Mariana.Common {

    /// <summary>
    /// A static zone that can be used to control the sharing of global state between
    /// threads through the use of zone-static variables.
    /// </summary>
    public sealed class StaticZone : IDisposable {

        /// <summary>
        /// The ID reserved for the default zone that is implied when not executing in an explicit zone.
        /// </summary>
        internal const int DEFAULT_ZONE_ID = 0;

        /// <summary>
        /// The first ID of an explicitly created static zone.
        /// </summary>
        internal const int NON_DEFAULT_ZONE_ID_BEGIN = 1;

        /// <summary>
        /// The ID assigned to disposed zones.
        /// </summary>
        internal const int DISPOSED_ZONE_ID = -1;

        private static object s_createDisposeLock = new object();

        private static readonly StaticZone s_defaultZone = new StaticZone(DEFAULT_ZONE_ID);

        private static int s_nextZoneId = NON_DEFAULT_ZONE_ID_BEGIN;

        private static readonly Stack<int> s_availableIds = new Stack<int>();

        private static List<IZoneStaticData>[] s_registeredVarsByZoneId = Array.Empty<List<IZoneStaticData>>();

        /// <summary>
        /// The current zone for this thread. Since thread static are initialized to null for
        /// new threads, the current zone is the default zone if this is null.
        /// </summary>
        [ThreadStatic]
        private static StaticZone? s_currentZone;

        /// <summary>
        /// The current zone ID for this thread.
        /// </summary>
        [ThreadStatic]
        private static int s_currentZoneId;

        /// <summary>
        /// An identifier for this zone used for indexing into zone-local data.
        /// </summary>
        private int m_id;

        /// <summary>
        /// The number of times this zone has been entered but not exited yet.
        /// </summary>
        private int m_enteredCount;

        /// <summary>
        /// Creates a new instance of <see cref="StaticZone"/>.
        /// </summary>
        public StaticZone() {
            lock (s_createDisposeLock) {
                if (s_availableIds.Count > 0) {
                    m_id = s_availableIds.Pop();
                }
                else {
                    m_id = s_nextZoneId;
                    s_nextZoneId++;
                }

                int collectionsIndex = m_id - NON_DEFAULT_ZONE_ID_BEGIN;
                List<IZoneStaticData>[] variableCollections =
                    DataStructureUtil.volatileEnsureArraySize(ref s_registeredVarsByZoneId!, collectionsIndex + 1);

                variableCollections[collectionsIndex] = new List<IZoneStaticData>();
            }
        }

        /// <summary>
        /// This constructor is used to create <see cref="StaticZone"/> instance for the default zone.
        /// </summary>
        private StaticZone(int id) => m_id = id;

        /// <summary>
        /// Gets the <see cref="StaticZone"/> instance representing the default zone.
        /// </summary>
        /// <remarks>
        /// The default zone is the zone that all new threads start in, and it cannot be disposed.
        /// </remarks>
        public static StaticZone defaultZone => s_defaultZone;

        /// <summary>
        /// Returns the zone ID of the current zone.
        /// </summary>
        internal static int currentZoneId => s_currentZoneId;

        /// <summary>
        /// Executes the given function in this <see cref="StaticZone"/>. All
        /// reads and writes to zone-local variables within the function will operate
        /// on the values that are associated with the zone.
        /// </summary>
        /// <param name="action">The function to be called in this zone.</param>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">This <see cref="StaticZone"/> instance has
        /// been disposed.</exception>
        ///
        /// <remarks>Any new threads started from <paramref name="action"/> will not inherit the
        /// active zone. To run a new thread in the same zone, the <see cref="enterAndRun"/>
        /// method must be called in the thread's entry point.</remarks>
        public void enterAndRun(Action action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (m_id == DISPOSED_ZONE_ID)
                throw new ObjectDisposedException(nameof(StaticZone));

            ref StaticZone? currentZoneRef = ref s_currentZone;
            ref int currentZoneIdRef = ref s_currentZoneId;

            StaticZone? prevZone = currentZoneRef;
            int prevZoneId = currentZoneIdRef;

            try {
                currentZoneRef = this;
                currentZoneIdRef = m_id;
                Interlocked.Increment(ref m_enteredCount);
                action();
            }
            finally {
                Interlocked.Decrement(ref m_enteredCount);
                currentZoneRef = prevZone;
                currentZoneIdRef = prevZoneId;
            }
        }

        /// <summary>
        /// Executes the given function in this <see cref="StaticZone"/>. All
        /// reads and writes to zone-local variables within the function will operate
        /// on the values that are associated with the zone.
        /// </summary>
        /// <param name="state">The state argument that will be passed into the call to
        /// <paramref name="action"/>.</param>
        /// <param name="action">The function to be called in this zone.</param>
        ///
        /// <typeparam name="TState">The type of the state parameter of the function.</typeparam>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">This <see cref="StaticZone"/> instance has
        /// been disposed.</exception>
        ///
        /// <remarks>Any new threads started from <paramref name="action"/> will not inherit the
        /// active zone. To run a new thread in the same zone, the <see cref="enterAndRun{TState}"/>
        /// method must be called in the thread's entry point.</remarks>
        public void enterAndRun<TState>(TState state, Action<TState> action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (m_id == DISPOSED_ZONE_ID)
                throw new ObjectDisposedException(nameof(StaticZone));

            ref StaticZone? currentZoneRef = ref s_currentZone;
            ref int currentZoneIdRef = ref s_currentZoneId;

            StaticZone? prevZone = currentZoneRef;
            int prevZoneId = currentZoneIdRef;

            try {
                currentZoneRef = this;
                currentZoneIdRef = m_id;
                Interlocked.Increment(ref m_enteredCount);
                action(state);
            }
            finally {
                Interlocked.Decrement(ref m_enteredCount);
                currentZoneRef = prevZone;
                currentZoneIdRef = prevZoneId;
            }
        }

        /// <summary>
        /// Returns a delegate that, when invoked, invokes the given delegate in the
        /// current <see cref="StaticZone"/> when this method is called. This can be used,
        /// for instance, to preserve the current zone across threads or await boundaries.
        /// </summary>
        ///
        /// <param name="action">The function to be called in the current zone.</param>
        ///
        /// <returns>A delegate that, when invoked, invokes <paramref name="action"/> in the
        /// current <see cref="StaticZone"/> when this method is called, irrespective of the
        /// zone from which the returned delegate is invoked.</returns>
        public static Action captureCurrent(Action action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            StaticZone capturedZone = s_currentZone ?? s_defaultZone;
            return () => capturedZone.enterAndRun(action);
        }

        /// <summary>
        /// Returns a delegate that, when invoked, invokes the given delegate in the
        /// current <see cref="StaticZone"/> when this method is called. This can be used,
        /// for instance, to preserve the current zone across threads or await boundaries.
        /// </summary>
        ///
        /// <param name="state">The state argument that will be passed into the call to
        /// <paramref name="action"/>.</param>
        /// <param name="action">The function to be called in the current zone.</param>
        /// <typeparam name="TState">The type of the state parameter of the function.</typeparam>
        ///
        /// <returns>A delegate that, when invoked, invokes <paramref name="action"/> in the
        /// current <see cref="StaticZone"/> when this method is called, irrespective of the
        /// zone from which the returned delegate is invoked.</returns>
        public static Action captureCurrent<TState>(TState state, Action<TState> action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            StaticZone capturedZone = s_currentZone ?? s_defaultZone;
            return () => capturedZone.enterAndRun(state, action);
        }

        /// <summary>
        /// Registers a zone-local variable in a zone. This ensures that the variable's
        /// value is disposed when the zone is disposed.
        /// </summary>
        /// <param name="zoneId">The ID of the zone for which to register the variable</param>
        /// <param name="variable">The zone-local variable to register in the zone.</param>
        internal static void registerVariable(int zoneId, IZoneStaticData variable) {
            List<IZoneStaticData> variableCollection =
                Volatile.Read(ref s_registeredVarsByZoneId)[zoneId - NON_DEFAULT_ZONE_ID_BEGIN];

            lock (variableCollection)
                variableCollection.Add(variable);
        }

        /// <summary>
        /// Disposes this zone. This makes the zone no longer usable and cleans up any zone-static
        /// variable values in this zone that have attached disposers.
        /// </summary>
        ///
        /// <exception cref="InvalidOperationException">An attempt is made to dispose the default
        /// zone, or a zone that is currently active (that is, a function passed to
        /// <see cref="enterAndRun"/> or <see cref="enterAndRun{TState}"/> has not yet finished
        /// executing).</exception>
        public void Dispose() {
            lock (s_createDisposeLock) {
                if (m_id == DISPOSED_ZONE_ID)
                    return;

                if (m_id == DEFAULT_ZONE_ID)
                    throw new InvalidOperationException("Cannot dispose the default zone.");

                if (m_enteredCount != 0)
                    throw new InvalidOperationException("Cannot dispose a currently active zone.");

                List<IZoneStaticData>[] variableCollections = Volatile.Read(ref s_registeredVarsByZoneId);
                List<IZoneStaticData> trackedVars = variableCollections[m_id - NON_DEFAULT_ZONE_ID_BEGIN];

                for (int i = 0; i < trackedVars.Count; i++)
                    trackedVars[i].onZoneDisposed(m_id);

                variableCollections[m_id - NON_DEFAULT_ZONE_ID_BEGIN] = null!;
                s_availableIds.Push(m_id);

                m_id = DISPOSED_ZONE_ID;
            }
        }

    }

    /// <summary>
    /// An interface implemented by <see cref="ZoneStaticData{T}"/> to enable disposal
    /// of zone-static variable values of disposed zones.
    /// </summary>
    interface IZoneStaticData {
        /// <summary>
        /// Called when a zone for which this <see cref="ZoneStaticData{T}"/> has an
        /// associated value is disposed.
        /// </summary>
        /// <param name="zoneId">The id of the zone being disposed.</param>
        void onZoneDisposed(int zoneId);
    }

    /// <summary>
    /// A variable that is local to a <see cref="StaticZone"/>.
    /// </summary>
    /// <typeparam name="T">The type of the variable. This must be a reference type.</typeparam>
    ///
    /// <remarks>
    /// A zone-static variable has a value associated with each <see cref="StaticZone"/>,
    /// which is accessed when executing in that zone with <see cref="StaticZone.enterAndRun"/>.
    /// </remarks>
    public sealed class ZoneStaticData<T> : IZoneStaticData where T : class {

        /// <summary>
        /// The default disposer for a zone-static variable of type <typeparamref name="T"/>.
        /// </summary>
        private static readonly Action<T>? s_defaultDisposer =
            typeof(IDisposable).IsAssignableFrom(typeof(T)) ? new Action<T>(x => ((IDisposable)x)?.Dispose()) : null;

        private readonly Func<T>? m_initializer;
        private readonly Action<T>? m_disposer;
        private readonly object m_lazyInitLock = new object();

        private T?[] m_zoneValues = Array.Empty<T?>();

        /// <summary>
        /// Creates a new instance of <see cref="ZoneStaticData{T}"/>.
        /// </summary>
        ///
        /// <param name="initializer">A function that provides the initial value of the zone local
        /// variable. It is called when accessing the value of the variable when executing in a
        /// <see cref="StaticZone"/> for which no value was previously assigned to the variable.</param>
        ///
        /// <param name="disposer">A function that is called when a zone that has a value for the
        /// zone-static variable is disposed. If this is null, a default disposer is used which
        /// calls <see cref="IDisposable.Dispose"/> if <typeparamref name="T"/> implements
        /// <see cref="IDisposable"/> and does nothing otherwise.</param>
        public ZoneStaticData(Func<T>? initializer = null, Action<T>? disposer = null) {
            m_initializer = initializer;
            m_disposer = disposer ?? s_defaultDisposer;
        }

        /// <summary>
        /// Gets or sets the value of this zone-local variable associated with the current
        /// <see cref="StaticZone"/>.
        /// </summary>
        ///
        /// <remarks>
        /// <para>If the getter of this property is called and the zone-static variable
        /// does not yet have a value for the current zone, the initializer is called
        /// and the value returned by it is set as the value for the current zone. An
        /// exception is thrown if the initializer returns null.</para>
        /// <para>If the setter of this property is called and the zone-static variable
        /// already has a value for the current zone (from the initializer or a previous
        /// assignment), and this <see cref="ZoneStaticData{T}"/> has a disposer, the disposer
        /// is called with the old value as its argument before the new value is assigned.</para>
        /// <para>The initializer of a <see cref="ZoneStaticData{T}"/> is guaranteed to be
        /// called at most once per zone, and this initialization is thread safe. However,
        /// writing to the zone-static variable using the setter is not guaranteed to be
        /// thread safe when a zone is shared by multiple threads.</para>
        /// </remarks>
        ///
        /// <exception cref="InvalidOperationException">
        /// <list type="bullet">
        /// <item><description>The initializer for this <see cref="ZoneStaticData{T}"/> instance is
        /// called and it returns null.</description></item>
        /// <item><description>This <see cref="ZoneStaticData{T}"/> does not have an initializer
        /// and an attempt is made to read the value of the variable without writing to it first.
        /// </description></item>
        /// <item><description>This is a disposable <see cref="ZoneStaticData{T}"/> (that is,
        /// <typeparamref name="T"/> implements <see cref="IDisposable"/> or a disposer was
        /// provided when constructing this instance) and this property is accessed from the
        /// default zone.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <exception cref="ArgumentNullException">This property is set to null.</exception>
        [DebuggerHidden]
        public T value {
            get {
                int zoneId = StaticZone.currentZoneId;
                T? currentValue = null;

                // Check if the value is initialized.
                T?[] values = Volatile.Read(ref m_zoneValues);

                if ((uint)zoneId < (uint)values.Length)
                    currentValue = Volatile.Read(ref values[zoneId]);

                if (currentValue != null) {
                    // Already initialized, no need to enter the lock.
                    return currentValue;
                }

                lock (m_lazyInitLock) {
                    if (zoneId == StaticZone.DEFAULT_ZONE_ID && m_disposer != null)
                        throw new InvalidOperationException("Cannot access a disposable ZoneStaticData from the default zone.");

                    // Resize the array if needed.
                    values = DataStructureUtil.volatileEnsureArraySize(ref m_zoneValues!, zoneId + 1);

                    // Check again inside the lock if the value has already been initialized.
                    ref T? currentValueRef = ref values[zoneId];
                    currentValue = Volatile.Read(ref currentValueRef);

                    if (currentValue != null)
                        return currentValue;

                    // Value not initialized, so initialize it with the value from the initializer.
                    if (m_initializer == null)
                        throw new InvalidOperationException("Cannot access the value of the ZoneStaticData because it is uninitialized and has no initializer.");

                    T initialValue = m_initializer();
                    if (initialValue == null)
                        throw new InvalidOperationException("Null returned by the initializer for a ZoneStaticData.");

                    Volatile.Write(ref currentValueRef, initialValue);

                    // Register the initialized variable to ensure that it gets disposed along with the zone.
                    if (zoneId != StaticZone.DEFAULT_ZONE_ID)
                        StaticZone.registerVariable(zoneId, this);

                    return initialValue;
                }
            }
            set {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                int zoneId = StaticZone.currentZoneId;
                T? currentValue;

                // We need to check if the value is already initialized before setting it
                // because it will have to be registered in the zone if it is being set for
                // the first time, and an existing value may have to be disposed.

                T?[] values = Volatile.Read(ref m_zoneValues);

                if ((uint)zoneId < (uint)values.Length) {
                    ref T? currentValueRef = ref values[zoneId];
                    currentValue = Volatile.Read(ref currentValueRef);

                    if (currentValue == value)
                        return;

                    if (currentValue != null) {
                        // Already initialized, no need to register.
                        m_disposer?.Invoke(currentValue);
                        Volatile.Write(ref currentValueRef, value);
                        return;
                    }
                }

                lock (m_lazyInitLock) {
                    if (zoneId == StaticZone.DEFAULT_ZONE_ID && m_disposer != null)
                        throw new InvalidOperationException("Cannot access a disposable ZoneStaticData from the default zone.");

                    // Resize the array if needed.
                    values = DataStructureUtil.volatileEnsureArraySize(ref m_zoneValues!, zoneId + 1);

                    // Check again for an initialized variable inside the lock.
                    ref T? currentValueRef = ref values[zoneId];
                    currentValue = Volatile.Read(ref currentValueRef);

                    if (currentValue == value)
                        return;

                    if (currentValue != null) {
                        // Call the disposer on the current value.
                        m_disposer?.Invoke(currentValue);
                    }
                    else {
                        // Register in the zone if this is an initialization (that is, there is no existing value)
                        if (zoneId != StaticZone.DEFAULT_ZONE_ID)
                            StaticZone.registerVariable(zoneId, this);
                    }

                    Volatile.Write(ref currentValueRef, value);
                }
            }
        }

        /// <summary>
        /// Called when a zone for which this <see cref="ZoneStaticData{T}"/> has an
        /// associated value is disposed.
        /// </summary>
        /// <param name="zoneId">The id of the zone being disposed.</param>
        void IZoneStaticData.onZoneDisposed(int zoneId) {
            T?[] values = Volatile.Read(ref m_zoneValues);
            T value = Volatile.Read(ref values[zoneId])!;

            m_disposer?.Invoke(value);
            Volatile.Write(ref values[zoneId], null);
        }

    }

}
