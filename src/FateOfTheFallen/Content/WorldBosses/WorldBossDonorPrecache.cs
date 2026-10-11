using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Opportunistically resolve the two late native world-boss donors
    /// asynchronously in stable ordinary gameplay (r90/r91), and now also
    /// on demand during first entry to a boss zone (r92).
    ///
    /// A targeted Resources.LoadAsync / loaded-AssetBundle LoadAssetAsync
    /// creates the *existing* cached templates early, without spawning NPCs,
    /// changing scenes, or calling the synchronous Resources.LoadAll API.
    /// When Erenshor does not expose these prefabs at the candidate paths,
    /// the proven r31 in-zone recovery remains untouched.
    /// </summary>
    internal static class WorldBossDonorPrecache
    {
        private static readonly string[] Names =
        {
            VarkeshWorldSpawnRuntime.NativeTemplateNpcName,
            GorathWorldSpawnRuntime.NativeTemplateNpcName,
            GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName
        };

        private static readonly string[] Prefixes =
        {
            string.Empty, "NPC/", "NPCs/", "Enemies/", "Monsters/",
            "Creatures/", "Characters/", "Prefabs/", "Prefabs/NPC/",
            "Prefabs/NPCs/", "Prefabs/Enemies/", "Prefabs/Monsters/"
        };

        // r93: Exact Resources addresses from the installed Erenshor build's
        // globalgamemanagers ResourceManager index. They are NOT NPC display
        // names and must never be reconstructed from resourceRoot.name.
        // Both were independently found in the user's serialized asset index;
        // run-time LoadAsync + native template capture still needs live testing.
        // The Gorath *asset* path intentionally spells "Apparation", while
        // the live NPC name spells "Apparition".
        private static readonly string[] IndexedDonorPaths =
        {
            "npcs/abyssal/sivakayan templar",   // Varkesh / Abyssal
            "npcs/an apparation of sivakaya"   // Gorath / Bonepits
        };

        // r91: Persistent hints are verified from the actual Resources root
        // containing each donor during successful in-zone recovery. We never
        // infer the address from the native NPC display name alone.
        // Application.version scopes hints to a specific Erenshor build.
        private const string VarkeshHintKey = "FOTF.r91.WorldBoss.VarkeshResource.";
        private const string GorathHintKey = "FOTF.r91.WorldBoss.GorathResource.";

        private static bool _initialized;
        private static int _epoch;
        private static Coroutine _routine;

        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            ++_epoch;
            _routine = CoroutineHost.Start(PrecacheWhenPlayable(_epoch));
        }

        internal static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            ++_epoch;
            if (_routine != null) CoroutineHost.Stop(_routine);
            _routine = null;
        }

        private static bool IsSafeToPrecache(int epoch)
        {
            if (!_initialized || epoch != _epoch ||
                !WorldBossPersistencePolicy.CanExistInWorld ||
                GameData.PlayerControl == null ||
                GameData.PlayerControl.Myself == null ||
                GameData.PlayerStats == null ||
                GameData.PlayerStats.Myself == null)
                return false;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded ||
                string.Equals(scene.name, VarkeshWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scene.name, GorathWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase) ||
                CustomRaidLoader.IsRaidPocScene(scene))
                return false;

            return true;
        }

        private static bool VarkeshReady
        {
            get { return VarkeshWorldSpawnRuntime.NativeTemplateReady; }
        }

        private static bool GorathReady
        {
            get
            {
                return NativeWorldBossVisualRegistry.HasCachedTemplate(
                    GorathWorldSpawnRuntime.NativeTemplateNpcName,
                    GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName);
            }
        }

        /// <summary>
        /// Called only AFTER a successful scoped Resources.LoadAll recovery,
        /// and only on the root that first supplied a donor. Unity has no
        /// runtime AssetDatabase.GetAssetPath; validate the guessed Resources
        /// path against the exact loaded root before storing it. Invalid
        /// guesses are logged for diagnosis but are never persisted.
        /// </summary>
        internal static void TryRememberRecoveredRoot(
            GameObject resourceRoot, string resourceFolder, string donor)
        {
            if (resourceRoot == null || string.IsNullOrEmpty(resourceRoot.name) ||
                string.IsNullOrEmpty(donor) || string.IsNullOrEmpty(resourceFolder))
                return;

            string key = string.Equals(donor, "Varkesh", StringComparison.OrdinalIgnoreCase)
                ? HintKey(VarkeshHintKey) : HintKey(GorathHintKey);
            if (PlayerPrefs.HasKey(key)) return;

            string path = resourceFolder.TrimEnd('/') + "/" + resourceRoot.name;
            if (path.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase)) return;
            GameObject check = null;
            float began = Time.realtimeSinceStartup;
            try { check = Resources.Load<GameObject>(path); }
            catch (Exception ex)
            {
                Plugin.NativeLog.LogWarning("[WorldBossPrecache] donor root verification " +
                    "failed for [" + donor + "] path=[" + path + "]: " + ex.Message);
            }
            bool verified = check != null && check == resourceRoot;
            float elapsed = (Time.realtimeSinceStartup - began) * 1000f;
            Plugin.NativeLog.LogInfo(
                "[WorldBossPrecache] donor root discovery donor=[" + donor +
                "] root=[" + resourceRoot.name + "] candidate=[" + path +
                "] verified=[" + verified + "] checkMs=[" +
                elapsed.ToString("0.0") + "].");

            if (!verified) return;
            try
            {
                PlayerPrefs.SetString(key, path);
                PlayerPrefs.Save();
                Plugin.NativeLog.LogInfo("[WorldBossPrecache] persisted verified " +
                    "async resource hint donor=[" + donor + "] path=[" + path + "].");
            }
            catch (Exception ex)
            {
                Plugin.NativeLog.LogWarning("[WorldBossPrecache] could not save " +
                    "learned donor path [" + donor + "]: " + ex.Message);
            }
        }

        private static string HintKey(string prefix)
        {
            return prefix + Application.version;
        }

        // r93: ResourceManager addresses trump r91's guessed root-based
        // hints. Never call Resources.LoadAll in this routine. A direct launch
        // to Willowwatch/Vitheo invokes the same targeted async path via r92's
        // active-zone recovery before any legacy synchronous fallback.
        private static IEnumerator LoadLearnedPaths(int epoch, Func<bool> isAllowed)
        {
            string[] donorNames = { "Varkesh", "Gorath" };
            string[] hintPrefixes = { VarkeshHintKey, GorathHintKey };
            for (int i = 0; i < donorNames.Length; i++)
            {
                if (!isAllowed()) yield break;
                if ((i == 0 && VarkeshReady) || (i == 1 && GorathReady)) continue;

                // A direct cold boot into one boss zone must not load the
                // other boss's prefab or wait for its template to be ready.
                Scene scene = SceneManager.GetActiveScene();
                bool willowwatch = string.Equals(scene.name,
                    VarkeshWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase);
                bool vitheo = string.Equals(scene.name,
                    GorathWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase);
                if (willowwatch && i != 0) continue;
                if (vitheo && i != 1) continue;

                string key = HintKey(hintPrefixes[i]);
                string learned = null;
                try { learned = PlayerPrefs.GetString(key, string.Empty); }
                catch { }
                string[] paths = { IndexedDonorPaths[i], learned };
                bool attempted = false;
                for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                {
                    if (!isAllowed()) yield break;
                    if ((i == 0 && VarkeshReady) || (i == 1 && GorathReady)) break;
                    string path = paths[pathIndex];
                    if (string.IsNullOrEmpty(path) ||
                        (pathIndex > 0 && string.Equals(path,
                            IndexedDonorPaths[i], StringComparison.OrdinalIgnoreCase)))
                        continue;

                    attempted = true;
                    string source = pathIndex == 0 ? "indexed" : "learned";
                    Plugin.NativeLog.LogInfo(
                        "[WorldBossPrecache] r93 async " + source +
                        " donor request donor=[" + donorNames[i] +
                        "] path=[" + path + "].");

                    float began = Time.realtimeSinceStartup;
                    ResourceRequest request = null;
                    try { request = Resources.LoadAsync<GameObject>(path); }
                    catch (Exception ex)
                    {
                        Plugin.NativeLog.LogWarning(
                            "[WorldBossPrecache] r93 async " + source +
                            " donor request failed path=[" + path +
                            "]: " + ex.Message);
                    }
                    if (request != null)
                    {
                        while (!request.isDone)
                        {
                            if (!isAllowed()) yield break;
                            yield return null;
                        }
                        GameObject resourceRoot = request.asset as GameObject;
                        if (resourceRoot != null)
                            TryPrimeDonorsFromRoot(resourceRoot,
                                "r93 async " + source + " Resources [" + path + "]");
                        else
                            Plugin.NativeLog.LogWarning(
                                "[WorldBossPrecache] r93 " + source +
                                " Resources path did not return a GameObject [" + path + "].");
                    }
                    bool ready = i == 0 ? VarkeshReady : GorathReady;
                    Plugin.NativeLog.LogInfo(
                        "[WorldBossPrecache] r93 async donor result donor=[" +
                        donorNames[i] + "] source=[" + source +
                        "] ready=[" + ready + "] elapsedMs=[" +
                        ((Time.realtimeSinceStartup - began) * 1000f).ToString("0.0") +
                        "].");
                    if (ready) break;
                    // A stale r91 hint is optional. Don't delete it until its
                    // own request fails, and never persist an unverified path.
                    if (pathIndex != 0)
                    {
                        try { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
                        catch { }
                    }
                    yield return null;
                }
                if (!attempted)
                    Plugin.NativeLog.LogInfo("[WorldBossPrecache] r93 no paths " +
                        "available for donor=[" + donorNames[i] + "].");
                yield return null;
            }
        }

        /// <summary>
        /// Called by the existing deferred boss recovery before any expensive
        /// synchronous Resources.LoadAll. This covers the r91 gap where the
        /// player launches DIRECTLY into Willowwatch / Vitheo, which is
        /// intentionally excluded from the five-second background prewarm.
        /// The only resource loading done here is Resources.LoadAsync with a
        /// path previously verified and stored by r91. Never spawn an NPC or
        /// load a Unity scene while priming a donor.
        /// </summary>
        internal static IEnumerator TryLearnedPathsInActiveBossScene()
        {
            if (!_initialized || !IsActiveBossScene()) yield break;
            int epoch = _epoch;
            Plugin.NativeLog.LogInfo(
                "[WorldBossPrecache] r93 active-zone async indexed-path recovery " +
                "started in [" + SceneManager.GetActiveScene().name + "].");
            IEnumerator learned = LoadLearnedPaths(epoch,
                () => _initialized && epoch == _epoch && IsActiveBossScene());
            while (learned.MoveNext())
            {
                if (!_initialized || epoch != _epoch || !IsActiveBossScene())
                    yield break;
                yield return learned.Current;
            }
            Plugin.NativeLog.LogInfo(
                "[WorldBossPrecache] r93 active-zone async result VarkeshReady=[" +
                VarkeshReady + "] GorathReady=[" + GorathReady + "].");
        }

        private static bool IsActiveBossScene()
        {
            if (!WorldBossPersistencePolicy.CanExistInWorld) return false;
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.isLoaded &&
                (string.Equals(scene.name,
                    VarkeshWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(scene.name,
                    GorathWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerator PrecacheWhenPlayable(int epoch)
        {
            // Starting this coroutine must not make plugin/menu startup block.
            // A new character or the menu can remain in this wait indefinitely.
            // Require five uninterrupted seconds of ordinary gameplay.
            // If a zone transition occurs during that grace period, wait for
            // the next stable non-boss gameplay scene rather than doing work
            // during the transition or silently losing the preload attempt.
            while (_initialized && epoch == _epoch)
            {
                while (_initialized && epoch == _epoch && !IsSafeToPrecache(epoch))
                    yield return new WaitForSecondsRealtime(1f);
                if (!_initialized || epoch != _epoch) yield break;
                yield return new WaitForSecondsRealtime(5f);
                if (IsSafeToPrecache(epoch)) break;
            }
            if (!_initialized || epoch != _epoch) yield break;

            float started = Time.realtimeSinceStartup;
            int candidateCount = 0;
            int matchedAssets = 0;
            Plugin.NativeLog.LogInfo(
                "[WorldBossPrecache] r93 beginning indexed-path and targeted async " +
                "donor prewarm in scene [" + SceneManager.GetActiveScene().name + "]. " +
                "No Resources.LoadAll / early world-boss spawn.");

            // First try the paths verified from a previous cold-session
            // fallback. This can succeed even when the donor's display name
            // is not the name of the addressable Resources prefab.
            IEnumerator learned = LoadLearnedPaths(epoch, () => IsSafeToPrecache(epoch));
            while (learned.MoveNext())
            {
                if (!IsSafeToPrecache(epoch)) yield break;
                yield return learned.Current;
            }

            // Stage one: native prefabs in *already loaded* AssetBundles.
            // Enumerating bundle names is bounded to known donor keywords and
            // never causes the plugin to load a new whole AssetBundle.
            try
            {
                foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (!IsSafeToPrecache(epoch)) yield break;
                    if (VarkeshReady && GorathReady) break;
                    if (bundle == null) continue;

                    string[] assetNames;
                    try { assetNames = bundle.GetAllAssetNames(); }
                    catch (Exception e)
                    {
                        Plugin.NativeLog.LogWarning(
                            "[WorldBossPrecache] bundle asset names unavailable: " + e.Message);
                        continue;
                    }

                    for (int i = 0; assetNames != null && i < assetNames.Length; i++)
                    {
                        if (!IsSafeToPrecache(epoch)) yield break;
                        if (VarkeshReady && GorathReady) break;
                        string path = assetNames[i];
                        if (!LooksLikeDonor(path)) continue;
                        ++candidateCount;

                        AssetBundleRequest request = null;
                        try { request = bundle.LoadAssetAsync<GameObject>(path); }
                        catch (Exception e)
                        {
                            Plugin.NativeLog.LogWarning(
                                "[WorldBossPrecache] async bundle request failed [" + path + "]: " + e.Message);
                        }

                        if (request == null) continue;
                        while (!request.isDone)
                        {
                            if (!IsSafeToPrecache(epoch)) yield break;
                            yield return null;
                        }
                        GameObject prefab = request.asset as GameObject;
                        if (prefab != null && TryPrimeDonorsFromRoot(prefab,
                            "async bundle [" + bundle.name + "] [" + path + "]"))
                            ++matchedAssets;
                        yield return null;
                    }
                    yield return null;
                }
            }
            finally
            {
                // No bundle handles are retained or unloaded: they belong to
                // Erenshor/the mod that originally loaded them.
            }

            // Stage two: exact, *asynchronous* Resources paths. No global
            // Resources.LoadAll scan can be used asynchronously in Unity.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int n = 0; n < Names.Length; n++)
            {
                if (VarkeshReady && GorathReady) break;
                string name = Names[n];
                string[] forms = { name, name.Replace(' ', '_'), name.Replace(" ", "") };
                for (int prefix = 0; prefix < Prefixes.Length; prefix++)
                {
                    for (int form = 0; form < forms.Length; form++)
                    {
                        if (!IsSafeToPrecache(epoch)) yield break;
                        if (VarkeshReady && GorathReady) break;
                        // Do not preload dozens of paths for a donor already
                        // cached by the other stage.
                        if (n == 0 && VarkeshReady) break;
                        if (n != 0 && GorathReady) break;

                        string path = Prefixes[prefix] + forms[form];
                        if (!seen.Add(path)) continue;
                        ++candidateCount;
                        ResourceRequest request = null;
                        try { request = Resources.LoadAsync<GameObject>(path); }
                        catch (Exception e)
                        {
                            Plugin.NativeLog.LogWarning(
                                "[WorldBossPrecache] async Resources request failed [" + path + "]: " + e.Message);
                        }
                        if (request == null) continue;
                        while (!request.isDone)
                        {
                            if (!IsSafeToPrecache(epoch)) yield break;
                            yield return null;
                        }
                        GameObject prefab = request.asset as GameObject;
                        if (prefab != null && TryPrimeDonorsFromRoot(prefab,
                            "async Resources [" + path + "]"))
                            ++matchedAssets;
                        yield return null;
                    }
                }
            }

            Plugin.NativeLog.LogInfo(
                "[WorldBossPrecache] r93 background finished. VarkeshReady=[" + VarkeshReady +
                "] GorathReady=[" + GorathReady + "] Candidates=[" + candidateCount +
                "] DonorAssets=[" + matchedAssets + "] Elapsed=[" +
                ((Time.realtimeSinceStartup - started) * 1000f).ToString("0") + "ms]. " +
                (!VarkeshReady || !GorathReady
                    ? "Indexed donor paths did not resolve; legacy deferred in-zone fallback retained. " +
                      "If the fallback recovers a donor, r91 will try to learn its root path."
                    : "Both native templates ready before boss scene entry."));
            _routine = null;
        }

        private static bool LooksLikeDonor(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string normalized = path.Replace('_', ' ').Replace('-', ' ').ToLowerInvariant();
            return (normalized.Contains("sivakayan") && normalized.Contains("templar")) ||
                (normalized.Contains("apparition") && normalized.Contains("sivakaya"));
        }

        private static bool TryPrimeDonorsFromRoot(GameObject root, string origin)
        {
            if (root == null) return false;
            bool primed = false;
            try
            {
                if (!VarkeshReady)
                {
                    primed |= VarkeshWorldSpawnRuntime.PrimeNativeTemplateFromResourceRoot(
                        root, false, origin);
                    if (!VarkeshReady)
                        primed |= VarkeshWorldSpawnRuntime.PrimeNativeTemplateFromResourceRoot(
                            root, true, origin);
                }
                if (!GorathReady)
                    primed |= NativeWorldBossVisualRegistry.TryPrimeFromPreloadedRoot(root, origin);
            }
            catch (Exception e)
            {
                Plugin.NativeLog.LogWarning(
                    "[WorldBossPrecache] prewarm of [" + origin + "] failed: " + e.Message);
            }
            return primed;
        }
    }
}
