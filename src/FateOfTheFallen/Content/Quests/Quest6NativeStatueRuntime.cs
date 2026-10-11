using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 6 native-statue bridge.
    ///
    /// Native source:
    ///     Reliquary
    ///
    /// The player no longer needs to manually visit the Reliquary first.
    /// If the four native stone statues have not yet been cached and the
    /// Ancient Warding Shrine needs them in Azure, this runtime temporarily
    /// loads Reliquary additively, clones the four exact native statue roots
    /// into inactive persistent templates, then unloads Reliquary.
    ///
    /// This follows the same proven cold-source pattern used by Quest 5's
    /// automatic PlaneOfBrax / Lava Fiend preload.
    /// </summary>
    internal static class Quest6NativeStatueRuntime
    {
        internal const string ReliquarySceneName =
            "Reliquary";

        private const string ActiveShrineSceneName =
            "Azure";

        private const string LogPrefix =
            "[Quest6Statues] ";

        // Visual tuning for the four shrine statues.
        // The previous 2.15 world-unit target made them only slightly taller
        // than a player. 3.40 gives the shrine the intended monumental scale
        // while still fitting comfortably on the four outer pedestals.
        private const float ShrineStatueVisibleHeight =
            3.40f;

        private static bool _initialized;

        private static bool _automaticSourcePreloadAttempted;
        private static bool _automaticSourcePreloadInProgress;
        private static bool _automaticSourcePreloadCancelRequested;

        private static bool _loggedAuxiliaryCharacterStartSuppression;
        private static bool _loggedAuxiliaryNavSuppression;

        private static Coroutine _automaticSourcePreloadRoutine;

        private static readonly List<GameObject>
            _spawnedStatues =
                new List<GameObject>();


        private sealed class StatueDefinition
        {
            internal readonly string DisplayName;
            internal readonly string NativePath;
            internal readonly string SlotName;
            internal readonly string PedestalTopName;
            internal readonly float TargetVisibleHeight;

            internal GameObject Template;


            internal StatueDefinition(
                string displayName,
                string nativePath,
                string slotName,
                string pedestalTopName,
                float targetVisibleHeight)
            {
                DisplayName =
                    displayName;

                NativePath =
                    nativePath;

                SlotName =
                    slotName;

                PedestalTopName =
                    pedestalTopName;

                TargetVisibleHeight =
                    targetVisibleHeight;
            }
        }


        private static readonly StatueDefinition[]
            Definitions =
            {
                new StatueDefinition(
                    "Soluna",
                    "Rooms/Room L1/Stone Soluna",
                    "StatueSlot_Soluna",
                    "Soluna_Pedestal_Top",
                    ShrineStatueVisibleHeight),

                new StatueDefinition(
                    "Brax",
                    "Rooms/Room L1/Stone Brax",
                    "StatueSlot_Brax",
                    "Brax_Pedestal_Top",
                    ShrineStatueVisibleHeight),

                new StatueDefinition(
                    "Vithean",
                    "Rooms/Room L1/StoneVitheo",
                    "StatueSlot_Vithean",
                    "Vithean_Pedestal_Top",
                    ShrineStatueVisibleHeight),

                new StatueDefinition(
                    "Fernalla",
                    "Rooms/Room L1/Stone Fernalla",
                    "StatueSlot_Fernalla",
                    "Fernalla_Pedestal_Top",
                    ShrineStatueVisibleHeight)
            };


        // ============================================================
        // LIFECYCLE
        // ============================================================

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized =
                true;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsReliquary(
                    activeScene))
            {
                TryCaptureFromReliquary(
                    activeScene);
            }
            else if (IsAzure(
                         activeScene) &&
                     !HasAllTemplates)
            {
                StartAutomaticSourcePreload(
                    activeScene);
            }

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "initialized. Templates=[" +
                CountCapturedTemplates() +
                "/" +
                Definitions.Length +
                "].");
        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized =
                false;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            _automaticSourcePreloadCancelRequested =
                true;

            ClearSpawnedStatues();
            DestroyTemplates();

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "shutdown.");
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (!_initialized)
            {
                return;
            }

            // Auxiliary additive Reliquary load: capture directly from the
            // scene passed by Unity. Do not require it to be active.
            if (IsReliquary(
                    scene))
            {
                TryCaptureFromReliquary(
                    scene);

                return;
            }

            // Normal gameplay transition into Azure.
            if (SceneManager.GetActiveScene() ==
                    scene &&
                IsAzure(
                    scene) &&
                !HasAllTemplates)
            {
                StartAutomaticSourcePreload(
                    scene);
            }
        }


        // ============================================================
        // PUBLIC API
        // ============================================================

        internal static bool HasAllTemplates
        {
            get
            {
                for (int i = 0;
                     i < Definitions.Length;
                     i++)
                {
                    if (Definitions[i].Template ==
                        null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }


        internal static bool PopulateShrine(
            Transform shrineRoot)
        {
            if (shrineRoot == null)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "PopulateShrine failed because shrineRoot was null.");

                return false;
            }

            EnsureInitialized();

            if (!HasAllTemplates)
            {
                Scene activeScene =
                    SceneManager.GetActiveScene();

                if (IsReliquary(
                        activeScene))
                {
                    TryCaptureFromReliquary(
                        activeScene);
                }
                else if (IsAzure(
                             activeScene))
                {
                    StartAutomaticSourcePreload(
                        activeScene);
                }
            }

            if (!HasAllTemplates)
            {
                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "templates not ready yet. Automatic Reliquary preload inProgress=[" +
                    _automaticSourcePreloadInProgress +
                    "] attempted=[" +
                    _automaticSourcePreloadAttempted +
                    "] captured=[" +
                    CountCapturedTemplates() +
                    "/" +
                    Definitions.Length +
                    "].");

                return false;
            }

            int populated =
                0;

            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                if (PopulateOne(
                        shrineRoot,
                        Definitions[i]))
                {
                    populated++;
                }
            }

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "shrine population complete. Populated=[" +
                populated +
                "/" +
                Definitions.Length +
                "].");

            return
                populated ==
                Definitions.Length;
        }


        internal static void ClearShrine(
            Transform shrineRoot)
        {
            if (shrineRoot == null)
            {
                return;
            }

            RemoveExistingFOTFStatues(
                shrineRoot,
                null);

            PruneSpawnedList();
        }


        // ============================================================
        // AUTOMATIC RELIQUARY PRELOAD
        // ============================================================

        private static void StartAutomaticSourcePreload(
            Scene gameplayScene)
        {
            if (!_initialized ||
                HasAllTemplates ||
                _automaticSourcePreloadInProgress ||
                _automaticSourcePreloadAttempted ||
                !IsAzure(
                    gameplayScene))
            {
                return;
            }

            _automaticSourcePreloadAttempted =
                true;

            _automaticSourcePreloadInProgress =
                true;

            _automaticSourcePreloadCancelRequested =
                false;

            _loggedAuxiliaryCharacterStartSuppression =
                false;

            _loggedAuxiliaryNavSuppression =
                false;

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "starting temporary additive preload of [" +
                ReliquarySceneName +
                "] to acquire native god statues without moving the player.");

            _automaticSourcePreloadRoutine =
                CoroutineHost.Start(
                    AutomaticSourcePreload(
                        gameplayScene));
        }


        private static IEnumerator AutomaticSourcePreload(
            Scene gameplayScene)
        {
            bool loadedByThisRuntime =
                false;

            Scene sourceScene =
                SceneManager.GetSceneByName(
                    ReliquarySceneName);

            // --------------------------------------------------------
            // LOAD RELIQUARY ADDITIVELY
            // --------------------------------------------------------
            if (!sourceScene.IsValid() ||
                !sourceScene.isLoaded)
            {
                AsyncOperation loadOperation =
                    null;

                try
                {
                    loadOperation =
                        SceneManager.LoadSceneAsync(
                            ReliquarySceneName,
                            LoadSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        LogPrefix +
                        "automatic additive load of [" +
                        ReliquarySceneName +
                        "] failed to start: " +
                        exception);

                    FinishAutomaticSourcePreload();

                    yield break;
                }

                if (loadOperation == null)
                {
                    Plugin.NativeLog.LogError(
                        LogPrefix +
                        "SceneManager.LoadSceneAsync returned null for [" +
                        ReliquarySceneName +
                        "].");

                    FinishAutomaticSourcePreload();

                    yield break;
                }

                loadedByThisRuntime =
                    true;

                while (!loadOperation.isDone)
                {
                    yield return null;
                }

                sourceScene =
                    SceneManager.GetSceneByName(
                        ReliquarySceneName);
            }

            // The auxiliary source must never become the gameplay scene.
            if (gameplayScene.IsValid() &&
                gameplayScene.isLoaded &&
                SceneManager.GetActiveScene() !=
                    gameplayScene)
            {
                SceneManager.SetActiveScene(
                    gameplayScene);
            }

            if (!sourceScene.IsValid() ||
                !sourceScene.isLoaded)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "additive source scene [" +
                    ReliquarySceneName +
                    "] did not become available after loading.");

                FinishAutomaticSourcePreload();

                yield break;
            }

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "additive Reliquary loaded. Capturing exact Room L1 stone statue roots.");

            bool captured =
                false;

            const int MaximumCaptureFrames =
                600;

            for (int frame = 0;
                 frame < MaximumCaptureFrames;
                 frame++)
            {
                if (_automaticSourcePreloadCancelRequested ||
                    !_initialized ||
                    !gameplayScene.IsValid() ||
                    !gameplayScene.isLoaded ||
                    !IsAzure(
                        SceneManager.GetActiveScene()))
                {
                    break;
                }

                if (HasAllTemplates)
                {
                    captured =
                        true;

                    break;
                }

                if (frame == 0 ||
                    frame % 10 == 0)
                {
                    if (TryCaptureFromReliquary(
                            sourceScene))
                    {
                        captured =
                            true;

                        break;
                    }
                }

                yield return null;
            }

            if (captured)
            {
                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "automatic Reliquary preload successfully cached all four native stone statues.");
            }
            else if (!_automaticSourcePreloadCancelRequested)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "automatic Reliquary preload completed but captured only [" +
                    CountCapturedTemplates() +
                    "/" +
                    Definitions.Length +
                    "] native statue templates.");
            }

            // --------------------------------------------------------
            // UNLOAD AUXILIARY RELIQUARY
            // --------------------------------------------------------
            if (loadedByThisRuntime &&
                sourceScene.IsValid() &&
                sourceScene.isLoaded)
            {
                if (gameplayScene.IsValid() &&
                    gameplayScene.isLoaded)
                {
                    SceneManager.SetActiveScene(
                        gameplayScene);
                }

                AsyncOperation unloadOperation =
                    null;

                try
                {
                    unloadOperation =
                        SceneManager.UnloadSceneAsync(
                            sourceScene);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        LogPrefix +
                        "failed to begin unloading temporary Reliquary scene: " +
                        exception.Message);
                }

                if (unloadOperation != null)
                {
                    while (!unloadOperation.isDone)
                    {
                        yield return null;
                    }

                    Plugin.NativeLog.LogInfo(
                        LogPrefix +
                        "temporary additive Reliquary source scene unloaded.");
                }
            }

            FinishAutomaticSourcePreload();
        }


        private static void FinishAutomaticSourcePreload()
        {
            _automaticSourcePreloadInProgress =
                false;

            _automaticSourcePreloadCancelRequested =
                false;

            _loggedAuxiliaryCharacterStartSuppression =
                false;

            _loggedAuxiliaryNavSuppression =
                false;

            _automaticSourcePreloadRoutine =
                null;
        }


        // ============================================================
        // AUXILIARY-SCENE SAFETY GUARDS
        // ============================================================

        internal static bool ShouldSuppressAuxiliaryReliquaryCharacterStart(
            Character character)
        {
            if (!_automaticSourcePreloadInProgress ||
                character == null ||
                character.gameObject == null)
            {
                return false;
            }

            Scene objectScene =
                character.gameObject.scene;

            if (!IsReliquary(
                    objectScene))
            {
                return false;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (!IsAzure(
                    activeScene))
            {
                return false;
            }

            if (!_loggedAuxiliaryCharacterStartSuppression)
            {
                _loggedAuxiliaryCharacterStartSuppression =
                    true;

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "suppressing Character.Start for temporary auxiliary Reliquary characters while Azure remains the active gameplay scene.");
            }

            return true;
        }


        internal static bool ShouldSuppressAuxiliaryReliquaryNpcNavigation(
            NPC npc)
        {
            if (!_automaticSourcePreloadInProgress ||
                npc == null ||
                npc.gameObject == null)
            {
                return false;
            }

            Scene objectScene =
                npc.gameObject.scene;

            if (!IsReliquary(
                    objectScene))
            {
                return false;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (!IsAzure(
                    activeScene))
            {
                return false;
            }

            if (!_loggedAuxiliaryNavSuppression)
            {
                _loggedAuxiliaryNavSuppression =
                    true;

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "suppressing NPC.UpdateNav for temporary auxiliary Reliquary NPCs while Azure remains the active gameplay scene.");
            }

            return true;
        }


        // ============================================================
        // RELIQUARY CAPTURE
        // ============================================================

        private static bool TryCaptureFromReliquary(
            Scene scene)
        {
            if (!IsReliquary(
                    scene))
            {
                return false;
            }

            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                StatueDefinition definition =
                    Definitions[i];

                if (definition.Template !=
                    null)
                {
                    continue;
                }

                Transform source =
                    FindSceneTransformByPath(
                        scene,
                        definition.NativePath);

                if (source == null)
                {
                    continue;
                }

                GameObject template =
                    CloneAsPersistentTemplate(
                        source.gameObject,
                        definition.DisplayName);

                if (template == null)
                {
                    continue;
                }

                definition.Template =
                    template;

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "captured native stone statue [" +
                    definition.DisplayName +
                    "] from [" +
                    definition.NativePath +
                    "] Scale=[" +
                    template.transform.localScale +
                    "].");
            }

            if (HasAllTemplates)
            {
                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "native template capture complete [4/4].");

                return true;
            }

            return false;
        }


        private static GameObject CloneAsPersistentTemplate(
            GameObject source,
            string displayName)
        {
            if (source == null)
            {
                return null;
            }

            bool sourceWasActiveSelf =
                source.activeSelf;

            GameObject clone =
                null;

            try
            {
                if (sourceWasActiveSelf)
                {
                    source.SetActive(
                        false);
                }

                clone =
                    UnityEngine.Object.Instantiate(
                        source);

                if (clone == null)
                {
                    return null;
                }

                clone.name =
                    "FOTF_Q6_Template_" +
                    displayName;

                clone.transform.SetParent(
                    null,
                    true);

                clone.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    clone);

                return clone;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "exception while capturing [" +
                    displayName +
                    "]: " +
                    exception);

                if (clone != null)
                {
                    UnityEngine.Object.Destroy(
                        clone);
                }

                return null;
            }
            finally
            {
                if (source != null &&
                    source.activeSelf !=
                    sourceWasActiveSelf)
                {
                    source.SetActive(
                        sourceWasActiveSelf);
                }
            }
        }


        // ============================================================
        // SHRINE POPULATION
        // ============================================================

        private static bool PopulateOne(
            Transform shrineRoot,
            StatueDefinition definition)
        {
            if (shrineRoot == null ||
                definition == null ||
                definition.Template == null)
            {
                return false;
            }

            Transform slot =
                FindChildRecursive(
                    shrineRoot,
                    definition.SlotName);

            Vector3 anchorPosition;
            Quaternion anchorRotation;
            string anchorSource;

            if (!TryResolvePlacementAnchor(
                    shrineRoot,
                    definition,
                    slot,
                    out anchorPosition,
                    out anchorRotation,
                    out anchorSource))
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "could not resolve a placement anchor for [" +
                    definition.DisplayName +
                    "]. Required slot=[" +
                    definition.SlotName +
                    "] pedestal=[" +
                    definition.PedestalTopName +
                    "].");

                return false;
            }

            // Statues are deliberately parented to the shrine root rather
            // than the imported FBX empties. The previous implementation
            // inherited both the slot's imported axis conversion and its
            // compensation scale. That is what produced sideways, oversized
            // statues. The visible pedestal mesh is now the source of truth
            // for position, and the statue receives an upright world-space
            // rotation before any parenting occurs.
            RemoveExistingFOTFStatues(
                shrineRoot,
                definition.DisplayName);

            GameObject statue =
                null;

            try
            {
                statue =
                    UnityEngine.Object.Instantiate(
                        definition.Template);

                if (statue == null)
                {
                    return false;
                }

                statue.name =
                    "FOTF_Q6_Statue_" +
                    definition.DisplayName;

                statue.transform.SetParent(
                    null,
                    false);

                statue.transform.position =
                    anchorPosition;

                statue.transform.rotation =
                    anchorRotation;

                HideNativeBaseMesh(
                    statue);

                DisableNativeColliders(
                    statue);

                statue.SetActive(
                    true);

                float originalVisibleHeight;
                float appliedScaleFactor;

                if (!FitVisibleStatueHeight(
                        statue,
                        definition.TargetVisibleHeight,
                        out originalVisibleHeight,
                        out appliedScaleFactor))
                {
                    Plugin.NativeLog.LogWarning(
                        LogPrefix +
                        "could not calculate visible bounds for [" +
                        definition.DisplayName +
                        "]; keeping template scale.");
                }

                SnapVisibleStatueToAnchor(
                    statue,
                    anchorPosition);

                // Preserve the corrected world transform. Do not parent to
                // StatueSlot_*; those imported Blender empties carry the FBX
                // axis conversion that tilted the native Unity statues.
                statue.transform.SetParent(
                    shrineRoot,
                    true);

                _spawnedStatues.Add(
                    statue);

                Bounds finalBounds;
                bool hasFinalBounds =
                    TryGetVisibleBounds(
                        statue,
                        out finalBounds);

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "placed [" +
                    definition.DisplayName +
                    "] AnchorSource=[" +
                    anchorSource +
                    "] AnchorWorldPos=[" +
                    anchorPosition +
                    "] TargetHeight=[" +
                    definition.TargetVisibleHeight.ToString("0.00") +
                    "] OriginalVisibleHeight=[" +
                    originalVisibleHeight.ToString("0.00") +
                    "] ScaleFactor=[" +
                    appliedScaleFactor.ToString("0.000") +
                    "] FinalVisibleHeight=[" +
                    (hasFinalBounds
                        ? finalBounds.size.y.ToString("0.00")
                        : "<unknown>") +
                    "] WorldRotation=[" +
                    statue.transform.rotation.eulerAngles +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "failed placing [" +
                    definition.DisplayName +
                    "]: " +
                    exception);

                if (statue != null)
                {
                    UnityEngine.Object.Destroy(
                        statue);
                }

                return false;
            }
        }


        private static bool TryResolvePlacementAnchor(
            Transform shrineRoot,
            StatueDefinition definition,
            Transform slot,
            out Vector3 position,
            out Quaternion rotation,
            out string sourceDescription)
        {
            position =
                shrineRoot.position;

            rotation =
                Quaternion.identity;

            sourceDescription =
                "<none>";

            Transform pedestalTop =
                FindChildRecursive(
                    shrineRoot,
                    definition.PedestalTopName);

            Renderer pedestalRenderer =
                pedestalTop != null
                    ? pedestalTop.GetComponent<Renderer>()
                    : null;

            if (pedestalRenderer != null)
            {
                Bounds pedestalBounds =
                    pedestalRenderer.bounds;

                position =
                    new Vector3(
                        pedestalBounds.center.x,
                        pedestalBounds.max.y,
                        pedestalBounds.center.z);

                sourceDescription =
                    definition.PedestalTopName;
            }
            else if (slot != null)
            {
                // Position-only fallback. Intentionally ignore slot.rotation:
                // an FBX Empty's world rotation includes Blender-to-Unity axis
                // compensation and was the cause of the statues lying over.
                position =
                    slot.position;

                sourceDescription =
                    definition.SlotName +
                    " position fallback";
            }
            else
            {
                return false;
            }

            Vector3 towardCenter =
                shrineRoot.position -
                position;

            towardCenter.y =
                0f;

            if (towardCenter.sqrMagnitude >
                0.0001f)
            {
                Quaternion visibleFacing =
                    Quaternion.LookRotation(
                        towardCenter.normalized,
                        Vector3.up);

                // The Reliquary stone roots are authored at Y=180 while
                // their visible child mesh resolves to an upright Y=0 world
                // orientation. Apply that root-space compensation while
                // keeping X/Z at zero so the statue can never inherit the
                // shrine FBX's 90-degree import tilt.
                rotation =
                    visibleFacing *
                    Quaternion.Euler(
                        0f,
                        180f,
                        0f);
            }
            else
            {
                rotation =
                    Quaternion.Euler(
                        0f,
                        180f,
                        0f);
            }

            return true;
        }


        private static void HideNativeBaseMesh(
            GameObject statueRoot)
        {
            if (statueRoot == null)
            {
                return;
            }

            MeshFilter[] filters =
                statueRoot.GetComponentsInChildren<MeshFilter>(
                    true);

            for (int i = 0;
                 i < filters.Length;
                 i++)
            {
                MeshFilter filter =
                    filters[i];

                if (filter == null ||
                    filter.sharedMesh == null ||
                    !string.Equals(
                        filter.sharedMesh.name,
                        "SM_Prop_Statue_Base_01",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Renderer[] renderers =
                    filter.gameObject.GetComponents<Renderer>();

                for (int r = 0;
                     r < renderers.Length;
                     r++)
                {
                    if (renderers[r] != null)
                    {
                        renderers[r].enabled =
                            false;
                    }
                }
            }
        }


        private static void DisableNativeColliders(
            GameObject statueRoot)
        {
            Collider[] colliders =
                statueRoot.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled =
                        false;
                }
            }
        }


        private static bool FitVisibleStatueHeight(
            GameObject statueRoot,
            float targetHeight,
            out float originalHeight,
            out float appliedScaleFactor)
        {
            originalHeight =
                0f;

            appliedScaleFactor =
                1f;

            if (statueRoot == null ||
                targetHeight <= 0.01f)
            {
                return false;
            }

            Bounds bounds;

            if (!TryGetVisibleBounds(
                    statueRoot,
                    out bounds))
            {
                return false;
            }

            originalHeight =
                bounds.size.y;

            if (originalHeight <=
                0.0001f)
            {
                return false;
            }

            appliedScaleFactor =
                targetHeight /
                originalHeight;

            // Defensive clamp only. A value outside this range means the
            // selected object was not the expected statue visual.
            if (float.IsNaN(
                    appliedScaleFactor) ||
                float.IsInfinity(
                    appliedScaleFactor) ||
                appliedScaleFactor < 0.01f ||
                appliedScaleFactor > 100f)
            {
                appliedScaleFactor =
                    1f;

                return false;
            }

            statueRoot.transform.localScale *=
                appliedScaleFactor;

            return true;
        }


        private static void SnapVisibleStatueToAnchor(
            GameObject statueRoot,
            Vector3 anchorPosition)
        {
            Bounds bounds;

            if (!TryGetVisibleBounds(
                    statueRoot,
                    out bounds))
            {
                return;
            }

            // Bounds are used only for translation after the statue has been
            // uniformly sized. This centers the visible model over the
            // pedestal and puts its lowest visible point on the pedestal top.
            Vector3 delta =
                new Vector3(
                    anchorPosition.x -
                        bounds.center.x,

                    anchorPosition.y -
                        bounds.min.y,

                    anchorPosition.z -
                        bounds.center.z);

            statueRoot.transform.position +=
                delta;
        }


        private static bool TryGetVisibleBounds(
            GameObject statueRoot,
            out Bounds bounds)
        {
            bounds =
                new Bounds();

            if (statueRoot == null)
            {
                return false;
            }

            Renderer[] renderers =
                statueRoot.GetComponentsInChildren<Renderer>(
                    true);

            bool initialized =
                false;

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds current =
                    renderer.bounds;

                if (current.size.sqrMagnitude <=
                    0.000001f)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds =
                        current;

                    initialized =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        current);
                }
            }

            return initialized;
        }


        private static void RemoveExistingFOTFStatues(
            Transform shrineRoot,
            string displayName)
        {
            if (shrineRoot == null)
            {
                return;
            }

            string exactName =
                string.IsNullOrEmpty(
                    displayName)
                    ? null
                    : "FOTF_Q6_Statue_" +
                      displayName;

            Transform[] transforms =
                shrineRoot.GetComponentsInChildren<Transform>(
                    true);

            List<GameObject> remove =
                new List<GameObject>();

            for (int i = 0;
                 i < transforms.Length;
                 i++)
            {
                Transform candidate =
                    transforms[i];

                if (candidate == null ||
                    candidate == shrineRoot)
                {
                    continue;
                }

                bool matches =
                    exactName != null
                        ? string.Equals(
                            candidate.name,
                            exactName,
                            StringComparison.Ordinal)
                        : candidate.name.StartsWith(
                            "FOTF_Q6_Statue_",
                            StringComparison.Ordinal);

                if (!matches)
                {
                    continue;
                }

                remove.Add(
                    candidate.gameObject);
            }

            for (int i = 0;
                 i < remove.Count;
                 i++)
            {
                _spawnedStatues.Remove(
                    remove[i]);

                if (remove[i] != null)
                {
                    UnityEngine.Object.Destroy(
                        remove[i]);
                }
            }
        }


        // ============================================================
        // LOOKUP HELPERS
        // ============================================================

        private static Transform FindSceneTransformByPath(
            Scene scene,
            string fullPath)
        {
            string[] segments =
                fullPath.Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

            GameObject[] roots =
                scene.GetRootGameObjects();

            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                GameObject root =
                    roots[i];

                if (root == null ||
                    !string.Equals(
                        root.name,
                        segments[0],
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Transform current =
                    root.transform;

                for (int s = 1;
                     s < segments.Length;
                     s++)
                {
                    current =
                        FindDirectChild(
                            current,
                            segments[s]);

                    if (current == null)
                    {
                        break;
                    }
                }

                if (current != null)
                {
                    return current;
                }
            }

            return null;
        }


        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0;
                 i < parent.childCount;
                 i++)
            {
                Transform child =
                    parent.GetChild(
                        i);

                if (child != null &&
                    string.Equals(
                        child.name,
                        childName,
                        StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }


        private static Transform FindChildRecursive(
            Transform root,
            string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    childName,
                    StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform result =
                    FindChildRecursive(
                        root.GetChild(
                            i),
                        childName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }


        private static bool IsReliquary(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    ReliquarySceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool IsAzure(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    ActiveShrineSceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }


        private static int CountCapturedTemplates()
        {
            int count =
                0;

            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                if (Definitions[i].Template !=
                    null)
                {
                    count++;
                }
            }

            return count;
        }


        private static void DestroyTemplates()
        {
            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                if (Definitions[i].Template !=
                    null)
                {
                    UnityEngine.Object.Destroy(
                        Definitions[i].Template);

                    Definitions[i].Template =
                        null;
                }
            }
        }


        private static void ClearSpawnedStatues()
        {
            for (int i =
                     _spawnedStatues.Count - 1;
                 i >= 0;
                 i--)
            {
                if (_spawnedStatues[i] !=
                    null)
                {
                    UnityEngine.Object.Destroy(
                        _spawnedStatues[i]);
                }
            }

            _spawnedStatues.Clear();
        }


        private static void PruneSpawnedList()
        {
            for (int i =
                     _spawnedStatues.Count - 1;
                 i >= 0;
                 i--)
            {
                if (_spawnedStatues[i] ==
                    null)
                {
                    _spawnedStatues.RemoveAt(
                        i);
                }
            }
        }
    }
}
