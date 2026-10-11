using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Three-stage Emberweaver progression vendor chain.
    ///
    /// Vendors deliberately clone native merchants already present in the
    /// destination scene. This keeps Erenshor's proven vendor interaction,
    /// targeting, animation and TradeWindow plumbing intact while FOTF owns
    /// only identity, placement and inventory.
    /// </summary>
    internal static class EmberweaverVendor
    {
        private enum VendorTier
        {
            Novice,
            Mid,
            Advanced
        }

        private sealed class VendorDefinition
        {
            internal VendorTier Tier;
            internal string[] SceneAliases;
            internal string TemplateName;
            internal string VendorName;
            internal string VendorDescription;
            internal Vector3 Position;
        }

        // Stowaway's Step uses the internal scene name "Stowaway".
        private static readonly VendorDefinition Novice =
            new VendorDefinition
            {
                Tier = VendorTier.Novice,
                SceneAliases = new[] { "Stowaway" },
                TemplateName = "Aldoa Manday",
                VendorName = "Emberweaver Initiate",
                VendorDescription = "Emberweaver",
                Position = new Vector3(606.42f, 72.09f, 453.06f)
            };

        // Hidden Hills has appeared as "Hidden" in Erenshor map/zone data.
        // Keep the display-name aliases as harmless fallbacks across builds.
        private static readonly VendorDefinition Mid =
            new VendorDefinition
            {
                Tier = VendorTier.Mid,
                SceneAliases = new[] { "Hidden", "HiddenHills", "Hidden Hills" },
                TemplateName = "Nylith Valorro",
                VendorName = "Emberweaver Adept",
                VendorDescription = "Emberweaver",
                Position = new Vector3(-7.70f, -2.97f, -35.13f)
            };

        // Port Azure uses the internal scene name "Azure".
        private static readonly VendorDefinition Advanced =
            new VendorDefinition
            {
                Tier = VendorTier.Advanced,
                SceneAliases = new[] { "Azure" },
                TemplateName = "Edwin Ansegg",
                VendorName = "Emberweaver Magister",
                VendorDescription = "Emberweaver",
                Position = new Vector3(166.23f, 26.31f, 349.28f)
            };

        private const string NoviceEmberweaverDialogue =
            "Start with control, not spectacle. These teachings cover the first forms of the Emberweaver path and the disciplines that make Heat, Flashfire, and Wildfire work together.";

        private const string MidEmberweaverDialogue =
            "Your flame is taking shape. These techniques demand better timing: build Heat, feed Wildfire, and learn when to turn stored fire into immediate pressure.";

        private const string AdvancedEmberweaverDialogue =
            "At this point, restraint matters as much as power. These are the strongest non-raid Emberweaver teachings I can sell. The final Bolt and Pyroclasm forms must be earned in the planes.";

        private const string OtherClassDialogue =
            "These teachings are written for Emberweavers. They will be of no use to you.";

        private static bool _installed;

        private static GameObject _noviceVendor;
        private static GameObject _midVendor;
        private static GameObject _advancedVendor;

        private static Coroutine _noviceSpawnRoutine;
        private static Coroutine _midSpawnRoutine;
        private static Coroutine _advancedSpawnRoutine;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            _installed = true;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            BeginForScene(
                SceneManager.GetActiveScene());
        }

        internal static void Uninstall()
        {
            if (!_installed)
            {
                return;
            }

            _installed = false;

            SceneManager.sceneLoaded -= OnSceneLoaded;

            StopSpawnRoutine(VendorTier.Novice);
            StopSpawnRoutine(VendorTier.Mid);
            StopSpawnRoutine(VendorTier.Advanced);

            DestroyVendor(ref _noviceVendor);
            DestroyVendor(ref _midVendor);
            DestroyVendor(ref _advancedVendor);
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Any old scene-owned vendor will already compare equal to null
            // after Unity unloads it. Explicitly clear those managed refs so
            // hot reload / scene re-entry can never retain a stale reference.
            ClearDestroyedVendorReferences();

            BeginForScene(scene);
        }

        private static void BeginForScene(
            Scene scene)
        {
            if (!_installed ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return;
            }

            if (MatchesScene(Novice, scene))
            {
                StartSpawn(Novice);
                return;
            }

            if (MatchesScene(Mid, scene))
            {
                StartSpawn(Mid);
                return;
            }

            if (MatchesScene(Advanced, scene))
            {
                StartSpawn(Advanced);
            }
        }

        private static void StartSpawn(
            VendorDefinition definition)
        {
            if (definition == null ||
                GetVendor(definition.Tier) != null ||
                GetSpawnRoutine(definition.Tier) != null)
            {
                return;
            }

            SetSpawnRoutine(
                definition.Tier,
                CoroutineHost.Start(
                    SpawnWhenReady(definition)));
        }

        private static IEnumerator SpawnWhenReady(
            VendorDefinition definition)
        {
            const float TimeoutSeconds = 15f;
            const float RetrySeconds = 0.5f;

            float elapsed = 0f;
            bool foundTemplate = false;

            while (_installed &&
                   elapsed < TimeoutSeconds)
            {
                Scene activeScene =
                    SceneManager.GetActiveScene();

                if (!MatchesScene(
                        definition,
                        activeScene))
                {
                    SetSpawnRoutine(
                        definition.Tier,
                        null);

                    yield break;
                }

                GameObject template =
                    FindNativeVendorTemplate(
                        definition.TemplateName,
                        activeScene);

                if (template != null)
                {
                    foundTemplate = true;

                    if (EmberweaverModule.EnsureRuntimeContentReady(
                            definition.VendorName + " vendor spawn"))
                    {
                        SpawnVendor(
                            definition,
                            template,
                            activeScene);

                        SetSpawnRoutine(
                            definition.Tier,
                            null);

                        yield break;
                    }
                }

                yield return new WaitForSeconds(
                    RetrySeconds);

                elapsed += RetrySeconds;
            }

            SetSpawnRoutine(
                definition.Tier,
                null);

            Plugin.ModLog.Error(
                foundTemplate
                    ? definition.VendorName +
                      " spawn failed: Emberweaver teaching content did not become ready within 15 seconds."
                    : definition.VendorName +
                      " spawn failed: native vendor template [" +
                      definition.TemplateName +
                      "] was not found in scene [" +
                      SceneManager.GetActiveScene().name +
                      "].");
        }

        private static GameObject FindNativeVendorTemplate(
            string templateName,
            Scene scene)
        {
            if (string.IsNullOrEmpty(templateName) ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return null;
            }

            GameObject direct =
                GameObject.Find(
                    templateName);

            if (direct != null &&
                direct.scene == scene)
            {
                return direct;
            }

            // This fallback is only used during the short vendor-spawn probe;
            // it is never a per-frame or HUD path.
            NPC[] npcs =
                UnityEngine.Object.FindObjectsOfType<NPC>();

            for (int i = 0;
                 i < npcs.Length;
                 i++)
            {
                NPC npc = npcs[i];

                if (npc == null ||
                    npc.gameObject == null ||
                    npc.gameObject.scene != scene)
                {
                    continue;
                }

                if (string.Equals(
                        npc.gameObject.name,
                        templateName,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        npc.NPCName,
                        templateName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return npc.gameObject;
                }
            }

            return null;
        }

        private static void SpawnVendor(
            VendorDefinition definition,
            GameObject template,
            Scene scene)
        {
            if (definition == null ||
                template == null ||
                GetVendor(definition.Tier) != null)
            {
                return;
            }

            try
            {
                GameObject vendor =
                    UnityEngine.Object.Instantiate(
                        template);

                if (vendor == null)
                {
                    Plugin.ModLog.Error(
                        definition.VendorName +
                        " spawn failed: Instantiate returned null.");

                    return;
                }

                SetVendor(
                    definition.Tier,
                    vendor);

                if (vendor.scene != scene)
                {
                    SceneManager.MoveGameObjectToScene(
                        vendor,
                        scene);
                }

                ConfigureVendorTransform(
                    vendor,
                    definition.Position);

                VendorInventory inventory =
                    vendor.GetComponent<VendorInventory>();

                if (inventory == null)
                {
                    Plugin.ModLog.Error(
                        definition.VendorName +
                        " spawn failed: cloned native vendor has no VendorInventory.");

                    DestroyTierVendor(
                        definition.Tier);

                    return;
                }

                inventory.VendorDesc =
                    definition.VendorDescription;

                ConfigureVendorIdentity(
                    vendor,
                    definition.TemplateName,
                    definition.VendorName);

                ConfigureInventory(
                    definition.Tier,
                    inventory);

                // Position once more after components have been configured so
                // a cloned movement component cannot restore the template's
                // original location during the same frame.
                vendor.transform.position =
                    definition.Position;

                Plugin.LogInfo(
                    "Emberweaver vendor spawned: [" +
                    definition.VendorName +
                    "] Scene=[" +
                    scene.name +
                    "] Position=[" +
                    definition.Position +
                    "].");
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    definition.VendorName +
                    " spawn failed",
                    exception);

                DestroyTierVendor(
                    definition.Tier);
            }
        }

        private static void ConfigureVendorTransform(
            GameObject vendor,
            Vector3 position)
        {
            if (vendor == null)
            {
                return;
            }

            // Preserve the native template's facing because only placement was
            // specified for this pass. Movement/patrol components are disabled
            // so the clone remains at the requested coordinates.
            Quaternion nativeRotation =
                vendor.transform.rotation;

            vendor.transform.position =
                position;

            MonoBehaviour[] behaviours =
                vendor.GetComponentsInChildren<MonoBehaviour>(
                    true);

            for (int i = 0;
                 i < behaviours.Length;
                 i++)
            {
                MonoBehaviour behaviour =
                    behaviours[i];

                if (behaviour == null ||
                    behaviour is NPC ||
                    behaviour is Character ||
                    behaviour is NPCDialogManager ||
                    behaviour is VendorInventory)
                {
                    continue;
                }

                string typeName =
                    behaviour.GetType().Name ??
                    string.Empty;

                if (typeName.IndexOf(
                        "Patrol",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf(
                        "Movement",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    behaviour.enabled = false;
                }
            }

            vendor.transform.SetPositionAndRotation(
                position,
                nativeRotation);
        }

        private static void ConfigureVendorIdentity(
            GameObject vendor,
            string nativeTemplateName,
            string vendorName)
        {
            if (vendor == null ||
                string.IsNullOrEmpty(vendorName))
            {
                return;
            }

            vendor.name =
                vendorName;

            NPC npc =
                vendor.GetComponent<NPC>();

            if (npc == null)
            {
                return;
            }

            try
            {
                npc.NPCName =
                    vendorName;
            }
            catch
            {
                // Retain the same reflection fallback used by the proven
                // Blightcaller vendor implementation for build compatibility.
                TrySetNativeNpcName(
                    npc,
                    nativeTemplateName,
                    vendorName);
            }
        }

        private static bool TrySetNativeNpcName(
            NPC npc,
            string nativeTemplateName,
            string vendorName)
        {
            if (npc == null ||
                string.IsNullOrEmpty(vendorName))
            {
                return false;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo[] fields =
                typeof(NPC).GetFields(
                    flags);

            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                FieldInfo field =
                    fields[i];

                if (field == null ||
                    field.FieldType != typeof(string))
                {
                    continue;
                }

                string current;

                try
                {
                    current =
                        field.GetValue(npc) as string;
                }
                catch
                {
                    continue;
                }

                if (!string.Equals(
                        current,
                        nativeTemplateName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    field.SetValue(
                        npc,
                        vendorName);

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static void ConfigureInventory(
            VendorTier tier,
            VendorInventory inventory)
        {
            if (inventory == null)
            {
                return;
            }

            List<Item> items =
                new List<Item>();

            switch (tier)
            {
                case VendorTier.Novice:
                    // Levels 1-10 plus the three Emberweaver passive books.
                    AddScroll(items, EmberweaverSpellCatalog.EmberBoltId, 100);
                    AddScroll(items, EmberweaverSpellCatalog.PyroclasmId, 150);
                    AddScroll(items, EmberweaverSpellCatalog.ScorchId, 250);
                    AddScroll(items, EmberweaverSpellCatalog.KindleId, 400);
                    AddScroll(items, EmberweaverSpellCatalog.FlameSurgeId, 800);

                    AddSkillbook(items, EmberweaverSkillCatalog.HeatId, 250);
                    AddSkillbook(items, EmberweaverSkillCatalog.FlashfireId, 500);
                    AddSkillbook(items, EmberweaverSkillCatalog.WildfireId, 1000);

                    AddAura(items, EmberweaverAuras.KindlingKey);
                    AddAura(items, EmberweaverAuras.EmbersKey);
                    break;

                case VendorTier.Mid:
                    // Levels 11-20.
                    AddScroll(items, EmberweaverSpellCatalog.SearingBoltId, 1850);
                    AddScroll(items, EmberweaverSpellCatalog.GreaterPyroclasmId, 2000);
                    AddScroll(items, EmberweaverSpellCatalog.SearingScorchId, 2500);
                    AddScroll(items, EmberweaverSpellCatalog.BlazingSurgeId, 3000);
                    AddScroll(items, EmberweaverSpellCatalog.StokeId, 3000);
                    AddScroll(items, EmberweaverSpellCatalog.CombustId, 4000);

                    AddAura(items, EmberweaverAuras.FurnaceKey);
                    break;

                case VendorTier.Advanced:
                    // Levels 21-34. Level-35 capstone ranks are deliberately
                    // excluded and remain native-raid-only drops.
                    AddScroll(items, EmberweaverSpellCatalog.InfernoBoltId, 8000);
                    AddScroll(items, EmberweaverSpellCatalog.DevastatingPyroclasmId, 10000);
                    AddScroll(items, EmberweaverSpellCatalog.InfernalScorchId, 12000);
                    AddScroll(items, EmberweaverSpellCatalog.CataclysmicSurgeId, 15000);
                    AddScroll(items, EmberweaverSpellCatalog.ConflagrateId, 15000);

                    // Same catch-up pattern as Blightcaller's advanced vendor:
                    // every non-raid aura rank remains purchasable here.
                    AddAura(items, EmberweaverAuras.KindlingKey);
                    AddAura(items, EmberweaverAuras.EmbersKey);
                    AddAura(items, EmberweaverAuras.FurnaceKey);
                    AddAura(items, EmberweaverAuras.IncandescenceKey);
                    break;
            }

            inventory.ItemsForSale =
                items;
        }

        private static void AddScroll(
            List<Item> items,
            string spellId,
            int price)
        {
            if (items == null ||
                string.IsNullOrEmpty(spellId))
            {
                return;
            }

            Item item =
                EmberweaverTeachingItems.GetScrollForSpell(
                    spellId);

            if (item == null)
            {
                Plugin.ModLog.Error(
                    "Emberweaver vendor item missing: scroll for [" +
                    spellId +
                    "].");

                return;
            }

            item.ItemValue =
                price;

            if (!items.Contains(item))
            {
                items.Add(item);
            }
        }

        private static void AddSkillbook(
            List<Item> items,
            string skillId,
            int price)
        {
            if (items == null ||
                string.IsNullOrEmpty(skillId))
            {
                return;
            }

            Item item =
                EmberweaverTeachingItems.GetSkillbookForSkill(
                    skillId);

            if (item == null)
            {
                Plugin.ModLog.Error(
                    "Emberweaver vendor item missing: skillbook for [" +
                    skillId +
                    "].");

                return;
            }

            item.ItemValue =
                price;

            if (!items.Contains(item))
            {
                items.Add(item);
            }
        }

        private static void AddAura(
            List<Item> items,
            string key)
        {
            if (items == null ||
                string.IsNullOrEmpty(key))
            {
                return;
            }

            Item item =
                EmberweaverAuras.GetItem(
                    key);

            if (item == null)
            {
                Plugin.ModLog.Error(
                    "Emberweaver vendor item missing: aura [" +
                    key +
                    "].");

                return;
            }

            if (!items.Contains(item))
            {
                items.Add(item);
            }
        }

        internal static void HandleGenericHail(
            NPCDialogManager manager)
        {
            if (manager == null ||
                manager.gameObject == null)
            {
                return;
            }

            string npcName =
                manager.gameObject.name ??
                string.Empty;

            string emberweaverDialogue =
                null;

            if (string.Equals(
                    npcName,
                    Novice.VendorName,
                    StringComparison.OrdinalIgnoreCase))
            {
                emberweaverDialogue =
                    NoviceEmberweaverDialogue;
            }
            else if (string.Equals(
                         npcName,
                         Mid.VendorName,
                         StringComparison.OrdinalIgnoreCase))
            {
                emberweaverDialogue =
                    MidEmberweaverDialogue;
            }
            else if (string.Equals(
                         npcName,
                         Advanced.VendorName,
                         StringComparison.OrdinalIgnoreCase))
            {
                emberweaverDialogue =
                    AdvancedEmberweaverDialogue;
            }

            if (emberweaverDialogue == null)
            {
                return;
            }

            SetDialogReturnString(
                manager,
                EmberweaverCatalog.IsCurrentPlayerEmberweaver()
                    ? emberweaverDialogue
                    : OtherClassDialogue);
        }

        private static void SetDialogReturnString(
            NPCDialogManager manager,
            string dialogue)
        {
            if (manager == null ||
                string.IsNullOrEmpty(dialogue))
            {
                return;
            }

            try
            {
                BindingFlags flags =
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;

                FieldInfo field =
                    typeof(NPCDialogManager).GetField(
                        "ReturnString",
                        flags);

                if (field != null &&
                    field.FieldType == typeof(string))
                {
                    field.SetValue(
                        manager,
                        dialogue);

                    return;
                }

                PropertyInfo property =
                    typeof(NPCDialogManager).GetProperty(
                        "ReturnString",
                        flags);

                if (property != null &&
                    property.CanWrite &&
                    property.PropertyType == typeof(string))
                {
                    property.SetValue(
                        manager,
                        dialogue,
                        null);
                }
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver vendor dialogue failed",
                    exception);
            }
        }

        private static bool MatchesScene(
            VendorDefinition definition,
            Scene scene)
        {
            if (definition == null ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                definition.SceneAliases == null)
            {
                return false;
            }

            for (int i = 0;
                 i < definition.SceneAliases.Length;
                 i++)
            {
                if (string.Equals(
                        scene.name,
                        definition.SceneAliases[i],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject GetVendor(
            VendorTier tier)
        {
            switch (tier)
            {
                case VendorTier.Novice:
                    return _noviceVendor;

                case VendorTier.Mid:
                    return _midVendor;

                case VendorTier.Advanced:
                    return _advancedVendor;

                default:
                    return null;
            }
        }

        private static void SetVendor(
            VendorTier tier,
            GameObject vendor)
        {
            switch (tier)
            {
                case VendorTier.Novice:
                    _noviceVendor = vendor;
                    break;

                case VendorTier.Mid:
                    _midVendor = vendor;
                    break;

                case VendorTier.Advanced:
                    _advancedVendor = vendor;
                    break;
            }
        }

        private static Coroutine GetSpawnRoutine(
            VendorTier tier)
        {
            switch (tier)
            {
                case VendorTier.Novice:
                    return _noviceSpawnRoutine;

                case VendorTier.Mid:
                    return _midSpawnRoutine;

                case VendorTier.Advanced:
                    return _advancedSpawnRoutine;

                default:
                    return null;
            }
        }

        private static void SetSpawnRoutine(
            VendorTier tier,
            Coroutine routine)
        {
            switch (tier)
            {
                case VendorTier.Novice:
                    _noviceSpawnRoutine = routine;
                    break;

                case VendorTier.Mid:
                    _midSpawnRoutine = routine;
                    break;

                case VendorTier.Advanced:
                    _advancedSpawnRoutine = routine;
                    break;
            }
        }

        private static void StopSpawnRoutine(
            VendorTier tier)
        {
            Coroutine routine =
                GetSpawnRoutine(
                    tier);

            if (routine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                routine);

            SetSpawnRoutine(
                tier,
                null);
        }

        private static void ClearDestroyedVendorReferences()
        {
            if (_noviceVendor == null)
            {
                _noviceVendor = null;
            }

            if (_midVendor == null)
            {
                _midVendor = null;
            }

            if (_advancedVendor == null)
            {
                _advancedVendor = null;
            }
        }

        private static void DestroyTierVendor(
            VendorTier tier)
        {
            switch (tier)
            {
                case VendorTier.Novice:
                    DestroyVendor(ref _noviceVendor);
                    break;

                case VendorTier.Mid:
                    DestroyVendor(ref _midVendor);
                    break;

                case VendorTier.Advanced:
                    DestroyVendor(ref _advancedVendor);
                    break;
            }
        }

        private static void DestroyVendor(
            ref GameObject vendor)
        {
            if (vendor == null)
            {
                vendor = null;
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(
                    vendor);
            }
            catch
            {
            }

            vendor = null;
        }
    }
}
