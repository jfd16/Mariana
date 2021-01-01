using System;
using System.Collections.Generic;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    internal sealed class ScriptClass : ClassImpl {

        private ABCClassInfo m_abcClassInfo;

        private SlotMap m_slotMap = new SlotMap();

        internal ScriptClass(ABCClassInfo classInfo, ApplicationDomain domain)
            : base(classInfo.name, domain, ClassTag.OBJECT)
        {
            m_abcClassInfo = classInfo;
            setIsDynamic((m_abcClassInfo.flags & ABCClassFlags.ClassSealed) == 0);
        }

        public override bool isFinal => (m_abcClassInfo.flags & ABCClassFlags.ClassFinal) != 0;

        public override bool isInterface => (m_abcClassInfo.flags & ABCClassFlags.ClassInterface) != 0;

        internal ABCClassInfo abcClassInfo => m_abcClassInfo;

        internal new void setParent(Class parent) => base.setParent(parent);

        internal new void setUnderlyingType(Type type) => base.setUnderlyingType(type);

        internal new void setInterfaces(Class[] ifaces) => base.setInterfaces(ifaces);

        internal new void setConstructor(ClassConstructor ctor) => base.setConstructor(ctor);

        internal new void setMetadata(MetadataTagCollection metadata) => base.setMetadata(metadata);

        internal new bool tryDefineTrait(Trait trait) => base.tryDefineTrait(trait);

        protected private override Class createVectorClass() =>
            (underlyingType != null) ? base.createVectorClass() : new VectorInstFromScriptClass(this);

        /// <summary>
        /// Gets the trait of this class at the given slot index.
        /// </summary>
        /// <returns>The trait corresponding to the slot index <paramref name="index"/>.
        /// If no slot exists at that index, returns null.</returns>
        /// <param name="index">The slot index.</param>
        /// <param name="isStatic">Set to true for static traits, false for instance traits.</param>
        internal Trait getTraitAtSlot(int index, bool isStatic) => m_slotMap.getSlot(index, isStatic);

        /// <summary>
        /// Gets the method of this class with the given dispatch index.
        /// </summary>
        /// <returns>The method corresponding to the dispatch index <paramref name="dispId"/>.
        /// If no method exists with that index, returns null.</returns>
        /// <param name="dispId">A dispatch index.</param>
        /// <param name="isStatic">Set to true for static methods, false for instance methods.</param>
        internal MethodTrait getMethodByDispId(int dispId, bool isStatic) => m_slotMap.getMethodByDispId(dispId, isStatic);

        /// <summary>
        /// Defines the slot index of a trait in this class.
        /// </summary>
        /// <param name="trait">The trait whose slot index to define.</param>
        /// <param name="slotid">The slot index for the trait. This must be greater than 0.</param>
        internal void defineTraitSlot(Trait trait, int slotid) {
            if (slotid <= 0)
                return;

            bool parentHasSlot =
                !trait.isStatic
                && parent is ScriptClass parentScriptClass
                && parentScriptClass.m_slotMap.getSlot(slotid, false) != null;

            if (parentHasSlot || !m_slotMap.tryAddSlot(slotid, trait))
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_SLOT_ID_ALREADY_TAKEN, slotid, name.ToString());
        }

        /// <summary>
        /// Defines the disp_id of a method in this class.
        /// </summary>
        /// <param name="method">The method of this class whose disp_id to define.</param>
        /// <param name="dispid">The disp_id for the method. This must be greater than 0.</param>
        internal void defineMethodDispId(MethodTrait method, int dispid) {
            if (dispid <= 0)
                return;

            if (!m_slotMap.tryAddMethod(dispid, method))
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_SLOT_ID_ALREADY_TAKEN, dispid, name.ToString());
        }

        protected private override void initClass() {
            SlotMap parentSlotMap = (parent is ScriptClass parentScriptClass) ? parentScriptClass.m_slotMap : null;
            if (parentSlotMap != null)
                m_slotMap.addParentSlots(parentSlotMap);
        }

    }

}
