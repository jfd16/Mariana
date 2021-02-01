using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A trait object representing a constant value.
    /// </summary>
    ///
    /// <remarks>
    /// Constant traits always have a fixed value (which can be null, undefined, Boolean, String,
    /// int, uint, Number or Namespace) and are always static. Their values are directly
    /// substituted into code generated by the IL compiler, unlike static fields which are looked
    /// up at runtime.
    /// </remarks>
    public class ConstantTrait : Trait {

        private ASAny m_val;

        /// <summary>
        /// The value of the constant trait.
        /// </summary>
        public ASAny constantValue => m_val;

        /// <inheritdoc/>
        public override TraitType traitType => TraitType.CONSTANT;

        internal ConstantTrait(in QName name, Class declaringClass, ApplicationDomain appDomain, ASAny value)
            : base(name, declaringClass, appDomain, true)
        {
            m_val = value;
        }

        /// <summary>
        /// Gets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to get the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value of the trait on the specified object.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus tryGetValue(ASAny target, out ASAny value) {
            value = constantValue;
            return BindStatus.SUCCESS;
        }

        /// <summary>
        /// Sets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to set the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        ///
        /// <remarks>
        /// The implementation of this method for constant traits simply returns
        /// <see cref="BindStatus.FAILED_READONLY" qualifyHint="true"/>, as constant traits cannot
        /// be modified.
        /// </remarks>
        public override BindStatus trySetValue(ASAny target, ASAny value) => BindStatus.FAILED_READONLY;

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait.</param>
        /// <param name="receiver">The receiver of the function call.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="result">The return value of the function call.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        ///
        /// <remarks>
        /// The implementation of this method for constant traits simply returns
        /// <see cref="BindStatus.FAILED_NOTFUNCTION" qualifyHint="true"/>, as constant traits
        /// cannot be called as functions.
        /// </remarks>
        public override BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            result = default(ASAny);
            return BindStatus.FAILED_NOTFUNCTION;
        }

        /// <summary>
        /// Invokes the trait as a constructor on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait as a
        /// constructor.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        ///
        /// <remarks>
        /// The implementation of this method for constant traits simply returns
        /// <see cref="BindStatus.FAILED_NOTCONSTRUCTOR" qualifyHint="true"/> as constant traits
        /// cannot be called as constructors.
        /// </remarks>
        public override BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result) {
            result = default(ASAny);
            return BindStatus.FAILED_NOTCONSTRUCTOR;
        }

        /// <summary>
        /// Returns a string representation of the currend <see cref="ConstantTrait"/>.
        /// </summary>
        /// <returns>A string representation of the currend <see cref="ConstantTrait"/>.</returns>
        public override string ToString() {
            var sb = new System.Text.StringBuilder();
            if (isStatic)
                sb.Append("static ");
            sb.Append("const ");
            sb.Append(name.ToString());

            if (constantValue.isUndefined) {
                sb.Append(": * = undefined");
            }
            else if (constantValue.isNull) {
                sb.Append(": Object = null");
            }
            else {
                Class valType = constantValue.AS_class;
                sb.Append(':').Append(' ').Append(valType.name.ToString()).Append(" = ");
                if (valType.tag == ClassTag.STRING) {
                    string strval = constantValue.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
                    sb.Append('"');
                    sb.Append(strval);
                    sb.Append('"');
                }
                else {
                    sb.Append(constantValue.ToString());
                }
            }

            return sb.ToString();
        }

    }

}
