using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FateOfTheFallen
{
    /// <summary>
    /// Runtime recoloring for native Eventide armour.
    ///
    /// We deliberately reference Erenshor's own already-equipped native
    /// Eventide renderers rather than redistributing or re-rigging those assets.
    /// Material and texture copies are created only at runtime for the actor
    /// currently wearing the FOTF item.
    ///
    /// The palette system is intentionally reusable for the four Fallen themes:
    ///   Blight  = toxic green
    ///   Void    = violet/purple
    ///   Undeath = cold spectral white
    ///   Demonic = molten orange/red
    ///
    /// Carapace of the Event Horizon currently uses the Void palette.
    /// </summary>
    internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        private const float EV_RefreshInterval = 0.50f;

        private enum EV_Theme
        {
            Blight,
            Void,
            Undeath,
            Demonic
        }

        private sealed class EV_Palette
        {
            internal string Name;
            internal Color DarkMetal;
            internal Color LightMetal;
            internal Color AccentDark;
            internal Color AccentBright;
            internal Color Emission;
            internal float EmissionStrength;
        }

        private sealed class EV_TexturePair
        {
            internal Texture2D BaseTexture;
            internal Texture2D EmissionMask;
            internal float AccentCoverage;
        }

        private sealed class EV_RendererSnapshot
        {
            internal Renderer Renderer;
            internal Material[] OriginalMaterials;
            internal Material[] VariantMaterials;

            // Erenshor applies Item colour channels through renderer property
            // state. A MaterialPropertyBlock can override values on the
            // material object itself, which is why reasserting Material.SetColor
            // did not bring the chest back once the old shoulder Item palette
            // was restored.
            internal MaterialPropertyBlock OriginalPropertyBlock;
            internal MaterialPropertyBlock[] OriginalIndexedPropertyBlocks;
            internal MaterialPropertyBlock WorkingPropertyBlock;
            internal MaterialPropertyBlock[] WorkingIndexedPropertyBlocks;
            internal bool WorkingPropertyBlocksPrepared;

            internal string DebugLabel;
        }

        private sealed class EV_ShoulderSnapshot
        {
            internal Renderer Renderer;
            internal MaterialPropertyBlock OriginalGlobal;
            internal MaterialPropertyBlock[] OriginalIndexed;
        }

        private sealed class EV_ActorStyle
        {
            internal Transform Root;
            internal string Context;
            internal EV_Theme Theme;
            internal bool LoggedActive;
            internal FOTFEventideMockupGlow GlowOverlay;
            internal readonly Dictionary<int, EV_RendererSnapshot> Renderers =
                new Dictionary<int, EV_RendererSnapshot>();

            internal readonly Dictionary<int, EV_ShoulderSnapshot> Shoulders =
                new Dictionary<int, EV_ShoulderSnapshot>();
        }

        private readonly Dictionary<int, EV_ActorStyle> _evActors =
            new Dictionary<int, EV_ActorStyle>();

        private readonly Dictionary<string, Material> _evMaterialCache =
            new Dictionary<string, Material>();

        private readonly Dictionary<string, EV_TexturePair> _evTextureCache =
            new Dictionary<string, EV_TexturePair>();

        private readonly HashSet<int> _evActiveActorIds =
            new HashSet<int>();

        private float _evNextRefresh;

        private static readonly EV_Palette EV_BlightPalette =
            new EV_Palette
            {
                Name = "Blight",
                DarkMetal = new Color(0.012f, 0.017f, 0.012f, 1f),
                LightMetal = new Color(0.075f, 0.095f, 0.075f, 1f),
                AccentDark = new Color(0.035f, 0.24f, 0.025f, 1f),
                AccentBright = new Color(0.20f, 1.00f, 0.05f, 1f),
                Emission = new Color(0.20f, 1.00f, 0.04f, 1f),
                EmissionStrength = 2.8f
            };

        private static readonly EV_Palette EV_VoidPalette =
            new EV_Palette
            {
                Name = "Void",
                DarkMetal = new Color(0.012f, 0.010f, 0.018f, 1f),
                LightMetal = new Color(0.085f, 0.070f, 0.105f, 1f),
                AccentDark = new Color(0.18f, 0.015f, 0.30f, 1f),
                AccentBright = new Color(0.62f, 0.08f, 1.00f, 1f),
                Emission = new Color(0.72f, 0.10f, 1.00f, 1f),
                EmissionStrength = 2.6f
            };

        private static readonly EV_Palette EV_UndeathPalette =
            new EV_Palette
            {
                Name = "Undeath",
                DarkMetal = new Color(0.018f, 0.020f, 0.024f, 1f),
                LightMetal = new Color(0.11f, 0.12f, 0.14f, 1f),
                AccentDark = new Color(0.44f, 0.50f, 0.55f, 1f),
                AccentBright = new Color(0.90f, 0.96f, 1.00f, 1f),
                Emission = new Color(0.88f, 0.96f, 1.00f, 1f),
                EmissionStrength = 2.1f
            };

        private static readonly EV_Palette EV_DemonicPalette =
            new EV_Palette
            {
                Name = "Demonic",
                DarkMetal = new Color(0.020f, 0.010f, 0.008f, 1f),
                LightMetal = new Color(0.10f, 0.055f, 0.040f, 1f),
                AccentDark = new Color(0.38f, 0.035f, 0.005f, 1f),
                AccentBright = new Color(1.00f, 0.28f, 0.015f, 1f),
                Emission = new Color(1.00f, 0.18f, 0.01f, 1f),
                EmissionStrength = 3.0f
            };

        private void EV_Update()
        {
            if (Time.unscaledTime < _evNextRefresh)
            {
                return;
            }

            _evNextRefresh =
                Time.unscaledTime +
                EV_RefreshInterval;

            _evActiveActorIds.Clear();

            EV_RefreshPlayer();
            EV_RefreshSimPlayers();
            EV_RestoreInactiveActors();
        }

        private void EV_LateUpdate()
        {
            // Intentionally empty.
            //
            // The previous implementation called Renderer.SetPropertyBlock()
            // on HeavyPlate5 every rendered frame. That dirties an animated
            // SkinnedMeshRenderer exactly when the player is moving.
            //
            // Chest colour is now native Item state. Shoulder colour is
            // refreshed only on the 0.5s Eventide integrity cadence.
        }


        private void EV_RefreshPlayer()
        {
            Inventory inventory =
                GameData.PlayerInv;

            if (inventory == null)
            {
                return;
            }

            Item item =
                PB_GetEquippedItem(
                    inventory,
                    Item.SlotType.Chest);

            if (!EV_IsCarapace(item))
            {
                return;
            }

            Transform root =
                PB_GetActorRoot(inventory);

            if (root == null)
            {
                return;
            }

            int id =
                root.GetInstanceID();

            _evActiveActorIds.Add(id);

            EV_EnsureActorStyle(
                root,
                item,
                EV_Theme.Void,
                "Player / " + item.ItemName);
        }

        private void EV_RefreshSimPlayers()
        {
            SimPlayer[] sims =
                GetCachedSimPlayers();

            for (int i = 0;
                 sims != null && i < sims.Length;
                 i++)
            {
                SimPlayer sim =
                    sims[i];

                if (sim == null ||
                    !sim.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Item item =
                    EV_GetSimEquippedItem(
                        sim,
                        Item.SlotType.Chest);

                if (!EV_IsCarapace(item))
                {
                    continue;
                }

                Transform root =
                    sim.transform;

                if (root == null)
                {
                    continue;
                }

                int id =
                    root.GetInstanceID();

                _evActiveActorIds.Add(id);

                EV_EnsureActorStyle(
                    root,
                    item,
                    EV_Theme.Void,
                    sim.name + " / " + item.ItemName);
            }
        }

        private static bool EV_IsCarapace(Item item)
        {
            return
                item != null &&
                !string.IsNullOrEmpty(item.Id) &&
                string.Equals(
                    item.Id,
                    NhalSeraEquipment.CarapaceId,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static Item EV_GetSimEquippedItem(
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

        private void EV_EnsureActorStyle(
            Transform root,
            Item item,
            EV_Theme theme,
            string context)
        {
            if (root == null ||
                item == null)
            {
                return;
            }

            int id =
                root.GetInstanceID();

            EV_ActorStyle state;

            if (!_evActors.TryGetValue(id, out state) ||
                state == null ||
                state.Root != root ||
                state.Theme != theme)
            {
                if (state != null)
                {
                    EV_RestoreActor(state);
                }

                state =
                    new EV_ActorStyle
                    {
                        Root = root,
                        Context = context,
                        Theme = theme
                    };

                _evActors[id] =
                    state;
            }

            // Once HeavyPlate5 has been resolved, keep the cached renderer
            // references. Rediscover only if Unity destroyed one of them.
            // This removes a full character Transform traversal every refresh.
            if (EV_HasValidRendererSet(state))
            {
                foreach (KeyValuePair<int, EV_RendererSnapshot> pair in state.Renderers)
                {
                    EV_RendererSnapshot snapshot = pair.Value;
                    EV_ReassertVariantMaterials(snapshot);
                }

                EV_EnsureNativeShoulderStyle(
                    root,
                    item,
                    state);

                if (!state.LoggedActive)
                {
                    state.LoggedActive = true;
                    Plugin.NativeLog.LogInfo(
                        "[FateEquipmentVisuals] native Eventide variant active [" +
                        context +
                        "] Theme=[" +
                        EV_GetPalette(theme).Name +
                        "] Renderers=[" +
                        state.Renderers.Count +
                        "] Mode=[CachedChest+ShoulderOnlyPropertyBlocks].");
                }

                return;
            }

            List<Renderer> targets =
                EV_FindNativeItemRenderers(
                    root,
                    item);

            if (targets.Count == 0)
            {
                // Native equipment objects can be activated a frame after the
                // inventory slot changes. Leave the state alive and retry.
                return;
            }

            HashSet<int> seen =
                new HashSet<int>();

            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                Renderer renderer =
                    targets[i];

                if (renderer == null)
                {
                    continue;
                }

                int rendererId =
                    renderer.GetInstanceID();

                seen.Add(rendererId);

                EV_RendererSnapshot snapshot;

                if (!state.Renderers.TryGetValue(
                        rendererId,
                        out snapshot) ||
                    snapshot == null ||
                    snapshot.Renderer != renderer)
                {
                    snapshot =
                        EV_StyleRenderer(
                            renderer,
                            theme);

                    if (snapshot != null)
                    {
                        state.Renderers[rendererId] =
                            snapshot;
                    }
                }
                else
                {
                    EV_ReassertVariantMaterials(snapshot);
                }
            }

            List<int> stale =
                null;

            foreach (KeyValuePair<int, EV_RendererSnapshot> pair in state.Renderers)
            {
                if (seen.Contains(pair.Key))
                {
                    continue;
                }

                if (stale == null)
                {
                    stale =
                        new List<int>();
                }

                stale.Add(pair.Key);
            }

            if (stale != null)
            {
                for (int i = 0;
                     i < stale.Count;
                     i++)
                {
                    EV_RendererSnapshot snapshot;

                    if (state.Renderers.TryGetValue(
                            stale[i],
                            out snapshot))
                    {
                        EV_RestoreRenderer(snapshot);
                    }

                    state.Renderers.Remove(
                        stale[i]);
                }
            }

            EV_EnsureNativeShoulderStyle(
                root,
                item,
                state);

            if (state.Renderers.Count > 0 &&
                !state.LoggedActive)
            {
                state.LoggedActive = true;

                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] native Eventide variant active [" +
                    context +
                    "] Theme=[" +
                    EV_GetPalette(theme).Name +
                    "] Renderers=[" +
                    state.Renderers.Count +
                    "] Mode=[NativeChestItemPalette+ShoulderOnlyPropertyBlocks].");
            }
        }

        private static bool EV_HasValidRendererSet(
            EV_ActorStyle state)
        {
            if (state == null ||
                state.Renderers == null ||
                state.Renderers.Count == 0)
            {
                return false;
            }

            foreach (KeyValuePair<int, EV_RendererSnapshot> pair in state.Renderers)
            {
                EV_RendererSnapshot snapshot = pair.Value;

                if (snapshot == null ||
                    snapshot.Renderer == null ||
                    snapshot.VariantMaterials == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<Renderer> EV_FindNativeItemRenderers(
            Transform actorRoot,
            Item item)
        {
            List<Renderer> result =
                new List<Renderer>();

            if (actorRoot == null ||
                item == null)
            {
                return result;
            }

            HashSet<string> targetNames =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            EV_AddTargetName(
                targetNames,
                item.EquipmentToActivate);

            EV_AddTargetName(
                targetNames,
                item.ShoulderTrimL);

            EV_AddTargetName(
                targetNames,
                item.ShoulderTrimR);

            EV_AddTargetName(
                targetNames,
                item.ElbowTrimL);

            EV_AddTargetName(
                targetNames,
                item.ElbowTrimR);

            // Included defensively in case the native chest definition owns
            // additional trim pieces in a future Erenshor build.
            EV_AddTargetName(
                targetNames,
                item.KneeTrimL);

            EV_AddTargetName(
                targetNames,
                item.KneeTrimR);

            if (targetNames.Count == 0)
            {
                return result;
            }

            HashSet<int> rendererIds =
                new HashSet<int>();

            Transform[] all =
                actorRoot.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0;
                 all != null && i < all.Length;
                 i++)
            {
                Transform candidate =
                    all[i];

                if (candidate == null ||
                    !targetNames.Contains(candidate.name))
                {
                    continue;
                }

                Renderer[] renderers =
                    candidate.GetComponentsInChildren<Renderer>(
                        true);

                for (int r = 0;
                     renderers != null && r < renderers.Length;
                     r++)
                {
                    Renderer renderer =
                        renderers[r];

                    if (renderer == null)
                    {
                        continue;
                    }

                    int rendererId =
                        renderer.GetInstanceID();

                    if (rendererIds.Add(rendererId))
                    {
                        result.Add(renderer);
                    }
                }
            }

            return result;
        }

        private static void EV_AddTargetName(
            HashSet<string> targetNames,
            string value)
        {
            if (targetNames == null ||
                string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            targetNames.Add(
                value.Trim());
        }

        private EV_RendererSnapshot EV_StyleRenderer(
            Renderer renderer,
            EV_Theme theme)
        {
            if (renderer == null)
            {
                return null;
            }

            Material[] originals =
                renderer.sharedMaterials;

            if (originals == null ||
                originals.Length == 0)
            {
                return null;
            }

            MaterialPropertyBlock originalPropertyBlock =
                new MaterialPropertyBlock();

            renderer.GetPropertyBlock(
                originalPropertyBlock);

            MaterialPropertyBlock[] originalIndexedPropertyBlocks =
                new MaterialPropertyBlock[
                    originals.Length];

            MaterialPropertyBlock[] workingIndexedPropertyBlocks =
                new MaterialPropertyBlock[
                    originals.Length];

            for (int i = 0;
                 i < originals.Length;
                 i++)
            {
                MaterialPropertyBlock indexed =
                    new MaterialPropertyBlock();

                renderer.GetPropertyBlock(
                    indexed,
                    i);

                originalIndexedPropertyBlocks[i] =
                    indexed;

                workingIndexedPropertyBlocks[i] =
                    new MaterialPropertyBlock();
            }

            Material[] variants =
                new Material[originals.Length];

            for (int i = 0;
                 i < originals.Length;
                 i++)
            {
                variants[i] =
                    EV_GetVariantMaterial(
                        renderer,
                        originals[i],
                        theme,
                        i);
            }

            renderer.sharedMaterials =
                variants;

            EV_RendererSnapshot snapshot =
                new EV_RendererSnapshot
                {
                    Renderer = renderer,
                    OriginalMaterials = originals,
                    VariantMaterials = variants,
                    OriginalPropertyBlock =
                        originalPropertyBlock,
                    OriginalIndexedPropertyBlocks =
                        originalIndexedPropertyBlocks,
                    WorkingPropertyBlock =
                        new MaterialPropertyBlock(),
                    WorkingIndexedPropertyBlocks =
                        workingIndexedPropertyBlocks,
                    DebugLabel =
                        renderer.name +
                        " / mats=" +
                        originals.Length
                };

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] Eventide chest property-block override armed Renderer=[" +
                renderer.name +
                "] Materials=[" +
                originals.Length +
                "] OriginalGlobalEmpty=[" +
                originalPropertyBlock.isEmpty +
                "] Mode=[Global+PerMaterialIndex].");

            return
                snapshot;
        }

        private static void EV_ReassertVariantMaterials(
            EV_RendererSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Renderer == null ||
                snapshot.VariantMaterials == null)
            {
                return;
            }

            // HeavyPlate5 currently uses one material. Avoid sharedMaterials
            // array allocation in the common case.
            if (snapshot.VariantMaterials.Length == 1)
            {
                Material desired = snapshot.VariantMaterials[0];
                if (snapshot.Renderer.sharedMaterial != desired)
                {
                    snapshot.Renderer.sharedMaterial = desired;
                }

                return;
            }

            Material[] current = snapshot.Renderer.sharedMaterials;
            bool different =
                current == null ||
                current.Length != snapshot.VariantMaterials.Length;

            if (!different)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i] != snapshot.VariantMaterials[i])
                    {
                        different = true;
                        break;
                    }
                }
            }

            if (different)
            {
                snapshot.Renderer.sharedMaterials = snapshot.VariantMaterials;
            }
        }

        private Material EV_GetVariantMaterial(
            Renderer owner,
            Material source,
            EV_Theme theme,
            int materialIndex)
        {
            if (source == null)
            {
                return null;
            }

            string ownerName =
                owner != null
                    ? owner.name
                    : "<unknown-renderer>";

            string key =
                source.GetInstanceID().ToString() +
                ":" +
                ownerName +
                ":" +
                materialIndex.ToString() +
                ":" +
                ((int)theme).ToString() +
                ":NativeSlotsV2";

            Material cached;

            if (_evMaterialCache.TryGetValue(
                    key,
                    out cached) &&
                cached != null)
            {
                return cached;
            }

            EV_Palette palette =
                EV_GetPalette(theme);

            Material material =
                UnityEngine.Object.Instantiate(
                    source);

            material.name =
                source.name +
                "_FOTF_" +
                palette.Name +
                "_NativeSlots";

            // NEVER replace POLYGON_CustomCharacters. Its internal masks are
            // exactly what makes each palette colour appear on the correct
            // Eventide plates/panels.
            material.shader =
                source.shader;

            Color dark =
                new Color(
                    0.008f,
                    0.006f,
                    0.012f,
                    1f);

            Color metal =
                new Color(
                    0.050f,
                    0.045f,
                    0.065f,
                    1f);

            // Native palette derived from the debug slot-map screenshots.
            // Primary is the exact native chest chevron/V region we want to
            // highlight. The broad torso/pauldron metal channels stay dark.
            Color primary =
                new Color(
                    2.35f,
                    0.24f,
                    3.60f,
                    1f);

            Color secondary =
                new Color(
                    0.72f,
                    0.10f,
                    1.05f,
                    1f);

            Color leatherPrimary =
                new Color(
                    0.34f,
                    0.06f,
                    0.50f,
                    1f);

            Color leatherSecondary =
                new Color(
                    0.08f,
                    0.025f,
                    0.11f,
                    1f);

            Color metalPrimary =
                new Color(
                    0.018f,
                    0.014f,
                    0.024f,
                    1f);

            Color metalSecondary =
                new Color(
                    0.040f,
                    0.034f,
                    0.052f,
                    1f);

            Color metalDark =
                new Color(
                    0.006f,
                    0.005f,
                    0.008f,
                    1f);

            int applied =
                EV_ApplyWorkingChestPalette(
                    material);

            int glowProperties =
                EV_MaximiseNativeGlowProperties(
                    material);

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] Eventide native Synty palette [" +
                palette.Name +
                "] Renderer=[" +
                ownerName +
                "] Material=[" +
                source.name +
                "] Shader=[" +
                (source.shader != null
                    ? source.shader.name
                    : "<null>") +
                "] PaletteSlotsApplied=[" +
                applied +
                "/7] GlowProperties=[" +
                glowProperties +
                "] NativeMapping=[Chest=ItemPalette, Shoulders=Shoulder7PropertyBlocks].");

            _evMaterialCache[key] =
                material;

            return material;
        }

        private static void EV_EnsureNativeShoulderStyle(
            Transform actorRoot,
            Item item,
            EV_ActorStyle state)
        {
            if (actorRoot == null ||
                item == null ||
                state == null)
            {
                return;
            }

            int leftIndex =
                EV_ParseShoulderIndex(
                    item.ShoulderTrimL);

            int rightIndex =
                EV_ParseShoulderIndex(
                    item.ShoulderTrimR);

            HashSet<int> seen =
                new HashSet<int>();

            EV_ApplyShoulderSide(
                actorRoot,
                true,
                leftIndex,
                state,
                seen);

            EV_ApplyShoulderSide(
                actorRoot,
                false,
                rightIndex,
                state,
                seen);

            List<int> stale =
                null;

            foreach (KeyValuePair<int, EV_ShoulderSnapshot> pair in state.Shoulders)
            {
                if (seen.Contains(
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

            if (stale != null)
            {
                for (int i = 0;
                     i < stale.Count;
                     i++)
                {
                    EV_ShoulderSnapshot shoulder;

                    if (state.Shoulders.TryGetValue(
                            stale[i],
                            out shoulder))
                    {
                        EV_RestoreShoulder(
                            shoulder);
                    }

                    state.Shoulders.Remove(
                        stale[i]);
                }
            }
        }

        private static int EV_ParseShoulderIndex(
            string value)
        {
            int result;

            if (string.IsNullOrWhiteSpace(
                    value) ||
                !int.TryParse(
                    value.Trim(),
                    out result))
            {
                return -1;
            }

            return result;
        }

        private static void EV_ApplyShoulderSide(
            Transform actorRoot,
            bool left,
            int index,
            EV_ActorStyle state,
            HashSet<int> seen)
        {
            if (actorRoot == null ||
                state == null ||
                seen == null ||
                index < 0)
            {
                return;
            }

            Transform container =
                EV_FindShoulderContainer(
                    actorRoot,
                    left);

            if (container == null ||
                index >= container.childCount)
            {
                return;
            }

            Transform selected =
                container.GetChild(
                    index);

            if (selected == null)
            {
                return;
            }

            Renderer[] renderers =
                selected.GetComponentsInChildren<Renderer>(
                    true);

            for (int i = 0;
                 renderers != null && i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                int id =
                    renderer.GetInstanceID();

                seen.Add(
                    id);

                EV_ShoulderSnapshot shoulder;

                if (!state.Shoulders.TryGetValue(
                        id,
                        out shoulder) ||
                    shoulder == null ||
                    shoulder.Renderer != renderer)
                {
                    if (shoulder != null)
                    {
                        EV_RestoreShoulder(
                            shoulder);
                    }

                    shoulder =
                        EV_CaptureShoulder(
                            renderer);

                    state.Shoulders[id] =
                        shoulder;
                }

                EV_ApplyShoulderPropertyBlocks(
                    shoulder);
            }
        }

        private static Transform EV_FindShoulderContainer(
            Transform actorRoot,
            bool left)
        {
            if (actorRoot == null)
            {
                return null;
            }

            string exact =
                left
                    ? "All_06_Shoulder_Attachment_Left"
                    : "All_05_Shoulder_Attachment_Right";

            Transform[] all =
                actorRoot.GetComponentsInChildren<Transform>(
                    true);

            Transform fallback =
                null;

            for (int i = 0;
                 all != null && i < all.Length;
                 i++)
            {
                Transform candidate =
                    all[i];

                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(
                        candidate.name,
                        exact,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                string lower =
                    (candidate.name ?? string.Empty)
                    .ToLowerInvariant();

                if (lower.Contains(
                        "shoulder") &&
                    lower.Contains(
                        "attachment") &&
                    (left
                        ? lower.Contains("left")
                        : lower.Contains("right")))
                {
                    fallback =
                        candidate;
                }
            }

            return fallback;
        }

        private static EV_ShoulderSnapshot EV_CaptureShoulder(
            Renderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            Material[] materials =
                renderer.sharedMaterials;

            int count =
                materials != null
                    ? materials.Length
                    : 0;

            EV_ShoulderSnapshot result =
                new EV_ShoulderSnapshot
                {
                    Renderer =
                        renderer,
                    OriginalGlobal =
                        new MaterialPropertyBlock(),
                    OriginalIndexed =
                        new MaterialPropertyBlock[count]
                };

            renderer.GetPropertyBlock(
                result.OriginalGlobal);

            for (int i = 0;
                 i < count;
                 i++)
            {
                MaterialPropertyBlock original =
                    new MaterialPropertyBlock();

                renderer.GetPropertyBlock(
                    original,
                    i);

                result.OriginalIndexed[i] =
                    original;
            }

            return result;
        }

        private static void EV_ApplyShoulderPropertyBlocks(
            EV_ShoulderSnapshot shoulder)
        {
            if (shoulder == null ||
                shoulder.Renderer == null)
            {
                return;
            }

            Renderer renderer =
                shoulder.Renderer;

            Material[] materials =
                renderer.sharedMaterials;

            int count =
                materials != null
                    ? materials.Length
                    : 0;

            if (count > 0)
            {
                for (int i = 0;
                     i < count;
                     i++)
                {
                    MaterialPropertyBlock block =
                        new MaterialPropertyBlock();

                    renderer.GetPropertyBlock(
                        block,
                        i);

                    EV_SetShoulderColors(
                        block);

                    renderer.SetPropertyBlock(
                        block,
                        i);
                }

                return;
            }

            MaterialPropertyBlock global =
                new MaterialPropertyBlock();

            renderer.GetPropertyBlock(
                global);

            EV_SetShoulderColors(
                global);

            renderer.SetPropertyBlock(
                global);
        }

        private static void EV_SetShoulderColors(
            MaterialPropertyBlock block)
        {
            if (block == null)
            {
                return;
            }

            Color dark =
                new Color(
                    0.008f,
                    0.006f,
                    0.012f,
                    1f);

            Color metal =
                new Color(
                    0.050f,
                    0.045f,
                    0.065f,
                    1f);

            Color accent =
                new Color(
                    0.95f,
                    0.10f,
                    1.45f,
                    1f);

            block.SetColor(
                "_Color_Primary",
                dark);

            block.SetColor(
                "_Color_Secondary",
                accent);

            block.SetColor(
                "_Color_Leather_Primary",
                dark);

            block.SetColor(
                "_Color_Leather_Secondary",
                accent);

            block.SetColor(
                "_Color_Metal_Primary",
                metal);

            block.SetColor(
                "_Color_Metal_Secondary",
                accent);

            block.SetColor(
                "_Color_Metal_Dark",
                new Color(
                    0.004f,
                    0.004f,
                    0.006f,
                    1f));
        }

        private static void EV_RestoreShoulder(
            EV_ShoulderSnapshot shoulder)
        {
            if (shoulder == null ||
                shoulder.Renderer == null)
            {
                return;
            }

            shoulder.Renderer.SetPropertyBlock(
                shoulder.OriginalGlobal);

            if (shoulder.OriginalIndexed != null)
            {
                for (int i = 0;
                     i < shoulder.OriginalIndexed.Length;
                     i++)
                {
                    shoulder.Renderer.SetPropertyBlock(
                        shoulder.OriginalIndexed[i],
                        i);
                }
            }
        }

        private static void EV_RefreshWorkingChestPropertyBlocks(
            EV_RendererSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            // Recapture any unrelated native MPB state at the slow 0.5s
            // integrity cadence, then rebuild our seven chest colour overrides.
            // LateUpdate remains Set-only.
            snapshot.WorkingPropertyBlocksPrepared = false;
            EV_ApplyWorkingChestPropertyBlocks(snapshot);
        }

        private static void EV_ApplyWorkingChestPropertyBlocks(
            EV_RendererSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Renderer == null)
            {
                return;
            }

            Renderer renderer =
                snapshot.Renderer;

            Material[] materials =
                snapshot.VariantMaterials;

            int materialCount =
                materials != null
                    ? materials.Length
                    : 0;

            if (!snapshot.WorkingPropertyBlocksPrepared)
            {
                MaterialPropertyBlock global =
                    snapshot.WorkingPropertyBlock ??
                    new MaterialPropertyBlock();

                renderer.GetPropertyBlock(global);
                EV_SetWorkingChestPropertyBlockColors(global);
                snapshot.WorkingPropertyBlock = global;

                if (snapshot.WorkingIndexedPropertyBlocks == null ||
                    snapshot.WorkingIndexedPropertyBlocks.Length != materialCount)
                {
                    snapshot.WorkingIndexedPropertyBlocks =
                        new MaterialPropertyBlock[materialCount];
                }

                for (int i = 0; i < materialCount; i++)
                {
                    MaterialPropertyBlock indexed =
                        snapshot.WorkingIndexedPropertyBlocks[i] ??
                        new MaterialPropertyBlock();

                    renderer.GetPropertyBlock(indexed, i);
                    EV_SetWorkingChestPropertyBlockColors(indexed);
                    snapshot.WorkingIndexedPropertyBlocks[i] = indexed;
                }

                snapshot.WorkingPropertyBlocksPrepared = true;
            }

            // Hot path: no GetPropertyBlock, no shader reflection, no arrays.
            // Indexed blocks override the renderer-level block, so once they
            // exist we only need to reassert those each frame.
            MaterialPropertyBlock[] indexedBlocks =
                snapshot.WorkingIndexedPropertyBlocks;

            if (indexedBlocks != null && indexedBlocks.Length > 0)
            {
                for (int i = 0; i < indexedBlocks.Length; i++)
                {
                    if (indexedBlocks[i] != null)
                    {
                        renderer.SetPropertyBlock(indexedBlocks[i], i);
                    }
                }

                return;
            }

            if (snapshot.WorkingPropertyBlock != null)
            {
                renderer.SetPropertyBlock(snapshot.WorkingPropertyBlock);
            }
        }

        private static void EV_SetWorkingChestPropertyBlockColors(
            MaterialPropertyBlock block)
        {
            if (block == null)
            {
                return;
            }

            block.SetColor(
                "_Color_Primary",
                new Color(
                    2.35f,
                    0.24f,
                    3.60f,
                    1f));

            block.SetColor(
                "_Color_Secondary",
                new Color(
                    0.72f,
                    0.10f,
                    1.05f,
                    1f));

            block.SetColor(
                "_Color_Leather_Primary",
                new Color(
                    0.34f,
                    0.06f,
                    0.50f,
                    1f));

            block.SetColor(
                "_Color_Leather_Secondary",
                new Color(
                    0.08f,
                    0.025f,
                    0.11f,
                    1f));

            block.SetColor(
                "_Color_Metal_Primary",
                new Color(
                    0.018f,
                    0.014f,
                    0.024f,
                    1f));

            block.SetColor(
                "_Color_Metal_Secondary",
                new Color(
                    0.040f,
                    0.034f,
                    0.052f,
                    1f));

            block.SetColor(
                "_Color_Metal_Dark",
                new Color(
                    0.006f,
                    0.005f,
                    0.008f,
                    1f));
        }

        private static int EV_ApplyWorkingChestPalette(
            Material material)
        {
            if (material == null)
            {
                return 0;
            }

            int applied =
                0;

            // Exact palette from the NativeChestAccent build that produced the
            // working purple chest chevron while keeping torso/pauldrons dark.
            applied +=
                EV_SetNativeColor(
                    material,
                    "_Color_Primary",
                    new Color(
                        2.35f,
                        0.24f,
                        3.60f,
                        1f));

            applied +=
                EV_SetNativeColor(
                    material,
                    "_Color_Secondary",
                    new Color(
                        0.72f,
                        0.10f,
                        1.05f,
                        1f));

            applied +=
                EV_SetNativeColor(
                    material,
                    "_Color_Leather_Primary",
                    new Color(
                        0.34f,
                        0.06f,
                        0.50f,
                        1f));

            applied +=
                EV_SetNativeColor(
                    material,
                    "_Color_Leather_Secondary",
                    new Color(
                        0.08f,
                        0.025f,
                        0.11f,
                        1f));

            applied +=
                EV_SetNativeColor(
                    material,
                    "_Color_Metal_Primary",
                    new Color(
                        0.018f,
                        0.014f,
                        0.024f,
                        1f));

            applied +=
                EV_SetNativeColor(
                    material,
                    "_Color_Metal_Secondary",
                    new Color(
                        0.040f,
                        0.034f,
                        0.052f,
                        1f));

            applied +=
                EV_SetNativeColor(
                    material,
                    "_Color_Metal_Dark",
                    new Color(
                        0.006f,
                        0.005f,
                        0.008f,
                        1f));

            return applied;
        }

        private static int EV_SetNativeColor(
            Material material,
            string propertyName,
            Color value)
        {
            if (material == null ||
                string.IsNullOrEmpty(propertyName) ||
                !material.HasProperty(propertyName))
            {
                return 0;
            }

            material.SetColor(
                propertyName,
                value);

            return 1;
        }

        private static int EV_MaximiseNativeGlowProperties(
            Material material)
        {
            if (material == null ||
                material.shader == null)
            {
                return 0;
            }

            int changed =
                0;

            Shader shader =
                material.shader;

            int propertyCount =
                shader.GetPropertyCount();

            for (int i = 0;
                 i < propertyCount;
                 i++)
            {
                string propertyName =
                    shader.GetPropertyName(
                        i);

                if (string.IsNullOrEmpty(
                        propertyName))
                {
                    continue;
                }

                string lower =
                    propertyName.ToLowerInvariant();

                ShaderPropertyType type =
                    shader.GetPropertyType(
                        i);

                bool glowName =
                    lower.Contains(
                        "glow") ||
                    lower.Contains(
                        "emission") ||
                    lower.Contains(
                        "emissive");

                if (!glowName)
                {
                    continue;
                }

                try
                {
                    if (type ==
                        ShaderPropertyType.Float ||
                        type ==
                        ShaderPropertyType.Range)
                    {
                        material.SetFloat(
                            propertyName,
                            8.0f);

                        changed++;
                    }
                    else if (type ==
                             ShaderPropertyType.Color)
                    {
                        material.SetColor(
                            propertyName,
                            new Color(
                                1.10f,
                                0.16f,
                                1.70f,
                                1f));

                        changed++;
                    }
                }
                catch
                {
                }
            }

            return changed;
        }

        private static Color EV_MakeHdrAccent(
            EV_Palette palette,
            float intensity)
        {
            Color source =
                palette != null
                    ? palette.Emission
                    : Color.magenta;

            return
                new Color(
                    source.r * intensity,
                    source.g * intensity,
                    source.b * intensity,
                    1f);
        }

        private static bool EV_ShouldPreserveCharacterColor(
            string propertyName)
        {
            return
                EV_NameContainsAny(
                    propertyName,
                    "skin",
                    "face",
                    "hair",
                    "eye",
                    "brow",
                    "eyebrow",
                    "beard",
                    "stubble",
                    "scar",
                    "mouth",
                    "teeth",
                    "tongue");
        }

        private static bool EV_NameContainsAny(
            string value,
            params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) ||
                tokens == null)
            {
                return false;
            }

            string lower =
                value.ToLowerInvariant();

            for (int i = 0;
                 i < tokens.Length;
                 i++)
            {
                string token =
                    tokens[i];

                if (!string.IsNullOrEmpty(token) &&
                    lower.Contains(
                        token.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EV_TrySetAccentColor(
            Material material,
            string propertyName,
            Color value)
        {
            if (material == null ||
                string.IsNullOrEmpty(propertyName) ||
                !material.HasProperty(propertyName))
            {
                return;
            }

            try
            {
                material.SetColor(
                    propertyName,
                    value);
            }
            catch
            {
            }
        }

        private static bool EV_IsLikelyTorsoMaterial(
            Renderer owner,
            Material source)
        {
            string ownerName =
                owner != null && !string.IsNullOrEmpty(owner.name)
                    ? owner.name
                    : string.Empty;

            string materialName =
                source != null && !string.IsNullOrEmpty(source.name)
                    ? source.name
                    : string.Empty;

            string combined =
                (ownerName + " " + materialName).ToLowerInvariant();

            return
                combined.Contains("body") ||
                combined.Contains("torso") ||
                combined.Contains("chest") ||
                combined.Contains("shirt") ||
                combined.Contains("robe") ||
                combined.Contains("coat") ||
                combined.Contains("armor") ||
                combined.Contains("armour");
        }

        private static bool EV_IsLikelyAccentMaterial(
            Renderer owner,
            Material source,
            int materialIndex)
        {
            string ownerName =
                owner != null && !string.IsNullOrEmpty(owner.name)
                    ? owner.name
                    : string.Empty;

            string materialName =
                source != null && !string.IsNullOrEmpty(source.name)
                    ? source.name
                    : string.Empty;

            string combined =
                (ownerName + " " + materialName).ToLowerInvariant();

            if (combined.Contains("gem") ||
                combined.Contains("crystal") ||
                combined.Contains("glow") ||
                combined.Contains("rune") ||
                combined.Contains("sigil") ||
                combined.Contains("vfx") ||
                combined.Contains("fx"))
            {
                return true;
            }

            if (combined.Contains("trim") ||
                combined.Contains("accent"))
            {
                return true;
            }

            // Some native sets use a small secondary material for accent bands.
            // Keep this narrow: only materials after index 0 are eligible here.
            if (materialIndex > 0 &&
                (combined.Contains("detail") ||
                 combined.Contains("band") ||
                 combined.Contains("line")))
            {
                return true;
            }

            return false;
        }

        private EV_TexturePair EV_GetVariantTextures(
            Texture source,
            EV_Theme theme,
            EV_Palette palette)
        {
            if (source == null ||
                source.width <= 0 ||
                source.height <= 0)
            {
                return null;
            }

            string key =
                source.GetInstanceID().ToString() +
                ":" +
                ((int)theme).ToString() +
                ":EmissionOnly";

            EV_TexturePair cached;

            if (_evTextureCache.TryGetValue(
                    key,
                    out cached) &&
                cached != null)
            {
                return cached;
            }

            Texture2D readable =
                EV_CreateReadableTexture(
                    source);

            if (readable == null)
            {
                return null;
            }

            Color[] sourcePixels =
                readable.GetPixels();

            Color[] emissionPixels =
                new Color[sourcePixels.Length];

            int accentPixels = 0;

            for (int i = 0;
                 i < sourcePixels.Length;
                 i++)
            {
                Color sourcePixel =
                    sourcePixels[i];

                bool accent =
                    EV_IsColoredTexturePixel(
                        sourcePixel);

                if (accent)
                {
                    accentPixels++;

                    float max =
                        Mathf.Max(
                            sourcePixel.r,
                            Mathf.Max(
                                sourcePixel.g,
                                sourcePixel.b));

                    float emission =
                        Mathf.Clamp01(
                            0.55f +
                            max * 0.90f);

                    emissionPixels[i] =
                        new Color(
                            emission,
                            emission,
                            emission,
                            sourcePixel.a);
                }
                else
                {
                    emissionPixels[i] =
                        new Color(
                            0f,
                            0f,
                            0f,
                            sourcePixel.a);
                }
            }

            Texture2D emissionMask =
                new Texture2D(
                    readable.width,
                    readable.height,
                    TextureFormat.RGBA32,
                    false);

            emissionMask.name =
                source.name +
                "_FOTF_" +
                palette.Name +
                "_Emission";

            emissionMask.wrapMode =
                source.wrapMode;

            emissionMask.filterMode =
                source.filterMode;

            emissionMask.anisoLevel =
                source.anisoLevel;

            emissionMask.SetPixels(
                emissionPixels);

            emissionMask.Apply(
                false,
                false);

            UnityEngine.Object.Destroy(
                readable);

            EV_TexturePair pair =
                new EV_TexturePair
                {
                    // Intentionally null: the native Eventide base texture must
                    // remain untouched so its purple pattern is not destroyed.
                    BaseTexture = null,
                    EmissionMask = emissionMask,
                    AccentCoverage =
                        sourcePixels.Length > 0
                            ? (float)accentPixels /
                              (float)sourcePixels.Length
                            : 0f
                };

            _evTextureCache[key] =
                pair;

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] Eventide native emission mask [" +
                palette.Name +
                "] Source=[" +
                source.name +
                "] Size=[" +
                source.width +
                "x" +
                source.height +
                "] ColoredCoverage=[" +
                (pair.AccentCoverage * 100f).ToString("0.00") +
                "%].");

            return pair;
        }

        private static bool EV_IsColoredTexturePixel(
            Color color)
        {
            if (color.a <= 0.01f)
            {
                return false;
            }

            float max =
                Mathf.Max(
                    color.r,
                    Mathf.Max(
                        color.g,
                        color.b));

            float min =
                Mathf.Min(
                    color.r,
                    Mathf.Min(
                        color.g,
                        color.b));

            float chroma =
                max - min;

            float saturation =
                max > 0.0001f
                    ? chroma / max
                    : 0f;

            // Eventide's neutral metal regions are near-greyscale; the purple
            // accents are the saturated/chromatic regions. This avoids relying
            // on an exact baked hue.
            return
                max >= 0.025f &&
                chroma >= 0.012f &&
                saturation >= 0.06f;
        }

        private static Texture2D EV_CreateReadableTexture(
            Texture source)
        {
            if (source == null ||
                source.width <= 0 ||
                source.height <= 0)
            {
                return null;
            }

            RenderTexture temporary =
                RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);

            RenderTexture previous =
                RenderTexture.active;

            try
            {
                Graphics.Blit(
                    source,
                    temporary);

                RenderTexture.active =
                    temporary;

                Texture2D readable =
                    new Texture2D(
                        source.width,
                        source.height,
                        TextureFormat.RGBA32,
                        false);

                readable.ReadPixels(
                    new Rect(
                        0f,
                        0f,
                        source.width,
                        source.height),
                    0,
                    0,
                    false);

                readable.Apply(
                    false,
                    false);

                return readable;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[FateEquipmentVisuals] Eventide texture readback failed [" +
                    source.name +
                    "]: " +
                    exception.Message);

                return null;
            }
            finally
            {
                RenderTexture.active =
                    previous;

                RenderTexture.ReleaseTemporary(
                    temporary);
            }
        }

        private static bool EV_IsAccentTexturePixel(
            Color color)
        {
            if (color.a <= 0.01f)
            {
                return false;
            }

            float max =
                Mathf.Max(
                    color.r,
                    Mathf.Max(
                        color.g,
                        color.b));

            float min =
                Mathf.Min(
                    color.r,
                    Mathf.Min(
                        color.g,
                        color.b));

            float saturation =
                max > 0.0001f
                    ? (max - min) / max
                    : 0f;

            // The Eventide atlas carries very dark baked shading in the purple
            // chest motifs, so keep the floor deliberately low for textured
            // accent recovery.
            if (max < 0.015f ||
                saturation < 0.04f)
            {
                return false;
            }

            float hue;
            float sat;
            float value;

            Color.RGBToHSV(
                color,
                out hue,
                out sat,
                out value);

            bool purpleHue =
                hue >= 0.58f &&
                hue <= 0.97f;

            bool magentaBlueDominant =
                color.b > color.g * 1.01f &&
                color.r > color.g * 0.98f &&
                (color.b + color.r) > color.g * 1.55f;

            return
                purpleHue ||
                magentaBlueDominant;
        }

        private static Color EV_GetMaterialColor(
            Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor(
                    "_Color");
            }

            return Color.white;
        }

        // Material-color fallback only. Texture pixel accent detection uses
        // EV_IsAccentTexturePixel(), which is intentionally broader because the
        // atlas already contains baked dark shading.
        private static bool EV_IsAccentColor(
            Color color)
        {
            if (color.a <= 0.01f)
            {
                return false;
            }

            float max =
                Mathf.Max(
                    color.r,
                    Mathf.Max(
                        color.g,
                        color.b));

            float min =
                Mathf.Min(
                    color.r,
                    Mathf.Min(
                        color.g,
                        color.b));

            float saturation =
                max > 0.0001f
                    ? (max - min) / max
                    : 0f;

            if (max < 0.08f ||
                saturation < 0.18f)
            {
                return false;
            }

            float hue;
            float sat;
            float value;

            Color.RGBToHSV(
                color,
                out hue,
                out sat,
                out value);

            // Native Eventide's distinguishing colour is violet/purple.
            // Keep this deliberately broad so palette-atlas filtering,
            // compression and lighting-authored texture values still match.
            bool purpleHue =
                hue >= 0.68f &&
                hue <= 0.93f;

            bool magentaBlueDominant =
                color.b > color.g * 1.08f &&
                color.r > color.g * 1.04f;

            return
                purpleHue ||
                magentaBlueDominant;
        }

        private static EV_Palette EV_GetPalette(
            EV_Theme theme)
        {
            switch (theme)
            {
                case EV_Theme.Blight:
                    return EV_BlightPalette;

                case EV_Theme.Undeath:
                    return EV_UndeathPalette;

                case EV_Theme.Demonic:
                    return EV_DemonicPalette;

                default:
                    return EV_VoidPalette;
            }
        }

        private void EV_RestoreInactiveActors()
        {
            List<int> stale =
                null;

            foreach (KeyValuePair<int, EV_ActorStyle> pair in _evActors)
            {
                if (_evActiveActorIds.Contains(
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
                EV_ActorStyle state;

                if (_evActors.TryGetValue(
                        stale[i],
                        out state))
                {
                    EV_RestoreActor(
                        state);
                }

                _evActors.Remove(
                    stale[i]);
            }
        }

        private static void EV_RestoreRenderer(
            EV_RendererSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Renderer == null ||
                snapshot.OriginalMaterials == null)
            {
                return;
            }

            snapshot.Renderer.sharedMaterials =
                snapshot.OriginalMaterials;

            if (snapshot.OriginalPropertyBlock != null)
            {
                snapshot.Renderer.SetPropertyBlock(
                    snapshot.OriginalPropertyBlock);
            }
            else
            {
                snapshot.Renderer.SetPropertyBlock(
                    null);
            }

            if (snapshot.OriginalIndexedPropertyBlocks != null)
            {
                for (int i = 0;
                     i < snapshot.OriginalIndexedPropertyBlocks.Length;
                     i++)
                {
                    snapshot.Renderer.SetPropertyBlock(
                        snapshot.OriginalIndexedPropertyBlocks[i],
                        i);
                }
            }
        }

        private static void EV_RestoreActor(
            EV_ActorStyle state)
        {
            if (state == null)
            {
                return;
            }

            foreach (KeyValuePair<int, EV_RendererSnapshot> pair in state.Renderers)
            {
                EV_RestoreRenderer(
                    pair.Value);
            }

            foreach (KeyValuePair<int, EV_ShoulderSnapshot> pair in state.Shoulders)
            {
                EV_RestoreShoulder(
                    pair.Value);
            }

            if (state.GlowOverlay != null)
            {
                UnityEngine.Object.Destroy(
                    state.GlowOverlay.gameObject);

                state.GlowOverlay = null;
            }

            state.Renderers.Clear();
            state.Shoulders.Clear();
            state.LoggedActive = false;
        }

        private void EV_Shutdown()
        {
            foreach (KeyValuePair<int, EV_ActorStyle> pair in _evActors)
            {
                EV_RestoreActor(
                    pair.Value);
            }

            _evActors.Clear();
            _evActiveActorIds.Clear();

            foreach (KeyValuePair<string, Material> pair in _evMaterialCache)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(
                        pair.Value);
                }
            }

            _evMaterialCache.Clear();

            foreach (KeyValuePair<string, EV_TexturePair> pair in _evTextureCache)
            {
                EV_TexturePair textures =
                    pair.Value;

                if (textures == null)
                {
                    continue;
                }

                if (textures.BaseTexture != null)
                {
                    UnityEngine.Object.Destroy(
                        textures.BaseTexture);
                }

                if (textures.EmissionMask != null)
                {
                    UnityEngine.Object.Destroy(
                        textures.EmissionMask);
                }
            }

            _evTextureCache.Clear();
        }
    }
}
