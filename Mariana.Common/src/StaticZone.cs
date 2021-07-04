using System;
using System.Collections.Concurrent;
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

        private static int s_nextZoneId = NON_DEFAULT_ZONE_ID_BEGIN;
        private static readonly Stack<int> s_availableIds = new Stack<int>();

        private static ConcurrentBag<IZoneStaticData>[] s_registeredVarsByZoneId = Array.Empty<ConcurrentBag<IZoneStaticData>>();

        /// <summary>
        /// The zone ID of the active zone for the thread.
        /// </summary>
        [ThreadStatic]
        private static int s_currentZoneId;

        /// <summary>
        /// An identifier for this zone. This must be a value other than <see cref="DEFAULT_ZONE_ID"/>
        /// </summary>
        private int m_id;

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

                    int collectionsIndex = m_id - NON_DEFAULT_ZONE_ID_BEGIN;
                    ConcurrentBag<IZoneStaticData>[] variableCollections =
                        DataStructureUtil.volatileEnsureArraySize(ref s_registeredVarsByZoneId!, collectionsIndex + 1);

                    variableCollections[collectionsIndex] = new ConcurrentBag<IZoneStaticData>();
                }
            }
        }

        /// <summary>
        /// Returns the zone ID of the current zone.
        /// </summary>
        internal static int currentZoneId => s_currentZoneId;

        /// <summary>
        /// Executes the given function in this <see cref="StaticZone"/>. All
        /// reads and writes to zone-local variables within the function will operate
        /// on the values that are associated with the zone.
        /// </summary>
        /// <param name="action">The function to execute in this zone.</param>
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

            _enterZoneWithIdAndRun(m_id, action);
        }

        /// <summary>
        /// Executes the given function in this <see cref="StaticZone"/>. All
        /// reads and writes to zone-local variables within the function will operate
        /// on the values that are associated with the zone.
        /// </summary>
        /// <param name="state">The state argument that will be passed into the call to
        /// <paramref name="action"/>.</param>
        /// <param name="action">The function to execute in this zone.</param>
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

            _enterZoneWithIdAndRun(m_id, action, state);
        }

        /// <summary>
        /// Executes the given function outside of any static zone. All
        /// reads and writes to zone-local variables within the function will operate
        /// on the non-zone shared values of the variables.
        /// </summary>
        /// <param name="action">The function to be called outside of any static zone.</param>
        ///
        /// <remarks>If this method is called when not executing in a static zone (that is, not
        /// from a function passed to <see cref="enterAndRun"/> or <see cref="enterAndRun{TState}"/>),
        /// it has the same effect as a direct call to <paramref name="action"/>.</remarks>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
        public static void runOutsideCurrentZone(Action action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _enterZoneWithIdAndRun(DEFAULT_ZONE_ID, action);
        }

        /// <summary>
        /// Executes the given function outside of any static zone. All
        /// reads and writes to zone-local variables within the function will operate
        /// on the non-zone shared values of the variables.
        /// </summary>
        /// <param name="state">The state argument that will be passed into the call to
        /// <paramref name="action"/>.</param>
        /// <param name="action">The function to be called outside of any static zone.</param>
        ///
        /// <typeparam name="TState">The type of the state parameter of the function.</typeparam>
        ///
        /// <remarks>If this method is called when not executing in a static zone (that is, not
        /// from a function passed to <see cref="enterAndRun"/> or <see cref="enterAndRun{TState}"/>),
        /// it has the same effect as a direct call to <paramref name="action"/>.</remarks>
        public static void runOutsideCurrentZone<TState>(TState state, Action<TState> action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            _enterZoneWithIdAndRun(DEFAULT_ZONE_ID, action, state);
        }

        /// <summary>
        /// Calls the given function in the zone with the given ID.
        /// </summary>
        /// <param name="zoneId">The zone identifier, or <see cref="DEFAULT_ZONE_ID"/> to execute
        /// the function in a non-zone context.</param>
        /// <param name="action">The function to be called.</param>
        private static void _enterZoneWithIdAndRun(int zoneId, Action action) {
            ref int zoneIdRef = ref s_currentZoneId;
            int prevZoneId = zoneIdRef;

            try {
                zoneIdRef = zoneId;
                action();
            }
            finally {
                zoneIdRef = prevZoneId;
            }
        }

        /// <summary>
        /// Calls the given function in the zone with the given ID.
        /// </summary>
        /// <param name="zoneId">The zone identifier, or <see cref="DEFAULT_ZONE_ID"/> to execute
        /// the function in a non-zone context.</param>
        /// <param name="action">The function to be called.</param>
        /// <param name="state">The state argument to pass to the call to <paramref name="action"/>.</param>
        /// <typeparam name="TState">The type of the state parameter of the callback function.</typeparam>
        private static void _enterZoneWithIdAndRun<TState>(int zoneId, Action<TState> action, TState state) {
            ref int zoneIdRef = ref s_currentZoneId;
            int prevZoneId = zoneIdRef;

            try {
                zoneIdRef = zoneId;
                action(state);
            }
            finally {
                zoneIdRef = prevZoneId;
            }
        }

        /// <summary>
        /// Registers a zone-local variable in a zone. This ensures that the variable's
        /// value is disposed when the zone is disposed.
        /// </summary>
        /// <param name="zoneId">The ID of the zone for which to register the variable</param>
        /// <param name="variable">The zone-local variable to register in the zone.</param>
        internal static void registerVariable(int zoneId, IZoneStaticData variable) {
            ConcurrentBag<IZoneStaticData> variableCollection =
                Volatile.Read(ref s_registeredVarsByZoneId)[zoneId - NON_DEFAULT_ZONE_ID_BEGIN];

            variableCollection.Add(variable);
        }

        /// <summary>
        /// Disposes this zone. This makes the zone no longer usable and cleans up any zone-static
        /// variable values in this zone that have attached disposers.
        /// </summary>
        ///
        /// <exception cref="InvalidOperationException">An attempt is made to dispose the
        /// active zone of the current thread (that is, this method is called from a function
        /// passed to <see cref="enterAndRun"/> or <see cref="enterAndRun{TState}"/>.</exception>
        public void Dispose() {
            if (m_id == DISPOSED_ZONE_ID)
                return;

            if (m_id == s_currentZoneId)
                throw new InvalidOperationException("Cannot dispose the active zone.");

            lock (s_createDisposeLock) {
                ConcurrentBag<IZoneStaticData> variableCollection =
                    Volatile.Read(ref s_registeredVarsByZoneId)[m_id - NON_DEFAULT_ZONE_ID_BEGIN];

                while (variableCollection.TryTake(out IZoneStaticData varToDispose))
                    varToDispose.onZoneDisposed(m_id);

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
    /// <typeparam name="T">The type of the variable. This muse be a reference type.</typeparam>
    ///
    /// <remarks>
    /// A zone-static variable has a value associated with each <see cref="StaticZone"/>,
    /// which is accessed when executing in that zone with <see cref="StaticZone.enterAndRun"/>.
    /// In addition, each zone-static variable that is of a type <typeparamref name="T"/> that
    /// does not implement <see cref="IDisposable"/> and that does not have an attached disposer
    /// has a non-zone value, which is used when not executing in a static zone and is globally
    /// shared.
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
        /// Gets or sets the value of this zone-local variable in the currently executing
        /// <see cref="StaticZone"/>, or the non-zone shared value if not executing
        /// in a static zone.
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
        /// provided when constructing this instance) and this property is accessed outside
        /// of a function passed to and called by <see cref="StaticZone.enterAndRun"/> or
        /// <see cref="StaticZone.enterAndRun{TState}"/></description></item>
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
                        throw new InvalidOperationException("Cannot access a disposable ZoneStaticData outside of a zone.");

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
                T? currentValue = null;

                // We need to check if the value is already initialized before setting it
                // because it will have to be registered in the zone if it is being set for
                // the first time.
                T?[] values = Volatile.Read(ref m_zoneValues);

                if ((uint)zoneId < (uint)values.Length)
                    currentValue = Volatile.Read(ref values[zoneId]);

                if (currentValue == value)
                    return;

                if (currentValue != null) {
                    // Already initialized, no need to register.
                    m_disposer?.Invoke(currentValue);
                    Volatile.Write(ref values[zoneId], value);
                    return;
                }

                lock (m_lazyInitLock) {
                    if (zoneId == StaticZone.DEFAULT_ZONE_ID && m_disposer != null)
                        throw new InvalidOperationException("Cannot access a disposable ZoneStaticData outside of a zone.");

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
