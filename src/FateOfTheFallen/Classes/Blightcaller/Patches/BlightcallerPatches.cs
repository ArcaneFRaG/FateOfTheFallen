using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    // ============================================================
    // CLASS DATABASE
    // ============================================================

    [HarmonyPatch(
        typeof(ClassDB),
        "Awake")]
    internal static class Patch_BlightcallerClassDatabase
    {
        private static void Postfix()
        {
            BlightcallerCatalog.EnsureClass();
        }
    }


    // ============================================================
    // SPELL DATABASE
    // ============================================================

    [HarmonyPatch(
        typeof(SpellDB),
        "Start")]
    internal static class Patch_BlightcallerSpellDatabase
    {
        private static void Postfix(
            SpellDB __instance)
        {
            if (__instance == null)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: SpellDB instance was null.");

                return;
            }

            if (__instance.SpellDatabase == null)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: native SpellDatabase was null.");

                return;
            }

            try
            {
                BlightcallerCatalog.EnsureClass();

                BlightcallerCatalog.RegisterSpells(
                    __instance);

                BlightcallerScrolls.Register();

                BlightcallerAuras.Register(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed during SpellDB initialization: " +
                    exception);
            }
        }
    }




    // ============================================================
    // CHARACTER CREATION
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "Start")]
    internal static class Patch_BlightcallerCharacterCreationStart
    {
        private static void Postfix(
            CharSelectManager __instance)
        {
            try
            {
                BlightcallerCharacterCreation
                    .InstallButton(__instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed to install character creation button: " +
                    exception);
            }
        }
    }


    // ============================================================
    // STARTING ITEMS
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "SaveChar")]
    internal static class Patch_BlightcallerStartingItems
    {
        private static void Prefix(
            CharSelectManager __instance)
        {
            try
            {
                BlightcallerCharacterCreation
                    .GrantStartingItemsIfValid(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed to grant starting items: " +
                    exception);
            }
        }
    }


    // ============================================================
    // ITEM DATABASE INITIALIZATION
    // ============================================================

    [HarmonyPatch(
        typeof(ItemDatabase),
        "Start")]
    internal static class Patch_BlightcallerItemDatabaseStart
    {
        private static void Postfix()
        {
            try
            {
                BlightcallerEquipment
                    .RegisterArcanistEquipment();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed to register Arcanist equipment: " +
                    exception);
            }
        }
    }


    // ============================================================
    // CUSTOM ITEM LOOKUP
    // ============================================================

    [HarmonyPatch(
        typeof(ItemDatabase),
        "GetItemByID")]
    internal static class Patch_BlightcallerItemDatabase
    {
        private static bool Prefix(
            ItemDatabase __instance,
            string id,
            ref Item __result)
        {
            try
            {
                Item item =
                    BlightcallerItemFactory.GetItemById(
                        id);

                if (item == null)
                {
                    return true;
                }

                __result = item;

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: custom ItemDatabase lookup failed: " +
                    exception);

                return true;
            }
        }
    }


    // ============================================================
    // ITEM TOOLTIP
    // ============================================================

    [HarmonyPatch(
        typeof(ItemInfoWindow),
        "DisplayItem",
        new Type[]
        {
            typeof(Item),
            typeof(Vector2),
            typeof(int)
        })]
    internal static class Patch_BlightcallerItemInfoWindow
    {
        private static void Postfix(
            Item item,
            ItemInfoWindow __instance)
        {
            if (item == null ||
                __instance == null)
            {
                return;
            }

            try
            {
                AddBlightcallerClassToTooltip(
                    item,
                    __instance);

                AddBlightcallerSkillRequirement(
                    item,
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: item tooltip modification failed: " +
                    exception);
            }
        }

        private static void AddBlightcallerClassToTooltip(
            Item item,
            ItemInfoWindow instance)
        {
            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                return;
            }

            if (item.Classes == null ||
                item.Classes.Count == 0)
            {
                return;
            }

            if (!item.Classes.Contains(
                    BlightcallerCatalog.BlightcallerClass))
            {
                return;
            }

            if (instance.Usable == null)
            {
                return;
            }

            TextMeshProUGUI usable =
                instance.Usable;

            if (!usable.text.Contains(
                    "Blightcaller"))
            {
                usable.text +=
                    " Blightcaller ";
            }
        }

        private static readonly string[] BlightcallerSkills =
        {
            "Arcane Proficiency",
            "Dodge",
            "Arcane Recovery",
            "Block",
            "Block II",
            "Dodge II"
        };

        private static void AddBlightcallerSkillRequirement(
            Item item,
            ItemInfoWindow instance)
        {
            if (item.TeachSkill == null)
            {
                return;
            }

            Skill skill =
                item.TeachSkill;

            if (!IsBlightcallerSkill(
                    skill.SkillName))
            {
                return;
            }

            if (skill.ArcanistRequiredLevel <= 0)
            {
                return;
            }

            if (instance.ReqLvl == null)
            {
                return;
            }

            TextMeshProUGUI text =
                instance.ReqLvl.GetComponent<TextMeshProUGUI>();

            if (text == null)
            {
                return;
            }

            string currentText =
                text.text;

            if (string.IsNullOrEmpty(
                    currentText))
            {
                return;
            }

            if (currentText.IndexOf(
                    "Blightcaller:",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            int headerIndex =
                currentText.IndexOf(
                    "Required Level:",
                    StringComparison.OrdinalIgnoreCase);

            if (headerIndex < 0)
            {
                return;
            }

            int insertPosition =
                currentText.IndexOf(
                    '\n',
                    headerIndex);

            if (insertPosition < 0)
            {
                return;
            }

            insertPosition++;

            string requirement =
                "Blightcaller: " +
                skill.ArcanistRequiredLevel.ToString() +
                "\n";

            text.text =
                currentText.Insert(
                    insertPosition,
                    requirement);
        }

        private static bool IsBlightcallerSkill(
            string skillName)
        {
            if (string.IsNullOrEmpty(
                    skillName))
            {
                return false;
            }

            foreach (
                string allowedSkill
                in BlightcallerSkills)
            {
                if (string.Equals(
                        skillName,
                        allowedSkill,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }


    // ============================================================
    // SPELL CASTING
    // ============================================================

    [HarmonyPatch(
        typeof(CastSpell),
        "StartSpell",
        new Type[]
        {
            typeof(Spell),
            typeof(Stats)
        })]
    internal static class Patch_BlightcallerCast
    {
        private static bool Prefix(
            CastSpell __instance,
            Spell _spell,
            ref Stats _target,
            ref bool __result)
        {
            try
            {
                BlightcallerSpellDefinition definition;

                if (!BlightcallerCatalog.TryGetDefinition(
                        _spell,
                        out definition))
                {
                    return true;
                }

                string denial;

                if (BlightcallerRuntime.PrepareCast(
                        __instance,
                        _spell,
                        ref _target,
                        out denial))
                {
                    return true;
                }

                /*
                 * A failed PrepareCast is an expected gameplay
                 * rejection, not a diagnostic condition.
                 */
                __result = false;

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: spell cast patch failed: " +
                    exception);

                return true;
            }
        }

        private static void Postfix(
            Spell _spell,
            bool __result)
        {
            if (!__result)
            {
                return;
            }

            try
            {
                BlightcallerSpellDefinition definition;

                if (!BlightcallerCatalog.TryGetDefinition(
                        _spell,
                        out definition))
                {
                    return;
                }

                if (definition.HiddenEffect)
                {
                    return;
                }

                if (GameData.PlayerCombat == null)
                {
                    return;
                }

                if (GameData.PlayerStats == null)
                {
                    return;
                }

                if (!BlightcallerCatalog.IsBlightcallerClass(
                        GameData.PlayerStats.CharacterClass))
                {
                    return;
                }

                GameData.PlayerCombat.ForceAttackOn();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: post-cast handling failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // HOTBAR RESTORATION
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "LoadHotkeys")]
    internal static class Patch_BlightcallerLoadHotkeys
    {
        private static bool Prefix()
        {
            if (GameData.CurrentCharacterSlot == null)
            {
                return true;
            }

            if (!string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    "Blightcaller",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                if (GameData.GM == null ||
                    GameData.GM.HKManager == null)
                {
                    return true;
                }

                if (GameData.CurrentCharacterSlot.HKSpells == null)
                {
                    return true;
                }

                if (GameData.CurrentCharacterSlot.SecHKSpells == null)
                {
                    GameData.CurrentCharacterSlot.SecHKSpells =
                        new string[10];

                    for (int i = 0; i <= 9; i++)
                    {
                        GameData.CurrentCharacterSlot.SecHKSpells[i] =
                            null;
                    }
                }

                if (GameData.CurrentCharacterSlot.SecHKSkills == null)
                {
                    GameData.CurrentCharacterSlot.SecHKSkills =
                        new string[10];

                    for (int i = 0; i <= 9; i++)
                    {
                        GameData.CurrentCharacterSlot.SecHKSkills[i] =
                            null;
                    }
                }

                if (GameData.CurrentCharacterSlot.SecHKItems == null)
                {
                    GameData.CurrentCharacterSlot.SecHKItems =
                        new int[10];

                    for (int i = 0; i <= 9; i++)
                    {
                        GameData.CurrentCharacterSlot.SecHKItems[i] =
                            -1;
                    }
                }

                for (int i = 0; i <= 9; i++)
                {
                    if (GameData.CurrentCharacterSlot.HKSpells.Length > i)
                    {
                        string spellId =
                            GameData.CurrentCharacterSlot.HKSpells[i];

                        if (!string.IsNullOrEmpty(
                                spellId))
                        {
                            Spell spell =
                                GetSpellBySavedId(
                                    spellId);

                            if (spell != null)
                            {
                                GameData.GM.HKManager
                                    .FirstHotkeys[i]
                                    .AssignSpellFromBook(
                                        spell);
                            }
                        }

                        if (GameData.CurrentCharacterSlot.HKSkills != null &&
                            GameData.CurrentCharacterSlot.HKSkills.Length > i)
                        {
                            string skillId =
                                GameData.CurrentCharacterSlot.HKSkills[i];

                            if (!string.IsNullOrEmpty(
                                    skillId))
                            {
                                Skill skill =
                                    GameData.SkillDatabase.GetSkillByID(
                                        skillId);

                                if (skill != null)
                                {
                                    GameData.GM.HKManager
                                        .FirstHotkeys[i]
                                        .AssignSkillFromBook(
                                            skill);
                                }
                            }
                        }

                        if (GameData.CurrentCharacterSlot.HKItems != null &&
                            GameData.CurrentCharacterSlot.HKItems.Length > i)
                        {
                            if (GameData.CurrentCharacterSlot.HKItems[i] != -1)
                            {
                                GameData.GM.HKManager
                                    .FirstHotkeys[i]
                                    .AssignItemFrominv(
                                        GameData.PlayerInv.ALLSLOTS[
                                            GameData.CurrentCharacterSlot.HKItems[i]]);
                            }
                        }
                    }

                    if (GameData.CurrentCharacterSlot.SecHKSpells.Length > i)
                    {
                        GameData.GM.HKManager
                            .SecondHotkeys[i]
                            .gameObject
                            .SetActive(true);

                        string spellId =
                            GameData.CurrentCharacterSlot.SecHKSpells[i];

                        if (!string.IsNullOrEmpty(
                                spellId))
                        {
                            Spell spell =
                                GetSpellBySavedId(
                                    spellId);

                            if (spell != null)
                            {
                                GameData.GM.HKManager
                                    .SecondHotkeys[i]
                                    .AssignSpellFromBook(
                                        spell);
                            }
                        }

                        if (GameData.CurrentCharacterSlot.SecHKSkills != null &&
                            GameData.CurrentCharacterSlot.SecHKSkills.Length > i)
                        {
                            string skillId =
                                GameData.CurrentCharacterSlot.SecHKSkills[i];

                            if (!string.IsNullOrEmpty(
                                    skillId))
                            {
                                Skill skill =
                                    GameData.SkillDatabase.GetSkillByID(
                                        skillId);

                                if (skill != null)
                                {
                                    GameData.GM.HKManager
                                        .SecondHotkeys[i]
                                        .AssignSkillFromBook(
                                            skill);
                                }
                            }
                        }

                        if (GameData.CurrentCharacterSlot.SecHKItems != null &&
                            GameData.CurrentCharacterSlot.SecHKItems.Length > i)
                        {
                            if (GameData.CurrentCharacterSlot.SecHKItems[i] != -1)
                            {
                                GameData.GM.HKManager
                                    .SecondHotkeys[i]
                                    .AssignItemFrominv(
                                        GameData.PlayerInv.ALLSLOTS[
                                            GameData.CurrentCharacterSlot.SecHKItems[i]]);
                            }
                        }

                        GameData.GM.HKManager
                            .SecondHotkeys[i]
                            .gameObject
                            .SetActive(false);
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: hotbar restoration failed: " +
                    exception);

                return true;
            }
        }

        private static Spell GetSpellBySavedId(
            string spellId)
        {
            if (string.IsNullOrEmpty(
                    spellId))
            {
                return null;
            }

            if (GameData.SpellDatabase != null)
            {
                Spell spell =
                    GameData.SpellDatabase.GetSpellByID(
                        spellId);

                if (spell != null)
                {
                    return spell;
                }
            }

            return BlightcallerCatalog.GetSpellById(
                spellId);
        }
    }


    // ============================================================
    // SPELL / SKILL / ASCENSION RESTORATION
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "LoadSpellsAndSkills")]
    internal static class Patch_BlightcallerLoadSpellsAndSkills
    {
        private static bool Prefix()
        {
            if (GameData.CurrentCharacterSlot == null)
            {
                return true;
            }

            if (!string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    "Blightcaller",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                if (GameData.GM == null ||
                    GameData.GM.PlayerSpells == null ||
                    GameData.GM.PlayerSpells.KnownSpells == null)
                {
                    return true;
                }

                if (GameData.CurrentCharacterSlot.CharacterSpells != null)
                {
                    foreach (
                        string spellId
                        in GameData.CurrentCharacterSlot.CharacterSpells)
                    {
                        if (string.IsNullOrEmpty(
                                spellId))
                        {
                            continue;
                        }

                        Spell spell = null;

                        if (GameData.SpellDatabase != null)
                        {
                            spell =
                                GameData.SpellDatabase.GetSpellByID(
                                    spellId);
                        }

                        if (spell == null)
                        {
                            spell =
                                BlightcallerCatalog.GetSpellById(
                                    spellId);
                        }

                        if (spell == null)
                        {
                            continue;
                        }

                        BlightcallerSpellDefinition definition;

                        if (BlightcallerCatalog.TryGetDefinition(
                                spell,
                                out definition))
                        {
                            if (definition.HiddenEffect)
                            {
                                continue;
                            }
                        }

                        if (spell.RequiredLevel > 0)
                        {
                            if (!GameData.GM.PlayerSpells.KnownSpells.Contains(
                                    spell))
                            {
                                GameData.GM.PlayerSpells.KnownSpells.Add(
                                    spell);
                            }
                        }
                    }
                }

                if (GameData.CurrentCharacterSlot.CharacterSkills != null &&
                    GameData.CurrentCharacterSlot.CharacterSkills.Count > 0)
                {
                    foreach (
                        string skillId
                        in GameData.CurrentCharacterSlot.CharacterSkills)
                    {
                        if (string.IsNullOrEmpty(
                                skillId))
                        {
                            continue;
                        }

                        Skill skill =
                            GameData.SkillDatabase.GetSkillByID(
                                skillId);

                        if (skill != null &&
                            !GameData.GM.PlayerSkills.KnownSkills.Contains(
                                skill))
                        {
                            GameData.GM.PlayerSkills.KnownSkills.Add(
                                skill);
                        }
                    }
                }

                if (GameData.CurrentCharacterSlot.Ascensions != null &&
                    GameData.CurrentCharacterSlot.Ascensions.Count > 0)
                {
                    foreach (
                        AscensionSkillEntry ascension
                        in GameData.CurrentCharacterSlot.Ascensions)
                    {
                        GameData.GM.PlayerSkills.MyAscensions.Add(
                            new AscensionSkillEntry(
                                ascension.id,
                                ascension.level));
                    }
                }

                GameData.GM.PlayerSkills.AscensionPoints =
                    GameData.CurrentCharacterSlot.AscensionPointsUnspent;

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: spell/skill restoration failed: " +
                    exception);

                return true;
            }
        }
    }


    // ============================================================
    // SKILL BOOK LEARNING
    // ============================================================

    [HarmonyPatch(
        typeof(ItemIcon),
        "OnPointerUp")]
    internal static class Patch_BlightcallerSkillBooks
    {
        private static readonly string[] AllowedSkills =
        {
        "Arcane Proficiency",
        "Dodge",
        "Arcane Recovery",
        "Block",
        "Block II",
        "Dodge II",
        "Multifocus"
    };

        private static bool Prefix(
            ItemIcon __instance,
            PointerEventData eventData)
        {
            if (__instance == null ||
                __instance.MyItem == null ||
                __instance.MyItem.TeachSkill == null)
            {
                return true;
            }

            if (GameData.PlayerStats == null ||
                !BlightcallerCatalog.IsBlightcallerClass(
                    GameData.PlayerStats.CharacterClass))
            {
                return true;
            }

            Skill skill =
                __instance.MyItem.TeachSkill;

            if (!IsAllowedSkill(
                    skill.SkillName))
            {
                return true;
            }

            try
            {
                if (GameData.PlayerControl == null)
                {
                    return true;
                }

                UseSkill useSkill =
                    GameData.PlayerControl.GetComponent<UseSkill>();

                if (useSkill == null)
                {
                    return true;
                }

                if (useSkill.KnownSkills.Contains(
                        skill))
                {
                    return false;
                }

                int requiredLevel =
                    skill.ArcanistRequiredLevel;

                if (requiredLevel <= 0)
                {
                    return true;
                }

                if (GameData.PlayerStats.Level < requiredLevel)
                {
                    UpdateSocialLog.LogAdd(
                        new ChatLogLine(
                            "You are not experienced enough to learn this skill yet...",
                            ChatLogLine.LogType.SystemMessages,
                            "yellow"));

                    return false;
                }

                useSkill.KnownSkills.Add(
                    skill);

                if (!GameData.PlayerStats.Myself.MySkills.KnownSkills.Contains(
                        skill))
                {
                    GameData.PlayerStats.Myself.MySkills.KnownSkills.Add(
                        skill);
                }

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Learned skill: " +
                        skill.SkillName,
                        ChatLogLine.LogType.SystemMessages,
                        "lightblue"));

                GameData.PlayerStats
                    .GetComponent<UseSkill>()
                    .LearnSkill.Play();

                GameData.PlayerAud.PlayOneShot(
                    GameData.Misc.NewSkill,
                    0.6f *
                    GameData.SFXVol *
                    GameData.MasterVol);

                if (GameData.PlayerSkillBook != null &&
                    GameData.PlayerSkillBook.Skillbook.activeSelf)
                {
                    GameData.PlayerSkillBook.UpdateSkillList(
                        GameData.PlayerSkillBook.GetPage());
                }

                if (GameData.HKMngr.FindBlankHotkey() != null)
                {
                    GameData.HKMngr.AssignNewSkillToHK(
                        skill,
                        GameData.HKMngr.FindBlankHotkey());
                }

                GameData.PlayerInv.RemoveItemFromInv(
                    __instance);

                return false;
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Blightcaller: skill book handling failed.",
                    exception);

                return true;
            }
        }

        private static bool IsAllowedSkill(
            string skillName)
        {
            foreach (
                string allowedSkill
                in AllowedSkills)
            {
                if (string.Equals(
                        skillName,
                        allowedSkill,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    // ============================================================
    // ADDITEM COMMAND
    // ============================================================

    [HarmonyPatch(
        typeof(TypeText))]
    internal static class Patch_BlightcallerAddItemCommand
    {
        [HarmonyPatch("CheckCommands")]
        [HarmonyPrefix]
        private static bool CheckCommandsPrefix(
            TypeText __instance)
        {
            if (__instance == null ||
                __instance.typed == null)
            {
                return true;
            }

            string command =
                __instance.typed.text;

            if (string.IsNullOrWhiteSpace(
                    command))
            {
                return true;
            }

            string trimmed =
                command.Trim();

            if (!trimmed.StartsWith(
                    "/additem",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] parts =
                trimmed.Split(
                    new char[]
                    {
                        ' '
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                return true;
            }

            int itemIndex;

            if (!int.TryParse(
                    parts[1],
                    out itemIndex))
            {
                return true;
            }

            try
            {
                if (BlightcallerScrolls.IsCustomScrollId(
                        itemIndex))
                {
                    Item scroll =
                        BlightcallerScrolls.GetScrollById(
                            itemIndex);

                    if (scroll == null)
                    {
                        return true;
                    }

                    AddCustomItem(
                        scroll);

                    return false;
                }

                Item aura =
                    GetAuraItemById(
                        itemIndex);

                if (aura != null)
                {
                    AddCustomItem(
                        aura);

                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: /additem handling failed: " +
                    exception);

                return true;
            }
        }

        private static Item GetAuraItemById(
            int itemIndex)
        {
            switch (itemIndex)
            {
                case 1650:
                    return BlightcallerAuras.GetItem(
                        "withering");

                case 1651:
                    return BlightcallerAuras.GetItem(
                        "pestilent_essence");

                case 1652:
                    return BlightcallerAuras.GetItem(
                        "festering_power");

                case 1653:
                    return BlightcallerAuras.GetItem(
                        "blighted_covenant");

                case 1654:
                    return BlightcallerAuras.GetItem(
                        "eternal_blight");

                default:
                    return null;
            }
        }

        private static void AddCustomItem(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            if (GameData.PlayerInv == null)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: PlayerInv was null while adding custom item.");

                return;
            }

            GameData.PlayerInv.AddItemToInv(
                item);

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    "Adding Blightcaller item to inventory at index " +
                    item.Id +
                    ": " +
                    item.ItemName,
                    ChatLogLine.LogType.SystemMessages,
                    ""));
        }
    }


    // ============================================================
    // LOW-LEVEL SPELL SCROLL DROPS
    // ============================================================

    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerLowLevelDrops
    {
        private const float ScrollDropChance =
            0.05f;

        private const int MaximumMobLevel =
            10;

        [HarmonyPatch("InitLootTable")]
        [HarmonyPostfix]
        private static void InitLootTablePostfix(
            LootTable __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                Character host =
                    __instance.hostChar;

                if (host == null ||
                    !host.isNPC ||
                    host.isVendor ||
                    host.MyStats == null)
                {
                    return;
                }

                int mobLevel =
                    host.MyStats.Level;

                if (mobLevel < 1 ||
                    mobLevel > MaximumMobLevel)
                {
                    return;
                }

                if (UnityEngine.Random.value >= ScrollDropChance)
                {
                    return;
                }

                List<Item> eligibleScrolls =
                    new List<Item>();

                foreach (
                    BlightcallerSpellDefinition definition
                    in BlightcallerSpellData.All)
                {
                    if (definition == null ||
                        definition.HiddenEffect ||
                        definition.Level < 1 ||
                        definition.Level > mobLevel)
                    {
                        continue;
                    }

                    Item scroll =
                        BlightcallerScrolls.GetScroll(
                            definition.Key);

                    if (scroll != null)
                    {
                        eligibleScrolls.Add(
                            scroll);
                    }
                }

                if (eligibleScrolls.Count == 0)
                {
                    return;
                }

                Item selectedScroll =
                    eligibleScrolls[
                        UnityEngine.Random.Range(
                            0,
                            eligibleScrolls.Count)];

                if (selectedScroll == null)
                {
                    return;
                }

                if (__instance.ActualDrops == null)
                {
                    __instance.ActualDrops =
                        new List<Item>();
                }

                __instance.ActualDrops.Add(
                    selectedScroll);

                if (__instance.ActualDropsQual == null)
                {
                    __instance.ActualDropsQual =
                        new List<int>();
                }

                __instance.ActualDropsQual.Add(
                    1);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: loot-table modification failed: " +
                    exception);
            }
        }
    }

    // ============================================================
    // MID-LEVEL SPELL SCROLL DROPS
    // ============================================================

    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerMidLevelDrops
    {
        private const float ScrollDropChance =
            0.05f;

        private const int MinimumMobLevel =
            11;

        private const int MaximumMobLevel =
            20;

        [HarmonyPatch("InitLootTable")]
        [HarmonyPostfix]
        private static void InitLootTablePostfix(
            LootTable __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                Character host =
                    __instance.hostChar;

                if (host == null ||
                    !host.isNPC ||
                    host.isVendor ||
                    host.MyStats == null)
                {
                    return;
                }

                int mobLevel =
                    host.MyStats.Level;

                if (mobLevel < MinimumMobLevel ||
                    mobLevel > MaximumMobLevel)
                {
                    return;
                }

                if (UnityEngine.Random.value >= ScrollDropChance)
                {
                    return;
                }

                List<Item> eligibleScrolls =
                    new List<Item>();

                foreach (
                    BlightcallerSpellDefinition definition
                    in BlightcallerSpellData.All)
                {
                    if (definition == null ||
                        definition.HiddenEffect ||
                        definition.Level < MinimumMobLevel ||
                        definition.Level > MaximumMobLevel ||
                        definition.Level > mobLevel)
                    {
                        continue;
                    }

                    Item scroll =
                        BlightcallerScrolls.GetScroll(
                            definition.Key);

                    if (scroll != null)
                    {
                        eligibleScrolls.Add(
                            scroll);
                    }
                }

                if (eligibleScrolls.Count == 0)
                {
                    return;
                }

                Item selectedScroll =
                    eligibleScrolls[
                        UnityEngine.Random.Range(
                            0,
                            eligibleScrolls.Count)];

                if (selectedScroll == null)
                {
                    return;
                }

                if (__instance.ActualDrops == null)
                {
                    __instance.ActualDrops =
                        new List<Item>();
                }

                __instance.ActualDrops.Add(
                    selectedScroll);

                if (__instance.ActualDropsQual == null)
                {
                    __instance.ActualDropsQual =
                        new List<int>();
                }

                __instance.ActualDropsQual.Add(
                    1);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Blightcaller: mid-level loot-table modification failed.",
                    exception);
            }
        }
    }


    // ============================================================
    // LATE-LEVEL SPELL SCROLL DROPS
    // ============================================================

    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerLateLevelDrops
    {
        private const float ScrollDropChance =
            0.05f;

        private const int MinimumMobLevel =
            21;

        private const int MaximumMobLevel =
            34;

        [HarmonyPatch("InitLootTable")]
        [HarmonyPostfix]
        private static void InitLootTablePostfix(
            LootTable __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                Character host =
                    __instance.hostChar;

                if (host == null ||
                    !host.isNPC ||
                    host.isVendor ||
                    host.MyStats == null)
                {
                    return;
                }

                int mobLevel =
                    host.MyStats.Level;

                if (mobLevel < MinimumMobLevel ||
                    mobLevel > MaximumMobLevel)
                {
                    return;
                }

                if (UnityEngine.Random.value >= ScrollDropChance)
                {
                    return;
                }

                List<Item> eligibleScrolls =
                    new List<Item>();

                foreach (
                    BlightcallerSpellDefinition definition
                    in BlightcallerSpellData.All)
                {
                    if (definition == null ||
                        definition.HiddenEffect ||
                        definition.Level < MinimumMobLevel ||
                        definition.Level > MaximumMobLevel ||
                        definition.Level > mobLevel)
                    {
                        continue;
                    }

                    Item scroll =
                        BlightcallerScrolls.GetScroll(
                            definition.Key);

                    if (scroll != null)
                    {
                        eligibleScrolls.Add(
                            scroll);
                    }
                }

                if (eligibleScrolls.Count == 0)
                {
                    return;
                }

                Item selectedScroll =
                    eligibleScrolls[
                        UnityEngine.Random.Range(
                            0,
                            eligibleScrolls.Count)];

                if (selectedScroll == null)
                {
                    return;
                }

                if (__instance.ActualDrops == null)
                {
                    __instance.ActualDrops =
                        new List<Item>();
                }

                __instance.ActualDrops.Add(
                    selectedScroll);

                if (__instance.ActualDropsQual == null)
                {
                    __instance.ActualDropsQual =
                        new List<int>();
                }

                __instance.ActualDropsQual.Add(
                    1);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Blightcaller: late-level loot-table modification failed.",
                    exception);
            }
        }
    }

    // ============================================================
    // RAID SPELL SCROLL DROPS
    // ============================================================

    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerRaidDrops
    {
        private const float ScrollDropChance =
            0.05f;

        private const int MinimumMobLevel =
            40;

        [HarmonyPatch("InitLootTable")]
        [HarmonyPostfix]
        private static void InitLootTablePostfix(
            LootTable __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                Character host =
                    __instance.hostChar;

                if (host == null ||
                    !host.isNPC ||
                    host.isVendor ||
                    host.MyStats == null)
                {
                    return;
                }

                int mobLevel =
                    host.MyStats.Level;

                if (mobLevel < MinimumMobLevel)
                {
                    return;
                }

                if (UnityEngine.Random.value >= ScrollDropChance)
                {
                    return;
                }

                List<Item> eligibleScrolls =
                    new List<Item>();

                foreach (
                    BlightcallerSpellDefinition definition
                    in BlightcallerSpellData.All)
                {
                    if (definition == null ||
                        definition.HiddenEffect ||
                        definition.Level < MinimumMobLevel ||
                        definition.Level > mobLevel)
                    {
                        continue;
                    }

                    Item scroll =
                        BlightcallerScrolls.GetScroll(
                            definition.Key);

                    if (scroll != null)
                    {
                        eligibleScrolls.Add(
                            scroll);
                    }
                }

                if (eligibleScrolls.Count == 0)
                {
                    return;
                }

                Item selectedScroll =
                    eligibleScrolls[
                        UnityEngine.Random.Range(
                            0,
                            eligibleScrolls.Count)];

                if (selectedScroll == null)
                {
                    return;
                }

                if (__instance.ActualDrops == null)
                {
                    __instance.ActualDrops =
                        new List<Item>();
                }

                __instance.ActualDrops.Add(
                    selectedScroll);

                if (__instance.ActualDropsQual == null)
                {
                    __instance.ActualDropsQual =
                        new List<int>();
                }

                __instance.ActualDropsQual.Add(1);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Blightcaller: raid loot-table modification failed.",
                    exception);
            }
        }
    }

    // ============================================================
    // TIER 5 AURA DROP
    // FERNALLAN HIGH PRIEST
    // ============================================================

    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerFernallenHighPriest
    {
        // TESTING:
        // 1.00f = 100%
        // Final intended value can be changed to 0.25f.
        private const float AuraDropChance =
            0.25f;

        private const string TargetBossName =
            "Fernallan High Priest";

        [HarmonyPatch("InitLootTable")]
        [HarmonyPostfix]
        private static void InitLootTablePostfix(
            LootTable __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                Character host =
                    __instance.hostChar;

                if (host == null)
                {
                    return;
                }

                // Only affect the Fernallan High Priest.
                string objectName =
                    host.gameObject.name;

                if (string.IsNullOrEmpty(objectName))
                {
                    return;
                }

                bool isFernallenHighPriest =
                    objectName.Equals(
                        TargetBossName,
                        StringComparison.OrdinalIgnoreCase) ||
                    objectName.Equals(
                        TargetBossName + "(Clone)",
                        StringComparison.OrdinalIgnoreCase);

                if (!isFernallenHighPriest)
                {
                    return;
                }

                if (UnityEngine.Random.value >= AuraDropChance)
                {
                    return;
                }

                Item eternalBlight =
                    BlightcallerAuras.GetItem(
                        "eternal_blight");

                if (eternalBlight == null)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: Eternal Blight aura item was unavailable.");

                    return;
                }

                if (__instance.ActualDrops == null)
                {
                    __instance.ActualDrops =
                        new List<Item>();
                }

                if (__instance.ActualDrops.Contains(
                        eternalBlight))
                {
                    return;
                }

                __instance.ActualDrops.Add(
                    eternalBlight);

                if (__instance.ActualDropsQual == null)
                {
                    __instance.ActualDropsQual =
                        new List<int>();
                }

                __instance.ActualDropsQual.Add(1);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Blightcaller: Fernallan High Priest loot modification failed.",
                    exception);
            }
        }
    }
    // ============================================================
    // AURA HIERARCHY
    // ============================================================

    [HarmonyPatch(
        typeof(Stats),
        "CheckForHigherLevelSE",
        new Type[]
        {
            typeof(Spell)
        })]
    internal static class Patch_BlightcallerAuraSpellLine
    {
        private const string BlightcallerAuraPrefix =
            "ARCBLC_AURA_";

        private static bool Prefix(
            Stats __instance,
            Spell spell,
            ref bool __result)
        {
            if (__instance == null)
            {
                return true;
            }

            if (!IsBlightcallerAura(
                    spell))
            {
                return true;
            }

            if (__instance.StatusEffects == null)
            {
                __result = false;
                return false;
            }

            try
            {
                foreach (
                    StatusEffect statusEffect
                    in __instance.StatusEffects)
                {
                    if (statusEffect == null)
                    {
                        continue;
                    }

                    Spell existingSpell =
                        statusEffect.Effect;

                    if (existingSpell == null)
                    {
                        continue;
                    }

                    if (!IsBlightcallerAura(
                            existingSpell))
                    {
                        continue;
                    }

                    if (existingSpell.RequiredLevel >
                        spell.RequiredLevel)
                    {
                        __result = true;
                        return false;
                    }
                }

                __result = false;
                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: aura hierarchy check failed: " +
                    exception);

                return true;
            }
        }

        private static bool IsBlightcallerAura(
            Spell spell)
        {
            if (spell == null ||
                string.IsNullOrEmpty(
                    spell.Id))
            {
                return false;
            }

            return spell.Id.StartsWith(
                BlightcallerAuraPrefix,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [HarmonyPatch(
        typeof(VendorInventory),
        "Start")]
    internal static class Patch_BlightcallerAmandaRostleyVendor
    {
        private const string VendorName =
            "Amanda Rostley";

        private const string FirstAuraKey =
            "withering";

        private static void Postfix(
            VendorInventory __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                if (!IsAmandaRostley(__instance))
                {
                    return;
                }

                Item auraItem =
                    BlightcallerAuras.GetItem(
                        FirstAuraKey);

                if (auraItem == null)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller aura vendor: Aura of Withering is not registered.");

                    return;
                }

                if (__instance.ItemsForSale == null)
                {
                    __instance.ItemsForSale =
                        new List<Item>();
                }

                if (__instance.ItemsForSale.Contains(
                        auraItem))
                {
                    return;
                }

                __instance.ItemsForSale.Add(
                    auraItem);
            }
            catch (Exception ex)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller aura vendor setup failed: " +
                    ex);
            }
        }

        private static bool IsAmandaRostley(
            VendorInventory vendor)
        {
            Transform current =
                vendor.transform;

            while (current != null)
            {
                if (string.Equals(
                        current.name,
                        VendorName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current =
                    current.parent;
            }

            return false;
        }
    }
}