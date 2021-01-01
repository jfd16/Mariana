using System;
using System.Collections.Generic;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A hash table used for trait lookups, both in classes and for global trait lookups in
    /// application domains.
    /// </summary>
    internal sealed class ClassTraitTable {

        private struct Link {
            public int hash;          // Hash code of link
            public int next;          // Index of next link in the chain
            public int chainHead;     // Start of chain corresponding to hashCode % slots.Length
        }

        private const int INITIAL_SIZE = 7;

        private Class m_class;
        private Trait[] m_slots;
        private int m_count;

        private Link[] m_linksForInstQualified;
        private Link[] m_linksForInstUnqualified;
        private Link[] m_linksForStaticQualified;
        private Link[] m_linksForStaticUnqalified;

        private bool m_isCorrupted;
        private bool m_isSealed;

        private int m_staticTraitsBeginIndex;
        private int m_declaredInstanceTraitsBeginIndex;

        /// <summary>
        /// Creates a new instance of <see cref="ClassTraitTable"/>.
        /// </summary>
        /// <param name="klass">The class to which the trait table belongs. (This should be null for
        /// the global trait table of an application domain)</param>
        /// <param name="staticOnly">Set this to true if only static traits will be stored in this
        /// table.</param>
        public ClassTraitTable(Class klass, bool staticOnly = false) {
            m_class = klass;
            m_slots = new Trait[INITIAL_SIZE];

            m_linksForStaticQualified = new Link[INITIAL_SIZE];
            m_linksForStaticUnqalified = new Link[INITIAL_SIZE];
            for (int i = 0; i < INITIAL_SIZE; i++)
                m_linksForStaticQualified[i].chainHead = m_linksForStaticUnqalified[i].chainHead = -1;

            if (!staticOnly) {
                m_linksForInstQualified = new Link[INITIAL_SIZE];
                m_linksForInstUnqualified = new Link[INITIAL_SIZE];
                for (int i = 0; i < INITIAL_SIZE; i++)
                    m_linksForInstQualified[i].chainHead = m_linksForInstUnqualified[i].chainHead = -1;
            }

            m_staticTraitsBeginIndex = -1;
            m_declaredInstanceTraitsBeginIndex = -1;
        }

        /// <summary>
        /// Gets the <see cref="Trait"/> object for the trait with the given name.
        /// </summary>
        ///
        /// <param name="name">The name of the trait.</param>
        /// <param name="isStatic">If this is true, the static traits are searched; otherwise the
        /// instance traits are searched.</param>
        /// <param name="trait">The <see cref="Trait"/> object representing the trait with the given
        /// name.</param>
        ///
        /// <returns>
        /// A <see cref="BindStatus"/> indicating the result of the operation. This is usually
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> (indicating success),
        /// <see cref="BindStatus.NOT_FOUND" qualifyHint="true"/> (indicating failure) or
        /// <see cref="BindStatus.AMBIGUOUS" qualifyHint="true"/> (indicating an ambiguous match,
        /// only applicable to names in the any namespace).
        /// </returns>
        public BindStatus tryGetTrait(in QName name, bool isStatic, out Trait trait) {
            if (m_isCorrupted)
                throw ErrorHelper.createError(ErrorCode.MARIANA__CLASS_TRAIT_TABLE_CORRUPTED);

            Trait[] slots = m_slots;
            Link[] links;
            int hash;
            int curIndex;

            if (m_count == 0 || name.localName == null || (!isStatic && m_linksForInstQualified == null)) {
                trait = null;
                return BindStatus.NOT_FOUND;
            }

            if (name.ns.isPublic) {
                // Optimized case for public namespace, since it is so commonly used.

                string localName = name.localName;
                links = isStatic ? m_linksForStaticUnqalified : m_linksForInstUnqualified;

                hash = localName.GetHashCode() & 0x7FFFFFFF;
                trait = null;

                curIndex = links[hash % links.Length].chainHead;
                while (curIndex != -1) {
                    ref Link link = ref links[curIndex];

                    if (link.hash == hash) {
                        Trait slot = slots[curIndex];
                        ref readonly QName slotName = ref slot.name;

                        if (slotName.ns.isPublic && slotName.localName == localName) {
                            trait = slot;
                            return BindStatus.SUCCESS;
                        }
                    }

                    curIndex = link.next;
                }

                return BindStatus.NOT_FOUND;
            }

            if (name.ns.kind == NamespaceKind.ANY) {
                // If the namespace of the target name is the any namespace, only local names must
                // match and namespaces are to be ignored. For this, the unqualified name links
                // are used for lookup, and an ambiguous match is returned if there are two
                // traits with the same name but different namespaces declared on the same class.
                string localName = name.localName;
                bool found = false;

                links = isStatic ? m_linksForStaticUnqalified : m_linksForInstUnqualified;
                hash = localName.GetHashCode() & 0x7FFFFFFF;
                trait = null;

                curIndex = links[hash % links.Length].chainHead;
                while (curIndex != -1) {
                    ref Link link = ref links[curIndex];
                    if (link.hash != hash) {
                        curIndex = link.next;
                        continue;
                    }

                    Trait slot = slots[curIndex];
                    if (slot.name.localName != localName) {
                        curIndex = link.next;
                        continue;
                    }

                    if (found) {
                        if (slot.declaringClass == trait.declaringClass)
                            return BindStatus.AMBIGUOUS;
                        break;
                    }

                    trait = slot;
                    found = true;
                    curIndex = link.next;
                }

                return found ? BindStatus.SUCCESS : BindStatus.NOT_FOUND;
            }

            // For namespaces other than the any and public namespaces, use the qualified name
            // links for lookup and compare namespaces directly.

            links = isStatic ? m_linksForStaticQualified : m_linksForInstQualified;
            hash = name.GetHashCode() & 0x7FFFFFFF;

            curIndex = links[hash % links.Length].chainHead;
            while (curIndex != -1) {
                ref Link link = ref links[curIndex];

                if (links[curIndex].hash == hash) {
                    Trait slot = slots[curIndex];
                    ref readonly QName slotName = ref slot.name;

                    if (slotName.localName == name.localName && slotName.ns == name.ns) {
                        trait = slot;
                        return BindStatus.SUCCESS;
                    }
                }

                curIndex = link.next;
            }

            trait = null;
            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Gets the <see cref="Trait"/> object for the trait whose local name is equal to the
        /// specified name and whose namespace is in the given set of namespaces.
        /// </summary>
        ///
        /// <param name="localName">The name of the trait.</param>
        /// <param name="nsSet">The set of namespaces in which the trait can be found.</param>
        /// <param name="isStatic">If this is true, the static traits are searched; otherwise the
        /// instance traits are searched.</param>
        /// <param name="trait">The <see cref="Trait"/> object representing the trait with the given
        /// name.</param>
        ///
        /// <returns>
        /// A <see cref="BindStatus"/> indicating the result of the operation. This is usually
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> (indicating success),
        /// <see cref="BindStatus.NOT_FOUND" qualifyHint="true"/> (indicating that the trait has not
        /// been found), or <see cref="BindStatus.AMBIGUOUS" qualifyHint="true"/> (indicating an
        /// ambiguous match, i.e. two traits with the same local name in different namespaces both
        /// belonging to the given set of namespaces)
        /// </returns>
        ///
        /// <remarks>
        /// If this method is called before the table is sealed (by calling the <see cref="seal"/>
        /// method), searching is not guaranteed to happen in proper inheritance order (most derived
        /// class first), which may result in incorrect ambiguous matches. However, it is OK to call
        /// this method on an unsealed table when there is no inheritance involved (for example, in
        /// the global trait table of an application domain).
        /// </remarks>
        public BindStatus tryGetTrait(string localName, in NamespaceSet nsSet, bool isStatic, out Trait trait) {
            if (m_isCorrupted)
                throw ErrorHelper.createError(ErrorCode.MARIANA__CLASS_TRAIT_TABLE_CORRUPTED);

            if (m_count == 0 || localName == null || (!isStatic && m_linksForInstQualified == null)) {
                trait = null;
                return BindStatus.NOT_FOUND;
            }

            bool found = false;
            trait = null;

            int hash = localName.GetHashCode() & 0x7FFFFFFF;
            Trait[] slots = m_slots;
            Link[] links = isStatic ? m_linksForStaticUnqalified : m_linksForInstUnqualified;

            int curIndex = links[hash % links.Length].chainHead;

            while (curIndex != -1) {
                ref Link link = ref links[curIndex];
                if (link.hash != hash) {
                    curIndex = link.next;
                    continue;
                }

                Trait slot = slots[curIndex];
                ref readonly QName slotName = ref slot.name;

                if (slotName.localName != localName || !nsSet.contains(slotName.ns)) {
                    curIndex = link.next;
                    continue;
                }

                if (found) {
                    // If an ambiguous match is found, it is considered ambiguous only if
                    // both the traits in the ambiguous match are declared on the same class.
                    // If one of the traits is declared on a class and the other is inherited
                    // from an ancestor class, the declared trait must be returned.
                    if (trait.declaringClass == slot.declaringClass)
                        return BindStatus.AMBIGUOUS;
                    break;
                }

                trait = slot;
                found = true;
                curIndex = link.next;
            }

            return found ? BindStatus.SUCCESS : BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Adds a trait to the table.
        /// </summary>
        ///
        /// <param name="trait">The trait to add to the table.</param>
        /// <param name="allowMergeProperties">If this is set to true, and a trait with the same name
        /// as that of the trait to be added exists, and both the traits are properties, an attempt
        /// will be made to merge the definitions of the two properties under certain
        /// conditions.</param>
        ///
        /// <returns>True, if the trait was added to the table; false it is was not added (such as if
        /// another trait with the same name exists).</returns>
        public bool tryAddTrait(Trait trait, bool allowMergeProperties = false) {

            if (m_isCorrupted || m_isSealed)
                return false;

            ref readonly QName name = ref trait.name;
            int hash = name.GetHashCode() & 0x7FFFFFFF;
            bool isStatic = trait.isStatic;

            Link[] links = isStatic ? m_linksForStaticQualified : m_linksForInstQualified;
            if (links == null)
                return false;

            int chain = hash % links.Length;
            int curIndex = links[chain].chainHead;

            while (curIndex != -1) {
                ref Link link = ref links[curIndex];

                if (link.hash != hash) {
                    curIndex = link.next;
                    continue;
                }

                Trait slot = m_slots[curIndex];
                if (slot.name != name) {
                    curIndex = links[curIndex].next;
                    continue;
                }

                if (!allowMergeProperties
                    || trait.traitType != TraitType.PROPERTY
                    || slot.traitType != TraitType.PROPERTY)
                {
                    return false;
                }

                // If allowMergeProperties is set to true, and if both the conflicting traits are properties,
                // check if they can be merged. (This happens, for example, when a parent class defines
                // one accessor of a property and the other accessor is supplied by a subclass, or a derived
                // class overrides one of the accessors of a property defined on a base class).

                PropertyTrait mergedProp = PropertyTrait.tryMerge((PropertyTrait)slot, (PropertyTrait)trait, m_class);

                if (mergedProp == null)
                    return false;

                m_slots[curIndex] = mergedProp;
                return true;
            }

            // No trait with the same name exists, so add the trait to the table.
            // First, check that there is empty space for a new trait, and resize the table if necessary.

            if (m_count == m_slots.Length) {
                _expandTable();
                links = isStatic ? m_linksForStaticQualified : m_linksForInstQualified;
                chain = hash % links.Length;
            }

            int newIndex = m_count;
            m_count++;

            m_slots[newIndex] = trait;
            links[newIndex].hash = hash;
            links[newIndex].next = links[chain].chainHead;
            links[chain].chainHead = newIndex;

            // Update the links for unqualified lookup.
            links = isStatic ? m_linksForStaticUnqalified : m_linksForInstUnqualified;
            hash = name.localName.GetHashCode() & 0x7FFFFFFF;
            chain = hash % links.Length;
            links[newIndex].hash = hash;
            links[newIndex].next = links[chain].chainHead;
            links[chain].chainHead = newIndex;

            return true;

        }

        /// <summary>
        /// Assigns a new trait to the slot in the table having the name of an existing trait.
        /// </summary>
        /// <param name="oldTrait">An existing trait in this table.</param>
        /// <param name="newTrait">The new trait to assign to the existing trait's slot. This must
        /// have the same qualified name as the existing trait.</param>
        private void _swapTraits(Trait oldTrait, Trait newTrait) {
            if (m_isCorrupted || m_isSealed)
                return;

            ref readonly QName name = ref oldTrait.name;
            int hash = name.GetHashCode() & 0x7FFFFFFF;
            bool isStatic = oldTrait.isStatic;

            Link[] links = isStatic ? m_linksForStaticQualified : m_linksForInstQualified;
            if (links == null)
                return;

            int chain = hash % links.Length;
            int curIndex = links[chain].chainHead;

            while (curIndex != -1) {
                ref Link link = ref links[curIndex];

                if (link.hash == hash) {
                    ref Trait slot = ref m_slots[curIndex];
                    if (slot.name == name) {
                        slot = newTrait;
                        return;
                    }
                }

                curIndex = link.next;
            }
        }

        /// <summary>
        /// Enlarges the table.
        /// </summary>
        private void _expandTable() {
            int newSize = DataStructureUtil.nextPrime(checked(m_count * 2));
            Trait[] newSlots = new Trait[newSize];
            (new ReadOnlySpan<Trait>(m_slots, 0, m_count)).CopyTo(newSlots);
            m_slots = newSlots;
            _resetLinks(false);
        }

        /// <summary>
        /// Resets all the links in the table. Call this after modifying any slots.
        /// </summary>
        /// <param name="recalculateHashes">If this is set to true, recompute the hash codes from the
        /// trait names, otherwise compute the chains using the existing hash codes.</param>
        private void _resetLinks(bool recalculateHashes) {

            int length = m_slots.Length;
            Link[] linksForStaticQualified = new Link[length],
                   linksForStaticUnqualified = new Link[length],
                   linksForInstQualified = null,
                   linksForInstUnqualified = null;

            for (int i = 0; i < length; i++) {
                linksForStaticQualified[i].chainHead = -1;
                linksForStaticUnqualified[i].chainHead = -1;
            }

            if (m_linksForInstQualified != null) {
                linksForInstQualified = new Link[length];
                linksForInstUnqualified = new Link[length];
                for (int i = 0; i < length; i++) {
                    linksForInstQualified[i].chainHead = -1;
                    linksForInstUnqualified[i].chainHead = -1;
                }
            }

            for (int i = 0, n = m_count; i < n; i++) {

                bool traitIsStatic = m_slots[i].isStatic;

                int qualifiedHash;
                int unqualifiedHash;

                if (recalculateHashes) {
                    qualifiedHash = m_slots[i].name.GetHashCode() & 0x7FFFFFFF;
                    unqualifiedHash = m_slots[i].name.localName.GetHashCode() & 0x7FFFFFFF;
                }
                else if (traitIsStatic) {
                    qualifiedHash = m_linksForStaticQualified[i].hash;
                    unqualifiedHash = m_linksForStaticUnqalified[i].hash;
                }
                else {
                    qualifiedHash = m_linksForInstQualified[i].hash;
                    unqualifiedHash = m_linksForInstUnqualified[i].hash;
                }

                int qualifiedChain = qualifiedHash % length;
                int unqualifiedChain = unqualifiedHash % length;

                if (traitIsStatic) {
                    linksForStaticQualified[i].hash = qualifiedHash;
                    linksForStaticUnqualified[i].hash = unqualifiedHash;

                    linksForStaticQualified[i].next = linksForStaticQualified[qualifiedChain].chainHead;
                    linksForStaticUnqualified[i].next = linksForStaticUnqualified[unqualifiedChain].chainHead;
                    linksForStaticQualified[qualifiedChain].chainHead = i;
                    linksForStaticUnqualified[unqualifiedChain].chainHead = i;
                }
                else {
                    linksForInstQualified[i].hash = qualifiedHash;
                    linksForInstUnqualified[i].hash = unqualifiedHash;

                    linksForInstQualified[i].next = linksForInstQualified[qualifiedChain].chainHead;
                    linksForInstUnqualified[i].next = linksForInstUnqualified[unqualifiedChain].chainHead;
                    linksForInstQualified[qualifiedChain].chainHead = i;
                    linksForInstUnqualified[unqualifiedChain].chainHead = i;
                }

            }

            m_linksForInstQualified = linksForInstQualified;
            m_linksForInstUnqualified = linksForInstUnqualified;
            m_linksForStaticQualified = linksForStaticQualified;
            m_linksForStaticUnqalified = linksForStaticUnqualified;
        }

        /// <summary>
        /// Adds the inherited traits of the parent class to the trait table.
        /// </summary>
        /// <param name="parentTraitTable">The trait table of the parent class to be merged into this
        /// trait table.</param>
        /// <param name="allowHiding">If this is true, declared traits are allowed to hide inherited
        /// traits with the same name without overriding them. Otherwise, hiding is an error.</param>
        public void mergeWithParentClass(ClassTraitTable parentTraitTable, bool allowHiding = false) {

            Trait[] parentTraits = parentTraitTable.m_slots;

            for (int i = 0, n = parentTraitTable.m_count; i < n; i++) {
                Trait parentTrait = parentTraits[i];

                if (parentTrait.isStatic)
                    continue;   // Don't merge static traits as they are not inherited.

                if (tryAddTrait(parentTrait, allowMergeProperties: true))
                    continue;

                tryGetTrait(parentTrait.name, false, out Trait conflictingTrait);

                if (allowHiding) {
                    if (conflictingTrait.traitType != parentTrait.traitType
                        || conflictingTrait.traitType != TraitType.PROPERTY)
                    {
                        continue;
                    }

                    PropertyTrait parentProp = (PropertyTrait)parentTrait;
                    PropertyTrait conflictProp = (PropertyTrait)conflictingTrait;
                    bool mustCreateNewProp = false;
                    if ((conflictProp.getter == null && parentProp.getter != null)
                        || (conflictProp.setter == null && parentProp.setter != null))
                    {
                        mustCreateNewProp = true;
                    }

                    if (!mustCreateNewProp)
                        continue;

                    PropertyTrait newProp = new PropertyTrait(
                        conflictProp.name, m_class, m_class.applicationDomain, false,
                        conflictProp.getter ?? parentProp.getter,
                        conflictProp.setter ?? parentProp.setter);

                    _swapTraits(conflictProp, newProp);
                    continue;
                }
                else {
                    // If hiding is not allowed, a declared trait cannot have the same name
                    // as an inherited one, except when it is a method that overrides an
                    // inherited method, or a property whose getter and/or setter override
                    // the corresponding inherited accessors.

                    if (_canMergeConflictingParentTrait(parentTrait, conflictingTrait))
                        continue;
                }

                m_isCorrupted = true;
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__CLASS_TRAIT_HIDES_INHERITED,
                    conflictingTrait.name.ToString(), m_class.name.ToString());

            }

        }

        /// <summary>
        /// Checks if an inherited trait having a name conflict with a declared trait can be ignored
        /// because the inherited trait can be merged into the declared one.
        /// </summary>
        /// <param name="parentTrait">The trait from the parent class having a name conflict.</param>
        /// <param name="conflictingTrait">The declared trait conflicting with
        /// <paramref name="parentTrait"/>.</param>
        /// <returns>True if the conflict can be ignored (declared trait stays in the table), false if
        /// this is a name conflict error.</returns>
        private bool _canMergeConflictingParentTrait(Trait parentTrait, Trait conflictingTrait) {
            if (conflictingTrait.traitType == TraitType.METHOD) {
                return parentTrait.traitType == TraitType.METHOD
                    && ((MethodTrait)conflictingTrait).isOverride;
            }

            if (conflictingTrait.traitType == TraitType.PROPERTY) {
                if (parentTrait.traitType != TraitType.PROPERTY)
                    return false;

                PropertyTrait conflictingProp = (PropertyTrait)conflictingTrait;
                PropertyTrait parentProp = (PropertyTrait)parentTrait;

                return !(parentProp.getter != null
                        && (conflictingProp.getter == null || !conflictingProp.getter.isOverride))
                    && !(parentProp.setter != null
                        && (conflictingProp.setter == null || !conflictingProp.setter.isOverride));
            }

            return false;
        }

        /// <summary>
        /// Adds the inherited traits of parent interfaces to the trait table of an interface.
        /// </summary>
        /// <param name="parentTraitTable">The trait table of a parent interface which is to be merged
        /// into this trait table.</param>
        public void mergeWithParentInterface(ClassTraitTable parentTraitTable) {

            Trait[] parentTraits = parentTraitTable.m_slots;

            // Since parent interfaces are flattened, we only need to consider the
            // declared traits of each of them.
            for (int i = parentTraitTable.m_count - 1; i >= 0; i--) {

                Trait parentTrait = parentTraits[i];

                if (parentTrait.declaringClass != parentTraitTable.m_class)
                    // Since it is assumed that the interface trait tables are sealed, declared
                    // traits will be at the end of the trait table slots array, so if the table
                    // is iterated in reverse order, we can stop as soon as an inherited trait
                    // is found.
                    break;

                if (parentTrait.isStatic)
                    continue;

                if (tryAddTrait(parentTrait, allowMergeProperties: true))
                    continue;

                tryGetTrait(parentTrait.name, false, out Trait conflictingTrait);

                if (!_canMergeConflictingInterfaceTraits(parentTrait, conflictingTrait)) {
                    m_isCorrupted = true;
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__INTERFACE_COMMON_TRAIT_SIG_MISMATCH,
                        conflictingTrait.name.ToString(), m_class.name.ToString(),
                        parentTrait.declaringClass.name.ToString(), conflictingTrait.declaringClass.name.ToString());
                }

            }

        }

        /// <summary>
        /// Checks if a name conflict between two inherited interface traits can be safely ignored
        /// because the one of the traits can be merged into the other.
        /// </summary>
        /// <param name="firstTrait">The first conflicting trait.</param>
        /// <param name="secondTrait">The second conflicting trait.</param>
        /// <returns>True if the conflict can be ignored (one of the traits stays in the derived
        /// interface table), false if this is a name conflict error.</returns>
        private bool _canMergeConflictingInterfaceTraits(Trait firstTrait, Trait secondTrait) {
            if (firstTrait == null || secondTrait == null)
                return true;

            if (firstTrait.traitType != secondTrait.traitType)
                return false;

            if (firstTrait.traitType == TraitType.METHOD) {
                MethodTrait firstMethod = (MethodTrait)firstTrait,
                            secondMethod = (MethodTrait)secondTrait;

                if (firstMethod.hasReturn != secondMethod.hasReturn
                    || firstMethod.returnType != secondMethod.returnType
                    || firstMethod.hasRest != secondMethod.hasRest)
                {
                    return false;
                }

                int paramCount = firstMethod.paramCount;
                if (paramCount != secondMethod.paramCount)
                    return false;

                ReadOnlyArrayView<MethodTraitParameter> firstMethodParams = firstMethod.getParameters();
                ReadOnlyArrayView<MethodTraitParameter> secondMethodParams = secondMethod.getParameters();

                for (int i = 0; i < paramCount; i++) {
                    MethodTraitParameter param1 = firstMethodParams[i], param2 = secondMethodParams[i];
                    if (param1.type != param2.type || param1.hasDefault != param2.hasDefault)
                        return false;
                }
                return true;
            }

            if (firstTrait.traitType == TraitType.PROPERTY) {
                PropertyTrait firstProp = (PropertyTrait)firstTrait;
                PropertyTrait secondProp = (PropertyTrait)secondTrait;

                return _canMergeConflictingInterfaceTraits(firstProp.getter, secondProp.getter)
                    && _canMergeConflictingInterfaceTraits(firstProp.setter, secondProp.setter);
            }

            return false;
        }

        /// <summary>
        /// This method is called once all traits (both declared and inherited) have been added to the
        /// table. It resizes the table to remove any excess slots, and sorts the traits according to
        /// the order of inheritance of the classes on which they are declared.
        /// </summary>
        public void seal() {

            int slotCount = m_count;

            // Sort the table so that the traits are ordered in the following manner
            // (in reverse): first static traits, then declared instance traits, followed
            // by inherited instance traits from most derived to least derived class.
            // This need not be done for static-only trait tables, since static
            // traits are not inherited.

            if (m_linksForInstQualified == null) {
                Trait[] newSlots = new Trait[m_count];
                (new ReadOnlySpan<Trait>(m_slots, 0, m_count)).CopyTo(newSlots);
                m_slots = newSlots;
                m_staticTraitsBeginIndex = 0;
                return;
            }

            DynamicArray<Trait> newTraitList = new DynamicArray<Trait>();
            ReferenceSet<Class> classSet = new ReferenceSet<Class>();
            int curPosInNewList = slotCount - 1;
            newTraitList.addDefault(slotCount);

            for (int i = 0; i < slotCount; i++) {
                Trait trait = m_slots[i];
                if (trait.isStatic) {
                    m_slots[i] = null;
                    newTraitList[curPosInNewList] = trait;
                    curPosInNewList--;
                }
                else {
                    classSet.add(trait.declaringClass);
                }
            }

            if (curPosInNewList != slotCount - 1)
                m_staticTraitsBeginIndex = curPosInNewList + 1;

            foreach (Class klass in _sortClassesByInheritance(classSet.toArray())) {
                int traitsAdded = 0;
                for (int i = 0; i < slotCount; i++) {
                    Trait trait = m_slots[i];
                    if (trait == null || trait.declaringClass != klass)
                        continue;

                    newTraitList[curPosInNewList] = trait;
                    curPosInNewList--;
                    traitsAdded++;
                }
                if (klass == m_class && traitsAdded != 0)
                    m_declaredInstanceTraitsBeginIndex = curPosInNewList + 1;
            }

            m_slots = newTraitList.toArray();

            _resetLinks(true);
            m_isSealed = true;

        }

        /// <summary>
        /// Sorts the given array of classes in order of inheritance. The sort is such that derived
        /// classes will be placed after their base classes in the sort order. This method returns an
        /// iterator that iterates over the classes in sort order.
        /// </summary>
        ///
        /// <param name="classArray">The array of classes or interfaces to sort.</param>
        private IEnumerable<Class> _sortClassesByInheritance(Class[] classArray) {

            int len = classArray.Length;
            bool[] marked = new bool[len];
            DynamicArray<int> indexStack = new DynamicArray<int>(len);

            int i = 0;
            Class currentClass = null;
            Class klass;

            while (true) {
                if (i == len) {
                    if (indexStack.length == 0)
                        yield break;

                    int topIndex = indexStack[indexStack.length - 1];
                    indexStack.removeLast();

                    Class popped = classArray[topIndex];
                    marked[topIndex] = true;

                    // After popping, it is sufficient to resume the search from the index
                    // after the one that was popped, since if any class would be pushed it must
                    // be derived from the class which is currently at the top of the stack, and
                    // this is precisely why anything before this index wasn't pushed.
                    i = topIndex + 1;

                    currentClass = (indexStack.length == 0) ? null : classArray[indexStack[indexStack.length - 1]];
                    yield return popped;
                }
                else {
                    if (!marked[i]) {
                        klass = classArray[i];
                        if (indexStack.length == 0 || klass.canAssignTo(currentClass)) {
                            indexStack.add(i);
                            currentClass = klass;
                        }
                    }
                    i++;
                }
            }

        }

        /// <summary>
        /// Appends all the traits in the table to the list <paramref name="outList"/>.
        /// </summary>
        /// <param name="outList">A <see cref="DynamicArray{T}"/> to which to append the
        /// traits.</param>
        public void getTraits(ref DynamicArray<Trait> outList) {
            int startIndex = outList.length;
            outList.addDefault(m_count);
            m_slots.CopyTo(outList.asSpan(startIndex, m_count));
        }

        /// <summary>
        /// Gets the start and end indices of the range in the <see cref="m_slots"/> array where the
        /// traits satisfying the given criteria will be found. This can only be called after calling
        /// the <see cref="seal"/> method, which does the necessary sorting.
        /// </summary>
        ///
        /// <param name="includeStatic">Set to true to consider instance traits.</param>
        /// <param name="includeInstance">Set to true to consider static traits.</param>
        /// <param name="declaredOnly">Set to true to consider only declared traits. Otherwise both
        /// declared and inherited traits will be considered.</param>
        /// <param name="startIndex">The first index (inclusive) of the range in the
        /// <see cref="m_slots"/> array in which the traits will be found. If the range is empty or
        /// the <see cref="seal"/> method has not been called, this is set to -1.</param>
        /// <param name="endIndex">The last index (inclusive) of the range in the
        /// <see cref="m_slots"/> array in which the traits will be found. If the range is empty or
        /// the <see cref="seal"/> method has not been called, this is set to -1.</param>
        private void _getSlotIndexBounds(
            bool includeStatic, bool includeInstance, bool declaredOnly,
            out int startIndex, out int endIndex)
        {
            if (!m_isSealed) {
                startIndex = 0;
                endIndex = m_count - 1;
                return;
            }

            startIndex = -1;
            endIndex = -1;

            bool hasStatic = m_staticTraitsBeginIndex != -1;
            bool hasDeclaredInst = m_declaredInstanceTraitsBeginIndex != -1;

            if (includeStatic && !includeInstance) {
                // Only static traits.
                if (hasStatic) {
                    startIndex = m_staticTraitsBeginIndex;
                    endIndex = m_count - 1;
                }
                return;
            }

            if (!includeInstance)
                return;

            if (includeStatic) {
                if (!declaredOnly) {
                    // All traits (static+instance, declared+inherited)
                    startIndex = 0;
                    endIndex = m_count - 1;
                }
                else if (hasDeclaredInst) {
                    // Declared-only, instance+static, has declared instance traits.
                    startIndex = m_declaredInstanceTraitsBeginIndex;
                    endIndex = m_count - 1;
                }
                else if (hasStatic) {
                    // Declared-only, instance+static, no declared instance traits.
                    startIndex = m_staticTraitsBeginIndex;
                    endIndex = m_count - 1;
                }
                return;
            }

            if (declaredOnly) {
                // Instance traits only, declared only.
                if (hasDeclaredInst) {
                    startIndex = m_declaredInstanceTraitsBeginIndex;
                    endIndex = hasStatic ? m_staticTraitsBeginIndex - 1 : m_count - 1;
                }
            }
            else {
                // Instance traits only, declared+inherited
                startIndex = 0;
                endIndex = hasStatic ? m_staticTraitsBeginIndex - 1 : m_count - 1;
            }
        }

        /// <summary>
        /// Appends all the traits in the given table of the kinds given by the flags in
        /// <paramref name="kinds"/> and in the scopes given by <paramref name="scopes"/>
        /// to the list <paramref name="outList"/>.
        /// </summary>
        /// <param name="kinds">The kinds of traits to include in the returned array.</param>
        /// <param name="scopes">The scopes in which to search for the traits.</param>
        /// <param name="outList">A <see cref="DynamicArray{T}"/> to which to append the
        /// traits.</param>
        /// <typeparam name="T">The type to which to cast the traits to. This must be the correct
        /// subclass of <see cref="Trait"/> corresponding to the trait kind that is given in
        /// <paramref name="kinds"/>, or the class <see cref="Trait"/> itself if there are
        /// multiple kinds set in <paramref name="kinds"/>.</typeparam>
        public void getTraits<T>(TraitType kinds, TraitScope scopes, ref DynamicArray<T> outList)
            where T : Trait
        {
            Trait[] slots = m_slots;
            bool searchStatic = (scopes & TraitScope.STATIC) != 0;
            bool searchInstance = (scopes & TraitScope.INSTANCE) != 0;
            bool declaredOnly = (scopes & TraitScope.INSTANCE_INHERITED) == 0;

            if (m_isSealed) {
                _getSlotIndexBounds(searchStatic, searchInstance, declaredOnly, out int startIndex, out int endIndex);

                if (startIndex == -1)
                    return;

                var span = new ReadOnlySpan<Trait>(slots, startIndex, endIndex - startIndex);
                for (int i = 0; i < span.Length; i++) {
                    Trait slot = span[i];
                    if ((slot.traitType & kinds) != 0)
                        outList.add((T)slot);
                }
            }
            else {
                var span = new ReadOnlySpan<Trait>(slots, 0, m_count);
                for (int i = 0; i < span.Length; i++) {
                    Trait slot = span[i];
                    if ((slot.declaringClass == m_class || !declaredOnly)
                        && (slot.isStatic ? searchStatic : searchInstance)
                        && (slot.traitType & kinds) != 0)
                    {
                        outList.add((T)slot);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the trait in the table for which the <paramref name="filter"/> function returns
        /// true.
        /// </summary>
        /// <param name="filter">A filter function.</param>
        /// <returns>The trait for which <paramref name="filter"/> returns true, or null if the
        /// function returns false for all the traits in the table. If there is more than one trait in
        /// the table for which the <paramref name="filter"/> returns true, any one of them is
        /// returned. (Which one is unspecified.)</returns>
        public Trait getTraitByFilter(Predicate<Trait> filter) {
            Trait[] slots = m_slots;
            for (int i = m_count - 1; i >= 0; i--) {
                if (filter(slots[i]))
                    return slots[i];
            }
            return null;
        }

        /// <summary>
        /// Appends all the traits in the given table for which the <paramref name="filter"/>
        /// function returns true to the list <paramref name="outList"/>.
        /// </summary>
        /// <param name="filter">A filter function.</param>
        /// <param name="outList">A <see cref="DynamicArray{T}"/> to which to append the
        /// traits.</param>
        public void getTraitsByFilter(Predicate<Trait> filter, ref DynamicArray<Trait> outList) {
            Trait[] slots = m_slots;
            for (int i = m_count - 1; i >= 0; i--) {
                if (filter(slots[i]))
                    outList.add(slots[i]);
            }
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{Trait}"/> that contains the traits
        /// in this <see cref="ClassTraitTable"/>.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Trait}"/> that contains the traits
        /// in the table.</returns>
        public ReadOnlyArrayView<Trait> getTraits() => new ReadOnlyArrayView<Trait>(m_slots, 0, m_count);

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{Trait}"/> that contains the traits
        /// in this <see cref="ClassTraitTable"/> in the given scopes.
        /// </summary>
        /// <param name="scopes">The scopes in which to search for the traits.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{Trait}"/> that contains the traits
        /// in the given scopes.</returns>
        public ReadOnlyArrayView<Trait> getTraits(TraitScope scopes) {
            if (!m_isSealed || (scopes & TraitScope.ALL) == (TraitScope.STATIC | TraitScope.INSTANCE_INHERITED)) {
                var list = new DynamicArray<Trait>();
                getTraits(TraitType.ALL, scopes, ref list);
                return new ReadOnlyArrayView<Trait>(list.toArray());
            }

            bool searchStatic = (scopes & TraitScope.STATIC) != 0;
            bool searchInstance = (scopes & TraitScope.INSTANCE) != 0;
            bool declaredOnly = (scopes & TraitScope.INSTANCE_INHERITED) == 0;

            _getSlotIndexBounds(searchStatic, searchInstance, declaredOnly, out int startIndex, out int endIndex);

            if (startIndex == -1)
                return ReadOnlyArrayView<Trait>.empty;

            return new ReadOnlyArrayView<Trait>(m_slots, startIndex, endIndex - startIndex + 1);
        }

    }
}
