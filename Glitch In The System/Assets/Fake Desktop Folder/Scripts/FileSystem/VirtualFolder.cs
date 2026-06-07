using System;
using System.Collections.Generic;

namespace GlitchInTheSystem.FileSystem
{
    /// <summary>
    /// A container node. Owns an ordered list of child ids.
    /// The manager resolves ids → nodes; the folder itself stays thin.
    /// </summary>
    [Serializable]
    public sealed class VirtualFolder : VirtualNode
    {
        /// <summary>Ordered child node ids. May be files or folders.</summary>
        public List<string> ChildIds = new List<string>();

        internal void AddChild(string id)
        {
            if (!ChildIds.Contains(id))
                ChildIds.Add(id);
        }

        internal void RemoveChild(string id) => ChildIds.Remove(id);
    }
}
