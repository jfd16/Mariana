namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Specifies whether script initializers are to be run after ABC files are compiled,
    /// when the <see cref="ScriptLoader.finishCompilation" qualifyHint="true"/> method is
    /// called.
    /// </summary>
    /// <seealso cref="ScriptCompileOptions.scriptInitializerRunMode"/>
    public enum ScriptInitializerRunMode : byte {

        /// <summary>
        /// Don't run any script initializers after compilation. Script initializers are
        /// always run lazily, when any classes or global variables from those scripts are
        /// accessed.
        /// </summary>
        DONT_RUN,

        /// <summary>
        /// When <see cref="ScriptLoader.finishCompilation" qualifyHint="true"/> is called,
        /// run the script initializers that are intended to be used as entry points (the
        /// last script_info entry of each ABC file).
        /// </summary>
        RUN_ENTRY_POINTS,

        /// <summary>
        /// Force the execution of all script initializers immediately after the ABC files
        /// have been compiled and the compiled assembly loaded.
        /// </summary>
        RUN_ALL,

    }

}
