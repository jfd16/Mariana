using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Error class is the base class for errors thrown in ActionScript 3.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The throw statement in ActionScript 3 does not require its argument to be an
    /// instance of the Error class. Any value may be thrown as an exception, though it is
    /// considered good practice to throw only instances of Error or its subclasses.
    /// </para>
    /// <para>
    /// To throw an exception from .NET code, the Error object (or any other value that is to be
    /// thrown) must be wrapped in an <see cref="AVM2Exception"/> instance, and that exception must
    /// be thrown. The <see cref="AVM2Exception.create" qualifyHint="true"/> method can be used to
    /// create instance of an Error subclass that has a public constructor taking a message and a
    /// code argument, and wrap it in an <see cref="AVM2Exception"/> instance.
    /// </para>
    /// </remarks>
    [AVM2ExportClass(name = "Error", isDynamic = true, hasPrototypeMethods = true)]
    public class ASError : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 Error class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// The error code.
        /// </summary>
        private int m_errorID;

        /// <summary>
        /// The error class name.
        /// </summary>
        [AVM2ExportTrait]
        public string name;

        /// <summary>
        /// The error message.
        /// </summary>
        [AVM2ExportTrait]
        public string message;

        /// <summary>
        /// The <see cref="StackTrace"/> representing the call stack at the time of the error's construction.
        /// </summary>
        private StackTrace m_stackTrace;

        /// <summary>
        /// A string providing information about the state of the call stack at the time the error was
        /// constructed. This is lazily initialized when <see cref="getStackTrace"/> is called.
        /// </summary>
        private LazyInitObject<string> m_lazyStackTraceString;

        /// <summary>
        /// Creates a new <see cref="ASError"/> object.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error ID.</param>
        [AVM2ExportTrait]
        public ASError(string message = "", int id = 0) {
            this.m_errorID = id;
            this.message = message;
            this.name = "Error";
            this.m_stackTrace = new StackTrace(1, false);
            this.m_lazyStackTraceString = new LazyInitObject<string>(_initStackTrace);
        }

        /// <summary>
        /// Gets the error code of the Error instance.
        /// </summary>
        [AVM2ExportTrait]
        public virtual int errorID => m_errorID;

        /// <summary>
        /// Gets the string representation of the state of the call stack at the point where the error
        /// was thrown.
        /// </summary>
        /// <returns>The string representation of the state of the call stack at the point where the
        /// error was thrown.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual string getStackTrace() => m_lazyStackTraceString.value;

        /// <summary>
        /// Function for lazy initialization of the stacktrace.
        /// </summary>
        private string _initStackTrace() {
            StackFrame[] frames = m_stackTrace.GetFrames();

            var sb = new StringBuilder();
            sb.Append(AS_convertString(this));

            void sbAppendName(in QName qname) {
                if (qname.ns.kind == NamespaceKind.NAMESPACE && !qname.ns.isPublic)
                    sb.Append(qname.ns.uri).Append("::");

                sb.Append(qname.localName);
            }

            for (int i = 0; i < frames.Length; i++) {
                MethodBase method = frames[i].GetMethod();
                var klass = Class.fromType(method.DeclaringType);

                if (klass == null) {
                    var domain = ApplicationDomain.getDomainFromMember(method);
                    if (domain == null)
                        continue;

                    Trait trait = domain.getGlobalTraitByFilter(t => t is MethodTrait m && m.underlyingMethodInfo == method);
                    if (trait == null)
                        continue;

                    sb.Append("\n    at global/");
                    sbAppendName(trait.name);
                    sb.Append("()");
                }
                else if (method is ConstructorInfo ctor) {
                    if ((object)klass.constructor.underlyingConstructorInfo != ctor)
                        continue;

                    sb.Append("\n    at ");
                    sbAppendName(klass.name);
                    sb.Append("()");
                }
                else {
                    Trait trait = klass.getTraitByFilter(t => t is MethodTrait m && m.underlyingMethodInfo == method);
                    if (trait == null)
                        continue;

                    sb.Append("\n    at ");
                    sbAppendName(klass.name);
                    sb.Append('/');
                    sbAppendName(trait.name);
                    sb.Append("()");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the string representation of the object.
        /// </summary>
        /// <returns>The string representation of the object.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        //[AVM2ExportTrait(name = "toString", nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public virtual new string AS_toString() {
            string name = this.name ?? "null";
            string message = this.message ?? "null";
            return (message.Length == 0) ? name : name + ": " + message;
        }

    }

}
