using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    internal static class BlightcallerVendor
    {
        // ------------------------------------------------------------
        // VENDOR SCENES
        // ------------------------------------------------------------

        private const string AdvancedVendorScene = "Azure";
        private const string MidVendorScene = "Loomingwood";
        private const string NoviceVendorScene = "Stowaway";

        // ------------------------------------------------------------
        // NATIVE VENDOR TEMPLATES
        // ------------------------------------------------------------

        private const string AdvancedVendorTemplateName = "Edwin Ansegg";
        private const string MidVendorTemplateName = "Juctin Mysia";
        private const string NoviceVendorTemplateName = "Amethi Plazzo";

        // ------------------------------------------------------------
        // VENDOR NAMES
        // ------------------------------------------------------------

        private const string AdvancedVendorName = "Kaelith the Blighted";
        private const string MidVendorName = "Vorren the Festering";
        private const string NoviceVendorName = "Veyra the Withered";

        // ------------------------------------------------------------
        // VENDOR DESCRIPTIONS
        // ------------------------------------------------------------

        private const string AdvancedVendorDesc = "Blightcaller";
        private const string MidVendorDesc = "Blightcaller";
        private const string NoviceVendorDesc = "Blightcaller";

        // ------------------------------------------------------------
        // VENDOR POSITIONS
        // ------------------------------------------------------------

        private static readonly Vector3 AdvancedVendorPosition =
            new Vector3(137.10f, 25.38f, 231.01f);

        private static readonly Quaternion AdvancedVendorRotation =
            Quaternion.Euler(0f, 270f, 0f);

        private static readonly Vector3 MidVendorPosition =
            new Vector3(333.36f, 29.48f, 457.78f);

        private static readonly Quaternion MidVendorRotation =
            Quaternion.Euler(0f, 90f, 0f);

        private static readonly Vector3 NoviceVendorPosition =
            new Vector3(603.37f, 72.07f, 442.37f);

        private static readonly Quaternion NoviceVendorRotation =
            Quaternion.Euler(0f, 270f, 0f);

        // ------------------------------------------------------------
        // KAELITH DIALOGUE
        // ------------------------------------------------------------

        private const string KaelithBlightcallerDialogue =
            "Ah... a Blightcaller. At last, someone who understands the power of decay. " +
            "Come, then. Have a look at what I have to offer. These scrolls contain the " +
            "more advanced secrets of the Blightcaller, while these auras will strengthen " +
            "the power of your covenant.";

        private const string KaelithOtherDialogue =
            "You're not a Blightcaller. Then why are you wasting my time? These scrolls " +
            "and auras are not meant for you. Leave before I decide to demonstrate exactly " +
            "what they can do.";

        // ------------------------------------------------------------
        // VORREN DIALOGUE
        // ------------------------------------------------------------

        private const string VorrenBlightcallerDialogue =
            "So... the mark of the Blightcaller has taken root. Good. " +
            "You have learned the first secrets of decay, but there is much more to uncover. " +
            "These teachings will carry your power beyond the rudiments and deeper into " +
            "the corruption that defines our path.";

        private const string VorrenOtherDialogue =
            "You have no place among these teachings. The power contained within these " +
            "scrolls belongs to those who walk the path of the Blightcaller. " +
            "Seek your own path elsewhere.";

        // ------------------------------------------------------------
        // VEYRA DIALOGUE
        // ------------------------------------------------------------

        private const string VeyraBlightcallerDialogue =
            "You carry the mark of the Blightcaller. Good. The path begins with these " +
            "teachings. Take what you need, and learn to wield decay before you seek its " +
            "deeper secrets.";

        private const string VeyraOtherDialogue =
            "These teachings belong to the Blightcaller. You have no place here. " +
            "Find another merchant for your needs.";

        // ------------------------------------------------------------
        // RUNTIME STATE
        // ------------------------------------------------------------

        private static bool _installed;

        private static GameObject _advancedVendor;
        private static GameObject _midVendor;
        private static GameObject _noviceVendor;

        private static Coroutine _advancedSpawnRoutine;
        private static Coroutine _midSpawnRoutine;
        private static Coroutine _noviceSpawnRoutine;

        // ------------------------------------------------------------
        // INSTALL
        // ------------------------------------------------------------

        internal static void Install()
        {
            if (_installed)
                return;

            _installed = true;

            SceneManager.sceneLoaded += OnSceneLoaded;

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.name == AdvancedVendorScene)
            {
                StartAdvancedSpawn();
            }
            else if (activeScene.name == MidVendorScene)
            {
                StartMidSpawn();
            }
            else if (activeScene.name == NoviceVendorScene)
            {
                StartNoviceSpawn();
            }
        }

        // ------------------------------------------------------------
        // UNINSTALL
        // ------------------------------------------------------------

        internal static void Uninstall()
        {
            if (!_installed)
                return;

            _installed = false;

            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_advancedSpawnRoutine != null)
            {
                CoroutineHost.Stop(_advancedSpawnRoutine);
                _advancedSpawnRoutine = null;
            }

            if (_midSpawnRoutine != null)
            {
                CoroutineHost.Stop(_midSpawnRoutine);
                _midSpawnRoutine = null;
            }

            if (_noviceSpawnRoutine != null)
            {
                CoroutineHost.Stop(_noviceSpawnRoutine);
                _noviceSpawnRoutine = null;
            }

            DestroyVendor(ref _advancedVendor);
            DestroyVendor(ref _midVendor);
            DestroyVendor(ref _noviceVendor);
        }

        // ------------------------------------------------------------
        // SCENE LOADED
        // ------------------------------------------------------------

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (scene.name == AdvancedVendorScene)
            {
                StartAdvancedSpawn();
            }
            else if (scene.name == MidVendorScene)
            {
                StartMidSpawn();
            }
            else if (scene.name == NoviceVendorScene)
            {
                StartNoviceSpawn();
            }
        }

        // ------------------------------------------------------------
        // SPAWN STARTERS
        // ------------------------------------------------------------

        private static void StartAdvancedSpawn()
        {
            if (_advancedVendor != null)
                return;

            if (_advancedSpawnRoutine != null)
                return;

            _advancedSpawnRoutine =
                CoroutineHost.Start(
                    SpawnAdvancedWhenReady()
                );
        }

        private static void StartMidSpawn()
        {
            if (_midVendor != null)
                return;

            if (_midSpawnRoutine != null)
                return;

            _midSpawnRoutine =
                CoroutineHost.Start(
                    SpawnMidWhenReady()
                );
        }

        private static void StartNoviceSpawn()
        {
            if (_noviceVendor != null)
                return;

            if (_noviceSpawnRoutine != null)
                return;

            _noviceSpawnRoutine =
                CoroutineHost.Start(
                    SpawnNoviceWhenReady()
                );
        }

        // ------------------------------------------------------------
        // KAELITH SPAWN
        // ------------------------------------------------------------

        private static IEnumerator SpawnAdvancedWhenReady()
        {
            const float timeout = 15f;
            const float interval = 0.5f;

            float elapsed = 0f;

            while (elapsed < timeout)
            {
                GameObject template =
                    FindNativeVendorTemplate(
                        AdvancedVendorTemplateName
                    );

                if (template != null)
                {
                    SpawnAdvancedVendor(template);

                    _advancedSpawnRoutine = null;

                    yield break;
                }

                yield return new WaitForSeconds(interval);

                elapsed += interval;
            }

            _advancedSpawnRoutine = null;

            Plugin.ModLog.Error(
                "Kaelith spawn failed: native Edwin Ansegg was not found in Azure."
            );
        }

        // ------------------------------------------------------------
        // VORREN SPAWN
        // ------------------------------------------------------------

        private static IEnumerator SpawnMidWhenReady()
        {
            const float timeout = 15f;
            const float interval = 0.5f;

            float elapsed = 0f;

            while (elapsed < timeout)
            {
                GameObject template =
                    FindNativeVendorTemplate(
                        MidVendorTemplateName
                    );

                if (template != null)
                {
                    SpawnMidVendor(template);

                    _midSpawnRoutine = null;

                    yield break;
                }

                yield return new WaitForSeconds(interval);

                elapsed += interval;
            }

            _midSpawnRoutine = null;

            Plugin.ModLog.Error(
                "Vorren spawn failed: native Juctin Mysia was not found in Loomingwood."
            );
        }

        // ------------------------------------------------------------
        // VEYRA SPAWN
        // ------------------------------------------------------------

        private static IEnumerator SpawnNoviceWhenReady()
        {
            const float timeout = 15f;
            const float interval = 0.5f;

            float elapsed = 0f;

            while (elapsed < timeout)
            {
                GameObject template =
                    FindNativeVendorTemplate(
                        NoviceVendorTemplateName
                    );

                if (template != null)
                {
                    SpawnNoviceVendor(template);

                    _noviceSpawnRoutine = null;

                    yield break;
                }

                yield return new WaitForSeconds(interval);

                elapsed += interval;
            }

            _noviceSpawnRoutine = null;

            Plugin.ModLog.Error(
                "Veyra spawn failed: native Amethi Plazzo was not found in Stowaway."
            );
        }

        // ------------------------------------------------------------
        // FIND NATIVE VENDOR TEMPLATE
        // ------------------------------------------------------------

        private static GameObject FindNativeVendorTemplate(
            string templateName)
        {
            if (string.IsNullOrEmpty(templateName))
                return null;

            GameObject template =
                GameObject.Find(templateName);

            if (template != null)
                return template;

            GameObject[] objects =
                Resources.FindObjectsOfTypeAll<GameObject>();

            Scene activeScene =
                SceneManager.GetActiveScene();

            foreach (GameObject obj in objects)
            {
                if (obj == null)
                    continue;

                if (!obj.scene.IsValid())
                    continue;

                if (obj.scene != activeScene)
                    continue;

                if (!string.Equals(
                        obj.name,
                        templateName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return obj;
            }

            return null;
        }

        // ------------------------------------------------------------
        // SPAWN KAELITH
        // ------------------------------------------------------------

        private static void SpawnAdvancedVendor(
            GameObject template)
        {
            if (_advancedVendor != null)
                return;

            try
            {
                GameObject vendor =
                    UnityEngine.Object.Instantiate(template);

                if (vendor == null)
                {
                    Plugin.ModLog.Error(
                        "Kaelith spawn failed: Instantiate returned null."
                    );

                    return;
                }

                _advancedVendor = vendor;

                ConfigureVendorTransform(
                    vendor,
                    AdvancedVendorPosition,
                    AdvancedVendorRotation
                );

                VendorInventory inventory =
                    vendor.GetComponent<VendorInventory>();

                if (inventory == null)
                {
                    Plugin.ModLog.Error(
                        "Kaelith spawn failed: cloned Edwin has no VendorInventory."
                    );

                    DestroyVendor(ref _advancedVendor);

                    return;
                }

                inventory.VendorDesc =
                    AdvancedVendorDesc;

                ConfigureVendorIdentity(
                    vendor,
                    AdvancedVendorName
                );

                ConfigureKaelithInventory(inventory);
                ConfigureKaelithQuests(
                    vendor);
            }
            catch (Exception ex)
            {
                Plugin.ModLog.Error(
                    "Kaelith spawn failed",
                    ex
                );

                DestroyVendor(ref _advancedVendor);
            }
        }

        // ------------------------------------------------------------
        // KAELITH QUESTS
        // ------------------------------------------------------------

        private static void ConfigureKaelithQuests(
            GameObject vendor)
        {
            if (vendor == null)
            {
                return;
            }

            Quest quest =
                FateOfTheFallenQuests.WeatheredNoteQuest;

            if (quest == null)
            {
                Plugin.NativeLog.LogError(
                    "Kaelith: Weathered Note quest was unavailable.");

                return;
            }

            QuestManager questManager =
                vendor.GetComponent<QuestManager>();

            if (questManager == null)
            {
                questManager =
                    vendor.AddComponent<QuestManager>();

                
            }

            if (questManager.NPCQuests == null)
            {
                questManager.NPCQuests =
                    new List<Quest>();
            }


            // Remove stale/duplicate versions of our quest.

            for (int i =
                     questManager.NPCQuests.Count - 1;
                 i >= 0;
                 i--)
            {
                Quest existing =
                    questManager.NPCQuests[i];

                if (existing == null)
                {
                    continue;
                }

                if (string.Equals(
                        existing.DBName,
                        FateOfTheFallenQuests
                            .WeatheredNoteQuestId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    questManager.NPCQuests.RemoveAt(
                        i);
                }
            }


            // Add the exact Quest object used by the custom quest system.

            questManager.NPCQuests.Add(
                quest);

            Plugin.NativeLog.LogInfo(
                "Kaelith: attached quest [" +
                quest.DBName +
                "] to QuestManager. Required item count: " +
                (
                    quest.RequiredItems != null
                        ? quest.RequiredItems.Count
                        : 0
                ) +
                ".");
        }



        // ------------------------------------------------------------
        // SPAWN VORREN
        // ------------------------------------------------------------

        private static void SpawnMidVendor(
            GameObject template)
        {
            if (_midVendor != null)
                return;

            try
            {
                GameObject vendor =
                    UnityEngine.Object.Instantiate(template);

                if (vendor == null)
                {
                    Plugin.ModLog.Error(
                        "Vorren spawn failed: Instantiate returned null."
                    );

                    return;
                }

                _midVendor = vendor;

                ConfigureVendorTransform(
                    vendor,
                    MidVendorPosition,
                    MidVendorRotation
                );

                VendorInventory inventory =
                    vendor.GetComponent<VendorInventory>();

                if (inventory == null)
                {
                    Plugin.ModLog.Error(
                        "Vorren spawn failed: cloned Juctin Mysia has no VendorInventory."
                    );

                    DestroyVendor(ref _midVendor);

                    return;
                }

                inventory.VendorDesc =
                    MidVendorDesc;

                ConfigureVendorIdentity(
                    vendor,
                    MidVendorName
                );

                ConfigureVorrenInventory(inventory);
            }
            catch (Exception ex)
            {
                Plugin.ModLog.Error(
                    "Vorren spawn failed",
                    ex
                );

                DestroyVendor(ref _midVendor);
            }
        }

        // ------------------------------------------------------------
        // SPAWN VEYRA
        // ------------------------------------------------------------

        private static void SpawnNoviceVendor(
            GameObject template)
        {
            if (_noviceVendor != null)
                return;

            try
            {
                GameObject vendor =
                    UnityEngine.Object.Instantiate(template);

                if (vendor == null)
                {
                    Plugin.ModLog.Error(
                        "Veyra spawn failed: Instantiate returned null."
                    );

                    return;
                }

                _noviceVendor = vendor;

                ConfigureVendorTransform(
                    vendor,
                    NoviceVendorPosition,
                    NoviceVendorRotation
                );

                VendorInventory inventory =
                    vendor.GetComponent<VendorInventory>();

                if (inventory == null)
                {
                    Plugin.ModLog.Error(
                        "Veyra spawn failed: cloned Amethi Plazzo has no VendorInventory."
                    );

                    DestroyVendor(ref _noviceVendor);

                    return;
                }

                inventory.VendorDesc =
                    NoviceVendorDesc;

                ConfigureVendorIdentity(
                    vendor,
                    NoviceVendorName
                );

                ConfigureVeyraInventory(inventory);
            }
            catch (Exception ex)
            {
                Plugin.ModLog.Error(
                    "Veyra spawn failed",
                    ex
                );

                DestroyVendor(ref _noviceVendor);
            }
        }

        // ------------------------------------------------------------
        // TRANSFORM / MOVEMENT
        // ------------------------------------------------------------

        private static void ConfigureVendorTransform(
            GameObject vendor,
            Vector3 position,
            Quaternion rotation)
        {
            vendor.transform.position = position;
            vendor.transform.rotation = rotation;

            MonoBehaviour[] behaviours =
                vendor.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                if (behaviour is NPC ||
                    behaviour is Character ||
                    behaviour is NPCDialogManager ||
                    behaviour is VendorInventory)
                {
                    continue;
                }

                string typeName =
                    behaviour.GetType().Name;

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

            vendor.transform.position = position;
            vendor.transform.rotation = rotation;
        }

        // ------------------------------------------------------------
        // IDENTITY
        // ------------------------------------------------------------

        private static void ConfigureVendorIdentity(
            GameObject vendor,
            string vendorName)
        {
            if (vendor == null)
                return;

            if (string.IsNullOrEmpty(vendorName))
                return;

            vendor.name =
                vendorName;

            NPC npc =
                vendor.GetComponent<NPC>();

            if (npc != null)
            {
                TrySetNativeNpcName(
                    npc,
                    vendorName
                );
            }
        }

        // ------------------------------------------------------------
        // NATIVE NPC NAME
        // ------------------------------------------------------------

        private static bool TrySetNativeNpcName(
            NPC npc,
            string vendorName)
        {
            if (npc == null)
                return false;

            Type npcType =
                typeof(NPC);

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo[] fields =
                npcType.GetFields(flags);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(string))
                    continue;

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

                if (string.IsNullOrEmpty(current))
                    continue;

                if (!IsNativeVendorTemplateName(current))
                    continue;

                try
                {
                    field.SetValue(
                        npc,
                        vendorName
                    );

                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.ModLog.Error(
                        "Failed to set NPC name field '" +
                        field.Name +
                        "'",
                        ex
                    );
                }
            }

            PropertyInfo[] properties =
                npcType.GetProperties(flags);

            foreach (PropertyInfo property in properties)
            {
                if (property.PropertyType != typeof(string))
                    continue;

                if (!property.CanRead ||
                    !property.CanWrite)
                {
                    continue;
                }

                string current;

                try
                {
                    current =
                        property.GetValue(
                            npc,
                            null
                        ) as string;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(current))
                    continue;

                if (!IsNativeVendorTemplateName(current))
                    continue;

                try
                {
                    property.SetValue(
                        npc,
                        vendorName,
                        null
                    );

                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.ModLog.Error(
                        "Failed to set NPC name property '" +
                        property.Name +
                        "'",
                        ex
                    );
                }
            }

            Plugin.ModLog.Error(
                "Blightcaller vendor identity failed: " +
                "could not find the native NPC name member."
            );

            return false;
        }

        private static bool IsNativeVendorTemplateName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return
                value.IndexOf(
                    "Edwin Ansegg",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                value.IndexOf(
                    "Juctin Mysia",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                value.IndexOf(
                    "Amethi Plazzo",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;
        }

        // ------------------------------------------------------------
        // KAELITH INVENTORY
        // ------------------------------------------------------------

        private static void ConfigureKaelithInventory(
            VendorInventory inventory)
        {
            List<Item> items =
                new List<Item>();

            AddScroll(items, "festering_bolt", 8000);
            AddScroll(items, "shadow_devastation", 10000);
            AddScroll(items, "potent_withering_touch", 14000);
            AddScroll(items, "virulent_blight", 15000);
            AddScroll(items, "lingering_agony", 16000);
            AddScroll(items, "soul_rend", 20000);

            AddScroll(
                items,
                "devastating_festering_weakness",
                16000
            );

            AddScroll(
                items,
                "eternal_withering_curse",
                16000
            );

            AddAura(items, "withering");
            AddAura(items, "pestilent_essence");
            AddAura(items, "festering_power");
            AddAura(items, "blighted_covenant");

            inventory.ItemsForSale =
                items;
        }

        // ------------------------------------------------------------
        // VORREN INVENTORY
        // ------------------------------------------------------------

        private static void ConfigureVorrenInventory(
            VendorInventory inventory)
        {
            List<Item> items =
                new List<Item>();

            AddScroll(items, "putrid_bolt", 1850);
            AddScroll(items, "shadow_rupture", 1850);
            AddScroll(items, "greater_withering_touch", 2000);
            AddScroll(items, "greater_blight", 2000);
            AddScroll(items, "greater_agony", 2000);

            AddScroll(
                items,
                "greater_festering_weakness",
                2000
            );

            AddScroll(
                items,
                "greater_withering_curse",
                2000
            );

            inventory.ItemsForSale =
                items;
        }

        // ------------------------------------------------------------
        // VEYRA INVENTORY
        // ------------------------------------------------------------

        private static void ConfigureVeyraInventory(
            VendorInventory inventory)
        {
            List<Item> items =
                new List<Item>();

            AddScroll(items, "blighted_bolt", 100);
            AddScroll(items, "shadow_rend", 250);
            AddScroll(items, "withering_touch", 350);
            AddScroll(items, "blight", 500);
            AddScroll(items, "agony", 800);

            AddScroll(
                items,
                "festering_weakness",
                750
            );

            AddScroll(
                items,
                "withering_curse",
                1000
            );

            AddAura(
                items,
                "pestilent_essence"
            );

            inventory.ItemsForSale =
                items;
        }

        // ------------------------------------------------------------
        // ITEM HELPERS
        // ------------------------------------------------------------

        private static void AddScroll(
            List<Item> items,
            string key,
            int price)
        {
            Item item =
                BlightcallerScrolls.GetScroll(key);

            if (item == null)
            {
                Plugin.ModLog.Error(
                    "Vendor item missing: scroll '" +
                    key +
                    "'."
                );

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
            Item item =
                BlightcallerAuras.GetItem(key);

            if (item == null)
            {
                Plugin.ModLog.Error(
                    "Vendor item missing: aura '" +
                    key +
                    "'."
                );

                return;
            }

            if (!items.Contains(item))
            {
                items.Add(item);
            }
        }

        // ------------------------------------------------------------
        // BLIGHTCALLER DIALOGUE
        // ------------------------------------------------------------

        internal static void HandleGenericHail(
            NPCDialogManager __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                GameObject owner =
                    __instance.gameObject;

                if (owner == null)
                    return;

                string npcName =
                    owner.name;

                bool isKaelith =
                    string.Equals(
                        npcName,
                        AdvancedVendorName,
                        StringComparison.OrdinalIgnoreCase
                    );

                bool isVorren =
                    string.Equals(
                        npcName,
                        MidVendorName,
                        StringComparison.OrdinalIgnoreCase
                    );

                bool isVeyra =
                    string.Equals(
                        npcName,
                        NoviceVendorName,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (!isKaelith &&
                    !isVorren &&
                    !isVeyra)
                {
                    return;
                }

                string dialogue;

                // ----------------------------------------------------
                // KAELITH QUEST TURN-IN
                // ----------------------------------------------------

                if (isKaelith)
                {
                    string questDialogue;

                    if (FateOfTheFallenQuests
                        .TryHandleKaelithTurnIn(
                            out questDialogue))
                    {
                        SetDialogReturnString(
                            __instance,
                            questDialogue
                        );

                        return;
                    }

                    dialogue =
                        GetKaelithDialogue();
                }
                else if (isVorren)
                {
                    dialogue =
                        GetVorrenDialogue();
                }
                else
                {
                    dialogue =
                        GetVeyraDialogue();
                }

                SetDialogReturnString(
                    __instance,
                    dialogue
                );
            }
            catch (Exception ex)
            {
                Plugin.ModLog.Error(
                    "Blightcaller vendor dialogue patch failed",
                    ex
                );
            }
        }

        private static string GetKaelithDialogue()
        {
            bool isBlightcaller =
                IsCurrentPlayerBlightcaller();

            return isBlightcaller
                ? KaelithBlightcallerDialogue
                : KaelithOtherDialogue;
        }

        private static string GetVorrenDialogue()
        {
            bool isBlightcaller =
                IsCurrentPlayerBlightcaller();

            return isBlightcaller
                ? VorrenBlightcallerDialogue
                : VorrenOtherDialogue;
        }

        private static string GetVeyraDialogue()
        {
            bool isBlightcaller =
                IsCurrentPlayerBlightcaller();

            return isBlightcaller
                ? VeyraBlightcallerDialogue
                : VeyraOtherDialogue;
        }

        // ------------------------------------------------------------
        // PLAYER CLASS DETECTION
        // ------------------------------------------------------------

        private static bool IsCurrentPlayerBlightcaller()
        {
            try
            {
                Character[] characters =
                    UnityEngine.Object.FindObjectsOfType<Character>();

                foreach (Character character in characters)
                {
                    if (character == null)
                        continue;

                    if (!IsLikelyPlayerCharacter(character))
                        continue;

                    FieldInfo[] fields =
                        typeof(Character).GetFields(
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic
                        );

                    foreach (FieldInfo field in fields)
                    {
                        if (field.FieldType != typeof(string))
                            continue;

                        string value =
                            field.GetValue(character) as string;

                        if (string.IsNullOrEmpty(value))
                            continue;

                        if (value.IndexOf(
                                "Blightcaller",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                /*
                 * Dialogue detection must never interfere with the
                 * native vendor interaction.
                 */
            }

            return false;
        }

        private static bool IsLikelyPlayerCharacter(
            Character character)
        {
            if (character == null)
                return false;

            try
            {
                string objectName =
                    character.gameObject.name;

                if (string.IsNullOrEmpty(objectName))
                    return false;

                PlayerControl playerControl =
                    character.GetComponent<PlayerControl>();

                return playerControl != null;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------
        // DIALOGUE RETURN STRING
        // ------------------------------------------------------------

        private static void SetDialogReturnString(
            NPCDialogManager manager,
            string dialogue)
        {
            if (manager == null)
                return;

            if (string.IsNullOrEmpty(dialogue))
                return;

            try
            {
                FieldInfo field =
                    typeof(NPCDialogManager).GetField(
                        "ReturnString",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic
                    );

                if (field != null &&
                    field.FieldType == typeof(string))
                {
                    field.SetValue(
                        manager,
                        dialogue
                    );

                    return;
                }

                PropertyInfo property =
                    typeof(NPCDialogManager).GetProperty(
                        "ReturnString",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic
                    );

                if (property != null &&
                    property.CanWrite &&
                    property.PropertyType == typeof(string))
                {
                    property.SetValue(
                        manager,
                        dialogue,
                        null
                    );
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.Error(
                    "Failed to set Blightcaller vendor dialogue",
                    ex
                );
            }
        }

        // ------------------------------------------------------------
        // DESTROY
        // ------------------------------------------------------------

        private static void DestroyVendor(
            ref GameObject vendor)
        {
            if (vendor == null)
                return;

            try
            {
                UnityEngine.Object.Destroy(vendor);
            }
            catch (Exception ex)
            {
                Plugin.ModLog.Error(
                    "Failed to destroy custom vendor",
                    ex
                );
            }

            vendor = null;
        }
    }

    // ================================================================
    // COROUTINE HOST
    // ================================================================

    internal static class CoroutineHost
    {
        private sealed class Runner : MonoBehaviour
        {
        }

        private static Runner _runner;

        internal static Coroutine Start(
            IEnumerator routine)
        {
            EnsureRunner();

            return _runner.StartCoroutine(routine);
        }

        internal static void Stop(
            Coroutine coroutine)
        {
            if (_runner == null)
                return;

            if (coroutine == null)
                return;

            _runner.StopCoroutine(coroutine);
        }

        private static void EnsureRunner()
        {
            if (_runner != null)
                return;

            GameObject host =
                new GameObject(
                    "FateOfTheFallen_CoroutineHost"
                );

            UnityEngine.Object.DontDestroyOnLoad(host);

            _runner =
                host.AddComponent<Runner>();
        }
    }
}