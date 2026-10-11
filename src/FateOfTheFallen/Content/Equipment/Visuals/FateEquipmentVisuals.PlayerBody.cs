using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FateOfTheFallen
{
internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        private const float PB_RefreshInterval = 0.50f;
        private const string PB_CustomPrefix = "FOTF_CustomBodyVisual_";

        private sealed class PB_TransformTuning
        {
            internal PB_TransformTuning(Vector3 position, Vector3 rotationEuler, float scale)
            {
                DefaultPosition = position;
                DefaultRotationEuler = rotationEuler;
                DefaultScale = scale;
                Reset();
            }

            internal void Reset()
            {
                Position = DefaultPosition;
                RotationEuler = DefaultRotationEuler;
                Scale = DefaultScale;
            }

            internal readonly Vector3 DefaultPosition;
            internal readonly Vector3 DefaultRotationEuler;
            internal readonly float DefaultScale;
            internal Vector3 Position;
            internal Vector3 RotationEuler;
            internal float Scale;
        }

        // The Nhal'Sera body pieces were authored around the same Synty
        // proportions used by Archivist Sereth. Their roots are reference
        // points, not arbitrary pivots:
        //   Carapace = centre of the Synty torso
        //   Crown    = centre of the Synty head
        //   Grasp    = wrist reference on each hand
        //   Girdle   = centre of the Synty belt line
        //
        // Blender -> Unity export converts Blender -Y to Unity +Z and Blender
        // +Z to Unity +Y. Carapace was authored with -Y as character-forward,
        // while Crown/Girdle were authored with +Y as character-forward, hence
        // the 180 degree Y correction on those two pieces.
        // Crown follows the animated Head pose in WORLD space, but stays under
        // the actor root to avoid inherited microscopic Head-bone scaling.
        private const float PB_SyntyBeltHeightOverTorso = 0.045f;

        // Body armour is instantiated beneath the stable actor root, whose
        // world scale is already the Synty character scale.  Unlike WeaponR /
        // WeaponL, these anchors do NOT need the x100 weapon import correction.
        // The Blender body models were authored directly in Synty-sized metres.
        private static readonly PB_TransformTuning PB_CarapaceTuning =
            new PB_TransformTuning(Vector3.zero, new Vector3(-90f, 0f, 0f), 0.50f);

        private static readonly PB_TransformTuning PB_CrownTuning =
            new PB_TransformTuning(new Vector3(0f, 0.16f, 0f), new Vector3(-90f, 180f, 0f), 1.00f);

        private static readonly PB_TransformTuning PB_GraspTuning =
            new PB_TransformTuning(Vector3.zero, Vector3.zero, 0.50f);

        private static readonly PB_TransformTuning PB_GirdleTuning =
            new PB_TransformTuning(Vector3.zero, new Vector3(0f, 180f, 0f), 1.00f);

        private readonly PB_VisualState _pbHead =
            new PB_VisualState(Item.SlotType.Head, "Head");

        private readonly PB_VisualState _pbChest =
            new PB_VisualState(Item.SlotType.Chest, "Chest");

        private readonly PB_VisualState _pbHands =
            new PB_VisualState(Item.SlotType.Hand, "Hands");

        private readonly PB_VisualState _pbWaist =
            new PB_VisualState(Item.SlotType.Waist, "Waist");

        private float _pbNextRefreshTime;
        private bool _pbClassRestrictionsRefreshed;
        private static int _pbCachedAnimatorRootId;
        private static Animator _pbCachedAnimator;

        private void PB_Update()
        {
            if (Time.unscaledTime < _pbNextRefreshTime)
            {
                return;
            }

            _pbNextRefreshTime =
                Time.unscaledTime + PB_RefreshInterval;

            PB_Refresh();
        }

        private void PB_LateUpdate()
        {
            Inventory inventory =
                GameData.PlayerInv;

            if (inventory == null)
            {
                return;
            }

            PB_Follow(inventory, _pbHead);
            PB_Follow(inventory, _pbChest);
            PB_Follow(inventory, _pbHands);
            PB_Follow(inventory, _pbWaist);
        }

        internal void PB_Shutdown()
        {
            PB_Remove(_pbHead);
            PB_Remove(_pbChest);
            PB_Remove(_pbHands);
            PB_Remove(_pbWaist);
            _pbCachedAnimatorRootId = 0;
            _pbCachedAnimator = null;
            _pbClassRestrictionsRefreshed = false;
        }

        private void PB_Refresh()
        {
            if (!_pbClassRestrictionsRefreshed)
            {
                NhalSeraEquipment.RefreshClassRestrictions();
                _pbClassRestrictionsRefreshed = true;
            }

            Inventory inventory =
                GameData.PlayerInv;

            if (inventory == null)
            {
                PB_Remove(_pbHead);
                PB_Remove(_pbChest);
                PB_Remove(_pbHands);
                PB_Remove(_pbWaist);
                return;
            }

            PB_RefreshSlot(inventory, _pbHead);
            PB_RefreshSlot(inventory, _pbChest);
            PB_RefreshSlot(inventory, _pbHands);
            PB_RefreshSlot(inventory, _pbWaist);
        }

        private void PB_RefreshSlot(
            Inventory inventory,
            PB_VisualState state)
        {
            if (inventory == null || state == null)
            {
                return;
            }

            Item item =
                PB_GetEquippedItem(
                    inventory,
                    state.SlotType);

            string prefabName =
                PB_GetPrefabForBodyItem(
                    item,
                    state.SlotType);

            if (item == null ||
                string.IsNullOrEmpty(prefabName))
            {
                PB_Remove(state);
                return;
            }

            Transform actorRoot = PB_GetActorRoot(inventory);
            if (state.CustomVisual != null &&
                string.Equals(state.ItemId, item.Id, StringComparison.OrdinalIgnoreCase) &&
                state.ActorRoot == actorRoot && actorRoot != null &&
                state.CustomVisual.transform.IsChildOf(actorRoot))
            {
                return;
            }

            PB_Remove(state);

            // A late-initializing rig must never trap the item under an
            // inactive actor root. Retry on the next normal refresh tick.
            if (actorRoot == null || !actorRoot.gameObject.activeInHierarchy)
            {
                return;
            }

            GameObject prefab =
                PW_ResolvePrefab(
                    prefabName);

            if (prefab == null)
            {
                Plugin.NativeLog.LogError(
                    "[FateEquipmentVisuals] body prefab unresolved [" +
                    prefabName +
                    "] for [" +
                    item.ItemName +
                    "].");
                return;
            }

            state.ItemId = item.Id;
            state.PrefabName = prefabName;
            state.ActorRoot = actorRoot;

            if (MB_UsesModularRig(item.Id, state.SlotType))
            {
                GameObject modularVisual =
                    UnityEngine.Object.Instantiate(prefab, actorRoot, false);

                if (modularVisual == null)
                {
                    PB_Remove(state);
                    return;
                }

                modularVisual.name =
                    PB_CustomPrefix + item.Id + "_" + state.Label + "_Modular";

                if (!MB_BindVisual(
                        actorRoot,
                        modularVisual,
                        item.Id,
                        state.SlotType,
                        item.ItemName))
                {
                    UnityEngine.Object.Destroy(modularVisual);
                    PB_Remove(state);
                    return;
                }

                state.CustomVisual = modularVisual;
                PB_CacheCustomHierarchy(state);
                PB_SuppressNativeVisuals(inventory, item, state);
                PB_LogSpawn(item, state);
                return;
            }

            if (SR_UsesNativeSkinnedRig(item.Id, state.SlotType))
            {
                GameObject skinnedVisual =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        actorRoot,
                        false);

                if (skinnedVisual == null)
                {
                    PB_Remove(state);
                    return;
                }

                skinnedVisual.name =
                    PB_CustomPrefix +
                    item.Id +
                    "_" +
                    state.Label +
                    "_Skinned";

                SR_PrepareSkinnedVisual(skinnedVisual);

                int rendererCount;
                int mappedBones;
                int missingBones;

                if (!SR_RemapSkinnedVisual(
                        actorRoot,
                        skinnedVisual,
                        item.ItemName,
                        out rendererCount,
                        out mappedBones,
                        out missingBones))
                {
                    UnityEngine.Object.Destroy(skinnedVisual);
                    PB_Remove(state);
                    return;
                }

                state.CustomVisual = skinnedVisual;
                PB_CacheCustomHierarchy(state);

                // Hide the inherited native chest/glove only after the new
                // skinned model has been successfully attached to the live
                // Synty skeleton.
                PB_SuppressNativeVisuals(inventory, item, state);
                PB_LogSpawn(item, state);
                return;
            }

            if (state.SlotType == Item.SlotType.Hand)
            {
                if (!PB_SpawnHandPair(
                        prefab,
                        item,
                        state,
                        actorRoot))
                {
                    PB_Remove(state);
                    return;
                }
            }
            else
            {
                // Crown is attached to the actor ROOT, never the animated
                // Head bone. Head bones on imported Synty rigs can contain
                // tiny/non-uniform scales that make a valid crown invisible.
                // Follow only the head's WORLD position and rotation below.
                Transform initialParent = actorRoot;

                bool isCrown =
                    state.SlotType == Item.SlotType.Head &&
                    string.Equals(item.Id, NhalSeraEquipment.CrownId,
                        StringComparison.OrdinalIgnoreCase);

                if (isCrown)
                {
                    Animator crownAnimator = PB_FindHumanoidAnimator(inventory);
                    if (crownAnimator != null)
                    {
                        try
                        {
                            state.AnchorA = crownAnimator.GetBoneTransform(
                                HumanBodyBones.Head);
                        }
                        catch { state.AnchorA = null; }
                    }
                }

                GameObject visual =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        initialParent,
                        false);

                if (visual == null)
                {
                    PB_Remove(state);
                    return;
                }

                visual.name =
                    PB_CustomPrefix +
                    item.Id +
                    "_" +
                    state.Label;

                state.CustomVisual = visual;

                PB_DisablePhysics(visual);
                PB_RepairUnsupportedShaders(visual);

                if (isCrown)
                {
                    // Establish the correct head pose BEFORE bounds-based
                    // scale/centering; renderer bounds otherwise describe the
                    // spawn/root pose rather than the worn crown.
                    if (state.AnchorA != null)
                    {
                        visual.transform.position = state.AnchorA.position;
                        visual.transform.rotation = state.AnchorA.rotation *
                            Quaternion.Euler(PB_CrownTuning.RotationEuler);
                    }

                    PB_PrepareCrownRigidVisual(
                        visual,
                        state.AnchorA,
                        prefabName);

                    state.RuntimeScaleMultiplier =
                        PB_CalculateCrownScaleMultiplier(
                            inventory,
                            visual,
                            state.AnchorA);

                    visual.transform.localScale =
                        Vector3.one *
                        PB_CrownTuning.Scale *
                        state.RuntimeScaleMultiplier;
                }

                PB_PrepareRigidAccessory(visual, actorRoot);
                PB_EnsureCustomVisible(visual);
                BA_MakeMeshFiltersDoubleSided(visual);

                if (isCrown && state.AnchorA != null)
                {
                    // Calibrate after scale at the same world-space pose
                    // used by PB_FollowCrown; never snap back to the actor
                    // root's local origin before calculating mesh bounds.
                    visual.transform.position = state.AnchorA.position;
                    visual.transform.rotation = state.AnchorA.rotation *
                        Quaternion.Euler(PB_CrownTuning.RotationEuler);
                    state.CrownHeadLocalOffset =
                        PB_CalculateCrownHeadOffset(visual, state.AnchorA);
                }

                if (!PB_HasRenderableCustomVisual(visual))
                {
                    Plugin.NativeLog.LogError(
                        "[FateEquipmentVisuals] body prefab [" +
                        prefabName +
                        "] instantiated but has no renderable mesh hierarchy.");

                    PB_Remove(state);
                    return;
                }
            }

            PB_CacheCustomHierarchy(state);

            if (state.SlotType == Item.SlotType.Head &&
                string.Equals(
                    item.Id,
                    NhalSeraEquipment.CrownId,
                    StringComparison.OrdinalIgnoreCase))
            {
                PB_LogCrownDiagnostics(
                    state.CustomVisual,
                    state.AnchorA,
                    "spawn-ready");
            }

            // Only suppress the inherited native armour after our custom
            // prefab is confirmed to contain renderable geometry.
            PB_SuppressNativeVisuals(
                inventory,
                item,
                state);

            PB_ResolveAnchors(
                inventory,
                state);

            PB_Follow(
                inventory,
                state);

            PB_LogSpawn(
                item,
                state);
        }

        private static void PB_ResolveAnchors(
            Inventory inventory,
            PB_VisualState state)
        {
            if (inventory == null || state == null)
            {
                return;
            }

            Animator animator =
                PB_FindHumanoidAnimator(
                    inventory);

            state.AnchorA = null;
            state.AnchorB = null;

            if (animator == null)
            {
                return;
            }

            try
            {
                if (state.SlotType == Item.SlotType.Head)
                {
                    state.AnchorA = animator.GetBoneTransform(HumanBodyBones.Head);
                    state.AnchorB = animator.GetBoneTransform(HumanBodyBones.Neck);
                }
                else if (state.SlotType == Item.SlotType.Chest ||
                         state.SlotType == Item.SlotType.Waist)
                {
                    state.AnchorA = animator.GetBoneTransform(HumanBodyBones.Hips);
                    state.AnchorB = animator.GetBoneTransform(HumanBodyBones.Neck);
                }
                else if (state.SlotType == Item.SlotType.Hand)
                {
                    state.AnchorA = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    state.AnchorB = animator.GetBoneTransform(HumanBodyBones.RightHand);
                }
            }
            catch
            {
                state.AnchorA = null;
                state.AnchorB = null;
            }
        }


        private static void PB_Follow(
            Inventory inventory,
            PB_VisualState state)
        {
            if (inventory == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            // Native armour refreshes can disable child GameObjects/renderers
            // after our custom body piece first appears.  Reassert the entire
            // custom hierarchy every LateUpdate, matching the persistence
            // behaviour already used by custom weapons.  This specifically
            // prevents Crown of the Unmade from flashing once and vanishing.
            PB_EnsureCustomVisibleCached(
                state,
                false);

            if (state.CustomVisualB != null)
            {
                PB_EnsureCustomVisibleCached(
                    state,
                    true);
            }

            if (state.ActorRoot == null)
            {
                state.ActorRoot =
                    PB_GetActorRoot(inventory);
            }

            if (state.AnchorA == null)
            {
                PB_ResolveAnchors(
                    inventory,
                    state);
            }

            Transform actorRoot =
                state.ActorRoot;

            if (actorRoot == null)
            {
                return;
            }

            if (MB_UsesModularRig(state.ItemId, state.SlotType))
            {
                PB_KeepNativeHidden(state);
                if (!state.CustomVisual.activeSelf)
                {
                    state.CustomVisual.SetActive(true);
                }
                return;
            }

            if (SR_UsesNativeSkinnedRig(state.ItemId, state.SlotType))
            {
                PB_KeepNativeHidden(state);

                if (!state.CustomVisual.activeSelf)
                {
                    state.CustomVisual.SetActive(true);
                }

                // The remade Carapace / Grasp are driven entirely by the live
                // Synty skeleton. No per-frame position/rotation/scale or
                // per-piece articulation is applied here.
                return;
            }

            PB_TransformTuning tuning =
                PB_GetTuning(state.ItemId);

            if (tuning == null)
            {
                return;
            }

            // Crown of the Unmade is a rigid head accessory.  Drive it from the
            // actual animated Head bone instead of approximating the head
            // centre from Hips/Neck and actor-root rotation.  The previous
            // torso-derived offset could place the crown above the head and
            // made it look as though the custom model had vanished.
            if (state.SlotType == Item.SlotType.Head &&
                string.Equals(
                    state.ItemId,
                    NhalSeraEquipment.CrownId,
                    StringComparison.OrdinalIgnoreCase))
            {
                PB_FollowCrown(
                    inventory,
                    state,
                    tuning);

                return;
            }

            if (state.SlotType == Item.SlotType.Hand)
            {
                PB_FollowHands(
                    inventory,
                    state,
                    tuning,
                    actorRoot);

                PB_KeepNativeHidden(state);
                return;
            }

            Animator animator =
                PB_FindHumanoidAnimator(
                    inventory);

            Transform hips = null;
            Transform neck = null;
            Transform head = null;

            if (animator != null)
            {
                try
                {
                    hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                    head = animator.GetBoneTransform(HumanBodyBones.Head);
                }
                catch
                {
                }
            }

            Vector3 worldPosition =
                actorRoot.position;

            if (state.SlotType == Item.SlotType.Chest &&
                hips != null &&
                neck != null)
            {
                worldPosition =
                    Vector3.Lerp(
                        hips.position,
                        neck.position,
                        0.50f);
            }
            else if (state.SlotType == Item.SlotType.Waist &&
                     hips != null &&
                     neck != null)
            {
                worldPosition =
                    Vector3.Lerp(
                        hips.position,
                        neck.position,
                        PB_SyntyBeltHeightOverTorso);
            }
            else if (state.SlotType == Item.SlotType.Head &&
                     head != null)
            {
                worldPosition =
                    head.position;
            }
            else if (state.AnchorA != null)
            {
                worldPosition =
                    state.AnchorA.position;
            }

            Transform visual =
                state.CustomVisual.transform;

            visual.position =
                worldPosition +
                actorRoot.TransformVector(
                    tuning.Position);

            visual.rotation =
                actorRoot.rotation *
                Quaternion.Euler(
                    tuning.RotationEuler);

            visual.localScale =
                Vector3.one *
                tuning.Scale *
                state.RuntimeScaleMultiplier;

            if (state.SlotType == Item.SlotType.Chest)
            {
                PB_EnsureCarapaceArticulation(inventory, state);
                BA_ApplyBindings(state.RigidBindings);
            }

            PB_KeepNativeHidden(state);

            if (!state.CustomVisual.activeSelf)
            {
                state.CustomVisual.SetActive(true);
            }
        }


        private static void PB_FollowCrown(
            Inventory inventory,
            PB_VisualState state,
            PB_TransformTuning tuning)
        {
            if (inventory == null ||
                state == null ||
                tuning == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Animator animator =
                PB_FindHumanoidAnimator(
                    inventory);

            Transform head =
                state.AnchorA;

            if (head == null &&
                animator != null)
            {
                try
                {
                    head =
                        animator.GetBoneTransform(
                            HumanBodyBones.Head);
                }
                catch
                {
                    head = null;
                }
            }

            if (head == null)
            {
                return;
            }

            state.AnchorA =
                head;

            Transform visual =
                state.CustomVisual.transform;

            // Head provides the animated pose, but never the parent scale.
            // Retain the actor-root parent so the imported mesh cannot shrink
            // to microscopic size on a Synty Head bone.
            if (state.ActorRoot != null && visual.parent != state.ActorRoot)
            {
                visual.SetParent(state.ActorRoot, false);
                PB_SetLayerRecursive(visual, state.ActorRoot.gameObject.layer);
            }

            visual.position = head.position +
                head.rotation * (tuning.Position + state.CrownHeadLocalOffset);
            visual.rotation = head.rotation *
                Quaternion.Euler(tuning.RotationEuler);
            visual.localScale = Vector3.one *
                tuning.Scale * state.RuntimeScaleMultiplier;

            PB_EnsureCustomVisibleCached(
                state,
                false);

            if (!state.CrownFollowLogged)
            {
                PB_LogCrownDiagnostics(
                    state.CustomVisual,
                    head,
                    "head-follow");

                state.CrownFollowLogged =
                    true;
            }

            // Erenshor may rebuild/re-enable the native head equipment object
            // after our first equip frame.  Re-discover native head renderers
            // while protecting the FOTF hierarchy, then keep them suppressed.
            if (Time.unscaledTime >=
                state.NextNativeRescanTime)
            {
                state.NextNativeRescanTime =
                    Time.unscaledTime + 0.75f;

                Item equippedHead =
                    PB_GetEquippedItem(
                        inventory,
                        Item.SlotType.Head);

                if (equippedHead != null)
                {
                    PB_SuppressNativeVisuals(
                        inventory,
                        equippedHead,
                        state);
                }
            }

            PB_KeepNativeHidden(
                state);
        }


        /// <summary>
        /// Crown of the Unmade is a rigid head accessory, but an imported FBX
        /// can still expose its geometry through a SkinnedMeshRenderer tied to
        /// the prefab's private armature. Once that animator is disabled, the
        /// skinned crown can vanish. Bake those skins once into ordinary rigid
        /// MeshRenderers before disabling prefab animators.
        /// </summary>
        private static void PB_PrepareCrownRigidVisual(
            GameObject visual,
            Transform head,
            string prefabName)
        {
            if (visual == null)
            {
                return;
            }

            visual.SetActive(true);

            SkinnedMeshRenderer[] skinned =
                visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            int baked = 0;
            int failed = 0;

            for (int i = 0; skinned != null && i < skinned.Length; i++)
            {
                SkinnedMeshRenderer source = skinned[i];

                if (source == null || source.sharedMesh == null ||
                    source.sharedMesh.vertexCount <= 0)
                {
                    continue;
                }

                try
                {
                    source.gameObject.SetActive(true);
                    source.enabled = true;

                    Mesh bakedMesh = new Mesh();
                    bakedMesh.name = "FOTF_Crown_Baked_" + source.sharedMesh.name;
                    source.BakeMesh(bakedMesh);

                    if (bakedMesh.vertexCount <= 0)
                    {
                        UnityEngine.Object.Destroy(bakedMesh);
                        failed++;
                        continue;
                    }

                    GameObject rigid =
                        new GameObject(source.gameObject.name + "_FOTF_Rigid");

                    Transform rigidTransform = rigid.transform;

                    // BakeMesh returns vertices in the SkinnedMeshRenderer's
                    // own local space.  Keep the rigid replacement directly
                    // beneath that renderer transform with an identity local
                    // transform so it remains inside the FOTF Crown hierarchy
                    // even when the SkinnedMeshRenderer lives on the prefab root.
                    rigidTransform.SetParent(source.transform, false);
                    rigidTransform.localPosition = Vector3.zero;
                    rigidTransform.localRotation = Quaternion.identity;
                    rigidTransform.localScale = Vector3.one;

                    MeshFilter filter = rigid.AddComponent<MeshFilter>();
                    filter.sharedMesh = bakedMesh;

                    MeshRenderer renderer = rigid.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = source.sharedMaterials;
                    renderer.shadowCastingMode = source.shadowCastingMode;
                    renderer.receiveShadows = source.receiveShadows;
                    renderer.lightProbeUsage = source.lightProbeUsage;
                    renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                    renderer.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
                    renderer.enabled = true;

                    source.enabled = false;
                    baked++;
                }
                catch (Exception exception)
                {
                    failed++;
                    Plugin.NativeLog.LogWarning(
                        "[CrownVisual] failed to bake SkinnedMeshRenderer [" +
                        source.name +
                        "] from prefab [" +
                        (prefabName ?? "<null>") +
                        "]: " +
                        exception.Message);
                }
            }

            Plugin.NativeLog.LogInfo(
                "[CrownVisual] rigid preparation complete. Prefab=[" +
                (prefabName ?? "<null>") +
                "] SkinnedFound=[" +
                (skinned != null ? skinned.Length : 0) +
                "] Baked=[" + baked +
                "] Failed=[" + failed +
                "] Head=[" +
                (head != null ? head.name : "<null>") +
                "].");
        }


        /// <summary>
        /// A SkinnedMeshRenderer that was baked into a rigid crown must remain
        /// disabled, even when the regular custom-item persistence pass restores
        /// renderer visibility. Otherwise the original imported armature is
        /// reactivated and can render at invalid bounds / replace the rigid mesh.
        /// </summary>
        private static bool PB_IsBakedCrownSkin(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer))
                return false;

            Transform source = renderer.transform;
            for (int i = 0; source != null && i < source.childCount; i++)
            {
                Transform child = source.GetChild(i);
                if (child != null &&
                    child.name.EndsWith("_FOTF_Rigid", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // Use Mesh.bounds transformed into world space rather than trusting
        // Renderer.bounds during the first frame of a fresh FBX instantiation.
        // Unity can report zero/stale bounds before the renderer has updated.
        private static bool PB_TryGetCrownWorldBounds(GameObject visual, out Bounds bounds)
        {
            bounds = new Bounds();
            if (visual == null) return false;

            MeshFilter[] filters = visual.GetComponentsInChildren<MeshFilter>(true);
            bool found = false;
            for (int i = 0; filters != null && i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null) continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;

                Bounds local = filter.sharedMesh.bounds;
                Vector3 center = local.center;
                Vector3 extent = local.extents;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 v = new Vector3(
                        center.x + ((corner & 1) == 0 ? -extent.x : extent.x),
                        center.y + ((corner & 2) == 0 ? -extent.y : extent.y),
                        center.z + ((corner & 4) == 0 ? -extent.z : extent.z));
                    Vector3 world = filter.transform.TransformPoint(v);
                    if (!found)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                        found = true;
                    }
                    else bounds.Encapsulate(world);
                }
            }
            if (found) return true;

            // Compatibility fallback for an older skinned-only Crown bundle.
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled ||
                    PB_IsBakedCrownSkin(renderer)) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static Vector3 PB_CalculateCrownHeadOffset(GameObject visual, Transform head)
        {
            if (visual == null || head == null) return Vector3.zero;

            Bounds bounds;
            if (!PB_TryGetCrownWorldBounds(visual, out bounds))
                return Vector3.zero;

            // The crown is WORLD-followed but root-parented. Head rotation
            // defines the offset axes, not Head lossyScale. Never clamp this
            // offset to 0.45m: some FBX exports put their geometry far from
            // the prefab pivot, and the old clamp left them invisible.
            Vector3 targetCenter = head.position + head.up * 0.125f - head.forward * 0.020f;
            Vector3 correction = targetCenter - bounds.center;
            if (correction.magnitude > 0.45f)
            {
                Plugin.NativeLog.LogWarning(
                    "[CrownVisual] prefab pivot is displaced by " +
                    correction.magnitude.ToString("0.000") +
                    "m; centering on the visible mesh rather than the pivot.");
            }
            Vector3 local = Quaternion.Inverse(head.rotation) * correction;
            Plugin.NativeLog.LogInfo(
                "[CrownVisual] geometry normalized. WorldSize=[" +
                bounds.size.ToString("F3") + "] OffsetWorld=[" +
                correction.ToString("F3") + "] HeadLocal=[" +
                local.ToString("F3") + "].");
            return local;
        }

        private static float PB_CalculateCrownScaleMultiplier(
            Inventory inventory,
            GameObject visual,
            Transform head)
        {
            if (visual == null)
            {
                return 1f;
            }

            // The current Crown FBX is imported at a much smaller world scale
            // than the other Synty-authored body pieces (runtime diagnostics
            // reported only ~0.007 world units across). Normalize the rigid
            // accessory once against the live head/neck span instead of baking
            // another arbitrary import multiplier into every update.
            visual.transform.localScale =
                Vector3.one;

            PB_EnsureCustomVisible(
                visual);

            Bounds combined;
            if (!PB_TryGetCrownWorldBounds(visual, out combined))
                return 1f;

            Vector3 size = combined.size;
            float currentMax = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (currentMax <= 0.0001f)
                return 1f;

            float targetMax =
                0.40f;

            Animator animator =
                PB_FindHumanoidAnimator(
                    inventory);

            Transform neck =
                null;

            if (animator != null)
            {
                try
                {
                    neck =
                        animator.GetBoneTransform(
                            HumanBodyBones.Neck);
                }
                catch
                {
                    neck =
                        null;
                }
            }

            if (head != null &&
                neck != null)
            {
                float headNeckSpan =
                    Vector3.Distance(
                        head.position,
                        neck.position);

                if (headNeckSpan > 0.03f &&
                    headNeckSpan < 0.60f)
                {
                    targetMax =
                        Mathf.Clamp(
                            headNeckSpan * 2.05f,
                            0.36f,
                            0.46f);
                }
            }

            float multiplier =
                Mathf.Clamp(
                    targetMax / currentMax,
                    0.25f,
                    100000f);

            Plugin.NativeLog.LogInfo(
                "[CrownVisual] auto-scale CurrentMax=[" +
                currentMax.ToString("0.0000") +
                "] TargetMax=[" +
                targetMax.ToString("0.000") +
                "] Multiplier=[" +
                multiplier.ToString("0.00") +
                "].");

            return multiplier;
        }


        private static void PB_LogCrownDiagnostics(
            GameObject visual,
            Transform head,
            string stage)
        {
            if (visual == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[CrownVisual] stage=[" + stage + "] visual=<null>.");
                return;
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(true);

            int enabled = 0;
            int meshRenderers = 0;
            int skinnedRenderers = 0;
            Bounds combined = new Bounds();
            bool haveBounds = false;

            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.enabled) enabled++;
                if (renderer is MeshRenderer) meshRenderers++;
                if (renderer is SkinnedMeshRenderer) skinnedRenderers++;

                if (!haveBounds)
                {
                    combined = renderer.bounds;
                    haveBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            Vector3 size = haveBounds ? combined.size : Vector3.zero;

            Plugin.NativeLog.LogInfo(
                "[CrownVisual] stage=[" + stage +
                "] RootActiveSelf=[" + visual.activeSelf +
                "] ActiveInHierarchy=[" + visual.activeInHierarchy +
                "] Parent=[" +
                (visual.transform.parent != null ? visual.transform.parent.name : "<none>") +
                "] Head=[" +
                (head != null ? head.name : "<null>") +
                "] Renderers=[" +
                (renderers != null ? renderers.Length : 0) +
                "] Enabled=[" + enabled +
                "] Mesh=[" + meshRenderers +
                "] Skinned=[" + skinnedRenderers +
                "] Bounds=(" +
                size.x.ToString("0.000") + "," +
                size.y.ToString("0.000") + "," +
                size.z.ToString("0.000") +
                " ).");
        }


        private static void PB_PrepareRigidAccessory(
            GameObject visual,
            Transform actorRoot)
        {
            if (visual == null)
            {
                return;
            }

            // Imported rigid FBX prefabs occasionally carry an Animator or
            // legacy Animation component.  A private prefab animator must never
            // be allowed to drive/reset a crown, girdle or other rigid accessory
            // after it has been mounted to the live Synty rig.
            Animator[] animators =
                visual.GetComponentsInChildren<Animator>(
                    true);

            for (int i = 0;
                 animators != null && i < animators.Length;
                 i++)
            {
                if (animators[i] != null)
                {
                    animators[i].enabled =
                        false;
                }
            }

            Animation[] animations =
                visual.GetComponentsInChildren<Animation>(
                    true);

            for (int i = 0;
                 animations != null && i < animations.Length;
                 i++)
            {
                if (animations[i] != null)
                {
                    animations[i].enabled =
                        false;
                }
            }

            if (actorRoot != null)
            {
                PB_SetLayerRecursive(
                    visual.transform,
                    actorRoot.gameObject.layer);
            }
        }


        private static void PB_SetLayerRecursive(
            Transform root,
            int layer)
        {
            if (root == null)
            {
                return;
            }

            if (root.gameObject != null)
            {
                root.gameObject.layer =
                    layer;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                PB_SetLayerRecursive(
                    root.GetChild(i),
                    layer);
            }
        }


        private static bool PB_SpawnHandPair(
            GameObject prefab,
            Item item,
            PB_VisualState state,
            Transform actorRoot)
        {
            if (prefab == null ||
                item == null ||
                state == null ||
                actorRoot == null)
            {
                return false;
            }

            GameObject left =
                UnityEngine.Object.Instantiate(
                    prefab,
                    actorRoot,
                    false);

            GameObject right =
                UnityEngine.Object.Instantiate(
                    prefab,
                    actorRoot,
                    false);

            if (left == null ||
                right == null)
            {
                if (left != null)
                {
                    UnityEngine.Object.Destroy(left);
                }

                if (right != null)
                {
                    UnityEngine.Object.Destroy(right);
                }

                return false;
            }

            left.name =
                PB_CustomPrefix +
                item.Id +
                "_Hands_Left";

            right.name =
                PB_CustomPrefix +
                item.Id +
                "_Hands_Right";

            PB_DisablePhysics(left);
            PB_DisablePhysics(right);
            PB_RepairUnsupportedShaders(left);
            PB_RepairUnsupportedShaders(right);

            PB_EnsureCustomVisible(left);
            PB_EnsureCustomVisible(right);
            BA_MakeMeshFiltersDoubleSided(left);
            BA_MakeMeshFiltersDoubleSided(right);

            PB_HideModelSide(
                left,
                "Right_");

            PB_HideModelSide(
                right,
                "Left_");

            if (!PB_HasRenderableCustomVisual(left) ||
                !PB_HasRenderableCustomVisual(right))
            {
                UnityEngine.Object.Destroy(left);
                UnityEngine.Object.Destroy(right);
                return false;
            }

            state.CustomVisual = left;
            state.CustomVisualB = right;

            state.HasReferenceA =
                PB_TryGetNamedRendererLocalPoint(
                    left,
                    "Left_WristLame_",
                    out state.ReferencePointA);

            state.HasReferenceB =
                PB_TryGetNamedRendererLocalPoint(
                    right,
                    "Right_WristLame_",
                    out state.ReferencePointB);

            return true;
        }


        private static void PB_FollowHands(
            Inventory inventory,
            PB_VisualState state,
            PB_TransformTuning tuning,
            Transform actorRoot)
        {
            if (inventory == null ||
                state == null ||
                tuning == null ||
                actorRoot == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Animator animator =
                PB_FindHumanoidAnimator(
                    inventory);

            if (animator == null)
            {
                return;
            }

            Transform leftHand = null;
            Transform rightHand = null;
            Transform leftLowerArm = null;
            Transform rightLowerArm = null;

            try
            {
                leftHand =
                    animator.GetBoneTransform(
                        HumanBodyBones.LeftHand);

                rightHand =
                    animator.GetBoneTransform(
                        HumanBodyBones.RightHand);

                leftLowerArm =
                    animator.GetBoneTransform(
                        HumanBodyBones.LeftLowerArm);

                rightLowerArm =
                    animator.GetBoneTransform(
                        HumanBodyBones.RightLowerArm);
            }
            catch
            {
            }

            PB_FitSingleHand(
                state.CustomVisual,
                leftHand,
                leftLowerArm,
                actorRoot,
                tuning,
                state.HasReferenceA,
                state.ReferencePointA,
                ref state.HasHandRotationOffsetA,
                ref state.HandRotationOffsetA);

            PB_FitSingleHand(
                state.CustomVisualB,
                rightHand,
                rightLowerArm,
                actorRoot,
                tuning,
                state.HasReferenceB,
                state.ReferencePointB,
                ref state.HasHandRotationOffsetB,
                ref state.HandRotationOffsetB);

            PB_EnsureHandArticulation(
                animator,
                state,
                leftHand,
                rightHand,
                leftLowerArm,
                rightLowerArm);

            BA_ApplyBindings(state.RigidBindings);
        }


        private static void PB_FitSingleHand(
            GameObject visual,
            Transform hand,
            Transform lowerArm,
            Transform actorRoot,
            PB_TransformTuning tuning,
            bool hasReferencePoint,
            Vector3 referencePoint,
            ref bool hasHandRotationOffset,
            ref Quaternion handRotationOffset)
        {
            if (visual == null ||
                hand == null ||
                actorRoot == null ||
                tuning == null)
            {
                return;
            }

            Transform transform = visual.transform;

            if (!hasHandRotationOffset)
            {
                Quaternion authoredWorldRotation =
                    BA_BuildAuthoredHandRotation(
                        hand,
                        lowerArm,
                        actorRoot);

                handRotationOffset =
                    Quaternion.Inverse(hand.rotation) *
                    authoredWorldRotation;

                hasHandRotationOffset = true;
            }

            transform.rotation =
                hand.rotation *
                handRotationOffset *
                Quaternion.Euler(tuning.RotationEuler);

            transform.localScale =
                Vector3.one * tuning.Scale;

            Vector3 target =
                hand.position +
                actorRoot.TransformVector(tuning.Position);

            transform.position = target;

            // The V7 prefab is authored around the centre of the wrist lames,
            // not around the FBX root. Pin that authored wrist point exactly to
            // the animated Synty Hand transform. This keeps the hand attached
            // while the captured rotation offset preserves the authored glove
            // orientation instead of inheriting arbitrary Hand local axes.
            if (hasReferencePoint)
            {
                Vector3 referenceWorld =
                    transform.TransformPoint(referencePoint);

                transform.position +=
                    target - referenceWorld;
            }

            if (!visual.activeSelf)
            {
                visual.SetActive(true);
            }
        }


        private static Transform PB_GetBoneSafe(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
            {
                return null;
            }

            try
            {
                return animator.GetBoneTransform(bone);
            }
            catch
            {
                return null;
            }
        }


        private static void PB_EnsureCarapaceArticulation(
            Inventory inventory,
            PB_VisualState state)
        {
            if (inventory == null ||
                state == null ||
                state.CustomVisual == null ||
                state.RigidBindingsInitialized)
            {
                return;
            }

            Animator animator = PB_FindHumanoidAnimator(inventory);
            if (animator == null)
            {
                return;
            }

            BA_DestroyBindings(state.RigidBindings);

            Transform leftUpperArm = PB_GetBoneSafe(animator, HumanBodyBones.LeftUpperArm);
            Transform rightUpperArm = PB_GetBoneSafe(animator, HumanBodyBones.RightUpperArm);
            Transform leftShoulder = PB_GetBoneSafe(animator, HumanBodyBones.LeftShoulder) ?? leftUpperArm;
            Transform rightShoulder = PB_GetBoneSafe(animator, HumanBodyBones.RightShoulder) ?? rightUpperArm;

            // Keep each side of the shoulder/upper-arm shell on ONE animated
            // basis. Earlier passes bound the pauldron to Shoulder and the arm
            // guard to UpperArm; those bones rotate differently and physically
            // pulled the authored overlapping plates apart. Prefer UpperArm for
            // the complete side assembly, falling back to Shoulder only if the
            // humanoid avatar does not expose an UpperArm transform.
            Transform leftArmBasis = leftUpperArm ?? leftShoulder;
            Transform rightArmBasis = rightUpperArm ?? rightShoulder;

            string[] shoulderPieces = { "Base", "Cap", "Tip" };
            for (int i = 0; i < shoulderPieces.Length; i++)
            {
                BA_AddBinding(state.RigidBindings, state.CustomVisual, "Pauldron_L_" + shoulderPieces[i], leftArmBasis);
                BA_AddBinding(state.RigidBindings, state.CustomVisual, "Pauldron_R_" + shoulderPieces[i], rightArmBasis);
            }

            BA_AddBinding(state.RigidBindings, state.CustomVisual, "ShoulderGem_L", leftArmBasis);
            BA_AddBinding(state.RigidBindings, state.CustomVisual, "ShoulderGem_R", rightArmBasis);
            BA_AddBinding(state.RigidBindings, state.CustomVisual, "UpperArmGuard_L", leftArmBasis);
            BA_AddBinding(state.RigidBindings, state.CustomVisual, "UpperArmTrim_L", leftArmBasis);
            BA_AddBinding(state.RigidBindings, state.CustomVisual, "UpperArmGuard_R", rightArmBasis);
            BA_AddBinding(state.RigidBindings, state.CustomVisual, "UpperArmTrim_R", rightArmBasis);

            state.RigidBindingsInitialized = state.RigidBindings.Count > 0;

            if (state.RigidBindingsInitialized)
            {
                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] Carapace unified arm bindings=[" +
                    state.RigidBindings.Count + "].");
            }
        }


        private static void PB_EnsureHandArticulation(
            Animator animator,
            PB_VisualState state,
            Transform leftHand,
            Transform rightHand,
            Transform leftLowerArm,
            Transform rightLowerArm)
        {
            if (animator == null ||
                state == null ||
                state.CustomVisual == null ||
                state.CustomVisualB == null ||
                state.RigidBindingsInitialized)
            {
                return;
            }

            BA_DestroyBindings(state.RigidBindings);

            string[] forearmPieces =
            {
                "Cuff",
                "DorsalRidge",
                "ElbowSpike",
                "SideFin_A",
                "SideFin_B",
                "VoidGem"
            };

            for (int i = 0; i < forearmPieces.Length; i++)
            {
                BA_AddBinding(state.RigidBindings, state.CustomVisual, "Left_" + forearmPieces[i], leftLowerArm);
                BA_AddBinding(state.RigidBindings, state.CustomVisualB, "Right_" + forearmPieces[i], rightLowerArm);
            }

            int resolvedFingerSegments = 0;

            // Prefer Animator humanoid finger mappings, but fall back to the
            // actual animated Synty hand descendants when those mappings are
            // absent. The latter is common on game avatars that still animate
            // the finger transforms normally.
            for (int modelFinger = 0; modelFinger < 4; modelFinger++)
            {
                for (int segment = 0; segment < 3; segment++)
                {
                    Transform leftBone =
                        BA_ResolveFingerTransform(
                            animator,
                            leftHand,
                            true,
                            modelFinger,
                            segment);

                    Transform rightBone =
                        BA_ResolveFingerTransform(
                            animator,
                            rightHand,
                            false,
                            modelFinger,
                            segment);

                    if (leftBone != null)
                    {
                        BA_AddBinding(
                            state.RigidBindings,
                            state.CustomVisual,
                            "Left_Finger_" + modelFinger + "_Lame_" + segment,
                            leftBone);
                        resolvedFingerSegments++;
                    }

                    if (rightBone != null)
                    {
                        BA_AddBinding(
                            state.RigidBindings,
                            state.CustomVisualB,
                            "Right_Finger_" + modelFinger + "_Lame_" + segment,
                            rightBone);
                        resolvedFingerSegments++;
                    }
                }

                Transform leftDistal =
                    BA_ResolveFingerTransform(animator, leftHand, true, modelFinger, 2);
                Transform rightDistal =
                    BA_ResolveFingerTransform(animator, rightHand, false, modelFinger, 2);

                BA_AddBinding(state.RigidBindings, state.CustomVisual, "Left_Finger_" + modelFinger + "_Claw", leftDistal);
                BA_AddBinding(state.RigidBindings, state.CustomVisualB, "Right_Finger_" + modelFinger + "_Claw", rightDistal);
            }

            for (int segment = 0; segment < 3; segment++)
            {
                Transform leftThumb =
                    BA_ResolveThumbTransform(animator, leftHand, true, segment);
                Transform rightThumb =
                    BA_ResolveThumbTransform(animator, rightHand, false, segment);

                BA_AddBinding(
                    state.RigidBindings,
                    state.CustomVisual,
                    "Left_Thumb_" + segment,
                    leftThumb);

                BA_AddBinding(
                    state.RigidBindings,
                    state.CustomVisualB,
                    "Right_Thumb_" + segment,
                    rightThumb);
            }

            state.RigidBindingsInitialized = state.RigidBindings.Count > 0;

            if (state.RigidBindingsInitialized)
            {
                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] Grasp bind-pose bindings=[" +
                    state.RigidBindings.Count +
                    "] FingerSegments=[" +
                    resolvedFingerSegments +
                    "/24].");
            }
        }


        private static bool PB_TryGetNamedRendererLocalPoint(
            GameObject root,
            string namePrefix,
            out Vector3 localPoint)
        {
            localPoint = Vector3.zero;

            if (root == null ||
                string.IsNullOrEmpty(namePrefix))
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            Bounds bounds =
                new Bounds();

            bool found =
                false;

            for (int i = 0;
                 renderers != null && i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    !renderer.name.StartsWith(
                        namePrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!found)
                {
                    bounds =
                        renderer.bounds;

                    found =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            if (!found)
            {
                return false;
            }

            localPoint =
                root.transform.InverseTransformPoint(
                    bounds.center);

            return true;
        }


        private static void PB_HideModelSide(
            GameObject root,
            string prefix)
        {
            if (root == null ||
                string.IsNullOrEmpty(prefix))
            {
                return;
            }

            Transform[] all =
                root.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 all != null && i < all.Length;
                 i++)
            {
                Transform current =
                    all[i];

                if (current == null ||
                    current == root.transform)
                {
                    continue;
                }

                if (current.name.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    current.gameObject.SetActive(false);
                }
            }
        }


        private static Transform PB_GetActorRoot(
            Inventory inventory)
        {
            if (inventory == null ||
                inventory.transform == null)
            {
                return null;
            }

            return inventory.transform.root;
        }

        private static Animator PB_FindHumanoidAnimator(
            Inventory inventory)
        {
            Transform root =
                PB_GetActorRoot(inventory);

            if (root == null)
            {
                return null;
            }

            int rootId = root.GetInstanceID();

            if (_pbCachedAnimatorRootId == rootId &&
                _pbCachedAnimator != null)
            {
                return _pbCachedAnimator;
            }

            Animator direct =
                root.GetComponent<Animator>();

            if (direct != null &&
                direct.avatar != null &&
                direct.avatar.isHuman)
            {
                _pbCachedAnimatorRootId = rootId;
                _pbCachedAnimator = direct;
                return direct;
            }

            Animator[] all =
                root.GetComponentsInChildren<Animator>(
                    true);

            for (int i = 0;
                 all != null && i < all.Length;
                 i++)
            {
                Animator candidate = all[i];

                if (candidate != null &&
                    candidate.avatar != null &&
                    candidate.avatar.isHuman)
                {
                    _pbCachedAnimatorRootId = rootId;
                    _pbCachedAnimator = candidate;
                    return candidate;
                }
            }

            return direct;
        }

        private static Item PB_GetEquippedItem(
            Inventory inventory,
            Item.SlotType slotType)
        {
            if (inventory == null ||
                inventory.EquipmentSlots == null)
            {
                return null;
            }

            for (int i = 0;
                 i < inventory.EquipmentSlots.Count;
                 i++)
            {
                ItemIcon slot =
                    inventory.EquipmentSlots[i];

                if (slot != null &&
                    slot.ThisSlotType == slotType)
                {
                    return slot.MyItem;
                }
            }

            return null;
        }

        private static string PB_GetPrefabForBodyItem(
            Item item,
            Item.SlotType slotType)
        {
            if (item == null)
            {
                return null;
            }

            if (slotType == Item.SlotType.Head &&
                string.Equals(
                    item.Id,
                    NhalSeraEquipment.CrownId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return NhalSeraEquipment.CrownPrefab;
            }

            if (slotType == Item.SlotType.Hand &&
                string.Equals(
                    item.Id,
                    NhalSeraEquipment.GraspId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return NhalSeraEquipment.GraspPrefab;
            }

            if (slotType == Item.SlotType.Waist &&
                string.Equals(
                    item.Id,
                    NhalSeraEquipment.GirdleId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return NhalSeraEquipment.GirdlePrefab;
            }

            return null;
        }

        private static PB_TransformTuning PB_GetTuning(string itemId)
        {
            if (string.Equals(itemId, NhalSeraEquipment.CarapaceId, StringComparison.OrdinalIgnoreCase))
                return PB_CarapaceTuning;
            if (string.Equals(itemId, NhalSeraEquipment.CrownId, StringComparison.OrdinalIgnoreCase))
                return PB_CrownTuning;
            if (string.Equals(itemId, NhalSeraEquipment.GraspId, StringComparison.OrdinalIgnoreCase))
                return PB_GraspTuning;
            if (string.Equals(itemId, NhalSeraEquipment.GirdleId, StringComparison.OrdinalIgnoreCase))
                return PB_GirdleTuning;
            return null;
        }

        private static void PB_SuppressNativeVisuals(
            Inventory inventory,
            Item item,
            PB_VisualState state)
        {
            if (inventory == null || item == null || state == null || state.ActorRoot == null)
                return;

            string[] targets =
            {
                item.EquipmentToActivate,
                item.ShoulderTrimL, item.ShoulderTrimR,
                item.ElbowTrimL, item.ElbowTrimR,
                item.KneeTrimL, item.KneeTrimR
            };

            Transform[] all = state.ActorRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null || candidate == state.ActorRoot ||
                    candidate.name.StartsWith(PB_CustomPrefix, StringComparison.Ordinal))
                    continue;

                bool matched = false;
                for (int t = 0; t < targets.Length; t++)
                {
                    if (!string.IsNullOrWhiteSpace(targets[t]) &&
                        string.Equals(candidate.name, targets[t], StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    continue;

                Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; renderers != null && r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null)
                        continue;

                    // If the native template's EquipmentToActivate happens to
                    // resolve to a broad head/rig transform, do not let its
                    // renderer sweep disable our custom crown simply because
                    // the crown is now correctly parented under the Head bone.
                    if ((state.CustomVisual != null &&
                         (renderer.transform == state.CustomVisual.transform ||
                          renderer.transform.IsChildOf(
                              state.CustomVisual.transform))) ||
                        (state.CustomVisualB != null &&
                         (renderer.transform == state.CustomVisualB.transform ||
                          renderer.transform.IsChildOf(
                              state.CustomVisualB.transform))) ||
                        renderer.transform.name.StartsWith(
                            PB_CustomPrefix,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!state.HiddenNativeRenderers.Contains(renderer))
                        state.HiddenNativeRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }
        }

        private static void PB_KeepNativeHidden(PB_VisualState state)
        {
            if (state == null)
                return;

            for (int i = state.HiddenNativeRenderers.Count - 1; i >= 0; i--)
            {
                Renderer renderer = state.HiddenNativeRenderers[i];
                if (renderer == null)
                {
                    state.HiddenNativeRenderers.RemoveAt(i);
                    continue;
                }
                if (renderer.enabled)
                    renderer.enabled = false;
            }
        }

        private static void PB_RestoreNative(PB_VisualState state)
        {
            if (state == null)
                return;
            for (int i = 0; i < state.HiddenNativeRenderers.Count; i++)
            {
                Renderer renderer = state.HiddenNativeRenderers[i];
                if (renderer != null)
                    renderer.enabled = true;
            }
            state.HiddenNativeRenderers.Clear();
        }

        internal bool PB_TryTuneEquippedVisual(
            string slotToken,
            string operation,
            string[] values,
            out string message)
        {
            message = null;
            Inventory inventory = GameData.PlayerInv;
            if (inventory == null)
            {
                message = "Player inventory is not available.";
                return false;
            }

            PB_VisualState state = PB_ResolveTuneState(slotToken);
            if (state == null)
            {
                message = "Use chest, head, hands, or waist.";
                return false;
            }

            Item item = PB_GetEquippedItem(inventory, state.SlotType);
            PB_TransformTuning tuning = item != null ? PB_GetTuning(item.Id) : null;
            if (item == null || tuning == null)
            {
                message = "No tunable FOTF item is equipped in " + state.Label + ".";
                return false;
            }

            if (MB_UsesModularRig(item.Id, state.SlotType))
            {
                message =
                    item.ItemName +
                    " uses authored bone-local modular pieces. Position/rotation/scale is defined by the Blender asset.";
                return true;
            }

            if (SR_UsesNativeSkinnedRig(item.Id, state.SlotType))
            {
                message =
                    item.ItemName +
                    " uses native Synty skinning. Position/rotation/scale tuning is no longer applied at runtime.";
                return true;
            }

            string op = string.IsNullOrWhiteSpace(operation) ? "status" : operation.Trim().ToLowerInvariant();
            if (op == "reset")
            {
                tuning.Reset();
            }
            else if (op == "pos" || op == "move" || op == "rot" || op == "turn")
            {
                Vector3 value;
                if (!PB_TryParseVector(values, out value))
                {
                    message = "This operation requires three numbers: x y z.";
                    return false;
                }
                if (op == "pos") tuning.Position = value;
                else if (op == "move") tuning.Position += value;
                else if (op == "rot") tuning.RotationEuler = value;
                else tuning.RotationEuler += value;
            }
            else if (op == "scale")
            {
                float value;
                if (values == null || values.Length < 1 ||
                    !float.TryParse(values[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out value) || value <= 0f)
                {
                    message = "Scale requires one positive number.";
                    return false;
                }
                tuning.Scale = value;
            }
            else if (op != "status")
            {
                message = "Usage: /fotfvisual [chest|head|hands|waist] status|pos|move|rot|turn|scale|reset";
                return false;
            }

            // Articulated arm/finger pivots are parented directly under the
            // native animated skeleton.  A live transform change therefore
            // needs a clean respawn so their bind pose is captured from the new
            // fitted root instead of leaving old detached pivots behind.
            if (op != "status")
            {
                PB_Remove(state);
                PB_RefreshSlot(inventory, state);
            }
            else
            {
                if (state.CustomVisual == null)
                    PB_RefreshSlot(inventory, state);
                if (state.CustomVisual != null)
                    PB_Follow(inventory, state);
            }

            message =
                item.ItemName + " [" + state.Label + "] pos " +
                tuning.Position.x.ToString("0.###") + " " +
                tuning.Position.y.ToString("0.###") + " " +
                tuning.Position.z.ToString("0.###") +
                " rot " + tuning.RotationEuler.x.ToString("0.###") + " " +
                tuning.RotationEuler.y.ToString("0.###") + " " +
                tuning.RotationEuler.z.ToString("0.###") +
                " scale " + tuning.Scale.ToString("0.###");
            return true;
        }

        private PB_VisualState PB_ResolveTuneState(string slotToken)
        {
            if (string.Equals(slotToken, "chest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "c", StringComparison.OrdinalIgnoreCase)) return _pbChest;
            if (string.Equals(slotToken, "head", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "h", StringComparison.OrdinalIgnoreCase)) return _pbHead;
            if (string.Equals(slotToken, "hands", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "hand", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "gloves", StringComparison.OrdinalIgnoreCase)) return _pbHands;
            if (string.Equals(slotToken, "waist", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "belt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "w", StringComparison.OrdinalIgnoreCase)) return _pbWaist;
            return null;
        }

        private static bool PB_TryParseVector(string[] values, out Vector3 result)
        {
            result = Vector3.zero;
            if (values == null || values.Length < 3) return false;
            float x, y, z;
            if (!float.TryParse(values[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(values[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) ||
                !float.TryParse(values[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
                return false;
            result = new Vector3(x, y, z);
            return true;
        }

        private static void PB_CacheCustomHierarchy(
            PB_VisualState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.CustomVisual != null)
            {
                state.CachedHierarchyA =
                    state.CustomVisual.GetComponentsInChildren<Transform>(
                        true);

                state.CachedRenderersA =
                    state.CustomVisual.GetComponentsInChildren<Renderer>(
                        true);
            }

            if (state.CustomVisualB != null)
            {
                state.CachedHierarchyB =
                    state.CustomVisualB.GetComponentsInChildren<Transform>(
                        true);

                state.CachedRenderersB =
                    state.CustomVisualB.GetComponentsInChildren<Renderer>(
                        true);
            }
        }


        private static void PB_EnsureCustomVisibleCached(
            PB_VisualState state,
            bool secondary)
        {
            if (state == null)
            {
                return;
            }

            GameObject visual =
                secondary
                    ? state.CustomVisualB
                    : state.CustomVisual;

            if (visual == null)
            {
                return;
            }

            if (!visual.activeSelf)
            {
                visual.SetActive(
                    true);
            }

            Transform[] hierarchy =
                secondary
                    ? state.CachedHierarchyB
                    : state.CachedHierarchyA;

            Renderer[] renderers =
                secondary
                    ? state.CachedRenderersB
                    : state.CachedRenderersA;

            if (hierarchy == null ||
                renderers == null)
            {
                PB_CacheCustomHierarchy(
                    state);

                hierarchy =
                    secondary
                        ? state.CachedHierarchyB
                        : state.CachedHierarchyA;

                renderers =
                    secondary
                        ? state.CachedRenderersB
                        : state.CachedRenderersA;
            }

            for (int i = 0;
                 hierarchy != null &&
                 i < hierarchy.Length;
                 i++)
            {
                Transform child =
                    hierarchy[i];

                if (child != null &&
                    child.gameObject != null &&
                    !child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(
                        true);
                }
            }

            for (int i = 0;
                 renderers != null &&
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null) continue;
                if (PB_IsBakedCrownSkin(renderer))
                {
                    if (renderer.enabled) renderer.enabled = false;
                    continue;
                }
                if (!renderer.enabled) renderer.enabled = true;
            }
        }


        private static void PB_EnsureCustomVisible(
            GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            visual.SetActive(true);

            Transform[] transforms =
                visual.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 transforms != null && i < transforms.Length;
                 i++)
            {
                Transform transform = transforms[i];

                if (transform != null &&
                    transform.gameObject != null &&
                    !transform.gameObject.activeSelf)
                {
                    transform.gameObject.SetActive(true);
                }
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(
                    true);

            for (int i = 0;
                 renderers != null && i < renderers.Length;
                 i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].enabled = !PB_IsBakedCrownSkin(renderers[i]);
            }
        }

        private static bool PB_HasRenderableCustomVisual(
            GameObject visual)
        {
            if (visual == null)
            {
                return false;
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(
                    true);

            if (renderers == null ||
                renderers.Length == 0)
            {
                return false;
            }

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                MeshRenderer meshRenderer =
                    renderer as MeshRenderer;

                if (meshRenderer != null)
                {
                    MeshFilter filter =
                        meshRenderer.GetComponent<MeshFilter>();

                    if (filter != null &&
                        filter.sharedMesh != null &&
                        filter.sharedMesh.vertexCount > 0)
                    {
                        return true;
                    }
                }

                SkinnedMeshRenderer skinned =
                    renderer as SkinnedMeshRenderer;

                if (skinned != null &&
                    skinned.sharedMesh != null &&
                    skinned.sharedMesh.vertexCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void PB_DisablePhysics(
            GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            Collider[] colliders =
                visual.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 colliders != null && i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody[] bodies =
                visual.GetComponentsInChildren<Rigidbody>(
                    true);

            for (int i = 0;
                 bodies != null && i < bodies.Length;
                 i++)
            {
                Rigidbody body = bodies[i];

                if (body != null)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }
            }
        }

        private static void PB_RepairUnsupportedShaders(
            GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            Shader standard =
                Shader.Find("Standard");

            if (standard == null)
            {
                return;
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(
                    true);

            for (int r = 0;
                 renderers != null && r < renderers.Length;
                 r++)
            {
                Renderer renderer = renderers[r];

                if (renderer == null)
                {
                    continue;
                }

                Material[] materials =
                    renderer.materials;

                for (int m = 0;
                     materials != null && m < materials.Length;
                     m++)
                {
                    Material material = materials[m];

                    if (material == null)
                    {
                        continue;
                    }

                    Shader shader = material.shader;
                    string shaderName =
                        shader != null
                            ? shader.name
                            : string.Empty;

                    if (shader == null ||
                        !shader.isSupported ||
                        shaderName.IndexOf(
                            "Universal Render Pipeline",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shaderName.IndexOf(
                            "HDRP",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        material.shader = standard;
                    }
                }
            }
            BA_ForceDoubleSided(visual);
        }

        private static void PB_LogSpawn(
            Item item,
            PB_VisualState state)
        {
            if (item == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Renderer[] renderersA =
                state.CustomVisual.GetComponentsInChildren<Renderer>(
                    true);

            Renderer[] renderersB =
                state.CustomVisualB != null
                    ? state.CustomVisualB.GetComponentsInChildren<Renderer>(true)
                    : null;

            int rendererCount =
                (renderersA != null ? renderersA.Length : 0) +
                (renderersB != null ? renderersB.Length : 0);

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] Synty-fit body visual attached [" +
                state.PrefabName +
                "] to [" +
                item.ItemName +
                "] Slot=[" +
                state.Label +
                "] AnchorA=[" +
                (state.AnchorA != null ? state.AnchorA.name : "<none>") +
                "] AnchorB=[" +
                (state.AnchorB != null ? state.AnchorB.name : "<none>") +
                "] Renderers=[" +
                rendererCount +
                "] WorldPos=[" +
                state.CustomVisual.transform.position +
                "] Scale=[" +
                state.CustomVisual.transform.localScale +
                "].");
        }

        private static void PB_Remove(
            PB_VisualState state)
        {
            if (state == null)
            {
                return;
            }

            PB_RestoreNative(state);
            BA_DestroyBindings(state.RigidBindings);

            if (state.CustomVisual != null)
            {
                UnityEngine.Object.Destroy(
                    state.CustomVisual);
            }

            if (state.CustomVisualB != null)
            {
                UnityEngine.Object.Destroy(
                    state.CustomVisualB);
            }

            state.ItemId = null;
            state.PrefabName = null;
            state.CustomVisual = null;
            state.CustomVisualB = null;
            state.ActorRoot = null;
            state.AnchorA = null;
            state.AnchorB = null;
            state.ReferencePointA = Vector3.zero;
            state.ReferencePointB = Vector3.zero;
            state.HasReferenceA = false;
            state.HasReferenceB = false;
            state.RigidBindingsInitialized = false;
            state.CrownFollowLogged = false;
            state.CrownHeadLocalOffset = Vector3.zero;
            state.RuntimeScaleMultiplier = 1f;
            state.NextNativeRescanTime = 0f;
            state.CachedHierarchyA = null;
            state.CachedHierarchyB = null;
            state.CachedRenderersA = null;
            state.CachedRenderersB = null;
            state.HasHandRotationOffsetA = false;
            state.HasHandRotationOffsetB = false;
            state.HandRotationOffsetA = Quaternion.identity;
            state.HandRotationOffsetB = Quaternion.identity;
        }

        private sealed class PB_VisualState
        {
            internal PB_VisualState(
                Item.SlotType slotType,
                string label)
            {
                SlotType = slotType;
                Label = label;
            }

            internal Item.SlotType SlotType;
            internal string Label;
            internal string ItemId;
            internal string PrefabName;
            internal GameObject CustomVisual;
            internal GameObject CustomVisualB;
            internal Transform ActorRoot;
            internal Transform AnchorA;
            internal Transform AnchorB;
            internal Vector3 ReferencePointA;
            internal Vector3 ReferencePointB;
            internal bool HasReferenceA;
            internal bool HasReferenceB;
            internal bool HasHandRotationOffsetA;
            internal bool HasHandRotationOffsetB;
            internal Quaternion HandRotationOffsetA = Quaternion.identity;
            internal Quaternion HandRotationOffsetB = Quaternion.identity;
            internal bool RigidBindingsInitialized;
            internal bool CrownFollowLogged;
            internal Vector3 CrownHeadLocalOffset;
            internal float RuntimeScaleMultiplier = 1f;
            internal float NextNativeRescanTime;
            internal Transform[] CachedHierarchyA;
            internal Transform[] CachedHierarchyB;
            internal Renderer[] CachedRenderersA;
            internal Renderer[] CachedRenderersB;
            internal readonly List<BA_RigidBinding> RigidBindings =
                new List<BA_RigidBinding>();
            internal readonly List<Renderer> HiddenNativeRenderers =
                new List<Renderer>();
        }
    }
}
