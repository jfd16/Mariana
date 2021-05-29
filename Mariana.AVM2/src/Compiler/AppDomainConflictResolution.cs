namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Specifies the action to be taken when an ABC file contains a global class or trait having
    /// the same name as one that already exists in the application domain into which the script
    /// is being loaded, or one of its ancestors.
    /// </summary>
    /// <seealso cref="ScriptCompileOptions.appDomainConflictResolution"/>
    public enum AppDomainConflictResolution : byte {

        /// <summary>
        /// If a conflicting definition exists in the application domain (declared or inherited), do not add
        /// the definition from the script so that the existing definition remains visible. This is the
        /// default behaviour.
        /// </summary>
        USE_PARENT,

        /// <summary>
        /// If a conflicting definition exists in the application domain, and it is not inherited, do not
        /// add the definition from the script and the existing definition is visible. If the conflicting
        /// definition is inherited, add the definition from the script into its application domain, which
        /// will hide the inherited definition.
        /// </summary>
        USE_CHILD,

        /// <summary>
        /// A global name conflict is always a fatal error which aborts the compilation.
        /// </summary>
        FAIL,

    }

}
