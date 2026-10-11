using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FateOfTheFallen
{
internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        private const float SW_RefreshInterval = 0.50f;
        private const string SW_CustomPrefix = "FOTF_CustomSimItemVisual_";

        private readonly Dictionary<int, SW_ActorState> _swActors =
            new Dictionary<int, SW_ActorState>();

        private readonly HashSet<int> _swSeen =
            new HashSet<int>();

        private float _swNextRefresh;


        private void SW_Update()
        {
            if (Time.unscaledTime < _swNextRefresh)
            {
                return;
            }

            _swNextRefresh =
                Time.unscaledTime + SW_RefreshInterval;

            SW_RefreshActors();
        }

        private void SW_LateUpdate()
        {
            foreach (KeyValuePair<int, SW_ActorState> pair in _swActors)
            {
                SW_ActorState actor = pair.Value;

                if (actor == null || actor.Sim == null)
                {
                    continue;
                }

                if (actor.Primary.CustomVisual != null) SW_MaintainSlot(actor, actor.Primary);
                if (actor.Secondary.CustomVisual != null) SW_MaintainSlot(actor, actor.Secondary);
                if (actor.Neck.CustomVisual != null) SW_MaintainSlot(actor, actor.Neck);
            }
        }

        internal void SW_Shutdown()
        {
            foreach (KeyValuePair<int, SW_ActorState> pair in _swActors)
            {
                SW_RemoveActor(pair.Value);
            }

            _swActors.Clear();
            _swSeen.Clear();
        }


        private void SW_RefreshActors()
        {
            _swSeen.Clear();

            SimPlayer[] sims =
                GetCachedSimPlayers();

            if (sims != null)
            {
                for (int i = 0; i < sims.Length; i++)
                {
                    SimPlayer sim = sims[i];

                    if (sim == null ||
                        !sim.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    int id = sim.GetInstanceID();

                    SW_ActorState actor;
                    bool hasActor =
                        _swActors.TryGetValue(id, out actor) &&
                        actor != null;

                    if (!SW_HasRelevantEquipment(sim))
                    {
                        if (hasActor)
                        {
                            SW_RemoveActor(actor);
                            _swActors.Remove(id);
                        }

                        continue;
                    }

                    _swSeen.Add(id);

                    if (!hasActor)
                    {
                        actor = new SW_ActorState(sim);
                        _swActors[id] = actor;
                    }

                    actor.RefreshComponents();

                    if (actor.Inventory == null ||
                        actor.Modulars == null)
                    {
                        continue;
                    }

                    SW_RefreshSlot(actor, actor.Primary);
                    SW_RefreshSlot(actor, actor.Secondary);
                    SW_RefreshSlot(actor, actor.Neck);
                }
            }

            if (_swActors.Count == 0)
            {
                return;
            }

            List<int> stale = null;

            foreach (KeyValuePair<int, SW_ActorState> pair in _swActors)
            {
                if (_swSeen.Contains(pair.Key))
                {
                    continue;
                }

                if (stale == null)
                {
                    stale = new List<int>();
                }

                stale.Add(pair.Key);
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                SW_ActorState actor;

                if (_swActors.TryGetValue(stale[i], out actor))
                {
                    SW_RemoveActor(actor);
                }

                _swActors.Remove(stale[i]);
            }
        }


        private static bool SW_HasRelevantEquipment(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return false;
            }

            Item primary = SW_GetEquippedItem(sim, Item.SlotType.Primary);
            if (!string.IsNullOrEmpty(SW_GetPrefabForItem(primary, Item.SlotType.Primary)))
            {
                return true;
            }

            Item secondary = SW_GetEquippedItem(sim, Item.SlotType.Secondary);
            if (!string.IsNullOrEmpty(SW_GetPrefabForItem(secondary, Item.SlotType.Secondary)))
            {
                return true;
            }

            Item neck = SW_GetEquippedItem(sim, Item.SlotType.Neck);
            return !string.IsNullOrEmpty(SW_GetPrefabForItem(neck, Item.SlotType.Neck));
        }

        private void SW_RefreshSlot(
            SW_ActorState actor,
            SW_VisualState state)
        {
            Item item =
                SW_GetEquippedItem(
                    actor.Sim,
                    state.SlotType);

            string prefabName =
                SW_GetPrefabForItem(
                    item,
                    state.SlotType);

            if (item == null ||
                string.IsNullOrEmpty(prefabName))
            {
                SW_Remove(state, true);
                return;
            }

            if (item.IsBow &&
                state.Parent == actor.Root &&
                state.CustomVisual != null &&
                state.CustomVisual.transform.IsChildOf(actor.Root) &&
                state.FollowTransform != null &&
                string.Equals(
                    state.ItemId,
                    item.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                SW_MaintainSlot(actor, state);
                SW_HideNativeVisuals(actor, item, state);
                return;
            }

            // A Sim may swap its native equipment holder during spawn/load.
            // Clean an old item before provisioning a new socket proxy.
            if (state.CustomVisual != null &&
                (!string.Equals(state.ItemId, item.Id, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(state.PrefabName, prefabName, StringComparison.OrdinalIgnoreCase)))
                SW_Remove(state, true);

            Transform actorRoot =
                actor.Root;

            Transform handRoot = null;
            Transform nativeVisual = null;
            Transform parent = null;
            Transform follow = null;
            bool followWorld = false;

            if (state.SlotType == Item.SlotType.Primary)
            {
                handRoot = actor.Modulars.WeaponR;

                if (item.IsBow)
                {
                    nativeVisual =
                        SW_FindNativeBowVisual(
                            actorRoot,
                            item);

                    // Parent the custom bow under the stable Sim root and
                    // follow one frozen native bow reference. Do not re-parent
                    // beneath a holder that native equipment refresh can toggle.
                    parent = actorRoot;
                    follow = nativeVisual;
                    followWorld = nativeVisual != null;
                }
                else
                {
                    nativeVisual =
                        SW_FindNativeVisual(
                            handRoot,
                            item,
                            true);

                    parent = VR_GetHandParent(handRoot, actorRoot,
                        ref state.SocketProxy, "Sim_" + state.Label + "_" + actor.Sim.GetInstanceID());
                }
            }
            else if (state.SlotType == Item.SlotType.Secondary)
            {
                handRoot = actor.Modulars.WeaponL;

                nativeVisual =
                    SW_FindNativeVisual(
                        handRoot,
                        item,
                        true);

                parent = VR_GetHandParent(handRoot, actorRoot,
                    ref state.SocketProxy, "Sim_" + state.Label + "_" + actor.Sim.GetInstanceID());
            }
            else if (state.SlotType == Item.SlotType.Neck)
            {
                nativeVisual =
                    SW_FindNativeVisual(
                        actorRoot,
                        item,
                        false);

                if (nativeVisual != null &&
                    nativeVisual.parent != null)
                {
                    parent = nativeVisual.parent;
                }
                else
                {
                    parent =
                        SW_FindBodyAnchor(
                            actorRoot);
                }
            }

            if (parent == null)
            {
                return;
            }

            bool alignmentSourceChanged =
                state.NativeVisual != nativeVisual ||
                state.FollowTransform != follow ||
                state.FollowWorld != followWorld;

            // Hand socket activation shouldn't destroy an already aligned
            // custom prefab. Transfer the same visual from proxy to socket.
            if (state.CustomVisual != null && state.Parent != parent)
            {
                state.CustomVisual.transform.SetParent(parent, false);
                state.Parent = parent;
                SW_ApplyAlignment(state);
            }

            if (state.SocketProxy != null && state.SocketProxy != parent)
            {
                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] Sim restored native hand socket for [" +
                    item.ItemName + "] Slot=[" + state.Label + "].");
                VR_DestroyProxy(ref state.SocketProxy);
            }

            state.ItemId = item.Id;
            state.PrefabName = prefabName;
            state.Parent = parent;
            state.HandRoot = handRoot;
            state.NativeVisual = nativeVisual;
            state.FollowWorld = followWorld;
            state.FollowTransform = follow;

            if (state.CustomVisual == null)
            {
                SW_Spawn(actor, item, state);
            }
            else
            {
                // Late native bow/weapon holder discovery can change the
                // alignment reference after the first equipped-item pass.
                // Rebase against that new reference without a visual respawn.
                if (alignmentSourceChanged || !state.HasAlignment)
                    SW_ResolveAlignment(actor, item, state);
                SW_MaintainSlot(actor, state);
            }

            if (state.CustomVisual != null)
            {
                SW_HideNativeVisuals(actor, item, state);
            }
        }


        private void SW_Spawn(
            SW_ActorState actor,
            Item item,
            SW_VisualState state)
        {
            GameObject prefab =
                SW_ResolvePrefab(
                    state.PrefabName);

            if (prefab == null)
            {
                return;
            }

            GameObject visual =
                UnityEngine.Object.Instantiate(
                    prefab,
                    state.Parent,
                    false);

            if (visual == null)
            {
                return;
            }

            state.CustomVisual = visual;
            SW_CacheVisualComponents(state);
            visual.name =
                SW_CustomPrefix +
                actor.Sim.GetInstanceID() +
                "_" +
                item.Id +
                "_" +
                state.Label;

            SW_DisablePhysics(visual);
            SW_RepairUnsupportedShaders(visual);
            SW_EnsureCustomVisible(state);

            if (item.IsBow)
            {
                SW_PrepareBowRendering(state);
            }

            SW_ResolveAlignment(
                actor,
                item,
                state);

            SW_ApplyAlignment(state);
            SW_EnsureCustomVisible(state);
            SW_HideNativeVisuals(actor, item, state);

            string simName =
                actor.Sim.GetThisNPC() != null
                    ? actor.Sim.GetThisNPC().NPCName
                    : actor.Sim.name;

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] attached [" +
                state.PrefabName +
                "] to Sim [" +
                simName +
                "] Item=[" +
                item.ItemName +
                "] Slot=[" +
                state.Label +
                "].");
        }


        private void SW_MaintainSlot(
            SW_ActorState actor,
            SW_VisualState state)
        {
            if (actor == null ||
                actor.Sim == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Item item =
                SW_GetEquippedItem(
                    actor.Sim,
                    state.SlotType);

            if (item == null ||
                !string.Equals(
                    item.Id,
                    state.ItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            VR_FollowHandSocket(state.SocketProxy, state.HandRoot);

            if (!state.FollowWorld && state.Parent != null &&
                (state.SlotType == Item.SlotType.Primary ||
                 state.SlotType == Item.SlotType.Secondary) &&
                state.CustomVisual.transform.parent != state.Parent)
            {
                state.CustomVisual.transform.SetParent(state.Parent, false);
            }

            SW_EnsureCustomVisible(state);
            SW_ApplyAlignment(state);
            SW_ReassertHiddenNative(state);
        }


        private static void SW_ResolveAlignment(
            SW_ActorState actor,
            Item item,
            SW_VisualState state)
        {
            if (actor == null ||
                item == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            if (state.FollowWorld &&
                state.FollowTransform != null)
            {
                state.Position = Vector3.zero;
                state.Rotation = Quaternion.identity;
                state.Scale = Vector3.one;

                SW_ApplyItemTuning(
                    item,
                    state);

                state.HasAlignment = true;
                return;
            }

            if (state.SlotType == Item.SlotType.Neck &&
                state.NativeVisual == null)
            {
                Transform custom =
                    state.CustomVisual.transform;

                Transform actorTransform =
                    actor.Root != null
                        ? actor.Root
                        : actor.Sim.transform;

                custom.rotation =
                    actorTransform.rotation;

                custom.position =
                    state.Parent.position +
                    actorTransform.forward * 0.085f +
                    Vector3.down * 0.115f;

                state.Position =
                    custom.localPosition;
                state.Rotation =
                    custom.localRotation;
                state.Scale =
                    custom.localScale;
                state.HasAlignment = true;
                return;
            }

            Transform native =
                state.NativeVisual;

            Transform visualTransform =
                state.CustomVisual.transform;

            if (native != null)
            {
                visualTransform.position =
                    native.position;
                visualTransform.rotation =
                    native.rotation;

                // Keep the same native-to-custom bounds normalization used
                // by the working pre-r21 path. Authored tuning is applied on
                // top of this factor in SW_ApplyItemTuning().
                float factor =
                    SW_ResolveScaleFactor(
                        native.gameObject,
                        state.CustomVisual);

                visualTransform.localScale =
                    Vector3.one * factor;
            }
            else
            {
                visualTransform.localPosition =
                    Vector3.zero;
                visualTransform.localRotation =
                    Quaternion.identity;
                visualTransform.localScale =
                    Vector3.one;
            }

            state.Position =
                visualTransform.localPosition;
            state.Rotation =
                visualTransform.localRotation;
            state.Scale =
                visualTransform.localScale;

            // Rotcleaver needs the native weapon's ROTATION/SCALE so it keeps
            // the correct 2H axe orientation, but it must not inherit the
            // native axe mesh's model-specific positional offset.  That offset
            // is what left the custom axe floating in front of the Sim hand.
            // Start from the actual WeaponR socket origin, then the existing
            // authored grip-centre correction in SW_ApplyItemTuning() seats the
            // wrapped grip in the hand.  Sporebound deliberately keeps the
            // native positional baseline because its Sim placement is already
            // confirmed correct.
            if (state.SlotType == Item.SlotType.Primary &&
                !state.FollowWorld &&
                (string.Equals(
                    item.Id,
                    MoldermawEquipment.RotcleaverId,
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                    item.Id,
                    VarkeshEquipment.GravetideReaverId,
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                    item.Id,
                    GorathEquipment.HellforgedTalonId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                state.Position = Vector3.zero;
            }

            SW_ApplyItemTuning(
                item,
                state);

            state.HasAlignment = true;
        }


        private static void SW_ApplyItemTuning(
            Item item,
            SW_VisualState state)
        {
            if (item == null || state == null)
            {
                return;
            }

            Vector3 rotation = Vector3.zero;
            float scale = 1.0f;
            float gripCenter = 0.0f;

            if (string.Equals(
                    item.Id,
                    FallenScepter.ItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                state.Position += new Vector3(10f, 5f, 15f);
                rotation = new Vector3(180f, 0f, 0f);
                scale = 1.0f;
                gripCenter = 0.0f;
            }
            else if (string.Equals(
                    item.Id,
                    MoldermawEquipment.SporeboundScepterId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    item.Id,
                    NhalSeraEquipment.LightlessScepterId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    item.Id,
                    GorathEquipment.EmberOfTheDemonKingId,
                    StringComparison.OrdinalIgnoreCase))
            {
                state.Position +=
                    state.SlotType == Item.SlotType.Secondary
                        ? new Vector3(0f, 0f, -20f)
                        : new Vector3(0f, 0f, 20f);

                rotation =
                    new Vector3(270f, 0f, 0f);

                scale = 100.0f;
                gripCenter = 0.0f;
            }
            else if (string.Equals(
                         item.Id,
                         MoldermawEquipment.RotcleaverId,
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(
                         item.Id,
                         VarkeshEquipment.GravetideReaverId,
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(
                         item.Id,
                         GorathEquipment.HellforgedTalonId,
                         StringComparison.OrdinalIgnoreCase))
            {
                state.Position +=
                    new Vector3(0f, 0f, 30f);

                rotation =
                    new Vector3(-90f, -90f, 0f);

                scale = 100.0f;
                gripCenter = 0.0f;
            }
            else if (string.Equals(
                         item.Id,
                         MoldermawEquipment.BulwarkId,
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(
                         item.Id,
                         GorathEquipment.InfernalBastionId,
                         StringComparison.OrdinalIgnoreCase))
            {
                rotation =
                    new Vector3(90f, 0f, 0f);

                scale = 100.0f;
            }
            else if (string.Equals(
                         item.Id,
                         MoldermawEquipment.SporepiercerId,
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(
                         item.Id,
                         GorathEquipment.StormfireId,
                         StringComparison.OrdinalIgnoreCase))
            {
                rotation =
                    new Vector3(90f, 90f, 0f);

                scale = 100.0f;
            }
            else if (string.Equals(
                         item.Id,
                         VarkeshEquipment.SoulbinderGrimoireId,
                         StringComparison.OrdinalIgnoreCase))
            {
                state.Position += new Vector3(0f, 0f, -8f);
                rotation = new Vector3(270f, 0f, 0f);
                scale = 20.0f;
            }
            else if (string.Equals(
                         item.Id,
                         VarkeshEquipment.LanternOfDepartedSoulsId,
                         StringComparison.OrdinalIgnoreCase))
            {
                state.Position += new Vector3(0f, 0f, -12f);
                rotation = new Vector3(270f, 0f, 0f);
                scale = 35.0f;
            }

            state.Rotation =
                state.Rotation *
                Quaternion.Euler(rotation);

            state.Scale *= scale;

            if (gripCenter > 0.0f &&
                state.SlotType == Item.SlotType.Primary &&
                !state.FollowWorld)
            {
                // The native Sim weapon reference is effectively the hand/grip
                // anchor.  Our scepter and axe roots are at their pommels.
                // Shift the prefab backwards along its own authored long axis
                // so the centre of the wrapped grip, not the pommel/root, lands
                // in the Sim's hand.  Use the final rotation and uniform scale
                // so the correction remains valid for every Sim orientation.
                float uniformScale =
                    Mathf.Abs(state.Scale.x);

                Vector3 authoredGripOffset =
                    new Vector3(
                        0.0f,
                        gripCenter * uniformScale,
                        0.0f);

                state.Position -=
                    state.Rotation *
                    authoredGripOffset;
            }
        }


        private static void SW_ApplyAlignment(
            SW_VisualState state)
        {
            if (state == null ||
                state.CustomVisual == null ||
                !state.HasAlignment)
            {
                return;
            }

            // Match the player lantern behaviour for SimPlayers: authored +Z
            // points up, the grip ring tracks the hand, and the lantern hangs
            // a little BELOW the hand rather than intersecting the palm.
            if (PW_IsLantern(state.ItemId))
            {
                SW_ApplyLanternHang(state);
                return;
            }

            Transform custom =
                state.CustomVisual.transform;

            if (state.FollowWorld &&
                state.FollowTransform != null)
            {
                Transform follow =
                    state.FollowTransform;

                custom.position =
                    follow.TransformPoint(
                        state.Position);

                custom.rotation =
                    follow.rotation *
                    state.Rotation;

                custom.localScale =
                    state.Scale;

                return;
            }

            custom.localPosition =
                state.Position;
            custom.localRotation =
                state.Rotation;
            custom.localScale =
                state.Scale;
        }


        private static void SW_ApplyLanternHang(SW_VisualState state)
        {
            Transform target = state.FollowWorld && state.FollowTransform != null
                ? state.FollowTransform
                : state.Parent;
            if (target == null || state.CustomVisual == null) return;

            Transform custom = state.CustomVisual.transform;
            custom.localScale = state.Scale;
            Vector3 forward = target.root != null
                ? target.root.forward
                : target.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            custom.rotation =
                Quaternion.LookRotation(forward.normalized, Vector3.up) *
                Quaternion.Euler(-90f, 0f, 0f);
            custom.position = target.position;

            Transform grip = PW_FindNamedDescendant(custom, "RingGrip");
            if (grip == null)
                grip = PW_FindNamedDescendant(custom, "Handle");
            if (grip != null)
                custom.position += target.position - grip.position;
            custom.position += Vector3.down * PW_LanternHandClearance;
        }


        private static float SW_ResolveScaleFactor(
            GameObject native,
            GameObject custom)
        {
            Bounds nativeBounds;
            Bounds customBounds;

            if (!SW_TryGetBounds(native, out nativeBounds) ||
                !SW_TryGetBounds(custom, out customBounds))
            {
                return 1.0f;
            }

            float nativeSize =
                Mathf.Max(
                    nativeBounds.size.x,
                    Mathf.Max(
                        nativeBounds.size.y,
                        nativeBounds.size.z));

            float customSize =
                Mathf.Max(
                    customBounds.size.x,
                    Mathf.Max(
                        customBounds.size.y,
                        customBounds.size.z));

            if (nativeSize <= 0.0001f ||
                customSize <= 0.0001f)
            {
                return 1.0f;
            }

            return Mathf.Clamp(
                nativeSize / customSize,
                0.01f,
                100.0f);
        }


        private static void SW_HideNativeVisuals(
            SW_ActorState actor,
            Item item,
            SW_VisualState state)
        {
            if (actor == null ||
                item == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            if (item.IsBow)
            {
                SW_HideNativeBowRenderers(
                    actor.Root,
                    item,
                    state);

                return;
            }

            if (state.NativeVisual != null)
            {
                SW_HideTransform(
                    state,
                    state.NativeVisual);
            }

            if ((state.SlotType == Item.SlotType.Primary ||
                 state.SlotType == Item.SlotType.Secondary) &&
                state.HandRoot != null)
            {
                for (int i = 0;
                     i < state.HandRoot.childCount;
                     i++)
                {
                    Transform child =
                        state.HandRoot.GetChild(i);

                    if (child == null ||
                        child == state.CustomVisual.transform ||
                        SW_IsInsideCustom(child, state.CustomVisual.transform) ||
                        !child.gameObject.activeInHierarchy ||
                        string.Equals(
                            child.name,
                            "DEFAULT",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (child.GetComponentInChildren<Renderer>(true) != null)
                    {
                        SW_HideTransform(state, child);
                    }
                }
            }

        }


        private static void SW_HideTransform(
            SW_VisualState state,
            Transform transform)
        {
            if (state == null ||
                transform == null ||
                state.CustomVisual == null ||
                SW_IsInsideCustom(
                    transform,
                    state.CustomVisual.transform))
            {
                return;
            }

            SW_HiddenObject hidden = null;

            for (int i = 0;
                 i < state.HiddenObjects.Count;
                 i++)
            {
                SW_HiddenObject existing =
                    state.HiddenObjects[i];

                if (existing != null &&
                    existing.Transform == transform)
                {
                    hidden = existing;
                    break;
                }
            }

            if (hidden == null)
            {
                hidden =
                    new SW_HiddenObject(transform);

                state.HiddenObjects.Add(hidden);
            }

            hidden.Hide();
        }


        private static void SW_HideNativeBowRenderers(
            Transform actorRoot,
            Item item,
            SW_VisualState state)
        {
            if (actorRoot == null ||
                item == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Renderer[] renderers =
                actorRoot.GetComponentsInChildren<Renderer>(true);

            string equipmentName =
                SW_NormalizeName(
                    item.EquipmentToActivate);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null ||
                    SW_IsInsideCustom(
                        renderer.transform,
                        state.CustomVisual.transform) ||
                    !SW_LooksLikeBow(
                        renderer.transform,
                        actorRoot,
                        equipmentName))
                {
                    continue;
                }

                SW_HiddenRenderer hidden = null;

                for (int h = 0;
                     h < state.HiddenRenderers.Count;
                     h++)
                {
                    SW_HiddenRenderer existing =
                        state.HiddenRenderers[h];

                    if (existing != null &&
                        existing.Renderer == renderer)
                    {
                        hidden = existing;
                        break;
                    }
                }

                if (hidden == null)
                {
                    hidden =
                        new SW_HiddenRenderer(renderer);

                    state.HiddenRenderers.Add(hidden);
                }

                hidden.Hide();
            }
        }


        private static Transform SW_FindNativeBowVisual(
            Transform actorRoot,
            Item item)
        {
            if (actorRoot == null || item == null)
            {
                return null;
            }

            Transform exact =
                SW_FindNativeVisual(
                    actorRoot,
                    item,
                    false);

            if (exact != null &&
                exact.gameObject.activeInHierarchy &&
                exact.GetComponentInChildren<Renderer>(true) != null)
            {
                return exact;
            }

            Transform[] all =
                actorRoot.GetComponentsInChildren<Transform>(true);

            string equipmentName =
                SW_NormalizeName(
                    item.EquipmentToActivate);

            Transform best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];

                if (candidate == null ||
                    !candidate.gameObject.activeInHierarchy ||
                    candidate.GetComponentInChildren<Renderer>(false) == null)
                {
                    continue;
                }

                int score =
                    SW_ScoreBow(
                        candidate,
                        actorRoot,
                        equipmentName);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore >= 40
                ? best
                : exact;
        }


        private static int SW_ScoreBow(
            Transform transform,
            Transform actorRoot,
            string equipmentName)
        {
            int score = 0;
            Transform current = transform;
            int depth = 0;

            while (current != null && depth < 8)
            {
                string name =
                    SW_NormalizeName(current.name);

                if (!string.IsNullOrEmpty(equipmentName) &&
                    string.Equals(
                        name,
                        equipmentName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    score += 120;
                }
                else if (!string.IsNullOrEmpty(equipmentName) &&
                         name.Contains(equipmentName))
                {
                    score += 80;
                }

                if (name.Contains("honsu"))
                {
                    score += 70;
                }

                if (name.Contains("bow"))
                {
                    score += 50;
                }

                if (current == actorRoot)
                {
                    break;
                }

                current = current.parent;
                depth++;
            }

            return score;
        }


        private static bool SW_LooksLikeBow(
            Transform transform,
            Transform actorRoot,
            string equipmentName)
        {
            return SW_ScoreBow(
                       transform,
                       actorRoot,
                       equipmentName) >= 40;
        }


        private static Transform SW_FindNativeVisual(
            Transform root,
            Item item,
            bool allowFallback)
        {
            if (root == null || item == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(
                    item.EquipmentToActivate))
            {
                Transform exact =
                    SW_FindByName(
                        root,
                        item.EquipmentToActivate,
                        true);

                if (exact != null)
                {
                    return exact;
                }
            }

            if (!allowFallback)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                if (child == null ||
                    !child.gameObject.activeInHierarchy ||
                    string.Equals(
                        child.name,
                        "DEFAULT",
                        StringComparison.OrdinalIgnoreCase) ||
                    child.name.StartsWith(
                        SW_CustomPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child;
                }
            }

            return null;
        }


        private static Transform SW_FindByName(
            Transform root,
            string name,
            bool preferActive)
        {
            if (root == null ||
                string.IsNullOrEmpty(name))
            {
                return null;
            }

            Transform fallback = null;
            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];

                if (candidate == null ||
                    candidate.name.StartsWith(
                        SW_CustomPrefix,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!preferActive ||
                    candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }
            }

            return fallback;
        }


        private static Transform SW_FindBodyAnchor(
            Transform root)
        {
            string[] names =
            {
                "Neck",
                "Neck_01",
                "Spine_02",
                "UpperChest",
                "Chest"
            };

            for (int i = 0; i < names.Length; i++)
            {
                Transform found =
                    SW_FindByName(
                        root,
                        names[i],
                        false);

                if (found != null)
                {
                    return found;
                }
            }

            return root;
        }


        private static Item SW_GetEquippedItem(
            SimPlayer sim,
            Item.SlotType slotType)
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
                SimInvSlot slot =
                    sim.MyEquipment[i];

                if (slot != null &&
                    slot.ThisSlotType == slotType)
                {
                    return slot.MyItem;
                }
            }

            return null;
        }


        private static string SW_GetPrefabForItem(
            Item item,
            Item.SlotType slotType)
        {
            if (item == null)
            {
                return null;
            }

            if (string.Equals(
                    item.Id,
                    MoldermawEquipment.SporeboundScepterId,
                    StringComparison.OrdinalIgnoreCase) &&
                (slotType == Item.SlotType.Primary ||
                 slotType == Item.SlotType.Secondary))
            {
                return MoldermawEquipment.SporeboundScepterPrefab;
            }

            if (string.Equals(
                    item.Id,
                    NhalSeraEquipment.LightlessScepterId,
                    StringComparison.OrdinalIgnoreCase) &&
                (slotType == Item.SlotType.Primary ||
                 slotType == Item.SlotType.Secondary))
            {
                return NhalSeraEquipment.LightlessScepterPrefab;
            }

            if (string.Equals(
                    item.Id,
                    FallenScepter.ItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                (slotType == Item.SlotType.Primary ||
                 slotType == Item.SlotType.Secondary))
            {
                return "FallenScepter";
            }

            if (string.Equals(
                    item.Id,
                    MoldermawEquipment.RotcleaverId,
                    StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Primary)
            {
                return MoldermawEquipment.RotcleaverPrefab;
            }

            if (string.Equals(
                    item.Id,
                    MoldermawEquipment.BulwarkId,
                    StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Secondary)
            {
                return MoldermawEquipment.BulwarkPrefab;
            }

            if (string.Equals(
                    item.Id,
                    MoldermawEquipment.BloomId,
                    StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Neck)
            {
                return MoldermawEquipment.BloomPrefab;
            }

            if (string.Equals(
                    item.Id,
                    MoldermawEquipment.SporepiercerId,
                    StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Primary)
            {
                return MoldermawEquipment.SporepiercerPrefab;
            }

            if (string.Equals(item.Id, VarkeshEquipment.SoulbinderGrimoireId, StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Secondary)
                return VarkeshEquipment.SoulbinderGrimoirePrefab;

            if (string.Equals(item.Id, VarkeshEquipment.GravetideReaverId, StringComparison.OrdinalIgnoreCase) &&
                (slotType == Item.SlotType.Primary || slotType == Item.SlotType.Secondary))
                return VarkeshEquipment.GravetideReaverPrefab;

            if (string.Equals(item.Id, VarkeshEquipment.LanternOfDepartedSoulsId, StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Secondary)
                return VarkeshEquipment.LanternOfDepartedSoulsPrefab;

            if (string.Equals(item.Id, GorathEquipment.EmberOfTheDemonKingId, StringComparison.OrdinalIgnoreCase) &&
                (slotType == Item.SlotType.Primary || slotType == Item.SlotType.Secondary))
                return GorathEquipment.EmberOfTheDemonKingPrefab;

            if (string.Equals(item.Id, GorathEquipment.HellforgedTalonId, StringComparison.OrdinalIgnoreCase) &&
                (slotType == Item.SlotType.Primary || slotType == Item.SlotType.Secondary))
                return GorathEquipment.HellforgedTalonPrefab;

            if (string.Equals(item.Id, GorathEquipment.InfernalBastionId, StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Secondary)
                return GorathEquipment.InfernalBastionPrefab;

            if (string.Equals(item.Id, GorathEquipment.StormfireId, StringComparison.OrdinalIgnoreCase) &&
                slotType == Item.SlotType.Primary)
                return GorathEquipment.StormfirePrefab;

            return null;
        }


        private static GameObject SW_ResolvePrefab(
            string prefabName)
        {
            GameObject direct =
                AssetBundleRegistry.GetPrefab(
                    MoldermawEquipment.EquipmentBundleName,
                    prefabName);

            if (direct != null)
            {
                return direct;
            }

            AssetBundle bundle =
                AssetBundleRegistry.GetBundle(
                    MoldermawEquipment.EquipmentBundleName);

            if (bundle == null)
            {
                return null;
            }

            string wanted =
                SW_NormalizeName(prefabName);

            string[] names =
                bundle.GetAllAssetNames();

            if (names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string path = names[i];
                string fileName =
                    Path.GetFileNameWithoutExtension(path);

                if (!string.Equals(
                        SW_NormalizeName(fileName),
                        wanted,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject prefab =
                    bundle.LoadAsset<GameObject>(path);

                if (prefab != null)
                {
                    return prefab;
                }
            }

            try
            {
                foreach (AssetBundle loaded in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (loaded == null || loaded == bundle)
                        continue;

                    string[] loadedNames = loaded.GetAllAssetNames();
                    for (int i = 0; loadedNames != null && i < loadedNames.Length; i++)
                    {
                        string path = loadedNames[i];
                        string fileName = Path.GetFileNameWithoutExtension(path);

                        if (!string.Equals(
                                SW_NormalizeName(fileName),
                                wanted,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        GameObject prefab = loaded.LoadAsset<GameObject>(path);
                        if (prefab != null)
                            return prefab;
                    }
                }
            }
            catch
            {
            }

            return null;
        }


        private static string SW_NormalizeName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string result =
                value.Trim()
                    .Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty)
                    .ToLowerInvariant();

            if (result.StartsWith(
                    "fotf",
                    StringComparison.Ordinal))
            {
                result = result.Substring(4);
            }

            return result;
        }


        private static bool SW_TryGetBounds(
            GameObject obj,
            out Bounds bounds)
        {
            bounds = new Bounds();

            if (obj == null)
            {
                return false;
            }

            Renderer[] renderers =
                obj.GetComponentsInChildren<Renderer>(true);

            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
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
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            return found;
        }


        private static void SW_CacheVisualComponents(
            SW_VisualState state)
        {
            if (state == null || state.CustomVisual == null)
            {
                return;
            }

            GameObject visual = state.CustomVisual;
            state.CachedHierarchy = visual.GetComponentsInChildren<Transform>(true);
            state.CachedRenderers = visual.GetComponentsInChildren<Renderer>(true);
            state.CachedLodGroups = visual.GetComponentsInChildren<LODGroup>(true);
            state.CachedSkinnedRenderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private static void SW_EnsureCustomVisible(
            SW_VisualState state)
        {
            if (state == null || state.CustomVisual == null)
            {
                return;
            }

            if (!state.CustomVisual.activeSelf)
            {
                state.CustomVisual.SetActive(true);
            }

            Transform[] hierarchy = state.CachedHierarchy;
            for (int i = 0; hierarchy != null && i < hierarchy.Length; i++)
            {
                if (hierarchy[i] != null && !hierarchy[i].gameObject.activeSelf)
                {
                    hierarchy[i].gameObject.SetActive(true);
                }
            }

            Renderer[] renderers = state.CachedRenderers;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                if (renderers[i] != null && !renderers[i].enabled)
                {
                    renderers[i].enabled = true;
                }
            }
        }

        private static void SW_PrepareBowRendering(
            SW_VisualState state)
        {
            if (state == null || state.CustomVisual == null)
            {
                return;
            }

            SW_EnsureCustomVisible(state);

            LODGroup[] lodGroups = state.CachedLodGroups;
            for (int i = 0; lodGroups != null && i < lodGroups.Length; i++)
            {
                if (lodGroups[i] != null)
                {
                    lodGroups[i].ForceLOD(0);
                }
            }

            SkinnedMeshRenderer[] skinned = state.CachedSkinnedRenderers;
            for (int i = 0; skinned != null && i < skinned.Length; i++)
            {
                if (skinned[i] != null)
                {
                    skinned[i].enabled = true;
                    skinned[i].updateWhenOffscreen = true;
                }
            }
        }

        private static void SW_ReassertHiddenNative(
            SW_VisualState state)
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < state.HiddenObjects.Count; i++)
            {
                if (state.HiddenObjects[i] != null)
                {
                    state.HiddenObjects[i].Hide();
                }
            }

            for (int i = 0; i < state.HiddenRenderers.Count; i++)
            {
                if (state.HiddenRenderers[i] != null)
                {
                    state.HiddenRenderers[i].Hide();
                }
            }
        }

        private static void SW_EnsureCustomVisible_Legacy(
            GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            if (!visual.activeSelf)
            {
                visual.SetActive(true);
            }

            Transform[] hierarchy =
                visual.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < hierarchy.Length; i++)
            {
                Transform child = hierarchy[i];

                if (child != null &&
                    !child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                }
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                }
            }
        }


        private static void SW_PrepareBowRendering_Legacy(
            GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            SW_EnsureCustomVisible_Legacy(visual);

            LODGroup[] lodGroups =
                visual.GetComponentsInChildren<LODGroup>(true);

            for (int i = 0; i < lodGroups.Length; i++)
            {
                if (lodGroups[i] != null)
                {
                    lodGroups[i].ForceLOD(0);
                }
            }

            SkinnedMeshRenderer[] skinned =
                visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            for (int i = 0; i < skinned.Length; i++)
            {
                if (skinned[i] != null)
                {
                    skinned[i].enabled = true;
                    skinned[i].updateWhenOffscreen = true;
                }
            }

            SW_RepairUnsupportedShaders(visual);
        }


        private static void SW_DisablePhysics(
            GameObject visual)
        {
            Collider[] colliders =
                visual.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody[] bodies =
                visual.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                {
                    bodies[i].isKinematic = true;
                    bodies[i].detectCollisions = false;
                }
            }
        }


        private static void SW_RepairUnsupportedShaders(
            GameObject visual)
        {
            Shader standard = Shader.Find("Standard");

            if (standard == null ||
                !standard.isSupported)
            {
                return;
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(true);

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];

                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];

                    if (material != null &&
                        (material.shader == null ||
                         !material.shader.isSupported))
                    {
                        material.shader = standard;
                    }
                }
            }
        }


        private static bool SW_IsInsideCustom(
            Transform transform,
            Transform customRoot)
        {
            Transform current = transform;

            while (current != null)
            {
                if (current == customRoot ||
                    current.name.StartsWith(
                        SW_CustomPrefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }


        private static void SW_RemoveActor(
            SW_ActorState actor)
        {
            if (actor == null)
            {
                return;
            }

            SW_Remove(actor.Primary, true);
            SW_Remove(actor.Secondary, true);
            SW_Remove(actor.Neck, true);
        }


        private static void SW_Remove(
            SW_VisualState state,
            bool restoreNative)
        {
            if (state == null)
            {
                return;
            }

            if (state.CustomVisual != null)
            {
                UnityEngine.Object.Destroy(
                    state.CustomVisual);

                state.CustomVisual = null;
            }

            VR_DestroyProxy(ref state.SocketProxy);
            state.CachedHierarchy = null;
            state.CachedRenderers = null;
            state.CachedLodGroups = null;
            state.CachedSkinnedRenderers = null;

            if (restoreNative)
            {
                for (int i = 0;
                     i < state.HiddenObjects.Count;
                     i++)
                {
                    SW_HiddenObject hidden =
                        state.HiddenObjects[i];

                    if (hidden != null)
                    {
                        hidden.Restore();
                    }
                }

                for (int i = 0;
                     i < state.HiddenRenderers.Count;
                     i++)
                {
                    SW_HiddenRenderer hidden =
                        state.HiddenRenderers[i];

                    if (hidden != null)
                    {
                        hidden.Restore();
                    }
                }
            }

            state.HiddenObjects.Clear();
            state.HiddenRenderers.Clear();
            state.ItemId = null;
            state.PrefabName = null;
            state.Parent = null;
            state.HandRoot = null;
            state.NativeVisual = null;
            state.FollowTransform = null;
            state.FollowWorld = false;
            state.HasAlignment = false;
            state.Position = Vector3.zero;
            state.Rotation = Quaternion.identity;
            state.Scale = Vector3.one;
        }


        private sealed class SW_ActorState
        {
            internal SW_ActorState(
                SimPlayer sim)
            {
                Sim = sim;

                Primary =
                    new SW_VisualState(
                        Item.SlotType.Primary,
                        "Primary");

                Secondary =
                    new SW_VisualState(
                        Item.SlotType.Secondary,
                        "Secondary");

                Neck =
                    new SW_VisualState(
                        Item.SlotType.Neck,
                        "Neck");

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

                // SimPlayer, Inventory and ModularParts live on the same
                // actor GameObject in the native prefab.  Use that actor as
                // the search root instead of transform.root so a scene-level
                // container can never make one Sim hide another Sim's bow.
                Root = Sim.transform;
            }

            internal SimPlayer Sim;
            internal Inventory Inventory;
            internal ModularParts Modulars;
            internal Transform Root;
            internal SW_VisualState Primary;
            internal SW_VisualState Secondary;
            internal SW_VisualState Neck;
        }


        private sealed class SW_VisualState
        {
            internal SW_VisualState(
                Item.SlotType slotType,
                string label)
            {
                SlotType = slotType;
                Label = label;
                HiddenObjects =
                    new List<SW_HiddenObject>();
                HiddenRenderers =
                    new List<SW_HiddenRenderer>();
            }

            internal Item.SlotType SlotType;
            internal string Label;
            internal string ItemId;
            internal string PrefabName;
            internal Transform SocketProxy;
            internal Transform Parent;
            internal Transform HandRoot;
            internal Transform NativeVisual;
            internal Transform FollowTransform;
            internal bool FollowWorld;
            internal GameObject CustomVisual;
            internal Transform[] CachedHierarchy;
            internal Renderer[] CachedRenderers;
            internal LODGroup[] CachedLodGroups;
            internal SkinnedMeshRenderer[] CachedSkinnedRenderers;
            internal List<SW_HiddenObject> HiddenObjects;
            internal List<SW_HiddenRenderer> HiddenRenderers;
            internal bool HasAlignment;
            internal Vector3 Position;
            internal Quaternion Rotation = Quaternion.identity;
            internal Vector3 Scale = Vector3.one;
        }


        private sealed class SW_HiddenObject
        {
            internal SW_HiddenObject(
                Transform transform)
            {
                Transform = transform;
                WasActive =
                    transform != null &&
                    transform.gameObject != null &&
                    transform.gameObject.activeSelf;
            }

            internal void Hide()
            {
                if (Transform != null &&
                    Transform.gameObject != null &&
                    Transform.gameObject.activeSelf)
                {
                    Transform.gameObject.SetActive(false);
                }
            }

            internal void Restore()
            {
                if (Transform != null &&
                    Transform.gameObject != null)
                {
                    Transform.gameObject.SetActive(
                        WasActive);
                }
            }

            internal Transform Transform;
            internal bool WasActive;
        }


        private sealed class SW_HiddenRenderer
        {
            internal SW_HiddenRenderer(
                Renderer renderer)
            {
                Renderer = renderer;
                WasEnabled =
                    renderer != null &&
                    renderer.enabled;
            }

            internal void Hide()
            {
                if (Renderer != null)
                {
                    Renderer.enabled = false;
                }
            }

            internal void Restore()
            {
                if (Renderer != null)
                {
                    Renderer.enabled = WasEnabled;
                }
            }

            internal Renderer Renderer;
            internal bool WasEnabled;
        }
    }
}
