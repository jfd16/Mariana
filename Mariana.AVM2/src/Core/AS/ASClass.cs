using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Class class is used to represent a class in ActionScript 3. It is an object wrapper
    /// for AVM2 <see cref="Class"/> objects.
    /// </summary>
    [AVM2ExportClass(name = "Class", isDynamic = true)]
    public sealed class ASClass : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 Class class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// The <see cref="Class"/> instance wrapped by this object.
        /// </summary>
        private readonly Class m_internalClass;

        internal ASClass(Class internalClass) {
            m_internalClass = internalClass;
        }

        /// <summary>
        /// Returns the <see cref="Class"/> instance representing the AVM2 class that is associated
        /// with this <see cref="ASClass"/> instance.
        /// </summary>
        public Class internalClass => m_internalClass;

        /// <summary>
        /// Gets the prototype object of the class.
        /// </summary>
        [AVM2ExportTrait]
        public ASObject prototype => m_internalClass.prototypeObject;

        /// <summary>
        /// Performs a trait lookup on the object.
        /// </summary>
        /// <param name="name">The name of the trait to find.</param>
        /// <param name="trait">The trait with the name <paramref name="name"/>, if one
        /// exists.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        internal override BindStatus AS_lookupTrait(in QName name, out Trait trait) {
            BindStatus bindStatus = m_internalClass.lookupTrait(name, isStatic: true, out trait);
            if (bindStatus != BindStatus.NOT_FOUND)
                return bindStatus;

            return AS_class.lookupTrait(name, isStatic: false, out trait);
        }

        /// <summary>
        /// Performs a trait lookup on the object.
        /// </summary>
        /// <param name="name">The name of the trait to find.</param>
        /// <param name="nsSet">A set of namespaces in which to search for the trait.</param>
        /// <param name="trait">The trait with the name <paramref name="name"/> in a namespace of
        /// <paramref name="nsSet"/>, if one exists.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        internal override BindStatus AS_lookupTrait(string name, in NamespaceSet nsSet, out Trait trait) {
            BindStatus bindStatus = m_internalClass.lookupTrait(name, nsSet, isStatic: true, out trait);
            if (bindStatus != BindStatus.NOT_FOUND)
                return bindStatus;

            return AS_class.lookupTrait(name, nsSet, isStatic: false, out trait);
        }

        /// <summary>
        /// Invokes the object as a function.
        /// </summary>
        /// <param name="receiver">The receiver of the call.</param>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <returns>true, if the call was successful, otherwise false.</returns>
        public override bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) =>
            internalClass.tryInvoke(receiver, args, out result) == BindStatus.SUCCESS;

        /// <summary>
        /// Invokes the object as a constructor.
        /// </summary>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The object created by the constructor call.</param>
        /// <returns>true, if the call was successful, otherwise false.</returns>
        public override bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) =>
            internalClass.tryConstruct(ASAny.@null, args, out result) == BindStatus.SUCCESS;

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) =>
            throw ErrorHelper.createError(ErrorCode.CLASS_NOT_CONSTRUCTOR);

    }

}
