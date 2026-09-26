using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidCatsBestiary
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BestiaryPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "lucidcats.bestiary";
        public const string PluginName = "Bestiary";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> UnlockAllForTesting;
        internal static ConfigEntry<float> ViewerSpinSpeed;
        internal static ConfigEntry<float> ViewerLightIntensity;

        private void Awake()
        {
            Log = Logger;

            ConfigEntry<bool> resetOnStart = Config.Bind(
                "Debug", "ResetBestiaryOnStart", false,
                "If true, the bestiary is wiped every time the game starts. Useful for testing.");

            UnlockAllForTesting = Config.Bind(
                "Debug", "UnlockAllForTesting", false,
                "If true, every monster is shown as unlocked (nothing is saved). Useful to preview the bestiary.");

            ViewerSpinSpeed = Config.Bind(
                "Viewer", "SpinSpeed", 25f,
                "How fast the 3D models turn on their own, in degrees per second. 0 = no spinning.");

            ViewerLightIntensity = Config.Bind(
                "Viewer", "LightIntensity", 1.5f,
                "Brightness of the lights used in the 3D viewer.");

            string savePath = Path.Combine(Paths.ConfigPath, "LucidCatsBestiary.txt");
            BestiaryData.Load(savePath, resetOnStart.Value);

            Harmony.CreateAndPatchAll(typeof(SightPatch), PluginGuid);

            SceneManager.sceneLoaded += OnSceneLoaded;

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. " +
                        $"Unlocked {BestiaryData.UnlockedCount}/{BestiaryData.Monsters.Length}: " +
                        $"{BestiaryData.UnlockedListText()}");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
                BestiaryMenu.Create(scene);
        }
    }

    /// <summary>
    /// Keeps track of which monsters have been unlocked and saves them to a small text file.
    /// </summary>
    internal static class BestiaryData
    {
        public static readonly string[] Monsters = BuildIdList();

        private static readonly HashSet<string> Unlocked = new HashSet<string>();
        private static string savePath;

        public static int UnlockedCount => Unlocked.Count;

        public static bool IsUnlocked(string id)
        {
            if (BestiaryPlugin.UnlockAllForTesting != null && BestiaryPlugin.UnlockAllForTesting.Value)
                return true;
            return Unlocked.Contains(id);
        }

        private static string[] BuildIdList()
        {
            var ids = new string[MonsterInfo.All.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = MonsterInfo.All[i].Id;
            return ids;
        }

        public static void Load(string path, bool reset)
        {
            savePath = path;
            Unlocked.Clear();

            if (reset)
            {
                Save();
                BestiaryPlugin.Log.LogWarning("Bestiary reset (ResetBestiaryOnStart is enabled).");
                return;
            }

            if (!File.Exists(savePath))
                return;

            try
            {
                foreach (string line in File.ReadAllLines(savePath))
                {
                    string id = line.Trim();
                    if (Array.IndexOf(Monsters, id) >= 0)
                        Unlocked.Add(id);
                }
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogError($"Could not read bestiary file: {e.Message}");
            }
        }

        public static void Unlock(string id)
        {
            if (Array.IndexOf(Monsters, id) < 0)
            {
                BestiaryPlugin.Log.LogWarning($"Saw an unknown monster: '{id}'");
                return;
            }

            if (!Unlocked.Add(id))
            {
                BestiaryPlugin.Log.LogDebug($"Saw {id} (already unlocked)");
                return;
            }

            Save();
            BestiaryPlugin.Log.LogInfo($"NEW MONSTER UNLOCKED: {id} ({UnlockedCount}/{Monsters.Length})");
        }

        /// <summary>Turns "DreamEnemy_Flower Eye(Clone)" into "Flower Eye".</summary>
        public static string IdFromObjectName(string objectName)
        {
            const string prefix = "DreamEnemy_";
            string name = objectName.Replace("(Clone)", string.Empty).Trim();
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                name = name.Substring(prefix.Length);
            return name.Trim();
        }

        public static string UnlockedListText()
        {
            if (Unlocked.Count == 0)
                return "none yet";

            var list = new List<string>();
            foreach (string id in Monsters)
                if (Unlocked.Contains(id))
                    list.Add(id);
            return string.Join(", ", list);
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (string id in Monsters)
                    if (Unlocked.Contains(id))
                        lines.Add(id);
                File.WriteAllLines(savePath, lines);
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogError($"Could not save bestiary file: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The game calls DreamEnemy.TryPlayFirstSightFeedback on your own PC the moment
    /// you lay eyes on a monster. We hook in right after it to unlock that monster.
    /// </summary>
    [HarmonyPatch(typeof(DreamEnemy), nameof(DreamEnemy.TryPlayFirstSightFeedback))]
    internal static class SightPatch
    {
        private static void Postfix(DreamEnemy __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                string id = BestiaryData.IdFromObjectName(__instance.name);
                BestiaryData.Unlock(id);
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogError($"Error while unlocking a monster: {e}");
            }
        }
    }
}
