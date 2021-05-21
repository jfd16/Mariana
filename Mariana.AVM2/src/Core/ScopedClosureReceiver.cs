using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A wrapper for a receiver and scope argument to pass to a function call that takes a scope
    /// argument.
    /// </summary>
    /// <remarks>
    /// Scope arguments contain variables that have been captured by a function closure from the
    /// context in which it was created. Such methods require two hidden arguments (a receiver and
    /// a scope argument) to be passed before the call arguments.
    /// </remarks>
    [AVM2ExportClass(hiddenFromGlobal = true)]
    public sealed class ScopedClosureReceiver : ASObject {

        /// <summary>
        /// The receiver argument passed to the method.
        /// </summary>
        public readonly ASObject receiver;

        /// <summary>
        /// The scope argument passed to the method.
        /// </summary>
        public readonly object scope;

        /// <summary>
        /// Creates a new instance of <see cref="ScopedClosureReceiver"/>.
        /// </summary>
        /// <param name="receiver">The receiver argument passed to the method.</param>
        /// <param name="scope">The scope argument passed to the method.</param>
        public ScopedClosureReceiver(ASObject receiver, object scope) {
            this.receiver = receiver;
            this.scope = scope;
        }

    }
}
