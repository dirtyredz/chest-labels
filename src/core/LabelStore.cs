using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ChestLabels
{
    /// <summary>
    /// Save-scoped store of chest labels, persisted to a sidecar JSON file.
    ///
    /// Deliberately free of Unity and BepInEx types so it can be exercised by the test
    /// project without a running game. See research/02-save-format.md for why the file is
    /// keyed by save GUID and why labels are NOT written into the game's own save.
    /// </summary>
    public sealed class LabelStore
    {
        /// <summary>Longest label we will store. Keeps the chest header from overflowing.</summary>
        public const int MaxLabelLength = 48;

        private readonly string rootDirectory;
        private Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool dirty;

        public LabelStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("rootDirectory is required", nameof(rootDirectory));
            }
            this.rootDirectory = rootDirectory;
        }

        /// <summary>Save GUID currently loaded, or null if none.</summary>
        public string SaveGuid { get; private set; }

        /// <summary>Path of the sidecar file for the loaded save, or null if none.</summary>
        public string CurrentPath =>
            SaveGuid == null ? null : Path.Combine(rootDirectory, SaveGuid + ".json");

        public int Count => labels.Count;

        /// <summary>True when a load hit an unreadable file and quarantined it.</summary>
        public bool LastLoadRecoveredFromCorruption { get; private set; }

        /// <summary>
        /// Point the store at a save. Flushes any pending changes for the previous save
        /// first, so switching saves in-session cannot lose edits.
        /// </summary>
        public void LoadForSave(string saveGuid)
        {
            if (string.IsNullOrWhiteSpace(saveGuid))
            {
                throw new ArgumentException("saveGuid is required", nameof(saveGuid));
            }

            var normalized = Normalize(saveGuid);
            if (normalized == SaveGuid)
            {
                return;
            }

            Save();

            SaveGuid = normalized;
            labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dirty = false;
            LastLoadRecoveredFromCorruption = false;

            var path = CurrentPath;
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (loaded != null)
                {
                    foreach (var pair in loaded)
                    {
                        var key = Normalize(pair.Key);
                        var value = Clean(pair.Value);
                        if (key.Length > 0 && value != null)
                        {
                            labels[key] = value;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Never let a bad sidecar take the game down, and never silently delete the
                // player's data - move it aside so it can be recovered by hand.
                Quarantine(path);
                labels.Clear();
                LastLoadRecoveredFromCorruption = true;
            }
        }

        public string Get(string chestGuid)
        {
            if (string.IsNullOrWhiteSpace(chestGuid))
            {
                return null;
            }
            return labels.TryGetValue(Normalize(chestGuid), out var label) ? label : null;
        }

        public bool Has(string chestGuid) => Get(chestGuid) != null;

        /// <summary>
        /// Set a label. A null/blank label removes the entry rather than storing an empty
        /// string, so clearing a name leaves no residue in the file.
        /// Returns true if anything changed.
        /// </summary>
        public bool Set(string chestGuid, string label)
        {
            if (string.IsNullOrWhiteSpace(chestGuid))
            {
                return false;
            }

            var key = Normalize(chestGuid);
            var value = Clean(label);

            if (value == null)
            {
                return Remove(key);
            }

            if (labels.TryGetValue(key, out var existing) && existing == value)
            {
                return false;
            }

            labels[key] = value;
            dirty = true;
            return true;
        }

        public bool Remove(string chestGuid)
        {
            if (string.IsNullOrWhiteSpace(chestGuid))
            {
                return false;
            }

            if (!labels.Remove(Normalize(chestGuid)))
            {
                return false;
            }

            dirty = true;
            return true;
        }

        /// <summary>
        /// Flush to disk if anything changed. Writes to a temp file and moves it into place
        /// so an interrupted write cannot leave a half-written sidecar.
        /// </summary>
        public void Save()
        {
            if (!dirty || SaveGuid == null)
            {
                return;
            }

            var path = CurrentPath;
            Directory.CreateDirectory(rootDirectory);

            // Sorted purely so hand-editing and diffing the file is pleasant.
            var ordered = labels.OrderBy(p => p.Key, StringComparer.Ordinal)
                                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

            var json = JsonConvert.SerializeObject(ordered, Formatting.Indented);

            var temp = path + ".tmp";
            File.WriteAllText(temp, json);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);

            dirty = false;
        }

        /// <summary>Snapshot of the current labels, for logging and diagnostics.</summary>
        public IReadOnlyDictionary<string, string> Snapshot() =>
            new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase);

        private static string Normalize(string guid) => guid.Trim().ToLowerInvariant();

        /// <summary>Trim, collapse newlines, cap length. Returns null for "no label".</summary>
        private static string Clean(string label)
        {
            if (label == null)
            {
                return null;
            }

            var cleaned = label.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (cleaned.Length == 0)
            {
                return null;
            }

            if (cleaned.Length > MaxLabelLength)
            {
                cleaned = cleaned.Substring(0, MaxLabelLength).TrimEnd();
            }

            return cleaned;
        }

        private static void Quarantine(string path)
        {
            try
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Move(path, path + ".corrupt-" + stamp);
            }
            catch (Exception)
            {
                // If even the rename fails there is nothing useful left to do; the caller
                // continues with an empty store rather than crashing the game.
            }
        }
    }
}
