using System;

namespace GlitchInTheSystem.FileSystem
{
    /// <summary>
    /// A leaf node. Carries a FileType and an optional string payload.
    ///
    /// Payload convention by type:
    ///   TextFile   — raw text content
    ///   StickyNote — raw text content (colour stored in Metadata["color"])
    ///   Image      — base64-encoded PNG, or an asset path (Metadata["assetPath"])
    ///   Shortcut   — target node id (Metadata["targetId"])
    ///   Unknown    — unused / reserved
    /// </summary>
    [Serializable]
    public sealed class VirtualFile : VirtualNode
    {
        public FileType Type;

        /// <summary>
        /// Optional inline payload. For large blobs (images) prefer Metadata["assetPath"].
        /// Kept as a single string so JsonUtility can serialise it without converters.
        /// </summary>
        public string Payload;

        /// <summary>Byte length hint. 0 means unknown/not set.</summary>
        public long SizeBytes;
    }
}
