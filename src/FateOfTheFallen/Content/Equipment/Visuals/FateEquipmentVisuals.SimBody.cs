using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FateOfTheFallen
{
internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        private const float SB_RefreshInterval = 0.75f;
        private const string SB_CustomPrefix = "FOTF_CustomSimBodyVisual_";
        private const float SB_SyntyBeltHeightOverTorso = 0.045f;

        private static readonly string[] SB_TokensHips = { "hips", "pelvis", "hip" };
        private static readonly string[] SB_TokensNeck = { "neck" };
        private static readonly string[] SB_TokensHead = { "head", "head_01" };
        private static readonly string[] SB_TokensLeftHand = { "hand_l", "lefthand", "left_hand", "lhand", "hand.l" };
        private static readonly string[] SB_TokensRightHand = { "hand_r", "righthand", "right_hand", "rhand", "hand.r" };
        private static readonly string[] SB_TokensLeftLowerArm = { "lowerarm_l", "forearm_l", "elbow_l" };
        private static readonly string[] SB_TokensRightLowerArm = { "lowerarm_r", "forearm_r", "elbow_r" };
        private static readonly string[] SB_TokensLeftUpperArm = { "upperarm_l", "leftupperarm", "arm_l" };
        private static readonly string[] SB_TokensRightUpperArm = { "upperarm_r", "rightupperarm", "arm_r" };
        private static readonly string[] SB_TokensLeftShoulder = { "shoulder_l", "leftshoulder" };
        private static readonly string[] SB_TokensRightShoulder = { "shoulder_r", "rightshoulder" };
        private static readonly string[] SB_TokensUpperChest = { "upperchest", "spine_02", "spine2" };
        private static readonly string[] SB_TokensChest = { "chest", "spine_01", "spine1" };

        private readonly Dictionary<int, SB_ActorState> _sbActors =
            new Dictionary<int, SB_ActorState>();

        private readonly HashSet<int> _sbSeen =
            new HashSet<int>();

        private float _sbNextRefresh;
        private readonly Dictionary<int, Animator[]> _sbAnimatorCache = new Dictionary<int, Animator[]>();
        private readonly Dictionary<int, Transform[]> _sbTransformCache = new Dictionary<int, Transform[]>();
        private readonly Dictionary<long, Transform> _sbBoneCache = new Dictionary<long, Transform>();

        private void SB_Update()
        {
            if (Time.unscaledTime < _sbNextRefresh)
            {
                return;
            }

            _sbNextRefresh =
                Time.unscaledTime +
                SB_RefreshInterval;

            SB_RefreshActors();
        }

        private void SB_LateUpdate()
        {
            foreach (KeyValuePair<int, SB_ActorState> pair in _sbActors)
            {
                SB_ActorState actor =
                    pair.Value;

                if (actor == null ||
                    actor.Sim == null ||
                    actor.Root == null)
                {
                    continue;
                }

                if (actor.Head.HasVisual) SB_FitState(actor, actor.Head);
                if (actor.Chest.HasVisual) SB_FitState(actor, actor.Chest);
                if (actor.Hands.HasVisual) SB_FitState(actor, actor.Hands);
                if (actor.Waist.HasVisual) SB_FitState(actor, actor.Waist);
            }
        }

        internal void SB_Shutdown()
        {
            foreach (KeyValuePair<int, SB_ActorState> pair in _sbActors)
            {
                SB_RemoveActor(
                    pair.Value);
            }

            _sbActors.Clear();
            _sbSeen.Clear();
            _sbAnimatorCache.Clear();
            _sbTransformCache.Clear();
            _sbBoneCache.Clear();
        }

        private void SB_RefreshActors()
        {
            _sbSeen.Clear();

            SimPlayer[] sims =
                GetCachedSimPlayers();

            if (sims != null)
            {
                for (int i = 0;
                     i < sims.Length;
                     i++)
                {
                    SimPlayer sim =
                        sims[i];

                    if (sim == null ||
                        !sim.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    int id =
                        sim.GetInstanceID();

                    SB_ActorState actor;
                    bool hasActor =
                        _sbActors.TryGetValue(id, out actor) &&
                        actor != null;

                    if (!SB_HasRelevantEquipment(sim))
                    {
                        if (hasActor)
                        {
                            SB_RemoveActor(actor);
                            _sbActors.Remove(id);
                            SB_ClearActorCaches(id);
                        }

                        continue;
                    }

                    _sbSeen.Add(
                        id);

                    if (!hasActor)
                    {
                        actor =
                            new SB_ActorState(
                                sim);

                        _sbActors[id] =
                            actor;
                    }

                    actor.RefreshComponents();

                    if (actor.Root == null)
                    {
                        continue;
                    }
                    SB_RefreshState(actor, actor.Head);
                    SB_RefreshState(actor, actor.Chest);
                    SB_RefreshState(actor, actor.Hands);
                    SB_RefreshState(actor, actor.Waist);
                }
            }

            List<int> stale =
                null;

            foreach (KeyValuePair<int, SB_ActorState> pair in _sbActors)
            {
                if (_sbSeen.Contains(
                        pair.Key))
                {
                    continue;
                }

                if (stale == null)
                {
                    stale =
                        new List<int>();
                }

                stale.Add(
                    pair.Key);
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0;
                 i < stale.Count;
                 i++)
            {
                SB_ActorState actor;

                if (_sbActors.TryGetValue(
                        stale[i],
                        out actor))
                {
                    SB_RemoveActor(
                        actor);
                }

                _sbActors.Remove(
                    stale[i]);
                SB_ClearActorCaches(stale[i]);
            }
        }

        private bool SB_HasRelevantEquipment(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return false;
            }

            Item head = SB_GetEquippedItem(sim, Item.SlotType.Head);
            if (!string.IsNullOrEmpty(SB_GetPrefabForSlot(head, Item.SlotType.Head)))
            {
                return true;
            }

            Item chest = SB_GetEquippedItem(sim, Item.SlotType.Chest);
            if (!string.IsNullOrEmpty(SB_GetPrefabForSlot(chest, Item.SlotType.Chest)))
            {
                return true;
            }

            Item hands = SB_GetEquippedItem(sim, Item.SlotType.Hand);
            if (!string.IsNullOrEmpty(SB_GetPrefabForSlot(hands, Item.SlotType.Hand)))
            {
                return true;
            }

            Item waist = SB_GetEquippedItem(sim, Item.SlotType.Waist);
            return !string.IsNullOrEmpty(SB_GetPrefabForSlot(waist, Item.SlotType.Waist));
        }

        private void SB_ClearActorCaches(
            int rootId)
        {
            _sbAnimatorCache.Remove(rootId);
            _sbTransformCache.Remove(rootId);

            List<long> staleBoneKeys = null;

            foreach (KeyValuePair<long, Transform> pair in _sbBoneCache)
            {
                if ((int)(pair.Key >> 32) != rootId)
                {
                    continue;
                }

                if (staleBoneKeys == null)
                {
                    staleBoneKeys = new List<long>();
                }

                staleBoneKeys.Add(pair.Key);
            }

            if (staleBoneKeys == null)
            {
                return;
            }

            for (int i = 0; i < staleBoneKeys.Count; i++)
            {
                _sbBoneCache.Remove(staleBoneKeys[i]);
            }
        }

        private void SB_RefreshState(
            SB_ActorState actor,
            SB_VisualState state)
        {
            Item item =
                SB_GetEquippedItem(
                    actor.Sim,
                    state.Slot);

            string prefabName =
                SB_GetPrefabForSlot(
                    item,
                    state.Slot);

            if (item == null ||
                string.IsNullOrEmpty(prefabName))
            {
                // Do not touch unrelated SimPlayer equipment. In particular,
                // Moldermaw's Sporebound/Rotcleaver visual controller owns its
                // own native/custom state and must not be reset by the
                // Nhal'Sera controller simply because this slot is not one of
                // ours. Only clean up if this state previously owned a
                // Nhal'Sera visual/native suppression.
                if (!string.IsNullOrEmpty(state.ItemId) ||
                    state.HasVisual ||
                    state.HiddenNative.Count > 0)
                {
                    SB_Remove(state);
                }

                return;
            }

            if (string.Equals(
                    state.ItemId,
                    item.Id,
                    StringComparison.OrdinalIgnoreCase) &&
                state.HasVisual &&
                actor.Root != null &&
                (state.VisualA == null || state.VisualA.transform.IsChildOf(actor.Root)) &&
                (state.VisualB == null || state.VisualB.transform.IsChildOf(actor.Root)))
            {
                SB_SuppressNativeVisuals(
                    actor,
                    item,
                    state);
                return;
            }

            SB_Remove(state);

            state.ItemId =
                item.Id;

            state.PrefabName =
                prefabName;

            SB_Spawn(
                actor,
                item,
                state);
        }

        private void SB_Spawn(
            SB_ActorState actor,
            Item item,
            SB_VisualState state)
        {
            GameObject prefab =
                PW_ResolvePrefab(state.PrefabName);

            if (prefab == null ||
                actor == null ||
                actor.Root == null)
            {
                return;
            }

            if (MB_UsesModularRig(item.Id, state.Slot))
            {
                GameObject modularVisual =
                    UnityEngine.Object.Instantiate(prefab, actor.Root, false);

                if (modularVisual == null)
                {
                    return;
                }

                modularVisual.name =
                    SB_CustomPrefix + actor.Sim.GetInstanceID() + "_" +
                    item.Id + "_" + state.Label + "_Modular";

                if (!MB_BindVisual(
                        actor.Root,
                        modularVisual,
                        item.Id,
                        state.Slot,
                        actor.Sim.name + " / " + item.ItemName))
                {
                    UnityEngine.Object.Destroy(modularVisual);
                    return;
                }

                state.VisualA = modularVisual;
                state.VisualB = null;
                SB_CacheVisualState(state);
                SB_SuppressNativeVisuals(actor, item, state);
                return;
            }

            if (SR_UsesNativeSkinnedRig(item.Id, state.Slot))
            {
                GameObject skinnedVisual =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        actor.Root,
                        false);

                if (skinnedVisual == null)
                {
                    return;
                }

                skinnedVisual.name =
                    SB_CustomPrefix +
                    actor.Sim.GetInstanceID() +
                    "_" +
                    item.Id +
                    "_" +
                    state.Label +
                    "_Skinned";

                SR_PrepareSkinnedVisual(skinnedVisual);

                int rendererCount;
                int mappedBones;
                int missingBones;

                if (!SR_RemapSkinnedVisual(
                        actor.Root,
                        skinnedVisual,
                        actor.Sim.name + " / " + item.ItemName,
                        out rendererCount,
                        out mappedBones,
                        out missingBones))
                {
                    UnityEngine.Object.Destroy(skinnedVisual);
                    return;
                }

                state.VisualA = skinnedVisual;
                state.VisualB = null;
                SB_CacheVisualState(state);

                SB_SuppressNativeVisuals(actor, item, state);
                return;
            }

            if (state.Slot == Item.SlotType.Hand)
            {
                SB_SpawnGauntlets(
                    actor.Root,
                    prefab,
                    state);
            }
            else
            {
                bool isCrown =
                    state.Slot == Item.SlotType.Head &&
                    string.Equals(
                        item.Id,
                        NhalSeraEquipment.CrownId,
                        StringComparison.OrdinalIgnoreCase);

                // Use actor root for crown instantiation; the animated Head
                // bone supplies position/rotation only (not inherited scale).
                Transform initialParent = actor.Root;
                Transform crownHead = isCrown
                    ? SB_ResolveHumanoidBone(actor.Root,
                        HumanBodyBones.Head, SB_TokensHead)
                    : null;

                GameObject visual =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        initialParent,
                        false);

                if (visual == null)
                {
                    return;
                }

                visual.name =
                    SB_CustomPrefix +
                    actor.Sim.GetInstanceID() +
                    "_" +
                    item.Id +
                    "_" +
                    state.Label;

                visual.transform.localPosition =
                    Vector3.zero;

                visual.transform.localRotation =
                    Quaternion.identity;

                // Sim body pieces live under the stable actor root, so use
                // their authored Synty world scale directly.
                visual.transform.localScale =
                    Vector3.one;

                SB_PrepareVisualOnce(
                    visual,
                    null);

                if (isCrown)
                {
                    if (crownHead != null)
                    {
                        visual.transform.position = crownHead.position;
                        visual.transform.rotation = crownHead.rotation *
                            Quaternion.Euler(-90f, 180f, 0f);
                    }

                    PB_PrepareCrownRigidVisual(
                        visual,
                        crownHead,
                        state.PrefabName);

                    Transform neck =
                        SB_ResolveHumanoidBone(
                            actor.Root,
                            HumanBodyBones.Neck,
                            SB_TokensNeck);

                    state.RuntimeScaleMultiplier =
                        SB_CalculateCrownScaleMultiplier(
                            visual,
                            crownHead,
                            neck);

                    visual.transform.localScale =
                        Vector3.one *
                        state.RuntimeScaleMultiplier;
                    if (crownHead != null)
                    {
                        visual.transform.position = crownHead.position;
                        visual.transform.rotation = crownHead.rotation *
                            Quaternion.Euler(-90f, 180f, 0f);
                    }
                    state.CrownHeadLocalOffset =
                        PB_CalculateCrownHeadOffset(visual, crownHead);
                }

                PB_PrepareRigidAccessory(
                    visual,
                    actor.Root);

                if (!SB_HasRenderableVisual(visual))
                {
                    UnityEngine.Object.Destroy(visual);
                    return;
                }

                state.VisualA =
                    visual;

                SB_CacheVisualState(state);
            }

            SB_FitState(
                actor,
                state);

            SB_SuppressNativeVisuals(
                actor,
                item,
                state);
        }


        private void SB_SpawnGauntlets(
            Transform actorRoot,
            GameObject prefab,
            SB_VisualState state)
        {
            if (actorRoot == null ||
                prefab == null ||
                state == null)
            {
                return;
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

                return;
            }

            left.name =
                SB_CustomPrefix +
                "1814_Left";

            right.name =
                SB_CustomPrefix +
                "1814_Right";

            left.transform.localScale =
                Vector3.one;

            right.transform.localScale =
                Vector3.one;

            SB_PrepareVisualOnce(
                left,
                "Right_");

            SB_PrepareVisualOnce(
                right,
                "Left_");

            if (!SB_HasRenderableVisual(left) ||
                !SB_HasRenderableVisual(right))
            {
                UnityEngine.Object.Destroy(left);
                UnityEngine.Object.Destroy(right);
                return;
            }

            state.VisualA = left;
            state.VisualB = right;
            SB_CacheVisualState(state);

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
        }


        private static float SB_CalculateCrownScaleMultiplier(
            GameObject visual,
            Transform head,
            Transform neck)
        {
            if (visual == null)
            {
                return 1f;
            }

            visual.transform.localScale =
                Vector3.one;

            PB_EnsureCustomVisible(
                visual);

            Bounds bounds;
            if (!PB_TryGetCrownWorldBounds(visual, out bounds))
                return 1f;

            Vector3 size = bounds.size;
            float currentMax = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (currentMax <= 0.0001f)
                return 1f;

            float targetMax = 0.40f;

            if (head != null && neck != null)
            {
                float span = Vector3.Distance(head.position, neck.position);

                if (span > 0.03f && span < 0.60f)
                {
                    targetMax = Mathf.Clamp(span * 2.05f, 0.36f, 0.46f);
                }
            }

            return Mathf.Clamp(targetMax / currentMax, 0.25f, 100000f);
        }


        private static void SB_CacheVisualState(
            SB_VisualState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.VisualA != null)
            {
                state.CachedHierarchyA =
                    state.VisualA.GetComponentsInChildren<Transform>(
                        true);

                state.CachedRenderersA =
                    state.VisualA.GetComponentsInChildren<Renderer>(
                        true);
            }

            if (state.VisualB != null)
            {
                state.CachedHierarchyB =
                    state.VisualB.GetComponentsInChildren<Transform>(
                        true);

                state.CachedRenderersB =
                    state.VisualB.GetComponentsInChildren<Renderer>(
                        true);
            }
        }


        private static void SB_EnsureVisualCached(
            SB_VisualState state,
            bool secondary)
        {
            if (state == null)
            {
                return;
            }

            GameObject visual =
                secondary
                    ? state.VisualB
                    : state.VisualA;

            if (visual == null)
            {
                return;
            }

            if (!visual.activeSelf)
            {
                visual.SetActive(true);
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
                SB_CacheVisualState(state);

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
                Transform child = hierarchy[i];

                if (child != null &&
                    child.gameObject != null &&
                    !child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                }
            }

            for (int i = 0;
                 renderers != null &&
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null) continue;
                if (PB_IsBakedCrownSkin(renderer))
                {
                    if (renderer.enabled) renderer.enabled = false;
                    continue;
                }
                if (!renderer.enabled) renderer.enabled = true;
            }
        }


        private void SB_FitState(
            SB_ActorState actor,
            SB_VisualState state)
        {
            if (actor == null ||
                state == null ||
                actor.Root == null ||
                state.VisualA == null)
            {
                return;
            }

            SB_EnsureVisualCached(
                state,
                false);

            if (state.VisualB != null)
            {
                SB_EnsureVisualCached(
                    state,
                    true);
            }

            if (MB_UsesModularRig(state.ItemId, state.Slot))
            {
                if (!state.VisualA.activeSelf)
                {
                    state.VisualA.SetActive(true);
                }
                return;
            }

            if (SR_UsesNativeSkinnedRig(state.ItemId, state.Slot))
            {
                if (!state.VisualA.activeSelf)
                {
                    state.VisualA.SetActive(true);
                }

                // Native Synty skeleton drives the remade body model.
                return;
            }

            Transform hips =
                SB_ResolveHumanoidBone(
                    actor.Root,
                    HumanBodyBones.Hips,
                    SB_TokensHips);

            Transform neck =
                SB_ResolveHumanoidBone(
                    actor.Root,
                    HumanBodyBones.Neck,
                    SB_TokensNeck);

            if (state.Slot == Item.SlotType.Chest)
            {
                Vector3 position =
                    actor.Root.position;

                if (hips != null &&
                    neck != null)
                {
                    position =
                        Vector3.Lerp(
                            hips.position,
                            neck.position,
                            0.50f);
                }

                SB_ApplyBodyTransform(
                    state.VisualA,
                    position,
                    actor.Root.rotation *
                    Quaternion.Euler(-90f, 0f, 0f),
                    0.50f);

                SB_EnsureCarapaceArticulation(actor, state);
                BA_ApplyBindings(state.RigidBindings);

                return;
            }

            if (state.Slot == Item.SlotType.Waist)
            {
                Vector3 position =
                    actor.Root.position;

                if (hips != null &&
                    neck != null)
                {
                    position =
                        Vector3.Lerp(
                            hips.position,
                            neck.position,
                            SB_SyntyBeltHeightOverTorso);
                }

                SB_ApplyBodyTransform(
                    state.VisualA,
                    position,
                    actor.Root.rotation *
                    Quaternion.Euler(0f, 180f, 0f),
                    1.00f);

                return;
            }

            if (state.Slot == Item.SlotType.Head)
            {
                Transform head =
                    SB_ResolveHumanoidBone(
                        actor.Root,
                        HumanBodyBones.Head,
                        SB_TokensHead);

                if (head == null)
                {
                    return;
                }

                Transform visual =
                    state.VisualA.transform;

                if (visual.parent != actor.Root)
                {
                    visual.SetParent(actor.Root, false);
                    PB_SetLayerRecursive(visual, actor.Root.gameObject.layer);
                }

                // Match the player's live /fotfvisual head placement.
                // The fitted geometry offset is retained and the crown's
                // calibrated move is added in animated Head-local axes.
                visual.position = head.position +
                    head.rotation * (PB_CrownTuning.Position + state.CrownHeadLocalOffset);
                visual.rotation = head.rotation *
                    Quaternion.Euler(-90f, 180f, 0f);
                visual.localScale = Vector3.one *
                    PB_CrownTuning.Scale * state.RuntimeScaleMultiplier;

                SB_EnsureVisualCached(
                    state,
                    false);

                return;
            }

            if (state.Slot == Item.SlotType.Hand)
            {
                Transform leftHand =
                    SB_ResolveHumanoidBone(
                        actor.Root,
                        HumanBodyBones.LeftHand,
                        SB_TokensLeftHand);

                Transform rightHand =
                    SB_ResolveHumanoidBone(
                        actor.Root,
                        HumanBodyBones.RightHand,
                        SB_TokensRightHand);

                Transform leftLowerArm =
                    SB_ResolveHumanoidBone(
                        actor.Root,
                        HumanBodyBones.LeftLowerArm,
                        SB_TokensLeftLowerArm);

                Transform rightLowerArm =
                    SB_ResolveHumanoidBone(
                        actor.Root,
                        HumanBodyBones.RightLowerArm,
                        SB_TokensRightLowerArm);

                SB_FitSimHand(
                    state.VisualA,
                    leftHand,
                    leftLowerArm,
                    actor.Root,
                    state.HasReferenceA,
                    state.ReferencePointA,
                    ref state.HasHandRotationOffsetA,
                    ref state.HandRotationOffsetA);

                SB_FitSimHand(
                    state.VisualB,
                    rightHand,
                    rightLowerArm,
                    actor.Root,
                    state.HasReferenceB,
                    state.ReferencePointB,
                    ref state.HasHandRotationOffsetB,
                    ref state.HandRotationOffsetB);

                SB_EnsureHandArticulation(
                    actor,
                    state,
                    leftHand,
                    rightHand,
                    leftLowerArm,
                    rightLowerArm);

                BA_ApplyBindings(state.RigidBindings);
            }
        }


        private static void SB_ApplyBodyTransform(
            GameObject visual,
            Vector3 position,
            Quaternion rotation,
            float scale)
        {
            if (visual == null)
            {
                return;
            }

            Transform transform =
                visual.transform;

            transform.position =
                position;

            transform.rotation =
                rotation;

            transform.localScale =
                Vector3.one *
                scale;

            if (!visual.activeSelf)
            {
                visual.SetActive(true);
            }
        }


        private static void SB_FitSimHand(
            GameObject visual,
            Transform hand,
            Transform lowerArm,
            Transform actorRoot,
            bool hasReferencePoint,
            Vector3 referencePoint,
            ref bool hasHandRotationOffset,
            ref Quaternion handRotationOffset)
        {
            if (visual == null ||
                hand == null ||
                actorRoot == null)
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
                handRotationOffset;

            transform.localScale = Vector3.one * 0.50f;

            Vector3 target = hand.position;
            transform.position = target;

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


        private void SB_EnsureCarapaceArticulation(
            SB_ActorState actor,
            SB_VisualState state)
        {
            if (actor == null ||
                actor.Root == null ||
                state == null ||
                state.VisualA == null ||
                state.RigidBindingsInitialized)
            {
                return;
            }

            BA_DestroyBindings(state.RigidBindings);

            Transform leftUpperArm = SB_ResolveHumanoidBone(actor.Root, HumanBodyBones.LeftUpperArm, SB_TokensLeftUpperArm);
            Transform rightUpperArm = SB_ResolveHumanoidBone(actor.Root, HumanBodyBones.RightUpperArm, SB_TokensRightUpperArm);
            Transform leftShoulder = SB_ResolveHumanoidBone(actor.Root, HumanBodyBones.LeftShoulder, SB_TokensLeftShoulder) ?? leftUpperArm;
            Transform rightShoulder = SB_ResolveHumanoidBone(actor.Root, HumanBodyBones.RightShoulder, SB_TokensRightShoulder) ?? rightUpperArm;

            Transform leftArmBasis = leftUpperArm ?? leftShoulder;
            Transform rightArmBasis = rightUpperArm ?? rightShoulder;

            string[] shoulderPieces = { "Base", "Cap", "Tip" };
            for (int i = 0; i < shoulderPieces.Length; i++)
            {
                BA_AddBinding(state.RigidBindings, state.VisualA, "Pauldron_L_" + shoulderPieces[i], leftArmBasis);
                BA_AddBinding(state.RigidBindings, state.VisualA, "Pauldron_R_" + shoulderPieces[i], rightArmBasis);
            }

            BA_AddBinding(state.RigidBindings, state.VisualA, "ShoulderGem_L", leftArmBasis);
            BA_AddBinding(state.RigidBindings, state.VisualA, "ShoulderGem_R", rightArmBasis);
            BA_AddBinding(state.RigidBindings, state.VisualA, "UpperArmGuard_L", leftArmBasis);
            BA_AddBinding(state.RigidBindings, state.VisualA, "UpperArmTrim_L", leftArmBasis);
            BA_AddBinding(state.RigidBindings, state.VisualA, "UpperArmGuard_R", rightArmBasis);
            BA_AddBinding(state.RigidBindings, state.VisualA, "UpperArmTrim_R", rightArmBasis);

            state.RigidBindingsInitialized = state.RigidBindings.Count > 0;
        }


        private void SB_EnsureHandArticulation(
            SB_ActorState actor,
            SB_VisualState state,
            Transform leftHand,
            Transform rightHand,
            Transform leftLowerArm,
            Transform rightLowerArm)
        {
            if (actor == null ||
                actor.Root == null ||
                state == null ||
                state.VisualA == null ||
                state.VisualB == null ||
                state.RigidBindingsInitialized)
            {
                return;
            }

            BA_DestroyBindings(state.RigidBindings);

            Animator humanoidAnimator = null;
            Animator[] animators = actor.Root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; animators != null && i < animators.Length; i++)
            {
                Animator candidate = animators[i];
                if (candidate != null &&
                    candidate.avatar != null &&
                    candidate.avatar.isValid &&
                    candidate.avatar.isHuman)
                {
                    humanoidAnimator = candidate;
                    break;
                }
            }

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
                BA_AddBinding(state.RigidBindings, state.VisualA, "Left_" + forearmPieces[i], leftLowerArm);
                BA_AddBinding(state.RigidBindings, state.VisualB, "Right_" + forearmPieces[i], rightLowerArm);
            }

            for (int modelFinger = 0; modelFinger < 4; modelFinger++)
            {
                for (int segment = 0; segment < 3; segment++)
                {
                    Transform leftBone =
                        BA_ResolveFingerTransform(
                            humanoidAnimator,
                            leftHand,
                            true,
                            modelFinger,
                            segment);

                    Transform rightBone =
                        BA_ResolveFingerTransform(
                            humanoidAnimator,
                            rightHand,
                            false,
                            modelFinger,
                            segment);

                    BA_AddBinding(state.RigidBindings, state.VisualA, "Left_Finger_" + modelFinger + "_Lame_" + segment, leftBone);
                    BA_AddBinding(state.RigidBindings, state.VisualB, "Right_Finger_" + modelFinger + "_Lame_" + segment, rightBone);
                }

                Transform leftDistal =
                    BA_ResolveFingerTransform(humanoidAnimator, leftHand, true, modelFinger, 2);
                Transform rightDistal =
                    BA_ResolveFingerTransform(humanoidAnimator, rightHand, false, modelFinger, 2);

                BA_AddBinding(state.RigidBindings, state.VisualA, "Left_Finger_" + modelFinger + "_Claw", leftDistal);
                BA_AddBinding(state.RigidBindings, state.VisualB, "Right_Finger_" + modelFinger + "_Claw", rightDistal);
            }

            for (int segment = 0; segment < 3; segment++)
            {
                BA_AddBinding(
                    state.RigidBindings,
                    state.VisualA,
                    "Left_Thumb_" + segment,
                    BA_ResolveThumbTransform(humanoidAnimator, leftHand, true, segment));

                BA_AddBinding(
                    state.RigidBindings,
                    state.VisualB,
                    "Right_Thumb_" + segment,
                    BA_ResolveThumbTransform(humanoidAnimator, rightHand, false, segment));
            }

            state.RigidBindingsInitialized = state.RigidBindings.Count > 0;
        }


        private Transform SB_ResolveAttachment(
            SB_ActorState actor,
            Item.SlotType slot)
        {
            if (actor == null)
            {
                return null;
            }

            if (slot == Item.SlotType.Primary)
            {
                return
                    actor.Modulars != null
                        ? actor.Modulars.WeaponR
                        : null;
            }

            if (slot == Item.SlotType.Secondary)
            {
                return
                    actor.Modulars != null
                        ? actor.Modulars.WeaponL
                        : null;
            }

            if (slot == Item.SlotType.Head)
            {
                return SB_ResolveHumanoidBone(
                    actor.Root,
                    HumanBodyBones.Head,
                    SB_TokensHead);
            }

            if (slot == Item.SlotType.Chest)
            {
                Transform upperChest =
                    SB_ResolveHumanoidBone(
                        actor.Root,
                        HumanBodyBones.UpperChest,
                        SB_TokensUpperChest);

                if (upperChest != null)
                {
                    return upperChest;
                }

                return SB_ResolveHumanoidBone(
                    actor.Root,
                    HumanBodyBones.Chest,
                    SB_TokensChest);
            }

            if (slot == Item.SlotType.Waist)
            {
                return SB_ResolveHumanoidBone(
                    actor.Root,
                    HumanBodyBones.Hips,
                    SB_TokensHips);
            }

            return null;
        }

        private Item SB_GetEquippedItem(
            SimPlayer sim,
            Item.SlotType slot)
        {
            if (sim == null ||
                sim.MyEquipment == null)
            {
                return null;
            }

            for (int i = 0;
                 i < sim.MyEquipment.Count;
                 i++)
            {
                SimInvSlot entry =
                    sim.MyEquipment[i];

                if (entry != null &&
                    entry.ThisSlotType == slot)
                {
                    return entry.MyItem;
                }
            }

            return null;
        }

        private string SB_GetPrefabForSlot(
            Item item,
            Item.SlotType slot)
        {
            if (slot == Item.SlotType.Primary ||
                slot == Item.SlotType.Secondary)
            {
                return null;
            }

            if (item == null)
            {
                return null;
            }

            // Primary/Secondary are owned by this same FateEquipmentVisuals
            // instance's SW_ weapon section.  The body section never touches
            // weapon roots or weapon template objects.

            if (string.Equals(
                    item.Id,
                    NhalSeraEquipment.CrownId,
                    StringComparison.OrdinalIgnoreCase) &&
                slot == Item.SlotType.Head)
            {
                return NhalSeraEquipment.CrownPrefab;
            }

            if (string.Equals(
                    item.Id,
                    NhalSeraEquipment.GraspId,
                    StringComparison.OrdinalIgnoreCase) &&
                slot == Item.SlotType.Hand)
            {
                return NhalSeraEquipment.GraspPrefab;
            }

            if (string.Equals(
                    item.Id,
                    NhalSeraEquipment.GirdleId,
                    StringComparison.OrdinalIgnoreCase) &&
                slot == Item.SlotType.Waist)
            {
                return NhalSeraEquipment.GirdlePrefab;
            }

            return null;
        }

        private Transform SB_ResolveHumanoidBone(
            Transform root,
            HumanBodyBones bone,
            string[] fallbackTokens)
        {
            if (root == null)
            {
                return null;
            }

            int rootId = root.GetInstanceID();
            long key = ((long)rootId << 32) ^ (uint)((int)bone);

            Transform cached;
            if (_sbBoneCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            Animator[] animators;
            if (!_sbAnimatorCache.TryGetValue(rootId, out animators) || animators == null)
            {
                animators = root.GetComponentsInChildren<Animator>(true);
                _sbAnimatorCache[rootId] = animators;
            }

            for (int i = 0; animators != null && i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null ||
                    animator.avatar == null ||
                    !animator.avatar.isValid ||
                    !animator.avatar.isHuman)
                {
                    continue;
                }

                try
                {
                    Transform resolved = animator.GetBoneTransform(bone);
                    if (resolved != null)
                    {
                        _sbBoneCache[key] = resolved;
                        return resolved;
                    }
                }
                catch
                {
                }
            }

            Transform fallback = SB_FindBone(root, fallbackTokens);
            if (fallback != null)
            {
                _sbBoneCache[key] = fallback;
            }

            return fallback;
        }

        private Transform SB_FindBone(
            Transform root,
            string[] tokens)
        {
            if (root == null ||
                tokens == null)
            {
                return null;
            }

            int rootId = root.GetInstanceID();
            Transform[] all;

            if (!_sbTransformCache.TryGetValue(rootId, out all) || all == null)
            {
                all = root.GetComponentsInChildren<Transform>(true);
                _sbTransformCache[rootId] = all;
            }

            Transform best = null;
            int bestScore = int.MinValue;

            for (int i = 0;
                 i < all.Length;
                 i++)
            {
                Transform candidate =
                    all[i];

                if (candidate == null ||
                    candidate.name.StartsWith(
                        SB_CustomPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string normalized =
                    SB_Normalize(
                        candidate.name);

                for (int t = 0;
                     t < tokens.Length;
                     t++)
                {
                    string token =
                        SB_Normalize(
                            tokens[t]);

                    int score =
                        string.Equals(
                            normalized,
                            token,
                            StringComparison.OrdinalIgnoreCase)
                            ? 100
                            : (normalized.Contains(token)
                                ? 50
                                : int.MinValue);

                    if (score == int.MinValue)
                    {
                        continue;
                    }

                    if (candidate.gameObject.activeInHierarchy)
                    {
                        score += 10;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            return best;
        }

        private string SB_Normalize(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "")
                .Replace(".", "")
                .Replace(":", "")
                .ToLowerInvariant();
        }

        private void SB_SuppressNativeVisuals(
            SB_ActorState actor,
            Item item,
            SB_VisualState state)
        {
            if (actor == null ||
                actor.Root == null ||
                item == null ||
                state == null)
            {
                return;
            }

            bool needsDiscovery =
                state.HiddenNative.Count == 0;

            for (int i = 0;
                 i < state.HiddenNative.Count;
                 i++)
            {
                GameObject hidden = state.HiddenNative[i];

                if (hidden == null)
                {
                    needsDiscovery = true;
                    continue;
                }

                if (hidden.activeSelf)
                {
                    hidden.SetActive(false);
                }
            }

            if (state.Slot == Item.SlotType.Primary ||
                state.Slot == Item.SlotType.Secondary)
            {
                needsDiscovery = true;
            }

            if (!needsDiscovery)
            {
                return;
            }

            string[] targets =
            {
                item.EquipmentToActivate,
                item.ShoulderTrimL,
                item.ShoulderTrimR,
                item.ElbowTrimL,
                item.ElbowTrimR,
                item.KneeTrimL,
                item.KneeTrimR
            };

            Transform[] all =
                actor.Root.GetComponentsInChildren<Transform>(true);

            for (int i = 0;
                 all != null && i < all.Length;
                 i++)
            {
                Transform candidate = all[i];

                if (candidate == null ||
                    candidate == actor.Root ||
                    candidate.name.StartsWith(
                        SB_CustomPrefix,
                        StringComparison.Ordinal) ||
                    candidate.name.StartsWith(
                        "FOTF_MoldermawSimVisual_",
                        StringComparison.Ordinal) ||
                    SB_IsAncestorOf(candidate, state.VisualA) ||
                    SB_IsAncestorOf(candidate, state.VisualB))
                {
                    continue;
                }

                bool matched = false;

                for (int t = 0; t < targets.Length; t++)
                {
                    string targetName = targets[t];

                    if (!string.IsNullOrWhiteSpace(targetName) &&
                        string.Equals(
                            candidate.name,
                            targetName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    continue;
                }

                Renderer renderer =
                    candidate.GetComponentInChildren<Renderer>(true);

                if (renderer == null)
                {
                    continue;
                }

                if (!state.HiddenNative.Contains(candidate.gameObject))
                {
                    state.HiddenNative.Add(candidate.gameObject);
                }

                if (candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        private bool SB_IsAncestorOf(
            Transform candidate,
            GameObject visual)
        {
            if (candidate == null || visual == null)
            {
                return false;
            }

            Transform current = visual.transform;

            while (current != null)
            {
                if (current == candidate)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void SB_HideNativeName(
            Transform root,
            string targetName,
            SB_VisualState state)
        {
            if (root == null ||
                string.IsNullOrWhiteSpace(targetName) ||
                state == null)
            {
                return;
            }

            Transform[] all =
                root.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 i < all.Length;
                 i++)
            {
                Transform candidate =
                    all[i];

                if (candidate == null ||
                    candidate == root ||
                    candidate.name.StartsWith(
                        SB_CustomPrefix,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.name,
                        targetName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!state.HiddenNative.Contains(candidate.gameObject))
                {
                    state.HiddenNative.Add(
                        candidate.gameObject);
                }

                if (candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.SetActive(
                        false);
                }
            }
        }

        private void SB_HideSide(
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
                 i < all.Length;
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
                    current.gameObject.SetActive(
                        false);
                }
            }
        }

        private void SB_DisablePhysics(
            GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody[] rigidbodies =
                root.GetComponentsInChildren<Rigidbody>(
                    true);

            for (int i = 0;
                 i < rigidbodies.Length;
                 i++)
            {
                if (rigidbodies[i] != null)
                {
                    rigidbodies[i].isKinematic = true;
                    rigidbodies[i].detectCollisions = false;
                }
            }
        }

        private void SB_PrepareVisualOnce(
            GameObject root,
            string sideToHide)
        {
            if (root == null)
            {
                return;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            Transform[] hierarchy =
                root.GetComponentsInChildren<Transform>(true);

            for (int i = 0;
                 hierarchy != null && i < hierarchy.Length;
                 i++)
            {
                Transform current = hierarchy[i];

                if (current != null &&
                    current.gameObject != null &&
                    !current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
            }

            if (!string.IsNullOrEmpty(sideToHide))
            {
                SB_HideSide(root, sideToHide);
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0;
                 renderers != null && i < renderers.Length;
                 i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = true;

                SkinnedMeshRenderer skinned =
                    renderer as SkinnedMeshRenderer;

                if (skinned != null)
                {
                    skinned.updateWhenOffscreen = true;
                }
            }

            BA_ForceDoubleSided(root);
            BA_MakeMeshFiltersDoubleSided(root);

            LODGroup[] lodGroups =
                root.GetComponentsInChildren<LODGroup>(true);

            for (int i = 0;
                 lodGroups != null && i < lodGroups.Length;
                 i++)
            {
                if (lodGroups[i] != null)
                {
                    lodGroups[i].ForceLOD(0);
                }
            }

            SB_DisablePhysics(root);
        }

        private bool SB_HasRenderableVisual(
            GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0;
                 renderers != null && i < renderers.Length;
                 i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
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

                    continue;
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

        private void SB_NormalizeVisualScaleOnce(
            GameObject root,
            float expectedWorldSize)
        {
            if (root == null || expectedWorldSize <= 0f)
            {
                return;
            }

            Bounds bounds;

            if (!SB_TryGetActiveRendererBounds(root, out bounds))
            {
                return;
            }

            float currentSize = Mathf.Max(
                bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z));

            if (currentSize <= 0.0001f)
            {
                return;
            }

            if (currentSize < expectedWorldSize * 0.10f ||
                currentSize > expectedWorldSize * 10f)
            {
                float correction = Mathf.Clamp(
                    expectedWorldSize / currentSize,
                    0.01f,
                    100f);

                root.transform.localScale *= correction;
            }
        }

        private bool SB_TryGetActiveRendererBounds(
            GameObject root,
            out Bounds bounds)
        {
            bounds = new Bounds();

            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);

            bool found = false;

            for (int i = 0;
                 renderers != null && i < renderers.Length;
                 i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private float SB_GetExpectedWorldSize(
            Item.SlotType slot)
        {
            if (slot == Item.SlotType.Primary ||
                slot == Item.SlotType.Secondary)
            {
                return 1.20f;
            }

            if (slot == Item.SlotType.Chest)
            {
                return 1.10f;
            }

            if (slot == Item.SlotType.Head)
            {
                return 0.50f;
            }

            if (slot == Item.SlotType.Waist)
            {
                return 0.65f;
            }

            return 0.65f;
        }

        private void SB_RemoveActor(
            SB_ActorState actor)
        {
            if (actor == null)
            {
                return;
            }

            SB_Remove(actor.Primary);
            SB_Remove(actor.Secondary);
            SB_Remove(actor.Head);
            SB_Remove(actor.Chest);
            SB_Remove(actor.Hands);
            SB_Remove(actor.Waist);
        }

        private void SB_Remove(
            SB_VisualState state)
        {
            if (state == null)
            {
                return;
            }

            BA_DestroyBindings(state.RigidBindings);

            if (state.VisualA != null)
            {
                UnityEngine.Object.Destroy(
                    state.VisualA);

                state.VisualA = null;
            }

            if (state.VisualB != null)
            {
                UnityEngine.Object.Destroy(
                    state.VisualB);

                state.VisualB = null;
            }

            for (int i = 0;
                 i < state.HiddenNative.Count;
                 i++)
            {
                GameObject hidden =
                    state.HiddenNative[i];

                if (hidden != null)
                {
                    hidden.SetActive(
                        true);
                }
            }

            state.HiddenNative.Clear();
            state.ItemId = null;
            state.PrefabName = null;
            state.ReferencePointA = Vector3.zero;
            state.ReferencePointB = Vector3.zero;
            state.HasReferenceA = false;
            state.HasReferenceB = false;
            state.RigidBindingsInitialized = false;
            state.RuntimeScaleMultiplier = 1f;
            state.CrownHeadLocalOffset = Vector3.zero;
            state.CachedHierarchyA = null;
            state.CachedHierarchyB = null;
            state.CachedRenderersA = null;
            state.CachedRenderersB = null;
            state.HasHandRotationOffsetA = false;
            state.HasHandRotationOffsetB = false;
            state.HandRotationOffsetA = Quaternion.identity;
            state.HandRotationOffsetB = Quaternion.identity;
        }

        private sealed class SB_ActorState
        {
            internal SB_ActorState(
                SimPlayer sim)
            {
                Sim = sim;

                Primary = new SB_VisualState(Item.SlotType.Primary, "Primary");
                Secondary = new SB_VisualState(Item.SlotType.Secondary, "Secondary");
                Head = new SB_VisualState(Item.SlotType.Head, "Head");
                Chest = new SB_VisualState(Item.SlotType.Chest, "Chest");
                Hands = new SB_VisualState(Item.SlotType.Hand, "Hands");
                Waist = new SB_VisualState(Item.SlotType.Waist, "Waist");

                RefreshComponents();
            }

            internal void RefreshComponents()
            {
                if (Sim == null)
                {
                    return;
                }

                Inventory =
                    Sim.GetComponent<Inventory>();

                Modulars =
                    Sim.Mods != null
                        ? Sim.Mods
                        : (Inventory != null
                            ? Inventory.Modulars
                            : null);

                Root =
                    Sim.transform;
            }

            internal SimPlayer Sim;
            internal Inventory Inventory;
            internal ModularParts Modulars;
            internal Transform Root;

            internal SB_VisualState Primary;
            internal SB_VisualState Secondary;
            internal SB_VisualState Head;
            internal SB_VisualState Chest;
            internal SB_VisualState Hands;
            internal SB_VisualState Waist;
        }

        private sealed class SB_VisualState
        {
            internal SB_VisualState(
                Item.SlotType slot,
                string label)
            {
                Slot = slot;
                Label = label;
            }

            internal readonly Item.SlotType Slot;
            internal readonly string Label;

            internal string ItemId;
            internal string PrefabName;
            internal GameObject VisualA;
            internal GameObject VisualB;
            internal Vector3 ReferencePointA;
            internal Vector3 ReferencePointB;
            internal bool HasReferenceA;
            internal bool HasReferenceB;
            internal bool HasHandRotationOffsetA;
            internal bool HasHandRotationOffsetB;
            internal Quaternion HandRotationOffsetA = Quaternion.identity;
            internal Quaternion HandRotationOffsetB = Quaternion.identity;
            internal bool RigidBindingsInitialized;
            internal float RuntimeScaleMultiplier = 1f;
            internal Vector3 CrownHeadLocalOffset;
            internal Transform[] CachedHierarchyA;
            internal Transform[] CachedHierarchyB;
            internal Renderer[] CachedRenderersA;
            internal Renderer[] CachedRenderersB;
            internal readonly List<BA_RigidBinding> RigidBindings =
                new List<BA_RigidBinding>();

            internal readonly List<GameObject> HiddenNative =
                new List<GameObject>();

            internal bool HasVisual
            {
                get
                {
                    return
                        VisualA != null ||
                        VisualB != null;
                }
            }
        }
    }
}
