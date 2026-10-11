using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FateOfTheFallen
{
internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        private const float PW_RefreshInterval = 0.50f;
        private const string PW_CustomPrefix = "FOTF_CustomItemVisual_";

        // Hand items remain attached directly to Erenshor's stable WeaponR /
        // WeaponL roots, but the Moldermaw models do NOT share the native
        // template pivots/axes. We therefore keep the native template only
        // as a coarse socket reference and apply an explicit per-item art
        // calibration afterwards.
        //
        // These are mutable during development so /fotfvisual can tune the
        // values live without rebuilding the DLL. Once confirmed in-game,
        // the final values can simply be baked back into these defaults.
        private sealed class PW_TransformTuning
        {
            internal PW_TransformTuning(
                Vector3 position,
                Vector3 rotationEuler,
                float scale)
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

        // Confirmed live in-game calibration values.
        // These values are explicit authored-model transforms; do not derive
        // them from the native template pivot.
        // Sporepiercer must inherit Erenshor's native bow pose so the idle and
        // attack animations remain native: at the side while idle, vertical in
        // front of the actor while firing. The only correction we apply is a
        // +90 degree roll around the bow's own long/local-Y axis. This rotates
        // the authored wooden belly (+X) toward the ground at rest without
        // replacing the native bow orientation or attack animation.
        private static readonly PW_TransformTuning PW_ScepterPrimaryTuning =
            new PW_TransformTuning(
                new Vector3(0f, 0f, 20f),
                new Vector3(270f, 0f, 0f),
                100.00f);

        private static readonly PW_TransformTuning PW_ScepterSecondaryTuning =
            new PW_TransformTuning(
                new Vector3(0f, 0f, -20f),
                new Vector3(270f, 0f, 0f),
                100.00f);

        private static readonly PW_TransformTuning PW_FallenScepterTuning =
            new PW_TransformTuning(
                new Vector3(10f, 5f, 15f),
                new Vector3(180f, 0f, 0f),
                1.00f);

        private static readonly PW_TransformTuning PW_RotcleaverTuning =
            new PW_TransformTuning(
                new Vector3(0f, 0f, 30f),
                new Vector3(-90f, -90f, 0f),
                100.00f);

        private static readonly PW_TransformTuning PW_BulwarkTuning =
            new PW_TransformTuning(
                Vector3.zero,
                new Vector3(90f, 0f, 0f),
                100.00f);

        // Infernal Bastion is authored substantially larger than Bulwark and
        // its front face is opposite the inherited shield template.  Give it
        // a dedicated transform instead of reusing Bulwark's 100x calibration.
        private static readonly PW_TransformTuning PW_InfernalBastionTuning =
            new PW_TransformTuning(
                Vector3.zero,
                new Vector3(90f, 180f, 0f),
                60.00f);

        private static readonly PW_TransformTuning PW_SporepiercerTuning =
            new PW_TransformTuning(
                Vector3.zero,
                new Vector3(90f, 90f, 0f),
                100.00f);

        // Initial late-boss authored-model alignments. These deliberately reuse
        // the proven Moldermaw transform families so the new prefabs are visible
        // immediately; /fotfvisual can fine-tune them in game before release.
        private static readonly PW_TransformTuning PW_GravetidePrimaryTuning =
            new PW_TransformTuning(
                new Vector3(0f, 0f, 20f),
                new Vector3(-90f, -90f, 0f),
                100.00f);

        private static readonly PW_TransformTuning PW_GravetideSecondaryTuning =
            new PW_TransformTuning(
                new Vector3(0f, 0f, -20f),
                new Vector3(-90f, 90f, 0f),
                100.00f);

        private static readonly PW_TransformTuning PW_GrimoireTuning =
            new PW_TransformTuning(
                Vector3.zero,
                new Vector3(270f, 0f, 0f),
                14.00f);

        // World-space clearance measured from the hand socket after the authored
        // RingGrip has been aligned. Keep this separate from the weapon tuning:
        // PW_ApplyLanternHang owns the final position every LateUpdate.
        private const float PW_LanternHandClearance = 0.12f;

        private static readonly PW_TransformTuning PW_LanternTuning =
            new PW_TransformTuning(
                Vector3.zero,
                Vector3.zero,
                12.00f);

        private static readonly PW_TransformTuning PW_HellforgedPrimaryTuning =
            new PW_TransformTuning(
                new Vector3(0f, 0f, 20f),
                new Vector3(-90f, -90f, 0f),
                100.00f);

        private static readonly PW_TransformTuning PW_HellforgedSecondaryTuning =
            new PW_TransformTuning(
                new Vector3(0f, 0f, -20f),
                new Vector3(-90f, 90f, 0f),
                100.00f);

        // Native necklaces are often stat-only. If Bloom has no native visual,
        // attach it to the character's Neck bone. These are deliberately kept
        // as isolated tuning values for the first in-game alignment pass.
        private const float PW_BloomForwardOffset = 0.085f;
        private const float PW_BloomDownOffset = 0.115f;

        private readonly PW_VisualState _pwPrimary =
            new PW_VisualState(Item.SlotType.Primary, "Primary");

        private readonly PW_VisualState _pwSecondary =
            new PW_VisualState(Item.SlotType.Secondary, "Secondary");

        private readonly PW_VisualState _pwNeck =
            new PW_VisualState(Item.SlotType.Neck, "Neck");

        private readonly HashSet<string> _pwLoggedMissingPrefabs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _pwLoggedResolvedPrefabs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private float _pwNextRefreshTime;
        private bool _pwClassRestrictionsRefreshed;


        private void PW_Update()
        {
            if (Time.unscaledTime < _pwNextRefreshTime)
            {
                return;
            }

            _pwNextRefreshTime =
                Time.unscaledTime + PW_RefreshInterval;

            PW_Refresh();
        }

        private void PW_LateUpdate()
        {
            Inventory inventory = GameData.PlayerInv;

            if (inventory == null)
            {
                return;
            }

            PW_Maintain(inventory, _pwPrimary);
            PW_Maintain(inventory, _pwSecondary);
            PW_Maintain(inventory, _pwNeck);
        }

        internal void PW_Shutdown()
        {
            PW_Remove(_pwPrimary, true);
            PW_Remove(_pwSecondary, true);
            PW_Remove(_pwNeck, true);
            _pwClassRestrictionsRefreshed = false;
        }


        private void PW_Refresh()
        {
            if (!_pwClassRestrictionsRefreshed)
            {
                MoldermawEquipment.RefreshClassRestrictions();
                _pwClassRestrictionsRefreshed = true;
            }

            Inventory inventory = GameData.PlayerInv;

            if (inventory == null)
            {
                PW_Remove(_pwPrimary, false);
                PW_Remove(_pwSecondary, false);
                PW_Remove(_pwNeck, false);
                return;
            }

            PW_RefreshSlot(inventory, _pwPrimary);
            PW_RefreshSlot(inventory, _pwSecondary);
            PW_RefreshSlot(inventory, _pwNeck);
        }

        private void PW_RefreshSlot(
            Inventory inventory,
            PW_VisualState state)
        {
            Item item =
                PW_GetEquippedItem(inventory, state.SlotType);

            string prefabName =
                PW_GetPrefabForItem(item, state.SlotType);

            if (item == null ||
                string.IsNullOrEmpty(prefabName))
            {
                PW_Remove(state, false);
                return;
            }

            // Bows can expose several HonsusBow transforms. Re-resolving the
            // attachment every 0.20s can alternate between those candidates,
            // which destroys/re-spawns the custom bow and presents as blinking.
            // Once a bow is attached, keep the exact same reference until the
            // equipped item actually changes or the reference is destroyed.
            if (item.IsBow &&
                state.OwnerInventory == inventory &&
                state.CustomVisual != null &&
                state.CustomVisual.transform.IsChildOf(PW_GetCharacterSearchRoot(inventory)) &&
                state.FollowTransform != null &&
                string.Equals(
                    state.ItemId,
                    item.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                PW_Maintain(inventory, state);
                PW_ReconcileNativeVisibility(inventory, item, state);
                return;
            }

            // Character switching and load restoration can replace the actual
            // player rig while keeping the same Item ID and Inventory reference.
            // A stale or effectively invisible visual must never satisfy the
            // cache merely because the item hasn't been unequipped.
            Transform currentActor = PW_GetCharacterSearchRoot(inventory);
            if (state.CustomVisual != null &&
                (state.OwnerInventory != inventory ||
                 !string.Equals(state.ItemId, item.Id, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(state.PrefabName, prefabName, StringComparison.OrdinalIgnoreCase) ||
                 currentActor == null ||
                 !state.CustomVisual.transform.IsChildOf(currentActor)))
            {
                if (state.SlotType == Item.SlotType.Secondary &&
                    string.Equals(item.Id, MoldermawEquipment.SporeboundScepterId, StringComparison.OrdinalIgnoreCase))
                    Plugin.NativeLog.LogInfo("[FOTFVisualRestore] rebuilding stale Sporebound offhand visual after load.");
                PW_Remove(state, false);
            }

            Transform attachmentRoot;
            Transform nativeVisual;

            if (!PW_ResolveAttachment(
                    inventory,
                    item,
                    state.SlotType,
                    out attachmentRoot,
                    out nativeVisual))
            {
                PW_LogSecondaryRestoreState(state, item, "no-attachment", null);
                return;
            }

            bool handSlot =
                state.SlotType == Item.SlotType.Primary ||
                state.SlotType == Item.SlotType.Secondary;

            // Every hand item, including bows, is parented beneath the same
            // stable attachment hierarchy as its resolved native visual.  The
            // previous bow world-space follower could switch between duplicate
            // HonsusBow transforms during native refreshes, which caused the
            // custom bow to spin and fly away.  Copy the native bow pose once
            // into its stable parent space instead; the parent then carries the
            // normal bow animation without any cumulative world-space math.
            Transform parent;

            if (handSlot)
            {
                // A native WeaponL/WeaponR holder may be inactive during
                // save/character restoration. The shared active socket proxy
                // mirrors its real WORLD transform, so authored item offsets
                // remain relative to the correct hand rather than an ancestor
                // pivot. Once the holder activates we transfer the SAME mesh
                // back without recreating it or losing calibrated alignment.
                parent =
                    PW_GetStableHandParent(
                        inventory,
                        attachmentRoot,
                        state);
            }
            else
            {
                parent =
                    nativeVisual != null && nativeVisual.parent != null
                        ? nativeVisual.parent
                        : attachmentRoot;
            }

            if (parent == null)
            {
                PW_LogSecondaryRestoreState(state, item, "no-active-parent", attachmentRoot);
                return;
            }

            PW_LogSecondaryRestoreState(state, item, "attached", attachmentRoot);
            bool nativeChanged =
                state.NativeVisual != nativeVisual;
            bool socketChanged =
                state.AttachmentRoot != attachmentRoot;

            // The same equipped item can migrate from a temporary active
            // proxy to the real socket. Keep its instantiated mesh instead
            // of destroying/rebuilding it (no loading flicker).
            if (state.CustomVisual != null && state.Parent != parent)
            {
                state.CustomVisual.transform.SetParent(parent, false);
                state.Parent = parent;
                PW_ApplyStoredAlignment(state);
            }

            if (state.SocketProxy != null && state.SocketProxy != parent)
            {
                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] restored native hand socket for [" +
                    item.ItemName + "] Slot=[" + state.Label + "].");
                VR_DestroyProxy(ref state.SocketProxy);
            }

            state.ItemId = item.Id;
            state.PrefabName = prefabName;
            state.OwnerInventory = inventory;
            state.Parent = parent;
            state.AttachmentRoot = attachmentRoot;
            state.NativeVisual = nativeVisual;
            // Bows live beneath the stable actor root and follow ONE resolved
            // native bow reference in world space. The reference is frozen for
            // the lifetime of the equipped item, so duplicate HonsusBow holders
            // cannot make the custom model jump, spin or blink.
            state.FollowWorld = item.IsBow && nativeVisual != null;
            state.FollowTransform = state.FollowWorld ? nativeVisual : null;

            if (state.CustomVisual == null)
            {
                PW_Spawn(inventory, item, state);
            }
            else
            {
                // Character loading can populate the equipped Item before the
                // final native off-hand visual is activated.  If a better
                // native reference becomes available after our initial spawn,
                // refresh the stored alignment immediately rather than waiting
                // for a manual unequip/re-equip cycle.
                if (nativeChanged || socketChanged || !state.HasAlignment)
                {
                    PW_ResolveAlignment(
                        inventory,
                        item,
                        state);

                    PW_ApplyStoredAlignment(
                        state);
                }

                PW_Maintain(inventory, state);
            }

            PW_ReconcileNativeVisibility(
                inventory,
                item,
                state);

            // Log the final state AFTER prefab spawn/visibility reconciliation,
            // not only the transitional state before attachment.
            PW_LogSecondaryRestoreState(state, item, "ready-" +
                PW_HasRenderableCustomVisual(state), state.AttachmentRoot);
        }


        private void PW_Maintain(
            Inventory inventory,
            PW_VisualState state)
        {
            if (inventory == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Item item =
                PW_GetEquippedItem(inventory, state.SlotType);

            if (item == null ||
                !string.Equals(
                    item.Id,
                    state.ItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Native Hand_L is not a reliable active parent while character
            // equipment is being restored. A fallback offhand proxy lives on
            // the ACTIVE actor root, independent of Hand_L's active state.
            // Follow the hand pose, but retain the calibrated weapon-socket
            // scale rather than inheriting the much larger bone scale.
            if (PW_IsFallbackLeftHandSocket(state))
                PW_FollowFallbackLeftHand(inventory, state);
            else
                VR_FollowHandSocket(state.SocketProxy, state.AttachmentRoot);

            // Native equipment refreshes are allowed to toggle every child
            // beneath WeaponR/WeaponL. Keep our object on the stable root and
            // reassert both hierarchy visibility and transform every frame,
            // just like the proven Fallen Scepter controller.
            if (!state.FollowWorld &&
                (state.SlotType == Item.SlotType.Primary ||
                 state.SlotType == Item.SlotType.Secondary) &&
                state.Parent != null &&
                state.CustomVisual.transform.parent != state.Parent)
            {
                // state.Parent may intentionally be an active ancestor of an
                // inactive WeaponL during save-load restoration.  Do not force
                // the custom object back beneath AttachmentRoot until the next
                // refresh has confirmed that the real hand holder is active.
                state.CustomVisual.transform.SetParent(
                    state.Parent,
                    false);

                PW_ApplyStoredAlignment(
                    state);
            }

            // Erenshor can toggle both native weapon children and arbitrary
            // descendants of our instantiated prefab during equipment/combat
            // refreshes. Reassert the complete custom hierarchy every frame,
            // then re-hide only the native objects we previously recorded.
            // This is the persistence pattern proven by the old dedicated
            // Fallen Scepter controller.
            PW_EnsureCustomVisible(state);

            // Lantern of Departed Souls is a hanging object rather than a
            // rigid weapon. Re-apply its world-upright pose every frame so the
            // top ring stays in the hand while the lantern body hangs down.
            if (PW_IsLantern(state.ItemId))
            {
                PW_ApplyStoredAlignment(
                    state);
            }
            else if (state.FollowWorld &&
                     state.FollowTransform != null)
            {
                PW_ApplyStoredAlignment(
                    state);
            }

            PW_ReassertHiddenNative(state);
        }


        private void PW_Spawn(
            Inventory inventory,
            Item item,
            PW_VisualState state)
        {
            GameObject prefab =
                PW_ResolvePrefab(state.PrefabName);

            if (prefab == null)
            {
                // Keep the inherited native visual if our prefab is absent.
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
            PW_CacheVisualComponents(state);
            visual.name =
                PW_CustomPrefix + item.Id + "_" + state.Label;

            PW_DisablePhysics(visual);
            PW_RepairUnsupportedShaders(visual);

            // The current Gravetide Reaver FBX contains faces whose winding
            // can be interpreted inward by Unity.  Make this specific model
            // genuinely two-sided at runtime as a safety net while the source
            // generator also recalculates outward normals for future exports.
            if (string.Equals(
                    item.Id,
                    VarkeshEquipment.GravetideReaverId,
                    StringComparison.OrdinalIgnoreCase))
            {
                BA_ForceDoubleSided(visual);
                BA_MakeMeshFiltersDoubleSided(visual);
            }

            if (item.IsBow)
            {
                PW_PrepareBowRendering(state);
            }

            // Make the imported hierarchy live before reading Renderer.bounds.
            // Inactive AssetBundle children can otherwise report zero-sized
            // bounds and leave a weapon at its tiny import scale.
            PW_EnsureCustomVisible(state);

            PW_ResolveAlignment(
                inventory,
                item,
                state);

            PW_ApplyStoredAlignment(
                state);

            PW_EnsureCustomVisible(state);

            if (!PW_HasRenderableCustomVisual(state))
            {
                Plugin.NativeLog.LogError(
                    "[FateEquipmentVisuals] prefab [" +
                    state.PrefabName +
                    "] instantiated but has no active renderable hierarchy. " +
                    "Native fallback will remain visible.");

                UnityEngine.Object.Destroy(
                    visual);

                state.CustomVisual =
                    null;

                PW_RestoreHiddenNativeVisuals(
                    state);

                return;
            }

            if (state.NativeVisual != null)
            {
                PW_HideNative(
                    state,
                    state.NativeVisual);
            }

            PW_HideOtherNativeHandVisuals(
                state,
                item);

            if (item.IsBow)
            {
                PW_HideNativeBowRenderers(
                    state,
                    PW_GetCharacterSearchRoot(inventory),
                    item);

                PW_LogBowRendererPayload(
                    visual,
                    item);
            }

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] attached [" +
                state.PrefabName +
                "] to [" +
                item.ItemName +
                "] Slot=[" +
                state.Label +
                "] Native=[" +
                (state.NativeVisual != null
                    ? state.NativeVisual.name
                    : "<none>") +
                "].");
        }


        // ============================================================
        // PREFAB LOOKUP
        // ============================================================

        private GameObject PW_ResolvePrefab(
            string prefabName)
        {
            GameObject direct =
                AssetBundleRegistry.GetPrefab(
                    MoldermawEquipment.EquipmentBundleName,
                    prefabName);

            if (direct != null)
            {
                PW_LogPrefabResolved(prefabName, prefabName);
                return direct;
            }

            AssetBundle bundle =
                AssetBundleRegistry.GetBundle(
                    MoldermawEquipment.EquipmentBundleName);

            if (bundle == null)
            {
                PW_LogPrefabMissing(prefabName, null);
                return null;
            }

            string[] names = bundle.GetAllAssetNames();
            string wanted = PW_NormalizeName(prefabName);

            if (names != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    string assetPath = names[i];
                    string fileName =
                        Path.GetFileNameWithoutExtension(assetPath);

                    if (!string.Equals(
                            PW_NormalizeName(fileName),
                            wanted,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!PW_IsExpectedPrefabFile(
                            prefabName,
                            fileName))
                    {
                        continue;
                    }

                    GameObject resolved =
                        bundle.LoadAsset<GameObject>(assetPath);

                    if (resolved != null)
                    {
                        PW_LogPrefabResolved(prefabName, assetPath);
                        return resolved;
                    }
                }
            }

            // Last-resort targeted search across already-loaded bundles.
            // This is only used when the expected equipment bundle lookup did
            // not return the requested prefab, and avoids falling back to a
            // native Corruptor/Honsus/etc. model simply because the bundle was
            // registered under a different runtime key.
            try
            {
                foreach (AssetBundle loaded in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (loaded == null || loaded == bundle)
                        continue;

                    string[] loadedNames = loaded.GetAllAssetNames();
                    for (int i = 0; loadedNames != null && i < loadedNames.Length; i++)
                    {
                        string assetPath = loadedNames[i];
                        string fileName = Path.GetFileNameWithoutExtension(assetPath);

                        if (!string.Equals(
                                PW_NormalizeName(fileName),
                                wanted,
                                StringComparison.OrdinalIgnoreCase) ||
                            !PW_IsExpectedPrefabFile(prefabName, fileName))
                        {
                            continue;
                        }

                        GameObject resolved = loaded.LoadAsset<GameObject>(assetPath);
                        if (resolved != null)
                        {
                            PW_LogPrefabResolved(
                                prefabName,
                                loaded.name + ":" + assetPath);
                            return resolved;
                        }
                    }
                }
            }
            catch
            {
            }

            PW_LogPrefabMissing(prefabName, names);
            return null;
        }

        private void PW_LogPrefabResolved(
            string requested,
            string resolved)
        {
            if (!_pwLoggedResolvedPrefabs.Add(requested))
            {
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] prefab resolved Requested=[" +
                requested +
                "] Asset=[" +
                resolved +
                "].");
        }

        private void PW_LogPrefabMissing(
            string requested,
            string[] available)
        {
            if (!_pwLoggedMissingPrefabs.Add(requested))
            {
                return;
            }

            Plugin.NativeLog.LogError(
                "[FateEquipmentVisuals] prefab [" +
                requested +
                "] not found in [" +
                MoldermawEquipment.EquipmentBundleName +
                "]. Available=[" +
                (available != null
                    ? string.Join(", ", available)
                    : "<bundle unavailable>") +
                "].");
        }

        private static bool PW_IsExpectedPrefabFile(
            string requested,
            string fileName)
        {
            if (string.IsNullOrEmpty(
                    requested) ||
                string.IsNullOrEmpty(
                    fileName))
            {
                return false;
            }

            return
                string.Equals(
                    PW_NormalizeName(requested),
                    PW_NormalizeName(fileName),
                    StringComparison.OrdinalIgnoreCase);
        }


        private static string PW_NormalizeName(
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

            if (result.StartsWith("fotf", StringComparison.Ordinal))
            {
                result = result.Substring(4);
            }

            return result;
        }


        // ============================================================
        // ATTACHMENTS
        // ============================================================

        private static bool PW_ResolveAttachment(
            Inventory inventory,
            Item item,
            Item.SlotType slotType,
            out Transform root,
            out Transform nativeVisual)
        {
            root = null;
            nativeVisual = null;

            if (inventory == null || item == null)
            {
                return false;
            }

            if (slotType == Item.SlotType.Primary)
            {
                // Bows do not reliably use the normal WeaponR hierarchy.
                // Erenshor can render them from a dedicated bow holder elsewhere
                // on the character rig.  Sporepiercer previously instantiated
                // beneath WeaponR while the real Honsus-bow template remained
                // active on that separate holder, so only the native bow was
                // ever visible.
                //
                // Resolve the exact EquipmentToActivate object across the whole
                // player rig first and attach our custom prefab beside it.  This
                // also gives us the correct active parent and native world pose.
                if (item.IsBow)
                {
                    nativeVisual =
                        PW_FindNativeBowVisual(
                            inventory,
                            item);

                    if (nativeVisual != null)
                    {
                        // Keep the custom prefab outside Erenshor's native bow
                        // holder. Native equipment refresh toggles those holders
                        // on/off, which made our child bow blink with them.
                        root = PW_GetCharacterSearchRoot(inventory);
                        return root != null;
                    }
                }

                if (inventory.Modulars == null)
                {
                    return false;
                }

                root = inventory.Modulars.WeaponR;
                nativeVisual = PW_FindNativeVisual(root, item, true);
                return root != null;
            }

            if (slotType == Item.SlotType.Secondary)
            {
                // The ItemIcon can restore before the Modulars component.
                // A missing modular reference is not a reason to leave an
                // equipped Sporebound scepter invisible until re-equipped.
                root = inventory.Modulars != null
                    ? inventory.Modulars.WeaponL
                    : null;
                Transform liveActor = PW_GetCharacterSearchRoot(inventory);
                if (root == null || liveActor == null || !root.IsChildOf(liveActor))
                {
                    // An equipped offhand can restore before the native
                    // Modulars.WeaponL reference. Resolve the live holder by
                    // name, then as a last resort use the humanoid left hand.
                    // Never change item state or unequip anything.
                    root = PW_FindRestoredOffhandSocket(inventory);
                }
                if (root == null) return false;
                nativeVisual = PW_FindNativeVisual(root, item, true);
                return true;
            }

            if (slotType == Item.SlotType.Neck)
            {
                nativeVisual =
                    PW_FindNativeVisual(
                        inventory.transform,
                        item,
                        false);

                if (nativeVisual != null)
                {
                    root =
                        nativeVisual.parent != null
                            ? nativeVisual.parent
                            : inventory.transform;
                    return true;
                }

                root = PW_FindBodyAnchor(inventory.transform);
                return root != null;
            }

            return false;
        }

        // A restore pass may have the ItemIcon but not yet the native WeaponL
        // object. A temporary left-hand anchor renders the existing equipment
        // without waiting for an unequip/re-equip; normal WeaponL takes over
        // automatically once the native modular rig reference appears.
        private static Transform PW_FindRestoredOffhandSocket(Inventory inventory)
        {
            Transform actor = PW_GetCharacterSearchRoot(inventory);
            if (actor == null || !actor.gameObject.activeInHierarchy) return null;
            Transform[] all = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];
                if (child != null &&
                    string.Equals(child.name, "WeaponL", StringComparison.OrdinalIgnoreCase) &&
                    !PW_IsCustom(child)) return child;
            }
            Animator[] animators = actor.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || !animator.isHuman) continue;
                try
                {
                    Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (hand != null && hand.IsChildOf(actor)) return hand;
                }
                catch (UnityException) { }
            }
            return null;
        }

        // Log transitions, not individual frames. This identifies whether the
        // missing offhand is due to inventory registration, socket restoration
        // or a render hierarchy hidden by the native equip refresh.
        private static string _pwLastSporeboundOffhandState;
        private static void PW_LogSecondaryRestoreState(
            PW_VisualState state, Item item, string reason, Transform socket)
        {
            if (state == null || state.SlotType != Item.SlotType.Secondary ||
                item == null || !string.Equals(item.Id, MoldermawEquipment.SporeboundScepterId,
                    StringComparison.OrdinalIgnoreCase)) return;
            // Only emit state changes, not the recurring attached/ready phases
            // of every 0.5-second poll. This makes a failed restoration clear.
            bool visible = PW_HasRenderableCustomVisual(state);
            string value = (socket == null ? "missing" : socket.name) +
                "/" + (visible ? "visible" : "invisible") +
                "/" + (state.SocketProxy != null ? state.SocketProxy.parent != null ?
                    state.SocketProxy.parent.name : "detached-proxy" : "native-socket") +
                (reason.StartsWith("no-", StringComparison.Ordinal) ? "/" + reason : "");
            if (string.Equals(_pwLastSporeboundOffhandState, value, StringComparison.Ordinal)) return;
            _pwLastSporeboundOffhandState = value;
            Bounds bounds = new Bounds();
            bool hasBounds = state.CustomVisual != null &&
                PW_TryGetRendererBounds(state.CustomVisual, out bounds);
            // If rendererActive stays true but the hand still appears empty,
            // these world-space bounds identify an off-screen pivot/scale
            // without changing the proven authored item transforms.
            string boundsInfo = hasBounds
                ? " rendererCenter=[" + PW_FormatVector(bounds.center) +
                  "] rendererSize=[" + PW_FormatVector(bounds.size) +
                  "] distanceFromHand=[" +
                  (socket != null ? Vector3.Distance(socket.position, bounds.center).ToString("0.###") : "n/a") + "]"
                : " rendererBounds=[none]";
            Plugin.NativeLog.LogInfo("[FOTFVisualRestore] Sporebound Secondary state=[" +
                value + "] handActive=[" + (socket != null && socket.gameObject.activeInHierarchy) +
                "] rendererActive=[" + visible + "]" + boundsInfo);
        }

        private static bool PW_IsFallbackLeftHandSocket(PW_VisualState state)
        {
            return state != null && state.SlotType == Item.SlotType.Secondary &&
                state.SocketProxy != null && state.AttachmentRoot != null &&
                !string.Equals(state.AttachmentRoot.name, "WeaponL", StringComparison.OrdinalIgnoreCase);
        }

        // The player may have a valid humanoid LeftHand bone but no active
        // Modulars.WeaponL until a fresh native equip. Never parent the custom
        // mesh to that bone: Erenshor disables it during equipment refreshes,
        // deactivating every descendant regardless of Renderer.enabled.
        // Follow its world pose from a sibling outside that toggle hierarchy.
        private static void PW_FollowFallbackLeftHand(
            Inventory inventory, PW_VisualState state)
        {
            if (inventory == null || !PW_IsFallbackLeftHandSocket(state)) return;
            Transform anchor = state.SocketProxy;
            Transform hand = state.AttachmentRoot;
            Transform actor = PW_GetCharacterSearchRoot(inventory);
            if (actor == null || !actor.gameObject.activeInHierarchy) return;
            Transform safeParent = hand.parent;
            while (safeParent != null && !safeParent.gameObject.activeInHierarchy)
                safeParent = safeParent.parent;
            if (safeParent == null || !safeParent.IsChildOf(actor)) safeParent = actor;
            if (anchor.parent != safeParent)
                anchor.SetParent(safeParent, false);

            anchor.position = hand.position;
            anchor.rotation = hand.rotation;

            // Restore our authored 100x import calibration in the same small
            // native-weapon coordinate space as the original WeaponL tuning.
            Transform right = inventory.Modulars != null ? inventory.Modulars.WeaponR : null;
            Vector3 reference = right != null && right.lossyScale.sqrMagnitude > 0.0000001f
                ? right.lossyScale : Vector3.one * 0.01f;
            Vector3 inherited = safeParent.lossyScale;
            anchor.localScale = new Vector3(
                VR_SafeScaleRatio(reference.x, inherited.x),
                VR_SafeScaleRatio(reference.y, inherited.y),
                VR_SafeScaleRatio(reference.z, inherited.z));
        }

        private static Transform PW_GetStableHandParent(
            Inventory inventory,
            Transform attachmentRoot,
            PW_VisualState state)
        {
            if (state == null || attachmentRoot == null) return null;
            if (state.SlotType == Item.SlotType.Secondary &&
                !string.Equals(attachmentRoot.name, "WeaponL", StringComparison.OrdinalIgnoreCase))
            {
                Transform actor = PW_GetCharacterSearchRoot(inventory);
                if (actor == null || !actor.gameObject.activeInHierarchy) return null;
                if (state.SocketProxy == null)
                {
                    GameObject proxy = new GameObject(
                        "FOTF_CustomItemVisual_LeftHandFallback_" + state.Label);
                    state.SocketProxy = proxy.transform;
                    Plugin.NativeLog.LogInfo(
                        "[FOTFVisualRestore] WeaponL unavailable; offhand proxy now follows Hand_L " +
                        "from outside the native hand activation hierarchy.");
                }
                // Even when Hand_L itself is inactive, its bone transform
                // still supplies a stable pose. Our proxy stays renderable.
                state.AttachmentRoot = attachmentRoot;
                PW_FollowFallbackLeftHand(inventory, state);
                return state.SocketProxy;
            }
            return VR_GetHandParent(
                attachmentRoot,
                PW_GetCharacterSearchRoot(inventory),
                ref state.SocketProxy,
                "Player_" + state.Label);
        }

        private static Transform PW_FindNativeVisualForSlot(
            Inventory inventory,
            Item item,
            Item.SlotType slotType)
        {
            if (inventory == null || item == null)
            {
                return null;
            }

            if (slotType == Item.SlotType.Primary)
            {
                if (item.IsBow)
                {
                    Transform bowVisual =
                        PW_FindNativeBowVisual(
                            inventory,
                            item);

                    if (bowVisual != null)
                    {
                        return bowVisual;
                    }
                }

                if (inventory.Modulars != null)
                {
                    return PW_FindNativeVisual(
                        inventory.Modulars.WeaponR,
                        item,
                        true);
                }
            }

            if (slotType == Item.SlotType.Secondary && inventory.Modulars != null)
            {
                return PW_FindNativeVisual(inventory.Modulars.WeaponL, item, true);
            }

            if (slotType == Item.SlotType.Neck)
            {
                return PW_FindNativeVisual(inventory.transform, item, false);
            }

            return null;
        }

        private static Transform PW_GetCharacterSearchRoot(
            Inventory inventory)
        {
            if (inventory == null ||
                inventory.transform == null)
            {
                return null;
            }

            Transform root =
                inventory.transform.root;

            return root != null
                ? root
                : inventory.transform;
        }

        private static Transform PW_FindNativeBowVisual(
            Inventory inventory,
            Item item)
        {
            if (inventory == null || item == null)
            {
                return null;
            }

            Transform searchRoot =
                PW_GetCharacterSearchRoot(
                    inventory);

            if (searchRoot == null)
            {
                return null;
            }

            // First prefer the exact inherited EquipmentToActivate object.
            Transform exact =
                PW_FindNativeVisual(
                    searchRoot,
                    item,
                    false);

            if (exact != null &&
                exact.gameObject.activeInHierarchy &&
                exact.GetComponentInChildren<Renderer>(true) != null)
            {
                return exact;
            }

            // Native bows are not always rendered from the exact
            // EquipmentToActivate transform.  Search the live rig for an
            // active renderer whose own name or an ancestor clearly belongs
            // to a bow.  This avoids binding the custom prefab to an inactive
            // template copy while a different native bow remains visible.
            Transform[] all =
                searchRoot.GetComponentsInChildren<Transform>(
                    true);

            Transform best = null;
            int bestScore = int.MinValue;

            string equipmentName =
                PW_NormalizeName(
                    item.EquipmentToActivate);

            for (int i = 0;
                 i < all.Length;
                 i++)
            {
                Transform candidate =
                    all[i];

                if (candidate == null ||
                    PW_IsCustom(candidate) ||
                    !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Renderer renderer =
                    candidate.GetComponentInChildren<Renderer>(
                        false);

                if (renderer == null)
                {
                    continue;
                }

                int score =
                    PW_ScoreBowTransform(
                        candidate,
                        searchRoot,
                        equipmentName);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (bestScore >= 40)
            {
                return best;
            }

            return exact;
        }

        private static int PW_ScoreBowTransform(
            Transform candidate,
            Transform searchRoot,
            string equipmentName)
        {
            if (candidate == null)
            {
                return int.MinValue;
            }

            int score = 0;
            Transform current = candidate;
            int depth = 0;

            while (current != null &&
                   depth < 8)
            {
                string normalized =
                    PW_NormalizeName(
                        current.name);

                if (!string.IsNullOrEmpty(equipmentName) &&
                    string.Equals(
                        normalized,
                        equipmentName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    score += 120;
                }
                else if (!string.IsNullOrEmpty(equipmentName) &&
                         normalized.Contains(equipmentName))
                {
                    score += 80;
                }

                if (normalized.Contains("honsu"))
                {
                    score += 70;
                }

                if (normalized.Contains("bow"))
                {
                    score += 50;
                }

                if (current == searchRoot)
                {
                    break;
                }

                current = current.parent;
                depth++;
            }

            return score;
        }



        private static Transform PW_FindNativeVisual(
            Transform root,
            Item item,
            bool allowActiveChildFallback)
        {
            if (root == null || item == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(item.EquipmentToActivate))
            {
                Transform exact =
                    PW_FindByName(
                        root,
                        item.EquipmentToActivate,
                        true);

                if (exact != null)
                {
                    return exact;
                }
            }

            if (!allowActiveChildFallback)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                if (child == null ||
                    !child.gameObject.activeInHierarchy ||
                    PW_IsCustom(child) ||
                    string.Equals(
                        child.name,
                        "DEFAULT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return child;
            }

            return null;
        }

        private static Transform PW_FindBodyAnchor(
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
                Transform found = PW_FindByName(root, names[i], false);

                if (found != null)
                {
                    return found;
                }
            }

            return root;
        }

        private static Transform PW_FindByName(
            Transform root,
            string name,
            bool preferActive)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Transform fallback = null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];

                if (candidate == null ||
                    PW_IsCustom(candidate) ||
                    !string.Equals(
                        candidate.name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!preferActive || candidate.gameObject.activeInHierarchy)
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


        // ============================================================
        // ALIGNMENT
        // ============================================================

        private static void PW_ResolveAlignment(
            Inventory inventory,
            Item item,
            PW_VisualState state)
        {
            if (inventory == null ||
                item == null ||
                state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            if (state.FollowWorld &&
                state.FollowTransform != null)
            {
                // The FOTF bow is authored at the same import convention as
                // the other custom weapons. Live calibration must therefore be
                // literal: Pos/Rot/Scale are applied relative to the frozen
                // native bow transform, with no renderer-bounds multiplier.
                state.AlignmentPosition = Vector3.zero;
                state.AlignmentRotation = Quaternion.identity;
                state.AlignmentScale = Vector3.one;

                PW_ApplyFineTuning(
                    item,
                    state);

                state.HasAlignment = true;
                return;
            }

            if (state.SlotType == Item.SlotType.Neck)
            {
                if (state.NativeVisual == null)
                {
                    PW_AlignBloomFallback(
                        inventory,
                        state);

                    state.AlignmentPosition =
                        state.CustomVisual.transform.localPosition;

                    state.AlignmentRotation =
                        state.CustomVisual.transform.localRotation;

                    state.AlignmentScale =
                        state.CustomVisual.transform.localScale;

                    state.HasAlignment = true;
                    return;
                }
            }

            Transform custom =
                state.CustomVisual.transform;

            Transform native =
                state.NativeVisual;

            if (native == null)
            {
                state.AlignmentPosition = Vector3.zero;
                state.AlignmentRotation = Quaternion.identity;
                state.AlignmentScale = Vector3.one;
                PW_ApplyFineTuning(item, state);
                state.HasAlignment = true;
                return;
            }

            // Important: state.Parent for hand items is the stable WeaponR /
            // WeaponL root, while the native mesh may be nested several
            // transforms below it. Copying native.localPosition/localRotation
            // therefore gives the wrong pose. Copy the native WORLD pose and
            // let Unity convert that pose into our stable parent's local space.
            custom.localScale = Vector3.one;
            custom.position = native.position;
            custom.rotation = native.rotation;

            // First normalize the imported prefab against the native visual,
            // then apply the item's authored tuning below. The authored values
            // were calibrated on top of this normalization; bypassing it made
            // multiple prefabs microscopic/invisible in r21.
            Bounds nativeBounds;
            Bounds customBounds;

            bool haveNativeBounds =
                PW_TryGetRendererBounds(
                    native.gameObject,
                    out nativeBounds);

            bool haveCustomBounds =
                PW_TryGetRendererBounds(
                    state.CustomVisual,
                    out customBounds);

            float factor = 1.0f;

            if (haveNativeBounds &&
                haveCustomBounds)
            {
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

                if (nativeSize > 0.0001f &&
                    customSize > 0.0001f)
                {
                    factor =
                        Mathf.Clamp(
                            nativeSize / customSize,
                            0.01f,
                            100.0f);
                }
            }

            custom.localScale =
                new Vector3(
                    factor,
                    factor,
                    factor);

            state.AlignmentPosition =
                custom.localPosition;

            state.AlignmentRotation =
                custom.localRotation;

            state.AlignmentScale =
                custom.localScale;

            PW_ApplyFineTuning(
                item,
                state);

            state.HasAlignment = true;
        }


        private static void PW_ApplyFineTuning(
            Item item,
            PW_VisualState state)
        {
            if (item == null ||
                state == null)
            {
                return;
            }

            PW_TransformTuning tuning =
                PW_GetTuning(
                    item.Id,
                    state.SlotType);

            if (tuning == null)
            {
                return;
            }

            state.AlignmentPosition +=
                tuning.Position;

            state.AlignmentRotation =
                state.AlignmentRotation *
                Quaternion.Euler(
                    tuning.RotationEuler);

            state.AlignmentScale *=
                tuning.Scale;

            PW_ApplyAuthoredGripCorrection(item, state);
        }


        /// <summary>
        /// Several late-boss prefabs were authored around their geometric
        /// centre instead of the point a hand should actually grip.  Align a
        /// named authored grip component to the inherited native hand socket
        /// after scale/rotation have been calculated.  This is more stable
        /// than guessing large positional offsets in parent space.
        /// </summary>
        private static void PW_ApplyAuthoredGripCorrection(
            Item item,
            PW_VisualState state)
        {
            if (item == null || state == null || state.CustomVisual == null)
                return;

            string gripName = null;

            if (string.Equals(
                    item.Id,
                    VarkeshEquipment.SoulbinderGrimoireId,
                    StringComparison.OrdinalIgnoreCase))
            {
                gripName = "Spine";
            }
            else if (string.Equals(
                         item.Id,
                         VarkeshEquipment.LanternOfDepartedSoulsId,
                         StringComparison.OrdinalIgnoreCase))
            {
                // New lantern exports contain an explicit RingGrip marker at
                // the centre of the top ring. Older prefabs can still use the
                // torus object's own Handle origin as a compatible fallback.
                gripName = "RingGrip";
            }
            else if (string.Equals(
                         item.Id,
                         GorathEquipment.InfernalBastionId,
                         StringComparison.OrdinalIgnoreCase))
            {
                gripName = "Grip";
            }

            if (string.IsNullOrEmpty(gripName))
                return;

            Transform root = state.CustomVisual.transform;
            Transform grip = PW_FindNamedDescendant(root, gripName);

            if (grip == null &&
                string.Equals(
                    item.Id,
                    VarkeshEquipment.LanternOfDepartedSoulsId,
                    StringComparison.OrdinalIgnoreCase))
            {
                grip = PW_FindNamedDescendant(root, "Handle");
            }

            if (grip == null)
                return;

            Vector3 localGrip = root.InverseTransformPoint(grip.position);
            Vector3 scaledGrip = Vector3.Scale(localGrip, state.AlignmentScale);
            Vector3 parentOffset = state.AlignmentRotation * scaledGrip;

            state.AlignmentPosition -= parentOffset;
        }


        private static Transform PW_FindNamedDescendant(
            Transform root,
            string wantedName)
        {
            if (root == null || string.IsNullOrEmpty(wantedName))
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; transforms != null && i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    string.Equals(
                        candidate.name,
                        wantedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }


        private static PW_TransformTuning PW_GetTuning(
            string itemId,
            Item.SlotType slotType)
        {
            if (string.Equals(
                    itemId,
                    MoldermawEquipment.SporeboundScepterId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    itemId,
                    NhalSeraEquipment.LightlessScepterId,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Both scepters share the same grip/rotation/scale, but the
                // left-hand socket needs the mirrored Z offset confirmed live.
                return
                    slotType == Item.SlotType.Secondary
                        ? PW_ScepterSecondaryTuning
                        : PW_ScepterPrimaryTuning;
            }

            if (string.Equals(
                    itemId,
                    FallenScepter.ItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PW_FallenScepterTuning;
            }

            if (string.Equals(
                    itemId,
                    MoldermawEquipment.RotcleaverId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PW_RotcleaverTuning;
            }

            if (string.Equals(
                    itemId,
                    MoldermawEquipment.BulwarkId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PW_BulwarkTuning;
            }

            if (string.Equals(
                    itemId,
                    MoldermawEquipment.SporepiercerId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    itemId,
                    GorathEquipment.StormfireId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PW_SporepiercerTuning;
            }

            if (string.Equals(
                    itemId,
                    VarkeshEquipment.GravetideReaverId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return slotType == Item.SlotType.Secondary
                    ? PW_GravetideSecondaryTuning
                    : PW_GravetidePrimaryTuning;
            }

            if (string.Equals(
                    itemId,
                    VarkeshEquipment.SoulbinderGrimoireId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PW_GrimoireTuning;
            }

            if (string.Equals(
                    itemId,
                    VarkeshEquipment.LanternOfDepartedSoulsId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PW_LanternTuning;
            }

            if (string.Equals(
                    itemId,
                    GorathEquipment.EmberOfTheDemonKingId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return slotType == Item.SlotType.Secondary
                    ? PW_ScepterSecondaryTuning
                    : PW_ScepterPrimaryTuning;
            }

            if (string.Equals(
                    itemId,
                    GorathEquipment.HellforgedTalonId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return slotType == Item.SlotType.Secondary
                    ? PW_HellforgedSecondaryTuning
                    : PW_HellforgedPrimaryTuning;
            }

            if (string.Equals(
                    itemId,
                    GorathEquipment.InfernalBastionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PW_InfernalBastionTuning;
            }

            return null;
        }


        internal bool PW_TryTuneEquippedVisual(
            string slotToken,
            string operation,
            string[] values,
            out string message)
        {
            message = null;

            Inventory inventory =
                GameData.PlayerInv;

            if (inventory == null)
            {
                message = "Player inventory is not available.";
                return false;
            }

            PW_VisualState state =
                PW_ResolveTuneState(
                    slotToken);

            if (state == null)
            {
                message =
                    "Use primary or secondary, e.g. /fotfvisual primary status.";
                return false;
            }

            Item item =
                PW_GetEquippedItem(
                    inventory,
                    state.SlotType);

            PW_TransformTuning tuning =
                item != null
                    ? PW_GetTuning(item.Id, state.SlotType)
                    : null;

            if (item == null ||
                tuning == null)
            {
                message =
                    "No tunable FOTF item is equipped in " +
                    state.Label +
                    ".";
                return false;
            }

            string op =
                string.IsNullOrWhiteSpace(operation)
                    ? "status"
                    : operation.Trim().ToLowerInvariant();

            if (op == "reset")
            {
                tuning.Reset();
            }
            else if (op == "pos" || op == "move")
            {
                Vector3 value;

                if (!PW_TryParseVector(values, out value))
                {
                    message =
                        "Position requires three numbers: x y z.";
                    return false;
                }

                if (op == "move")
                {
                    tuning.Position += value;
                }
                else
                {
                    tuning.Position = value;
                }
            }
            else if (op == "rot" || op == "turn")
            {
                Vector3 value;

                if (!PW_TryParseVector(values, out value))
                {
                    message =
                        "Rotation requires three numbers: x y z.";
                    return false;
                }

                if (op == "turn")
                {
                    tuning.RotationEuler += value;
                }
                else
                {
                    tuning.RotationEuler = value;
                }
            }
            else if (op == "scale")
            {
                float value;

                if (values == null ||
                    values.Length < 1 ||
                    !float.TryParse(
                        values[0],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out value) ||
                    value <= 0f)
                {
                    message =
                        "Scale requires one positive number.";
                    return false;
                }

                tuning.Scale = value;
            }
            else if (op != "status")
            {
                message =
                    "Usage: /fotfvisual [primary|secondary] status|pos|move|rot|turn|scale|reset";
                return false;
            }

            if (op != "status")
            {
                // If native equipment refresh happened between command entry
                // and this frame, rebuild the visual first instead of rejecting
                // live tuning. This is especially important for bows.
                if (state.CustomVisual == null)
                {
                    PW_RefreshSlot(inventory, state);
                }

                if (state.CustomVisual != null)
                {
                    PW_ResolveAlignment(
                        inventory,
                        item,
                        state);

                    PW_ApplyStoredAlignment(
                        state);

                    PW_EnsureCustomVisible(
                        state.CustomVisual);

                    PW_HideOtherNativeHandVisuals(
                        state,
                        item);
                }
            }

            message =
                PW_BuildTuningStatus(
                    item,
                    state,
                    tuning);

            return true;
        }


        private PW_VisualState PW_ResolveTuneState(
            string slotToken)
        {
            if (!string.IsNullOrWhiteSpace(slotToken))
            {
                if (string.Equals(
                        slotToken,
                        "primary",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        slotToken,
                        "p",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return _pwPrimary;
                }

                if (string.Equals(
                        slotToken,
                        "secondary",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        slotToken,
                        "s",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return _pwSecondary;
                }
            }

            if (_pwPrimary.CustomVisual != null &&
                PW_GetTuning(_pwPrimary.ItemId, Item.SlotType.Primary) != null)
            {
                return _pwPrimary;
            }

            if (_pwSecondary.CustomVisual != null &&
                PW_GetTuning(_pwSecondary.ItemId, Item.SlotType.Secondary) != null)
            {
                return _pwSecondary;
            }

            return null;
        }


        private static bool PW_TryParseVector(
            string[] values,
            out Vector3 result)
        {
            result = Vector3.zero;

            if (values == null ||
                values.Length < 3)
            {
                return false;
            }

            float x;
            float y;
            float z;

            if (!float.TryParse(
                    values[0],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out x) ||
                !float.TryParse(
                    values[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out y) ||
                !float.TryParse(
                    values[2],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out z))
            {
                return false;
            }

            result = new Vector3(x, y, z);
            return true;
        }


        private static string PW_BuildTuningStatus(
            Item item,
            PW_VisualState state,
            PW_TransformTuning tuning)
        {
            return
                "FOTF visual " +
                state.Label +
                " [" +
                item.ItemName +
                "] Pos=" +
                PW_FormatVector(tuning.Position) +
                " Rot=" +
                PW_FormatVector(tuning.RotationEuler) +
                " Scale=" +
                tuning.Scale.ToString(
                    "0.###",
                    System.Globalization.CultureInfo.InvariantCulture);
        }


        private static string PW_FormatVector(
            Vector3 value)
        {
            return
                "(" +
                value.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ", " +
                value.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ", " +
                value.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ")";
        }


        private static void PW_ApplyStoredAlignment(
            PW_VisualState state)
        {
            if (state == null ||
                state.CustomVisual == null ||
                !state.HasAlignment)
            {
                return;
            }

            if (PW_IsLantern(state.ItemId))
            {
                PW_ApplyLanternHang(state);
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
                        state.AlignmentPosition);

                custom.rotation =
                    follow.rotation *
                    state.AlignmentRotation;

                custom.localScale =
                    state.AlignmentScale;

                return;
            }

            custom.localPosition =
                state.AlignmentPosition;

            custom.localRotation =
                state.AlignmentRotation;

            custom.localScale =
                state.AlignmentScale;
        }


        private static bool PW_IsLantern(
            string itemId)
        {
            return
                !string.IsNullOrEmpty(itemId) &&
                string.Equals(
                    itemId,
                    VarkeshEquipment.LanternOfDepartedSoulsId,
                    StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Lantern of Departed Souls is a hanging object rather than a rigid
        /// weapon pose. Its authored +Y axis is vertical after Blender -> Unity
        /// conversion and the top ring is the hand contact point.
        ///
        /// Keep the body world-upright, face it with the actor, then translate
        /// the prefab so RingGrip/Handle lands exactly on the off-hand socket.
        /// </summary>
        private static void PW_ApplyLanternHang(
            PW_VisualState state)
        {
            if (state == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Transform target =
                state.AttachmentRoot != null
                    ? state.AttachmentRoot
                    : state.Parent;

            if (target == null)
            {
                return;
            }

            Transform custom =
                state.CustomVisual.transform;

            custom.localScale =
                state.AlignmentScale;

            Vector3 forward =
                GameData.PlayerControl != null
                    ? GameData.PlayerControl.transform.forward
                    : target.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            // The authored lantern is vertical along its local +Z axis.
            // Quaternion.LookRotation would map that +Z axis into world-forward,
            // which is exactly why the lantern appeared sideways. Rotate the
            // prefab -90 degrees around local X after facing it so local +Z
            // becomes world-up; RingGrip then sits in the hand and the body
            // hangs directly below it.
            custom.rotation =
                Quaternion.LookRotation(
                    forward.normalized,
                    Vector3.up) *
                Quaternion.Euler(
                    -90f,
                    0f,
                    0f);

            custom.position =
                target.position;

            Transform grip =
                PW_FindNamedDescendant(
                    custom,
                    "RingGrip");

            if (grip == null)
            {
                grip =
                    PW_FindNamedDescendant(
                        custom,
                        "Handle");
            }

            if (grip != null)
            {
                Vector3 correction =
                    target.position -
                    grip.position;

                custom.position +=
                    correction;
            }

            // The ring center was landing inside the palm. The actual hanging
            // point is below the closed hand, never at its centre. Do this
            // AFTER RingGrip alignment so older Handle-only bundles work too.
            custom.position += Vector3.down * PW_LanternHandClearance;
        }


        private static void PW_AlignBloomFallback(
            Inventory inventory,
            PW_VisualState state)
        {
            Transform actor =
                GameData.PlayerControl != null
                    ? GameData.PlayerControl.transform
                    : inventory.transform;

            Transform custom = state.CustomVisual.transform;

            // Preserve scale/child offsets authored into the Unity prefab.
            custom.rotation = actor.rotation;
            custom.position =
                state.Parent.position +
                actor.forward * PW_BloomForwardOffset +
                Vector3.down * PW_BloomDownOffset;
        }


        private static bool PW_TryGetRendererBounds(
            GameObject root,
            out Bounds bounds)
        {
            bounds =
                new Bounds();

            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            if (renderers == null ||
                renderers.Length == 0)
            {
                return false;
            }

            bool found =
                false;

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
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

            return found;
        }


        // ============================================================
        // VISIBILITY / SAFETY
        // ============================================================

        private static void PW_HideNative(
            PW_VisualState state,
            Transform native)
        {
            if (state == null ||
                native == null ||
                PW_IsCustom(native) ||
                state.CustomVisual == null ||
                state.CustomVisual.transform == native ||
                state.CustomVisual.transform.IsChildOf(native))
            {
                // The native equipment hierarchy sometimes chooses the holder
                // itself as its fallback visual. Hiding an ancestor of our
                // attached model hides the Sporebound scepter as well.
                return;
            }

            if (!PW_HasRenderableCustomVisual(state))
            {
                return;
            }

            for (int i = 0;
                 i < state.Hidden.Count;
                 i++)
            {
                PW_HiddenVisual existing =
                    state.Hidden[i];

                if (existing != null &&
                    existing.Transform ==
                        native)
                {
                    existing.Hide();
                    return;
                }
            }

            PW_HiddenVisual hidden =
                new PW_HiddenVisual(
                    native);

            hidden.Hide();

            state.Hidden.Add(
                hidden);
        }

        private static void PW_HideOtherNativeHandVisuals(
            PW_VisualState state,
            Item item)
        {
            if (state == null ||
                item == null ||
                state.AttachmentRoot == null ||
                state.CustomVisual == null ||
                (state.SlotType != Item.SlotType.Primary &&
                 state.SlotType != Item.SlotType.Secondary))
            {
                return;
            }

            // For bows the attachment root is the dedicated native bow holder.
            // We already suppress the exact EquipmentToActivate transform via
            // PW_HideNative().  Do not blanket-hide the holder's siblings because
            // that hierarchy can also contain arrow/hand animation objects.
            if (item.IsBow)
            {
                return;
            }

            Transform root =
                state.AttachmentRoot;

            // Hand_L is a humanoid bone, not a WeaponL equipment holder.
            // Iterating and hiding its renderable children risks deactivating
            // pieces of the skeleton/native weapon hierarchy (and therefore
            // the visual we are trying to restore). With the fallback proxy,
            // suppress only the EXACT native visual via PW_HideNative.
            if ((state.SlotType == Item.SlotType.Secondary &&
                 !string.Equals(root.name, "WeaponL", StringComparison.OrdinalIgnoreCase)) ||
                (state.SlotType == Item.SlotType.Primary &&
                 !string.Equals(root.name, "WeaponR", StringComparison.OrdinalIgnoreCase)))
                return;

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform child =
                    root.GetChild(i);

                if (child == null ||
                    child == state.CustomVisual.transform ||
                    PW_IsCustom(child) ||
                    !child.gameObject.activeInHierarchy ||
                    string.Equals(
                        child.name,
                        "DEFAULT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Renderer renderer =
                    child.GetComponentInChildren<Renderer>(
                        true);

                if (renderer == null)
                {
                    continue;
                }

                PW_HideNative(
                    state,
                    child);
            }
        }


        private static void PW_HideNativeBowRenderers(
            PW_VisualState state,
            Transform actorRoot,
            Item item)
        {
            if (state == null ||
                actorRoot == null ||
                item == null ||
                state.CustomVisual == null)
            {
                return;
            }

            Renderer[] renderers =
                actorRoot.GetComponentsInChildren<Renderer>(
                    true);

            string equipmentName =
                PW_NormalizeName(
                    item.EquipmentToActivate);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    PW_IsWithinCustomVisual(
                        renderer.transform,
                        state.CustomVisual.transform) ||
                    !PW_LooksLikeNativeBowRenderer(
                        renderer.transform,
                        actorRoot,
                        equipmentName))
                {
                    continue;
                }

                PW_HiddenRenderer hidden =
                    null;

                for (int h = 0;
                     h < state.HiddenRenderers.Count;
                     h++)
                {
                    PW_HiddenRenderer existing =
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
                        new PW_HiddenRenderer(
                            renderer);

                    state.HiddenRenderers.Add(
                        hidden);
                }

                hidden.Hide();
            }
        }

        private static bool PW_LooksLikeNativeBowRenderer(
            Transform transform,
            Transform actorRoot,
            string equipmentName)
        {
            Transform current = transform;
            int depth = 0;

            while (current != null &&
                   depth < 8)
            {
                string normalized =
                    PW_NormalizeName(
                        current.name);

                if (normalized.Contains("bow") ||
                    normalized.Contains("honsu") ||
                    (!string.IsNullOrEmpty(equipmentName) &&
                     (string.Equals(
                          normalized,
                          equipmentName,
                          StringComparison.OrdinalIgnoreCase) ||
                      normalized.Contains(
                          equipmentName))))
                {
                    return true;
                }

                if (current == actorRoot)
                {
                    break;
                }

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static bool PW_IsWithinCustomVisual(
            Transform transform,
            Transform customRoot)
        {
            Transform current = transform;

            while (current != null)
            {
                if (current == customRoot ||
                    PW_IsCustom(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void PW_CacheVisualComponents(
            PW_VisualState state)
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

        private static void PW_EnsureCustomVisible(
            PW_VisualState state)
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
            if (hierarchy != null)
            {
                for (int i = 0; i < hierarchy.Length; i++)
                {
                    Transform child = hierarchy[i];
                    if (child != null && !child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(true);
                    }
                }
            }

            Renderer[] renderers = state.CachedRenderers;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && !renderers[i].enabled)
                    {
                        renderers[i].enabled = true;
                    }
                }
            }
        }

        private static bool PW_HasRenderableCustomVisual(
            PW_VisualState state)
        {
            if (state == null || state.CustomVisual == null)
            {
                return false;
            }

            Renderer[] renderers = state.CachedRenderers;
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private static void PW_PrepareBowRendering(
            PW_VisualState state)
        {
            if (state == null || state.CustomVisual == null)
            {
                return;
            }

            PW_EnsureCustomVisible(state);

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

        private static void PW_ReassertHiddenNative(
            PW_VisualState state)
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < state.Hidden.Count; i++)
            {
                if (state.Hidden[i] != null)
                {
                    state.Hidden[i].Hide();
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

        private static void PW_ReconcileNativeVisibility(
            Inventory inventory,
            Item item,
            PW_VisualState state)
        {
            if (inventory == null || item == null || state == null || state.CustomVisual == null)
            {
                return;
            }

            Transform native =
                PW_FindNativeVisualForSlot(inventory, item, state.SlotType);

            if (native != null)
            {
                state.NativeVisual = native;
                PW_HideNative(state, native);
            }

            PW_HideOtherNativeHandVisuals(state, item);

            if (item.IsBow)
            {
                PW_HideNativeBowRenderers(
                    state,
                    PW_GetCharacterSearchRoot(inventory),
                    item);
            }
        }

        private static void PW_PrepareBowRendering_Legacy(
            GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            PW_EnsureCustomVisible(visual);

            LODGroup[] lodGroups =
                visual.GetComponentsInChildren<LODGroup>(
                    true);

            for (int i = 0;
                 i < lodGroups.Length;
                 i++)
            {
                if (lodGroups[i] != null)
                {
                    lodGroups[i].ForceLOD(0);
                }
            }

            SkinnedMeshRenderer[] skinned =
                visual.GetComponentsInChildren<SkinnedMeshRenderer>(
                    true);

            for (int i = 0;
                 i < skinned.Length;
                 i++)
            {
                if (skinned[i] != null)
                {
                    skinned[i].enabled = true;
                    skinned[i].updateWhenOffscreen = true;
                }
            }

            // Re-run shader repair after the complete hierarchy has been
            // activated.  Imported child renderers can lazily materialize
            // their materials only after activation.
            PW_RepairUnsupportedShaders(visual);
        }

        private static void PW_LogBowRendererPayload(
            GameObject visual,
            Item item)
        {
            if (visual == null ||
                item == null)
            {
                return;
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(
                    true);

            List<string> payload =
                new List<string>();

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                string meshInfo =
                    renderer.GetType().Name;

                MeshRenderer meshRenderer =
                    renderer as MeshRenderer;

                if (meshRenderer != null)
                {
                    MeshFilter filter =
                        meshRenderer.GetComponent<MeshFilter>();

                    if (filter != null &&
                        filter.sharedMesh != null)
                    {
                        meshInfo +=
                            ":" +
                            filter.sharedMesh.name +
                            "(" +
                            filter.sharedMesh.vertexCount +
                            ")";
                    }
                }

                SkinnedMeshRenderer skinned =
                    renderer as SkinnedMeshRenderer;

                if (skinned != null &&
                    skinned.sharedMesh != null)
                {
                    meshInfo +=
                        ":" +
                        skinned.sharedMesh.name +
                        "(" +
                        skinned.sharedMesh.vertexCount +
                        ")";
                }

                Material material =
                    renderer.sharedMaterial;

                string shader =
                    material != null &&
                    material.shader != null
                        ? material.shader.name
                        : "<none>";

                payload.Add(
                    renderer.name +
                    "=" +
                    meshInfo +
                    "/" +
                    shader);
            }

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] bow payload Item=[" +
                item.ItemName +
                "] Renderers=[" +
                string.Join(", ", payload.ToArray()) +
                "].");
        }

        private static void PW_EnsureCustomVisible(
            GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            if (!visual.activeSelf)
            {
                visual.SetActive(
                    true);
            }

            // A Renderer on an inactive child GameObject remains invisible
            // even when renderer.enabled is true. Reactivate the complete
            // imported prefab hierarchy first.
            Transform[] hierarchy =
                visual.GetComponentsInChildren<Transform>(
                    true);

            if (hierarchy != null)
            {
                for (int i = 0;
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
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(
                    true);

            if (renderers != null)
            {
                for (int i = 0;
                     i < renderers.Length;
                     i++)
                {
                    Renderer renderer =
                        renderers[i];

                    if (renderer != null &&
                        !renderer.enabled)
                    {
                        renderer.enabled =
                            true;
                    }
                }
            }
        }


        private static bool PW_HasRenderableCustomVisual(
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
                Renderer renderer =
                    renderers[i];

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

                    continue;
                }

                SkinnedMeshRenderer skinned =
                    renderer as SkinnedMeshRenderer;

                if (skinned != null)
                {
                    if (skinned.sharedMesh != null &&
                        skinned.sharedMesh.vertexCount > 0)
                    {
                        return true;
                    }

                    continue;
                }

                // Particle/trail/line/etc. renderer payload is still a valid
                // visible prefab component even if its bounds are transient.
                return true;
            }

            return false;
        }


        private static void PW_RestoreHiddenNativeVisuals(
            PW_VisualState state)
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0;
                 i < state.Hidden.Count;
                 i++)
            {
                PW_HiddenVisual hidden =
                    state.Hidden[i];

                if (hidden != null)
                {
                    hidden.Restore();
                }
            }

            state.Hidden.Clear();

            for (int i = 0;
                 i < state.HiddenRenderers.Count;
                 i++)
            {
                PW_HiddenRenderer hidden =
                    state.HiddenRenderers[i];

                if (hidden != null)
                {
                    hidden.Restore();
                }
            }

            state.HiddenRenderers.Clear();
        }

        private static void PW_DisablePhysics(
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

        private static void PW_RepairUnsupportedShaders(
            GameObject visual)
        {
            Shader standard = Shader.Find("Standard");

            if (standard == null || !standard.isSupported)
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
                        (material.shader == null || !material.shader.isSupported))
                    {
                        material.shader = standard;
                    }
                }
            }
        }


        // ============================================================
        // BOUNDS
        // ============================================================

        private static bool PW_TryBounds(
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
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static float PW_MaxAxis(Vector3 size)
        {
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }


        // ============================================================
        // ITEM MAPPING
        // ============================================================

        private static Item PW_GetEquippedItem(
            Inventory inventory,
            Item.SlotType slotType)
        {
            if (inventory == null || inventory.EquipmentSlots == null)
            {
                return null;
            }

            for (int i = 0; i < inventory.EquipmentSlots.Count; i++)
            {
                ItemIcon slot = inventory.EquipmentSlots[i];

                if (slot != null && slot.ThisSlotType == slotType)
                {
                    return slot.MyItem;
                }
            }

            return null;
        }

        private static string PW_GetPrefabForItem(
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
                // IMPORTANT: the original Moldermaw controller is now the
                // single owner of custom scepter visuals.  This prevents the
                // two equipment controllers from hiding/reparenting each
                // other's Corruptor-based weapon replacements.
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

        private static bool PW_IsCustom(Transform transform)
        {
            return transform != null &&
                   transform.name.StartsWith(
                       PW_CustomPrefix,
                       StringComparison.Ordinal);
        }


        // ============================================================
        // REMOVE / RESTORE
        // ============================================================

        private static void PW_Remove(
            PW_VisualState state,
            bool restoreNative)
        {
            if (state == null)
            {
                return;
            }

            if (state.CustomVisual != null)
            {
                UnityEngine.Object.Destroy(state.CustomVisual);
                state.CustomVisual = null;
            }

            VR_DestroyProxy(ref state.SocketProxy);
            state.OwnerInventory = null;
            state.CachedHierarchy = null;
            state.CachedRenderers = null;
            state.CachedLodGroups = null;
            state.CachedSkinnedRenderers = null;

            if (restoreNative)
            {
                PW_RestoreHiddenNativeVisuals(
                    state);
            }
            else
            {
                // On a normal unequip/swap, Erenshor chooses the next native
                // model itself. Re-enabling the old template here can create
                // the one-frame Buckler/axe ghost that the Fallen Scepter
                // controller already learned to avoid.
                state.Hidden.Clear();

                // Renderer-level bow suppression must always be restored on
                // a normal swap/unequip.  Native equipment refresh will then
                // decide which new bow renderer should be visible.
                for (int i = 0;
                     i < state.HiddenRenderers.Count;
                     i++)
                {
                    PW_HiddenRenderer hidden =
                        state.HiddenRenderers[i];

                    if (hidden != null)
                    {
                        hidden.Restore();
                    }
                }

                state.HiddenRenderers.Clear();
            }

            state.ItemId = null;
            state.PrefabName = null;
            state.Parent = null;
            state.AttachmentRoot = null;
            state.NativeVisual = null;
            state.FollowTransform = null;
            state.FollowWorld = false;
            state.HasAlignment = false;
            state.AlignmentPosition = Vector3.zero;
            state.AlignmentRotation = Quaternion.identity;
            state.AlignmentScale = Vector3.one;
        }


        private sealed class PW_VisualState
        {
            internal PW_VisualState(
                Item.SlotType slotType,
                string label)
            {
                SlotType = slotType;
                Label = label;
                Hidden = new List<PW_HiddenVisual>();
                HiddenRenderers = new List<PW_HiddenRenderer>();
            }

            internal Item.SlotType SlotType;
            internal string Label;
            internal string ItemId;
            internal string PrefabName;
            internal Inventory OwnerInventory;
            internal Transform SocketProxy;
            internal Transform Parent;
            internal Transform AttachmentRoot;
            internal Transform NativeVisual;
            internal Transform FollowTransform;
            internal bool FollowWorld;
            internal GameObject CustomVisual;
            internal Transform[] CachedHierarchy;
            internal Renderer[] CachedRenderers;
            internal LODGroup[] CachedLodGroups;
            internal SkinnedMeshRenderer[] CachedSkinnedRenderers;
            internal List<PW_HiddenVisual> Hidden;
            internal List<PW_HiddenRenderer> HiddenRenderers;
            internal bool HasAlignment;
            internal Vector3 AlignmentPosition;
            internal Quaternion AlignmentRotation = Quaternion.identity;
            internal Vector3 AlignmentScale = Vector3.one;
        }

        private sealed class PW_HiddenRenderer
        {
            internal PW_HiddenRenderer(
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
                    Renderer.enabled =
                        WasEnabled;
                }
            }

            internal Renderer Renderer;
            internal bool WasEnabled;
        }

        private sealed class PW_HiddenVisual
        {
            internal PW_HiddenVisual(
                Transform transform)
            {
                Transform =
                    transform;

                WasActive =
                    transform != null &&
                    transform.gameObject != null &&
                    transform.gameObject.activeSelf;
            }

            internal void Hide()
            {
                if (Transform == null ||
                    Transform.gameObject == null)
                {
                    return;
                }

                // Hide the complete native template object instead of only
                // renderer.enabled. Erenshor's equipment refresh re-enables
                // native renderers, which is why the Buckler kept coming
                // back. SetActive(false) is the proven Fallen Scepter path.
                if (Transform.gameObject.activeSelf)
                {
                    Transform.gameObject.SetActive(
                        false);
                }
            }

            internal void Restore()
            {
                if (Transform == null ||
                    Transform.gameObject == null)
                {
                    return;
                }

                Transform.gameObject.SetActive(
                    WasActive);
            }

            internal Transform Transform;
            internal bool WasActive;
        }
    }
}
