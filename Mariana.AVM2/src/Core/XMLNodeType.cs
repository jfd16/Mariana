namespace Mariana.AVM2.Core {

    /// <summary>
    /// The possible node types for an XML object.
    /// </summary>
    /// <seealso cref="ASXML.nodeType"/> 
    public enum XMLNodeType {

        /// <summary>
        /// The XML object represents an attribute.
        /// </summary>
        ATTRIBUTE,

        /// <summary>
        /// The XML object represents a comment.
        /// </summary>
        COMMENT,

        /// <summary>
        /// The XML object represents an element.
        /// </summary>
        ELEMENT,

        /// <summary>
        /// The XML object represents an XML processing instruction.
        /// </summary>
        PROCESSING_INSTRUCTION,

        /// <summary>
        /// The XML object represents a text node.
        /// </summary>
        TEXT,

        /// <summary>
        /// The XML object represents a CDATA text node.
        /// </summary>
        CDATA,

    }
}