using System;
using System.Reflection;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Specifies the configuration for the ActionScript bytecode compiler.
    /// </summary>
    public sealed class ScriptCompileOptions {

        private ABCParseOptions m_abcParseOptions;

        private bool m_hideParentDomainDefinitions;

        private bool m_failOnGlobalNameConflict;

        private bool m_emitPropertyDefs;

        private bool m_emitParamNames;

        private bool m_earlyThrowMethodBodyErrors;

        private bool m_enableTracing;

        private bool m_useNativeDoubleToIntConversions;

        private IntegerArithmeticMode m_integerArithmeticMode;

        private string m_emitAssemblyName;

        private int m_numParallelCompileThreads;

        private Func<byte[], Assembly> m_assemblyLoader;

        /// <summary>
        /// Specifies the ABC parser options used by the compiler, as a set of
        /// flags from the <see cref="ABCParseOptions"/> enumeration.
        /// </summary>
        /// <remarks>
        /// The default value of this property is 0 (no flags set).
        /// </remarks>
        public ABCParseOptions parserOptions {
            get => m_abcParseOptions;
            set => m_abcParseOptions = value;
        }

        /// <summary>
        /// Specifies the situations in which the compiler should use integer arithmetic
        /// for certain floating-point arithmetic instructions where the operands are integers.
        /// </summary>
        ///
        /// <remarks>The default value is <see cref="IntegerArithmeticMode.DEFAULT"/>.</remarks>
        ///
        /// <exception cref="AVM2Exception">
        /// ArgumentError #10061: The value being set to this property is not a valid value of the
        /// <see cref="IntegerArithmeticMode"/> enumeration.
        /// </exception>
        public IntegerArithmeticMode integerArithmeticMode {
            get => m_integerArithmeticMode;
            set {
                if (value > IntegerArithmeticMode.AGGRESSIVE)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(value));
                m_integerArithmeticMode = value;
            }
        }

        /// <summary>
        /// If this is set to true, trait definitions from loaded ABC code are allowed to hide
        /// definitions with the same name in an ancestor of the application domain into
        /// which the code is loaded.
        /// </summary>
        /// <remarks>
        /// <para>If hiding is disabled, and a script contains a definition whose name is the
        /// same as one that is defined in an ancestor application domain, the definition from
        /// the script is ignored and the one from the ancestor definition will be used, or
        /// an exception is thrown (depending on the value of <see cref="failOnGlobalNameConflict"/>).</para>
        /// <para>The default value is false.</para>
        /// </remarks>
        public bool hideParentDomainDefinitions {
            get => m_hideParentDomainDefinitions;
            set => m_hideParentDomainDefinitions = value;
        }

        /// <summary>
        /// If this is set to true, an exception is thrown when a script attempts to define a
        /// global trait with the same name as an existing one in the application domain in
        /// which the script is being loaded.
        /// </summary>
        /// <remarks>
        /// Enabling this option will result in a script failing to compile if it defines a global
        /// trait with the same name as one that already exists in the application domain in which
        /// the script is being loaded, or if <see cref="hideParentDomainDefinitions"/> is false,
        /// in any of the application domain's ancestors. This includes the case when a script defines
        /// two or more global traits with the same name. With this option set to false, if a script
        /// defines a global trait having the same name as an existing one, that trait will be ignored
        /// and the trait that already exists will be used.
        /// </remarks>
        public bool failOnGlobalNameConflict {
            get => m_failOnGlobalNameConflict;
            set => m_failOnGlobalNameConflict = value;
        }

        /// <summary>
        /// Set this to true to emit property definitions in the generated assembly. If this is false,
        /// only the accessor methods of properties are emitted.
        /// </summary>
        /// <remarks>
        /// The default value is false.
        /// </remarks>
        public bool emitPropertyDefinitions {
            get => m_emitPropertyDefs;
            set => m_emitPropertyDefs = value;
        }

        /// <summary>
        /// Set this to true to emit method parameter names in the generated assembly, if they
        /// are available in the ABC file method definitions.
        /// </summary>
        /// <remarks>
        /// The default value is false.
        /// </remarks>
        public bool emitParamNames {
            get => m_emitParamNames;
            set => m_emitParamNames = value;
        }

        /// <summary>
        /// The name of the assembly containing the compiled code. If this is null (the default value), a name is
        /// generated by the compiler.
        /// </summary>
        public string emitAssemblyName {
            get => m_emitAssemblyName;
            set => m_emitAssemblyName = value;
        }

        /// <summary>
        /// Set this to true to throw immediately when any error is found during compilation of
        /// a method body. Otherwise, the error will be thrown when the method is first called.
        /// The default value is false.
        /// </summary>
        public bool earlyThrowMethodBodyErrors {
            get => m_earlyThrowMethodBodyErrors;
            set => m_earlyThrowMethodBodyErrors = value;
        }

        /// <summary>
        /// Set to true to output a trace of the compilation state for each method compiled,
        /// which can aid in diagnosing possible compilation or IL generation issues. This
        /// requires a debug build (with the <c>DEBUG</c> symbol defined), and is ignored in
        /// release builds.
        /// </summary>
        /// <remarks>
        /// The default value is false.
        /// </remarks>
        public bool enableTracing {
            get => m_enableTracing;
            set => m_enableTracing = value;
        }

        /// <summary>
        /// Specifies the number of threads to use for parallel method compilation.
        /// </summary>
        ///
        /// <value>The number of threads to use for parallel method compilation. If this is 0 or
        /// 1, parallel compilation is disabled. The default value is 0.</value>
        ///
        /// <exception cref="AVM2Exception">
        /// ArgumentError #10061: The value being set to this property is a negative value.
        /// </exception>
        public int numParallelCompileThreads {
            get => m_numParallelCompileThreads;
            set {
                if (value < 0)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(value));
                m_numParallelCompileThreads = value;
            }
        }


        /// <summary>
        /// Set to true to use native conversions in compiled code when converting from the
        /// Number type to an integer type (int or uint), instead of using
        /// <see cref="ASNumber.AS_toInt" qualifyHint="true"/> and <see cref="ASNumber.AS_toUint" qualifyHint="true"/>
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        /// Using native conversions (the <c>conv.i4</c> and <c>conv.u4</c> instructions in IL) will usually result
        /// in a performance improvement as these conversions as the CLR's JIT compiler compiles them to hardware
        /// instructions on most architectures. However, they are not guaranteed to give the correct results (as
        /// per the ECMA-262 specification) for inputs outside the range of the integer type being converted to,
        /// so code that relies on ECMA-262 compliant conversions may break. The <see cref="ASNumber.AS_toInt" qualifyHint="true"/>
        /// and <see cref="ASNumber.AS_toUint" qualifyHint="true"/> conversion functions, on the other hand, are guaranteed
        /// to be ECMA-262 compliant for all inputs, but to provide these guarantees the conversions are implemented in
        /// software.
        /// </para>
        /// <para>
        /// The default value of this option is false.
        /// </para>
        /// </remarks>
        public bool useNativeDoubleToIntegerConversions {
            get => m_useNativeDoubleToIntConversions;
            set => m_useNativeDoubleToIntConversions = value;
        }

        /// <summary>
        /// Specifies a custom assembly loader that will be used to load compiled assemblies
        /// for execution.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The custom assembly loader is a delegate that takes a single argument, which
        /// is a byte array containing the PE file image for the assembly. It must return
        /// an <see cref="Assembly"/> instance representing the loaded assembly.
        /// </para>
        /// <para>If the value of this property is null (the default), or the assembly loader returns
        /// null when called, the assembly will be loaded using <see cref="Assembly.Load(Byte[])"/>.</para>
        /// <para>
        /// Custom assembly loaders can be used, for example, to load assemblies into an
        /// AssemblyLoadContext so that they can be unloaded later, or to save the generated
        /// assemblies to DLL files for inspection.
        /// </para>
        /// </remarks>
        public Func<byte[], Assembly> assemblyLoader {
            get => m_assemblyLoader;
            set => m_assemblyLoader = value;
        }

        internal ScriptCompileOptions clone() => (ScriptCompileOptions)MemberwiseClone();

    }

}
