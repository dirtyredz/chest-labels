using System;
using System.Collections.Generic;
using System.IO;

namespace ChestLabels.Tests
{
    /// <summary>
    /// Plain console test runner. No test framework, so it runs anywhere with just the SDK
    /// and needs no package restore.
    /// </summary>
    internal static class Program
    {
        private const string ChestA = "bd7e9591-c12a-a9df-791a-cbb4c6fb89e6"; // the real chest
        private const string ChestB = "7a677ccb-3fd3-fcd1-66b3-93fab37b870d"; // house storage
        private const string SaveA = "3de42d85-edbc-bae3-1d57-0e3666fb43a7";  // the real save
        private const string SaveB = "11111111-2222-3333-4444-555555555555";

        private static int failures;
        private static int checks;

        private static int Main()
        {
            var root = Path.Combine(Path.GetTempPath(), "ChestLabelsTests-" + Guid.NewGuid().ToString("N"));

            try
            {
                Run("set and get a label", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    IsTrue(store.Set(ChestA, "Ores and Gems"), "Set reports a change");
                    Equal("Ores and Gems", store.Get(ChestA), "Get returns the label");
                    Equal(1, store.Count, "one entry stored");
                });

                Run("labels survive a save/load round trip", () =>
                {
                    var dir = NewDir(root);
                    var store = Fresh(dir, SaveA);
                    store.Set(ChestA, "Wood");
                    store.Set(ChestB, "Seeds");
                    store.Save();

                    var reopened = new LabelStore(dir);
                    reopened.LoadForSave(SaveA);
                    Equal("Wood", reopened.Get(ChestA), "first label persisted");
                    Equal("Seeds", reopened.Get(ChestB), "second label persisted");
                    Equal(2, reopened.Count, "both entries persisted");
                });

                Run("chest GUID lookup is case insensitive", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA.ToUpperInvariant(), "Upper");
                    Equal("Upper", store.Get(ChestA.ToLowerInvariant()), "case does not matter");
                });

                Run("blank label removes the entry", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA, "Temporary");
                    IsTrue(store.Set(ChestA, "   "), "clearing reports a change");
                    IsNull(store.Get(ChestA), "label is gone");
                    Equal(0, store.Count, "no residue left behind");
                });

                Run("labels are trimmed and length capped", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA, "   padded   ");
                    Equal("padded", store.Get(ChestA), "whitespace trimmed");

                    store.Set(ChestB, new string('x', LabelStore.MaxLabelLength + 40));
                    Equal(LabelStore.MaxLabelLength, store.Get(ChestB).Length, "length capped");
                });

                Run("newlines are flattened", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA, "two\r\nlines");
                    IsTrue(!store.Get(ChestA).Contains("\n"), "no newline survives");
                });

                Run("setting the same label twice reports no change", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA, "Same");
                    IsTrue(!store.Set(ChestA, "Same"), "second set is a no-op");
                });

                Run("saves are isolated from each other", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA, "From save A");
                    store.Save();

                    store.LoadForSave(SaveB);
                    IsNull(store.Get(ChestA), "save B does not see save A's labels");

                    store.Set(ChestA, "From save B");
                    store.Save();

                    store.LoadForSave(SaveA);
                    Equal("From save A", store.Get(ChestA), "save A is unchanged");
                });

                Run("switching saves flushes pending edits", () =>
                {
                    var dir = NewDir(root);
                    var store = Fresh(dir, SaveA);
                    store.Set(ChestA, "Unsaved when switching");
                    store.LoadForSave(SaveB); // no explicit Save() first

                    var reopened = new LabelStore(dir);
                    reopened.LoadForSave(SaveA);
                    Equal("Unsaved when switching", reopened.Get(ChestA), "edit was flushed");
                });

                Run("removing a missing chest is a no-op", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    IsTrue(!store.Remove(ChestA), "nothing to remove");
                });

                Run("remove deletes the entry", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA, "Doomed");
                    IsTrue(store.Remove(ChestA), "remove reports a change");
                    IsNull(store.Get(ChestA), "entry is gone");
                });

                Run("a corrupt file is quarantined, not deleted", () =>
                {
                    var dir = Path.Combine(root, "corrupt");
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, SaveA + ".json");
                    File.WriteAllText(path, "{ this is not json ]");

                    var store = new LabelStore(dir);
                    store.LoadForSave(SaveA);

                    Equal(0, store.Count, "starts empty after corruption");
                    IsTrue(store.LastLoadRecoveredFromCorruption, "corruption was reported");
                    IsTrue(Directory.GetFiles(dir, SaveA + ".json.corrupt-*").Length == 1,
                        "original file was preserved with a .corrupt suffix");
                });

                Run("missing file loads as empty", () =>
                {
                    var store = new LabelStore(Path.Combine(root, "does-not-exist-yet"));
                    store.LoadForSave(SaveA);
                    Equal(0, store.Count, "no file means no labels");
                });

                Run("null and blank guids are ignored", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    IsTrue(!store.Set(null, "x"), "null guid rejected");
                    IsTrue(!store.Set("   ", "x"), "blank guid rejected");
                    IsNull(store.Get(null), "null guid lookup is null");
                    Equal(0, store.Count, "nothing stored");
                });

                Run("sidecar path is scoped by save guid", () =>
                {
                    var dir = NewDir(root);
                    var store = Fresh(dir, SaveA);
                    Equal(Path.Combine(dir, SaveA + ".json"), store.CurrentPath, "path shape");
                });

                Run("written file is readable JSON", () =>
                {
                    var store = Fresh(NewDir(root), SaveA);
                    store.Set(ChestA, "Readable");
                    store.Save();

                    var json = File.ReadAllText(store.CurrentPath);
                    IsTrue(json.Contains(ChestA), "guid present in file");
                    IsTrue(json.Contains("Readable"), "label present in file");
                    IsTrue(!File.Exists(store.CurrentPath + ".tmp"), "temp file cleaned up");
                });
            }
            finally
            {
                TryDelete(root);
            }

            Console.WriteLine();
            Console.WriteLine(failures == 0
                ? $"PASS - {checks} checks across all tests"
                : $"FAIL - {failures} failed check(s) of {checks}");

            return failures == 0 ? 0 : 1;
        }

        /// <summary>A private directory per test, so tests cannot see each other's files.</summary>
        private static string NewDir(string root) => Path.Combine(root, Guid.NewGuid().ToString("N"));

        private static LabelStore Fresh(string dir, string saveGuid)
        {
            var store = new LabelStore(dir);
            store.LoadForSave(saveGuid);
            return store;
        }

        private static void Run(string name, Action body)
        {
            try
            {
                body();
                Console.WriteLine($"  ok   {name}");
            }
            catch (Exception e)
            {
                failures++;
                Console.WriteLine($"  FAIL {name}");
                Console.WriteLine($"       {e.Message}");
            }
        }

        private static void Equal<T>(T expected, T actual, string what)
        {
            checks++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected <{expected}>, got <{actual}>");
            }
        }

        private static void IsTrue(bool condition, string what)
        {
            checks++;
            if (!condition)
            {
                throw new Exception($"{what}: expected true");
            }
        }

        private static void IsNull(object value, string what)
        {
            checks++;
            if (value != null)
            {
                throw new Exception($"{what}: expected null, got <{value}>");
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Temp cleanup is best effort.
            }
        }
    }
}
