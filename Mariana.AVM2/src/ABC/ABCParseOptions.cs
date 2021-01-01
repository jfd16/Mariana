using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// A set of flags that can be used to control parsing of ABC bytecode by <see cref="ABCParser"/>.
    /// </summary>
    [Flags]
    public enum ABCParseOptions {

        /// <summary>
        /// If this is set, invalid UTF-8 strings in an ABC file use fallback substitution
        /// instead of an error being thrown. Note that this may result in name conflict errors
        /// when the parsed ABC file is compiled, if two different UTF-8 strings decode to the
        /// same string after fallback replacement.
        /// </summary>
        NO_FAIL_INVALID_UTF8 = 1,

    }

}
