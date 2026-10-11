using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Three-stage Lightbringer progression vendor chain.
    ///
    /// Vendors deliberately clone native merchants already present in the
    /// destination scene. This keeps Erenshor's proven vendor interaction,
    /// targeting, animation and TradeWindow plumbing intact while FOTF owns
    /// only identity, placement and inventory.
    /// </summary>
    internal static class LightbringerVendor
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
                VendorName = "Aurelia Dawnkeeper",
                VendorDescription = "Lightbringer",
                Position = new Vector3(610.22f, 72.09f, 454.46f)
            };

        // Loomingwood uses the native merchant Juctin Mysia as a proven trading template.
        private static readonly VendorDefinition Mid =
            new VendorDefinition
            {
                Tier = VendorTier.Mid,
                SceneAliases = new[] { "Loomingwood" },
                TemplateName = "Juctin Mysia",
                VendorName = "Seren of the First Light",
                VendorDescription = "Lightbringer",
                Position = new Vector3(336.70f, 29.48f, 459.60f)
            };

        // Port Azure uses the internal scene name "Azure".
        private static readonly VendorDefinition Advanced =
            new VendorDefinition
            {
                Tier = VendorTier.Advanced,
                SceneAliases = new[] { "Azure" },
                TemplateName = "Edwin Ansegg",
                VendorName = "High Luminary Caelis",
                VendorDescription = "Lightbringer",
                Position = new Vector3(170.10f, 26.31f, 350.80f)
            };

        private const string NoviceLightbringerDialogue =
            "Every dawn begins with a spark of courage. Learn to mend, defend and hold the light when others cannot.";

        private const string MidLightbringerDialogue =
            "You have carried the first light through hardship. Here you will learn to defend an entire company and answer darkness with judgement.";

        private const string AdvancedLightbringerDialogue =
            "You have earned more demanding teachings. I sell the final mortal disciplines; celestial capstones belong to the gods and must be won in the native raid planes.";

        private const string OtherClassDialogue =
            "These teachings are written for Lightbringers. They will be of no use to you.";

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

                    if (EnsureRuntimeContentReady(
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
                      " spawn failed: Lightbringer teaching content did not become ready within 15 seconds."
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
                    "Lightbringer vendor spawned: [" +
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

        private static void ConfigureInventory(VendorTier tier, VendorInventory inventory)
        {
            if (inventory == null) return;
            List<Item> items = new List<Item>();
            switch (tier)
            {
                case VendorTier.Novice:
                    // Level 1-10. Starter scrolls may also be awarded at character creation.
                    AddScroll(items, LightbringerSpellCatalog.FlickerWardId, 100);
                    AddScroll(items, LightbringerSpellCatalog.RadiantBoltId, 100);
                    AddScroll(items, LightbringerSpellCatalog.SolarSparkId, 200);
                    AddScroll(items, LightbringerSpellCatalog.JudgementId, 450);
                    AddScroll(items, LightbringerSpellCatalog.GuardiansBlessingId, 450);
                    AddSkillbook(items, LightbringerItemIds.GuidingLightSkillbook); // Guiding Light, level 1
                    AddSkillbook(items, "1925"); // Radiant Fortitude
                    AddAura(items, LightbringerAuras.FirstLightKey);
                    break;
                case VendorTier.Mid:
                    // Level 11-20 and first native group-heal spell.
                    AddScroll(items, LightbringerSpellCatalog.RadiantEmbraceId, 1400);
                    AddScroll(items, LightbringerSpellCatalog.SolarFlareId, 1600);
                    AddScroll(items, LightbringerSpellCatalog.AegisOfFaithId, 1600);
                    AddScroll(items, LightbringerSpellCatalog.DawnBoltId, 2100);
                    AddScroll(items, LightbringerSpellCatalog.LuminousMendingId, 2800);
                    AddScroll(items, LightbringerSpellCatalog.GreaterJudgementId, 2800);
                    AddNativeGroupHeal(items, "Group Heal");
                    AddSkillbook(items, "1926"); // Enduring Light
                    AddAura(items, LightbringerAuras.DawnsEmbraceKey);
                    AddAura(items, LightbringerAuras.SanctuaryKey);
                    break;
                case VendorTier.Advanced:
                    // Level 21-34. Never sell the six RaidTier spell definitions.
                    AddScroll(items, LightbringerSpellCatalog.RadiantCommunionId, 5500);
                    AddScroll(items, LightbringerSpellCatalog.SolarIgnitionId, 6500);
                    AddScroll(items, LightbringerSpellCatalog.AegisOfDawnId, 6500);
                    AddScroll(items, LightbringerSpellCatalog.RadiantLanceId, 7500);
                    AddScroll(items, LightbringerSpellCatalog.JudgementOfLightId, 8500);
                    AddScroll(items, LightbringerSpellCatalog.DivineIntercessionId, 10500);
                    AddScroll(items, LightbringerSpellCatalog.GreaterRadiantCommunionId, 12000);
                    AddNativeGroupHeal(items, "Group Revitalize");
                    AddNativeGroupHeal(items, "Major Group Heal");
                    AddSkillbook(items, "1927"); // Sanctified Communion
                    // Catch-up access to non-raid auras at the high-tier vendor.
                    AddAura(items, LightbringerAuras.FirstLightKey);
                    AddAura(items, LightbringerAuras.DawnsEmbraceKey);
                    AddAura(items, LightbringerAuras.SanctuaryKey);
                    AddAura(items, LightbringerAuras.RadiantCovenantKey);
                    break;
            }
            inventory.ItemsForSale = items;
        }

        private static void AddScroll(List<Item> items, string spellId, int price)
        {
            if (items == null || string.IsNullOrEmpty(spellId)) return;
            // Defence in depth: raid scrolls must never become merchant stock.
            foreach (LightbringerSpellDefinition definition in LightbringerSpellCatalog.All)
                if (definition != null && definition.Id == spellId && definition.RaidTier)
                { Plugin.LogWarning("[LBVendor] Refused raid-tier spell " + spellId); return; }
            Item item = LightbringerTeachingItems.GetScrollForSpell(spellId);
            if (item == null)
            { Plugin.LogWarning("[LBVendor] Scroll unavailable: " + spellId); return; }
            item.ItemValue = price;
            if (!items.Contains(item)) items.Add(item);
        }

        private static void AddSkillbook(List<Item> items, string itemId)
        {
            Item item = LightbringerSkillbooks.GetById(itemId);
            if (item == null)
            { Plugin.LogWarning("[LBVendor] Skillbook unavailable: " + itemId); return; }
            if (!items.Contains(item)) items.Add(item);
        }

        private static void AddNativeGroupHeal(List<Item> items, string exactSpellName)
        {
            if (GameData.ItemDB == null || string.IsNullOrEmpty(exactSpellName)) return;
            // Reuse native scrolls at their native prices; never clone, mutate
            // pricing, or assign eligibility to Group Regrowth variants.
            ItemDatabase db = GameData.ItemDB;
            Item match = null;
            if (db.ItemDB != null)
                foreach (Item candidate in db.ItemDB)
                    if (IsNativeHealScroll(candidate, exactSpellName)) { match = candidate; break; }
            if (match == null && db.ItemDBList != null)
                foreach (Item candidate in db.ItemDBList)
                    if (IsNativeHealScroll(candidate, exactSpellName)) { match = candidate; break; }
            if (match == null && db.GenericItems != null)
                foreach (Item candidate in db.GenericItems)
                    if (IsNativeHealScroll(candidate, exactSpellName)) { match = candidate; break; }
            if (match == null)
            { Plugin.LogWarning("[LBVendor] Native teaching scroll not found: " + exactSpellName); return; }
            LightbringerNativeHealing.EnsureItemCompatibility(match);
            if (!items.Contains(match)) items.Add(match);
        }

        private static bool IsNativeHealScroll(Item item, string spellName)
        {
            return item != null && item.TeachSpell != null && item.TeachSkill == null &&
                !string.IsNullOrEmpty(item.Id) &&
                !item.Id.StartsWith("FOTF_", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.TeachSpell.SpellName, spellName, StringComparison.OrdinalIgnoreCase) &&
                LightbringerNativeHealing.IsApproved(item.TeachSpell);
        }

        private static void AddAura(List<Item> items, string key)
        {
            Item item = LightbringerAuras.GetItem(key);
            if (item == null)
            { Plugin.LogWarning("[LBVendor] Aura unavailable: " + key); return; }
            if (!items.Contains(item)) items.Add(item);
        }

        // SpellDB and ItemDatabase may finish registration after scene load.
        // The coroutine retries until all teaching assets exist.
        private static bool EnsureRuntimeContentReady(string source)
        {
            if (LightbringerCatalog.TryEnsureClass() == null ||
                GameData.SpellDatabase == null || GameData.SpellDatabase.SpellDatabase == null ||
                GameData.ItemDB == null || GameData.ItemDB.ItemDB == null ||
                GameData.SkillDatabase == null) return false;
            try
            {
                LightbringerSpellCatalog.Register(GameData.SpellDatabase);
                LightbringerAuras.Register(GameData.SpellDatabase);
                LightbringerTeachingItems.Register(GameData.ItemDB);
                LightbringerSkillbooks.Register(GameData.ItemDB);
                return LightbringerTeachingItems.GetScrollForSpell(LightbringerSpellCatalog.RadiantBoltId) != null &&
                       LightbringerAuras.GetItem(LightbringerAuras.FirstLightKey) != null &&
                       LightbringerSkillbooks.GetById("1925") != null;
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("[LBVendor] Registration retry: " + source + " " + exception.Message);
                return false;
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
                    NoviceLightbringerDialogue;
            }
            else if (string.Equals(
                         npcName,
                         Mid.VendorName,
                         StringComparison.OrdinalIgnoreCase))
            {
                emberweaverDialogue =
                    MidLightbringerDialogue;
            }
            else if (string.Equals(
                         npcName,
                         Advanced.VendorName,
                         StringComparison.OrdinalIgnoreCase))
            {
                emberweaverDialogue =
                    AdvancedLightbringerDialogue;
            }

            if (emberweaverDialogue == null)
            {
                return;
            }

            SetDialogReturnString(
                manager,
                LightbringerCatalog.IsCurrentPlayerLightbringer()
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
                    "Lightbringer vendor dialogue failed",
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
