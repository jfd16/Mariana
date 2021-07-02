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

        private const int END_OF_CHAIN = -1;
        private const int HASH_CODE_MASK = 0x7FFFFFFF;

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

            Link defaultLink = new Link {chainHead = END_OF_CHAIN};

            m_linksForStaticQualified = new Link[INITIAL_SIZE];
            m_linksForStaticUnqalified = new Link[INITIAL_SIZE];

            m_linksForStaticQualified.AsSpan().Fill(defaultLink);
            m_linksForStaticUnqalified.AsSpan().Fill(defaultLink);

            if (!staticOnly) {
                m_linksForInstQualified = new Link[INITIAL_SIZE];
                m_linksForInstUnqualified = new Link[INITIAL_SIZE];

                m_linksForInstQualified.AsSpan().Fill(defaultLink);
                m_linksForInstUnqualified.AsSpan().Fill(defaultLink);
            }
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

            if (m_count == 0 || name.localName == null || (!isStatic && m_linksForInstQualified == null)) {
                trait = null;
                return BindStatus.NOT_FOUND;
            }

            if (name.ns.isPublic) {
                // Optimized case for public namespace, since it is so commonly used.

                string localName = name.localName;
                Link[] links = isStatic ? m_linksForStaticUnqalified : m_linksForInstUnqualified;

                int hash = localName.GetHashCode() & HASH_CODE_MASK;
                trait = null;

                int curIndex = links[hash % links.Length].chainHead;

                while (curIndex != END_OF_CHAIN) {
                    ref Link link = ref links[curIndex];

                    if (link.hash == hash) {
                        Trait currentTrait = slots[curIndex];
                        ref readonly QName currentTraitName = ref currentTrait.name;

                        if (currentTraitName.ns.isPublic && currentTraitName.localName == localName) {
                            trait = currentTrait;
                            return BindStatus.SUCCESS;
                        }
                    }

                    curIndex = link.next;
                }

                return BindStatus.NOT_FOUND;
            }
            else if (name.ns.kind == NamespaceKind.ANY) {
                // If the namespace of the target name is the any namespace, only local names must
                // match and namespaces are to be ignored. For this, the unqualified name links
                // are used for lookup, and an ambiguous match is returned if there are two
                // traits with the same name but different namespaces declared on the same class.

                string localName = name.localName;
                bool found = false;
                bool ambiguousMatch = false;
                trait = null;

                Link[] links = isStatic ? m_linksForStaticUnqalified : m_linksForInstUnqualified;
                int hash = localName.GetHashCode() & HASH_CODE_MASK;

                int curIndex = links[hash % links.Length].chainHead;

                while (curIndex != END_OF_CHAIN) {
                    ref Link link = ref links[curIndex];
                    if (link.hash != hash) {
                        curIndex = link.next;
                        continue;
                    }

                    Trait currentTrait = slots[curIndex];
                    if (currentTrait.name.localName != localName) {
                        curIndex = link.next;
                        continue;
                    }

                    if (found) {
                        // If two conflicting traits are declared on the same class it is definitely
                        // an ambiguous match. This is also true for global traits.
                        if (currentTrait.declaringClass == trait.declaringClass)
                            return BindStatus.AMBIGUOUS;

                        if (m_isSealed) {
                            // If this table is sealed then the traits are always ordered from
                            // more derived to least derived class. This means that the match is definitely
                            // ambiguous if `trait` is declared on a class that is not derived from the
                            // declaring class of `currentTrait`. This can happen only with interfaces.
                            // For non-interface classes, either both the traits are declared by the same
                            // class (which was already checked) or `currentTrait` is declared on a class
                            // that `trait` derives from, in which case we terminate the search early and
                            // return `trait`.

                            if (!currentTrait.declaringClass.isInterface)
                                break;

                            if (!trait.declaringClass.canAssignTo(currentTrait.declaringClass))
                                return BindStatus.AMBIGUOUS;
                        }
                        else {
                            // For tables which are not sealed, if `currentTrait` is from a more derived
                            // class, set the result to that trait and reset the ambiguous match flag.
                            if (currentTrait.declaringClass.canAssignTo(trait.declaringClass)) {
                                trait = currentTrait;
                                ambiguousMatch = false;
                            }
                            else if (!trait.declaringClass.canAssignTo(currentTrait.declaringClass)) {
                                ambiguousMatch = true;
                            }
                        }
                    }
                    else {
                        trait = currentTrait;
                        found = true;
                    }

                    curIndex = link.next;
                }

                if (ambiguousMatch)
                    return BindStatus.AMBIGUOUS;

                return found ? BindStatus.SUCCESS : BindStatus.NOT_FOUND;
            }
            else {
                // For namespaces other than the any and public namespaces, use the qualified name
                // links for lookup and compare namespaces directly.

                Link[] links = isStatic ? m_linksForStaticQualified : m_linksForInstQualified;
                int hash = name.GetHashCode() & HASH_CODE_MASK;

                int curIndex = links[hash % links.Length].chainHead;

                while (curIndex != END_OF_CHAIN) {
                    ref Link link = ref links[curIndex];

                    if (link.hash == hash) {
                        Trait currentTrait = slots[curIndex];
                        ref readonly QName currentTraitName = ref currentTrait.name;

                        if (currentTraitName.localName == name.localName && currentTraitName.ns == name.ns) {
                            trait = currentTrait;
                            return BindStatus.SUCCESS;
                        }
                    }

                    curIndex = link.next;
                }

                trait = null;
                return BindStatus.NOT_FOUND;
            }
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

            if (m_count == 0 || nsSet.count == 0 || localName == null || (!isStatic && m_linksForInstQualified == null)) {
                trait = null;
                return BindStatus.NOT_FOUND;
            }

            bool found = false;
            bool ambiguousMatch = false;
            trait = null;

            int hash = localName.GetHashCode() & HASH_CODE_MASK;
            Trait[] slots = m_slots;
            Link[] links = isStatic ? m_linksForStaticUnqalified : m_linksForInstUnqualified;

            int curIndex = links[hash % links.Length].chainHead;

            while (curIndex != END_OF_CHAIN) {
                ref Link link = ref links[curIndex];

                if (link.hash != hash) {
                    curIndex = link.next;
                    continue;
                }

                Trait currentTrait = slots[curIndex];
                ref readonly QName currentTraitName = ref currentTrait.name;

                if (currentTraitName.localName != localName || !nsSet.contains(currentTraitName.ns)) {
                    curIndex = link.next;
                    continue;
                }

                if (found) {
                    // If two conflicting traits are declared on the same class it is definitely
                    // an ambiguous match. This is also true for global traits.
                    if (currentTrait.declaringClass == trait.declaringClass)
                        return BindStatus.AMBIGUOUS;

                    if (m_isSealed) {
                        // If this table is sealed then the traits are always ordered from
                        // more derived to least derived class. This means that the match is definitely
                        // ambiguous if `trait` is declared on a class that is not derived from the
                        // declaring class of `currentTrait`. This can happen only with interfaces.
                        // For non-interface classes, either both the traits are declared by the same
                        // class (which was already checked) or `currentTrait` is declared on a class
                        // that `trait` derives from, in which case we terminate the search early and
                        // return `trait`.

                        if (!currentTrait.declaringClass.isInterface)
                            break;

                        if (!trait.declaringClass.canAssignTo(currentTrait.declaringClass))
                            return BindStatus.AMBIGUOUS;
                    }
                    else {
                        // For tables which are not sealed, if `currentTrait` is from a more derived
                        // class, set the result to that trait and reset the ambiguous match flag.
                        if (currentTrait.declaringClass.canAssignTo(trait.declaringClass)) {
                            trait = currentTrait;
                            ambiguousMatch = false;
                        }
                        else if (!trait.declaringClass.canAssignTo(currentTrait.declaringClass)) {
                            ambiguousMatch = true;
                        }
                    }
                }
                else {
                    trait = currentTrait;
                    found = true;
                }

                curIndex = link.next;
            }

            if (ambiguousMatch)
                return BindStatus.AMBIGUOUS;

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
            int hash = name.GetHashCode() & HASH_CODE_MASK;
            bool isStatic = trait.isStatic;

            Link[] links = isStatic ? m_linksForStaticQualified : m_linksForInstQualified;
            if (links == null)
                return false;

            int chain = hash % links.Length;
            int curIndex = links[chain].chainHead;

            while (curIndex != END_OF_CHAIN) {
                ref Link link = ref links[curIndex];

                if (link.hash != hash) {
                    curIndex = link.next;
                    continue;
                }

                Trait currentTrait = m_slots[curIndex];
                if (currentTrait.name != name) {
                    curIndex = links[curIndex].next;
                    continue;
                }

                if (!allowMergeProperties
                    || trait.traitType != TraitType.PROPERTY
                    || currentTrait.traitType != TraitType.PROPERTY)
                {
                    return false;
                }

                // If allowMergeProperties is set to true, and if both the conflicting traits are properties,
                // check if they can be merged. (This happens, for example, when a parent class defines
                // one accessor of a property and the other accessor is supplied by a subclass, or a derived
                // class overrides one of the accessors of a property defined on a base class).

                PropertyTrait mergedProp = PropertyTrait.tryMerge((PropertyTrait)currentTrait, (PropertyTrait)trait, m_class);

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
            int hash = name.GetHashCode() & HASH_CODE_MASK;
            bool isStatic = oldTrait.isStatic;

            Link[] links = isStatic ? m_linksForStaticQualified : m_linksForInstQualified;
            if (links == null)
                return;

            int chain = hash % links.Length;
            int curIndex = links[chain].chainHead;

            while (curIndex != END_OF_CHAIN) {
                ref Link link = ref links[curIndex];

                if (link.hash == hash) {
                    ref Trait currentTrait = ref m_slots[curIndex];
                    if (currentTrait.name == name) {
                        currentTrait = newTrait;
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
            _resetLinks(recalculateHashes: false);
        }

        /// <summary>
        /// Resets all the links in the table. Call this after modifying any slots.
        /// </summary>
        /// <param name="recalculateHashes">If this is set to true, recompute the hash codes from the
        /// trait names, otherwise compute the chains using the existing hash codes.</param>
        private void _resetLinks(bool recalculateHashes) {
            int length = m_slots.Length;
            Link defaultLink = new Link {chainHead = END_OF_CHAIN};

            Link[] newLinksForStaticQualified = new Link[length];
            Link[] newLinksForStaticUnqualified = new Link[length];
            Link[] newLinksForInstQualified = null;
            Link[] newLinksForInstUnqualified = null;

            newLinksForStaticQualified.AsSpan().Fill(defaultLink);
            newLinksForStaticUnqualified.AsSpan().Fill(defaultLink);

            if (m_linksForInstQualified != null) {
                newLinksForInstQualified = new Link[length];
                newLinksForInstUnqualified = new Link[length];

                newLinksForInstQualified.AsSpan().Fill(defaultLink);
                newLinksForInstUnqualified.AsSpan().Fill(defaultLink);
            }

            ReadOnlySpan<Trait> slots = m_slots.AsSpan(0, m_count);

            for (int i = 0; i < slots.Length; i++) {
                bool traitIsStatic = slots[i].isStatic;

                int qualifiedHash;
                int unqualifiedHash;

                if (recalculateHashes) {
                    qualifiedHash = slots[i].name.GetHashCode() & HASH_CODE_MASK;
                    unqualifiedHash = slots[i].name.localName.GetHashCode() & HASH_CODE_MASK;
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
                    newLinksForStaticQualified[i].hash = qualifiedHash;
                    newLinksForStaticUnqualified[i].hash = unqualifiedHash;

                    newLinksForStaticQualified[i].next = newLinksForStaticQualified[qualifiedChain].chainHead;
                    newLinksForStaticUnqualified[i].next = newLinksForStaticUnqualified[unqualifiedChain].chainHead;

                    newLinksForStaticQualified[qualifiedChain].chainHead = i;
                    newLinksForStaticUnqualified[unqualifiedChain].chainHead = i;
                }
                else {
                    newLinksForInstQualified[i].hash = qualifiedHash;
                    newLinksForInstUnqualified[i].hash = unqualifiedHash;

                    newLinksForInstQualified[i].next = newLinksForInstQualified[qualifiedChain].chainHead;
                    newLinksForInstUnqualified[i].next = newLinksForInstUnqualified[unqualifiedChain].chainHead;

                    newLinksForInstQualified[qualifiedChain].chainHead = i;
                    newLinksForInstUnqualified[unqualifiedChain].chainHead = i;
                }
            }

            m_linksForInstQualified = newLinksForInstQualified;
            m_linksForInstUnqualified = newLinksForInstUnqualified;
            m_linksForStaticQualified = newLinksForStaticQualified;
            m_linksForStaticUnqalified = newLinksForStaticUnqualified;
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

                // Don't merge static traits as they are not inherited.
                if (parentTrait.isStatic)
                    continue;

                if (tryAddTrait(parentTrait, allowMergeProperties: true))
                    continue;

                tryGetTrait(parentTrait.name, isStatic: false, out Trait conflictingTrait);

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

                    var newProp = new PropertyTrait(
                        conflictProp.name, m_class,
                        m_class.applicationDomain,
                        isStatic: false,
                        conflictProp.getter ?? parentProp.getter,
                        conflictProp.setter ?? parentProp.setter
                    );

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
                    conflictingTrait.name.ToString(),
                    m_class.name.ToString()
                );
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
            if (conflictingTrait.traitType == TraitType.METHOD)
                return parentTrait.traitType == TraitType.METHOD && ((MethodTrait)conflictingTrait).isOverride;

            if (conflictingTrait.traitType == TraitType.PROPERTY) {
                if (parentTrait.traitType != TraitType.PROPERTY)
                    return false;

                PropertyTrait conflictingProp = (PropertyTrait)conflictingTrait;
                PropertyTrait parentProp = (PropertyTrait)parentTrait;

                if (parentProp.getter != null && (conflictingProp.getter == null || !conflictingProp.getter.isOverride))
                    return false;

                if (parentProp.setter != null && (conflictingProp.setter == null || !conflictingProp.setter.isOverride))
                    return false;

                return true;
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

                if (parentTrait.declaringClass != parentTraitTable.m_class) {
                    if (parentTraitTable.m_isSealed) {
                        // Since we are iterating in reverse order, we can stop when we find an
                        // inherited trait if the parent table is sealed (which is usually the case).
                        break;
                    }
                    continue;
                }

                // Don't merge static traits as they are not inherited.
                if (parentTrait.isStatic)
                    continue;

                if (tryAddTrait(parentTrait, allowMergeProperties: true))
                    continue;

                tryGetTrait(parentTrait.name, isStatic: false, out Trait conflictingTrait);

                if (!_canMergeConflictingInterfaceTraits(parentTrait, conflictingTrait)) {
                    m_isCorrupted = true;

                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__INTERFACE_COMMON_TRAIT_SIG_MISMATCH,
                        conflictingTrait.name.ToString(),
                        m_class.name.ToString(),
                        parentTrait.declaringClass.name.ToString(),
                        conflictingTrait.declaringClass.name.ToString()
                    );
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
                MethodTrait firstMethod = (MethodTrait)firstTrait;
                MethodTrait secondMethod = (MethodTrait)secondTrait;

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
                    if (param1.type != param2.type || param1.isOptional != param2.isOptional || param1.hasDefault != param2.hasDefault)
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

            if (m_linksForInstQualified == null) {
                // This table does not contain any instance traits, so no need of reordering.
                Trait[] newSlots = new Trait[m_count];
                (new ReadOnlySpan<Trait>(m_slots, 0, m_count)).CopyTo(newSlots);
                m_slots = newSlots;

                m_staticTraitsBeginIndex = 0;
                m_declaredInstanceTraitsBeginIndex = 0;

                return;
            }

            // Partition the traits into instance and static traits, with the instance traits
            // first. Then sort the instance traits so that base class traits are before derived
            // class traits.

            var newTraitList = new DynamicArray<Trait>(slotCount, true);
            int instanceTraitsEndIndex = 0;
            int staticTraitsBeginIndex = slotCount;

            for (int i = 0; i < slotCount; i++) {
                Trait trait = m_slots[i];
                if (trait.isStatic) {
                    staticTraitsBeginIndex--;
                    newTraitList[staticTraitsBeginIndex] = trait;
                }
                else {
                    newTraitList[instanceTraitsEndIndex] = trait;
                    instanceTraitsEndIndex++;
                }
            }

            ReferenceDictionary<Class, int> classOrderMap = _computeClassTopologicalOrdering();

            DataStructureUtil.sortSpan(
                newTraitList.asSpan(0, instanceTraitsEndIndex),
                (t1, t2) => classOrderMap[t1.declaringClass] - classOrderMap[t2.declaringClass]
            );

            int declInstTraitsBeginIndex = staticTraitsBeginIndex;
            while (declInstTraitsBeginIndex > 0
                && newTraitList[declInstTraitsBeginIndex - 1].declaringClass == m_class)
            {
                declInstTraitsBeginIndex--;
            }

            m_staticTraitsBeginIndex = staticTraitsBeginIndex;
            m_declaredInstanceTraitsBeginIndex = declInstTraitsBeginIndex;

            m_slots = newTraitList.toArray();
            _resetLinks(recalculateHashes: true);

            m_isSealed = true;
        }

        /// <summary>
        /// Computes the topological ordering of the inheritance dag of the class or interface
        /// to which this <see cref="ClassTraitTable"/> belongs. This is used to order traits
        /// when sealing the table.
        /// </summary>
        /// <returns>A map from classes to their order numbers. A class will have a lower order number
        /// than any class that extends from it (directly or transitively). The order numbers are
        /// guaranteed to be non-negative.</returns>
        private ReferenceDictionary<Class, int> _computeClassTopologicalOrdering() {
            var dict = new ReferenceDictionary<Class, int>();

            if (!m_class.isInterface) {
                // If this is not an interface then the inheritance dag is a linear chain and
                // the computation of the order numbers is trivial.

                Class currentClass = m_class;
                int chainDepth = 0;

                while (currentClass != null) {
                    chainDepth++;
                    currentClass = currentClass.parent;
                }

                currentClass = m_class;
                int currentIndex = chainDepth - 1;

                while (currentClass != null) {
                    dict[currentClass] = currentIndex;
                    currentIndex--;
                    currentClass = currentClass.parent;
                }
            }
            else {
                // For an interface, we need to do a DFS over the inheritance dag to compute
                // the order numbers.

                var baseInterfaces = m_class.getImplementedInterfaces();
                int currentIndex = 0;

                for (int i = 0; i < baseInterfaces.length; i++)
                    walk(baseInterfaces[i]);

                dict[m_class] = currentIndex;

                void walk(Class currentInterface) {
                    if (dict.containsKey(currentInterface))
                        return;

                    for (int i = 0; i < baseInterfaces.length; i++) {
                        if (currentInterface != baseInterfaces[i] && currentInterface.canAssignTo(baseInterfaces[i]))
                            walk(baseInterfaces[i]);
                    }

                    dict[currentInterface] = currentIndex;
                    currentIndex++;
                }
            }

            return dict;
        }

        /// <summary>
        /// Appends all the traits in the table to the list <paramref name="outList"/>.
        /// </summary>
        /// <param name="outList">A <see cref="DynamicArray{T}"/> to which to append the
        /// traits.</param>
        public void getTraits(ref DynamicArray<Trait> outList) {
            m_slots.CopyTo(outList.addDefault(m_count));
        }

        /// <summary>
        /// Gets the start and end indices of the range in the <see cref="m_slots"/> array where the
        /// traits in the given scope(s) will be found.
        /// </summary>
        ///
        /// <param name="scopes">A set of bit flags from the <see cref="TraitScope"/> enumeration.</param>
        /// <param name="startIndex">The first index (inclusive) of the range in the <see cref="m_slots"/>
        /// array in which the traits will be found.</param>
        /// <param name="endIndex">The last index (exclusive) of the range in the <see cref="m_slots"/>
        /// arrayin which the traits will be found.</param>
        /// <param name="isStrictRange">If this is true, the range defined by [startIndex, endIndex)
        /// is guaranteed to contain only traits whose scope matches the <paramref name="scopes"/>
        /// flags. Otherwise, the range may contain other traits and this must be checked during iteration.</param>
        private void _getTraitSearchRange(TraitScope scopes, out int startIndex, out int endIndex, out bool isStrictRange) {
            scopes &= TraitScope.ALL;

            if (scopes == 0) {
                // Always empty.
                (startIndex, endIndex, isStrictRange) = (0, 0, true);
            }
            else if (!m_isSealed) {
                // If this table is not sealed then we have to search the whole table (because it is not sorted).
                (startIndex, endIndex, isStrictRange) = (0, m_count, false);
            }
            else if ((scopes & TraitScope.INSTANCE) == 0) {
                // Only static traits
                (startIndex, endIndex, isStrictRange) = (m_staticTraitsBeginIndex, m_count, true);
            }
            else if ((scopes & TraitScope.STATIC) == 0) {
                // Only instance traits
                isStrictRange = true;
                startIndex = ((scopes & TraitScope.INSTANCE_INHERITED) != 0) ? 0 : m_declaredInstanceTraitsBeginIndex;
                endIndex = ((scopes & TraitScope.INSTANCE_DECLARED) != 0) ? m_staticTraitsBeginIndex : m_declaredInstanceTraitsBeginIndex;
            }
            else {
                // Static and instance traits
                if ((scopes & TraitScope.INSTANCE) == TraitScope.INSTANCE_INHERITED) {
                    // Since the instance-inherited and static traits are not contiguous, fall
                    // back to a full table search.
                    (startIndex, endIndex, isStrictRange) = (0, m_count, false);
                }
                else {
                    isStrictRange = true;
                    endIndex = m_count;
                    startIndex = ((scopes & TraitScope.INSTANCE_INHERITED) != 0) ? 0 : m_declaredInstanceTraitsBeginIndex;
                }
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
            _getTraitSearchRange(scopes, out int startIndex, out int endIndex, out bool isStrictRange);
            var searchRange = new ReadOnlySpan<Trait>(m_slots, startIndex, endIndex - startIndex);

            if (isStrictRange) {
                for (int i = 0; i < searchRange.Length; i++) {
                    Trait trait = searchRange[i];
                    if ((trait.traitType & kinds) != 0)
                        outList.add((T)trait);
                }
            }
            else {
                for (int i = 0; i < searchRange.Length; i++) {
                    Trait trait = searchRange[i];
                    if ((trait.traitType & kinds) == 0)
                        continue;

                    TraitScope traitScope;

                    if (trait.isStatic)
                        traitScope = TraitScope.STATIC;
                    else if (trait.declaringClass == m_class)
                        traitScope = TraitScope.INSTANCE_DECLARED;
                    else
                        traitScope = TraitScope.INSTANCE_INHERITED;

                    if ((traitScope & scopes) != 0)
                        outList.add((T)trait);
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
            _getTraitSearchRange(scopes, out int startIndex, out int endIndex, out bool isStrictRange);

            if (isStrictRange) {
                // We can return a read-only view over the internal slots array in this
                // case, so we do not have to allocate a new array.
                return new ReadOnlyArrayView<Trait>(m_slots, startIndex, endIndex - startIndex);
            }

            var list = new DynamicArray<Trait>();
            getTraits(TraitType.ALL, scopes, ref list);

            return list.asReadOnlyArrayView();
        }

    }
}
