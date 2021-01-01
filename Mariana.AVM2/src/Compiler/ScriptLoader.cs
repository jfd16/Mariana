using System;
using System.IO;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// A <see cref="ScriptLoader"/> instance is used for loading and compiling ActionScript
    /// bytecode. This class is the interface to the ActionScript bytecode compiler.
    /// </summary>
    public sealed class ScriptLoader {

        private ScriptCompileOptions m_compileOptions;
        private ScriptCompileContext m_compileContext;
        private bool m_compilationFinished;

        internal ScriptLoader(ApplicationDomain appDomain, ScriptCompileOptions compileOptions) {
            m_compileOptions = compileOptions.clone();
            m_compileContext = new ScriptCompileContext(appDomain, m_compileOptions);
        }

        /// <summary>
        /// Loads and compiles an ActionScript 3 bytecode file from a file.
        /// </summary>
        /// <param name="filename">The name of the file to load.</param>
        public void compileFile(string filename) =>
            compile(ABCFile.readFromFile(filename, m_compileOptions.parserOptions));

        /// <summary>
        /// Compiles an ActionScript 3 bytecode file from a byte array.
        /// </summary>
        /// <param name="data">The byte array containing the ActionScript 3 bytecode file.</param>
        public void compile(byte[] data) =>
            compile(ABCFile.read(data, m_compileOptions.parserOptions));

        /// <summary>
        /// Compiles an ActionScript 3 bytecode file from a stream.
        /// </summary>
        /// <param name="stream">The stream from which to read the ActionScript 3 bytecode file.</param>
        /// <remarks>
        /// <para>This method does not dispose <paramref name="stream"/> after the ABC file has been read.</para>
        /// </remarks>
        public void compile(Stream stream) =>
            compile(ABCFile.read(stream, m_compileOptions.parserOptions));

        /// <summary>
        /// Loads an ActionScript 3 bytecode file.
        /// </summary>
        /// <param name="abcFile">The <see cref="ABCFile"/> instance representing the ABC file
        /// to be loaded.</param>
        public void compile(ABCFile abcFile) {
            if (abcFile == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(abcFile));
            if (m_compilationFinished)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_SCRIPT_LOADER_FINISHED);

            m_compileContext.compileFile(abcFile);
        }

        /// <summary>
        /// Finishes the compilation and runs the compiled scripts.
        /// </summary>
        public void runCompiledScripts() {
            m_compileContext.finishCompilationAndLoad();
            var methodsToRun = m_compileContext.getAllScriptInitMethods();

            m_compileContext = null;
            m_compilationFinished = true;

            for (int i = 0; i < methodsToRun.length; i++)
                methodsToRun[i].invoke(default(ASAny), default(ReadOnlySpan<ASAny>));
        }

    }

}
