namespace ChestLabels
{
    /// <summary>
    /// Turns a <see cref="Chest"/> into the stable, normalized key its label is stored under.
    ///
    /// The key is <c>GridObjectPersistence.Guid</c>, trimmed and lower-cased so it matches
    /// however the sidecar file happens to be cased (see <see cref="LabelStore"/>). This was
    /// duplicated verbatim in the chest patches and the hover label; it lives here so the
    /// "how do I identify a chest" rule has a single owner and the two call sites cannot drift.
    /// </summary>
    internal static class ChestIdentity
    {
        /// <summary>
        /// The normalized label key for a chest, or null if the chest has no usable GUID
        /// (e.g. it is null, or its persistence component is gone during teardown).
        /// </summary>
        internal static string GuidOf(Chest chest)
        {
            var persistence = chest?.GridObjectPersistence;
            if (persistence == null)
            {
                return null;
            }

            var guid = persistence.Guid.ToString();
            return string.IsNullOrWhiteSpace(guid) ? null : guid.Trim().ToLowerInvariant();
        }
    }
}
