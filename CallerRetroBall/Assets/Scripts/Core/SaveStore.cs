using System;
using System.IO;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Local-only persistence for the career (no accounts, no network, no analytics).
    /// Writes are atomic (temp file then replace) so a crash mid-save can't corrupt progress,
    /// and an unreadable file is kept as a timestamped backup before starting fresh.
    /// </summary>
    public static class SaveStore
    {
        public const string FileName = "career.json";

        public static string Folder => Application.persistentDataPath;
        public static string FilePath => Path.Combine(Folder, FileName);

        public static CareerSaveData Load(ContentCatalog catalog, out LoadStatus status)
        {
            string json = null;
            try
            {
                if (File.Exists(FilePath)) json = File.ReadAllText(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveStore] Could not read save: " + e.Message);
            }

            var data = SaveCodec.Decode(json, catalog, out status);
            if (status == LoadStatus.Recovered)
            {
                Backup("unreadable");
                Debug.LogWarning("[SaveStore] Save file was unreadable; started a fresh career. The old file was kept as a backup.");
                Save(data);
            }
            return data;
        }

        public static bool Save(CareerSaveData data)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, SaveCodec.Encode(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveStore] Save failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Deletes the save (Settings ▸ Reset). The next load starts a fresh career.</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveStore] Reset failed: " + e.Message);
            }
        }

        private static void Backup(string reason)
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                File.Copy(FilePath, Path.Combine(Folder, "career." + reason + "." + stamp + ".json"), true);
            }
            catch (Exception)
            {
                // Backing up is best effort.
            }
        }
    }
}
