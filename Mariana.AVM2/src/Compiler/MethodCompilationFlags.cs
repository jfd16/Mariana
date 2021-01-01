using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    [Flags]
    internal enum MethodCompilationFlags {

        /// <summary>
        /// The method sets the default XML namespace.
        /// </summary>
        USES_DXNS = 1,

        /// <summary>
        /// The method is a script initializer.
        /// </summary>
        IS_SCRIPT_INIT = 4,

        /// <summary>
        /// The method is a class static initializer.
        /// </summary>
        IS_STATIC_INIT = 8,

        /// <summary>
        /// The method is a class instance method or constructor.
        /// </summary>
        IS_INSTANCE_METHOD = 16,

        /// <summary>
        /// The method is a scoped function (created using a newfunction instruction).
        /// </summary>
        IS_SCOPED_FUNCTION = 32,

        /// <summary>
        /// The method returns a value.
        /// </summary>
        HAS_RETURN_VALUE = 64,

        /// <summary>
        /// The method declares a "rest" parameter.
        /// </summary>
        HAS_REST_PARAM = 128,

        /// <summary>
        /// The method requires an actual Array for the "rest" argument, instead of accessing
        /// the <see cref="RestParam"/> directly.
        /// </summary>
        HAS_REST_ARRAY = 256,

        /// <summary>
        /// The method requires a runtime scope stack.
        /// </summary>
        HAS_RUNTIME_SCOPE_STACK = 512,

    }

}
