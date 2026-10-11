using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(CharSelectManager),
        "LoadHotkeys")]
    internal static class Patch_BlightcallerLoadHotkeys
    {
        private static bool _loggedForeignSpellPostfixFailure;


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

                EnsureSecondarySaveArrays();

                // ====================================================
                // PRIMARY ACTIONBAR
                // ====================================================

                for (int i = 0;
                     i < 10;
                     i++)
                {
                    Hotkeys hotkey =
                        GetPrimaryHotkey(
                            i);

                    if (hotkey == null)
                    {
                        continue;
                    }

                    PrepareHotkeyVisuals(
                        hotkey);

                    RestoreSlot(
                        hotkey,
                        i,
                        false);
                }


                // ====================================================
                // SECONDARY ACTIONBAR
                // ====================================================

                for (int i = 0;
                     i < 10;
                     i++)
                {
                    Hotkeys hotkey =
                        GetSecondaryHotkey(
                            i);

                    if (hotkey == null)
                    {
                        continue;
                    }

                    /*
                     * Secondary slots stay inactive during restoration.
                     *
                     * We explicitly prepare all child Image materials before
                     * assigning content. This allows UI/Harmony extensions
                     * which inspect icon.material during Assign* postfixes to
                     * operate without requiring SetActive(true).
                     */
                    PrepareHotkeyVisuals(
                        hotkey);

                    RestoreSlot(
                        hotkey,
                        i,
                        true);
                }


                // ====================================================
                // PRIMARY BAR IS THE LOGIN DEFAULT
                // ====================================================

                ForcePrimaryBarVisible();

                return false;
            }
            catch (Exception exception)
            {
                /*
                 * This is a Blightcaller save. Do not return true here.
                 *
                 * Returning true would make native LoadHotkeys run after our
                 * partial custom restore and can invoke the same foreign
                 * Harmony patches a second time, while also failing to resolve
                 * custom Blightcaller spell IDs correctly.
                 */
                Plugin.NativeLog.LogError(
                    "Blightcaller: unexpected hotbar restoration failure. " +
                    "Native fallback suppressed to avoid duplicate assignment: " +
                    exception);

                return false;
            }
        }


        // ============================================================
        // SAVE ARRAY INITIALIZATION
        // ============================================================

        private static void EnsureSecondarySaveArrays()
        {
            if (GameData.CurrentCharacterSlot.SecHKSpells == null)
            {
                GameData.CurrentCharacterSlot.SecHKSpells =
                    new string[10];
            }

            if (GameData.CurrentCharacterSlot.SecHKSkills == null)
            {
                GameData.CurrentCharacterSlot.SecHKSkills =
                    new string[10];
            }

            if (GameData.CurrentCharacterSlot.SecHKItems == null)
            {
                GameData.CurrentCharacterSlot.SecHKItems =
                    new int[10];

                for (int i = 0;
                     i < 10;
                     i++)
                {
                    GameData.CurrentCharacterSlot.SecHKItems[i] =
                        -1;
                }
            }
        }


        // ============================================================
        // SLOT RESTORATION
        // ============================================================

        private static void RestoreSlot(
            Hotkeys hotkey,
            int index,
            bool secondary)
        {
            if (hotkey == null ||
                index < 0 ||
                index >= 10)
            {
                return;
            }

            string[] spellIds =
                secondary
                    ? GameData.CurrentCharacterSlot.SecHKSpells
                    : GameData.CurrentCharacterSlot.HKSpells;

            string[] skillIds =
                secondary
                    ? GameData.CurrentCharacterSlot.SecHKSkills
                    : GameData.CurrentCharacterSlot.HKSkills;

            int[] itemIndices =
                secondary
                    ? GameData.CurrentCharacterSlot.SecHKItems
                    : GameData.CurrentCharacterSlot.HKItems;


            // --------------------------------------------------------
            // SPELL
            // --------------------------------------------------------

            if (spellIds != null &&
                spellIds.Length > index)
            {
                string spellId =
                    spellIds[index];

                if (!string.IsNullOrEmpty(
                        spellId))
                {
                    Spell spell =
                        GetSpellBySavedId(
                            spellId);

                    if (spell != null)
                    {
                        AssignSpellSafely(
                            hotkey,
                            spell,
                            secondary,
                            index);
                    }
                }
            }


            // --------------------------------------------------------
            // SKILL
            // --------------------------------------------------------

            if (skillIds != null &&
                skillIds.Length > index &&
                GameData.SkillDatabase != null)
            {
                string skillId =
                    skillIds[index];

                if (!string.IsNullOrEmpty(
                        skillId))
                {
                    Skill skill =
                        GameData.SkillDatabase.GetSkillByID(
                            skillId);

                    if (skill != null)
                    {
                        AssignSkillSafely(
                            hotkey,
                            skill,
                            secondary,
                            index);
                    }
                }
            }


            // --------------------------------------------------------
            // ITEM
            // --------------------------------------------------------

            if (itemIndices != null &&
                itemIndices.Length > index)
            {
                int inventoryIndex =
                    itemIndices[index];

                if (inventoryIndex >= 0 &&
                    GameData.PlayerInv != null &&
                    GameData.PlayerInv.ALLSLOTS != null &&
                    inventoryIndex <
                        GameData.PlayerInv.ALLSLOTS.Count)
                {
                    AssignItemSafely(
                        hotkey,
                        inventoryIndex,
                        secondary,
                        index);
                }
            }
        }


        // ============================================================
        // THIRD-PARTY-HARMONY-SAFE ASSIGNMENT
        // ============================================================

        private static void AssignSpellSafely(
            Hotkeys hotkey,
            Spell spell,
            bool secondary,
            int index)
        {
            if (hotkey == null ||
                spell == null)
            {
                return;
            }

            PrepareHotkeyVisuals(
                hotkey);

            try
            {
                hotkey.AssignSpellFromBook(
                    spell);
            }
            catch (ArgumentNullException exception)
            {
                /*
                 * Harmony postfixes execute after the original native method.
                 *
                 * The observed ErenshorDifficultyMod failure occurs inside its
                 * AssignSpellFromBook Postfix while constructing a Material:
                 *
                 *     new Material(icon.material)
                 *
                 * By the time that postfix throws, Erenshor's original
                 * AssignSpellFromBook has already completed the hotkey
                 * assignment. Therefore swallowing this specific downstream
                 * exception is safer than aborting the whole bar and invoking
                 * native LoadHotkeys again.
                 */
                if (string.Equals(
                        exception.ParamName,
                        "source",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!_loggedForeignSpellPostfixFailure)
                    {
                        _loggedForeignSpellPostfixFailure =
                            true;

                        Plugin.NativeLog.LogWarning(
                            "Blightcaller: a downstream Harmony postfix threw " +
                            "ArgumentNullException(source) after " +
                            "Hotkeys.AssignSpellFromBook. The native assignment " +
                            "already completed, so FOTF is continuing the " +
                            "hotbar restore without invoking native LoadHotkeys again.");
                    }

                    return;
                }

                LogSlotAssignmentFailure(
                    "spell",
                    spell.SpellName,
                    secondary,
                    index,
                    exception);
            }
            catch (Exception exception)
            {
                LogSlotAssignmentFailure(
                    "spell",
                    spell.SpellName,
                    secondary,
                    index,
                    exception);
            }
        }


        private static void AssignSkillSafely(
            Hotkeys hotkey,
            Skill skill,
            bool secondary,
            int index)
        {
            if (hotkey == null ||
                skill == null)
            {
                return;
            }

            PrepareHotkeyVisuals(
                hotkey);

            try
            {
                hotkey.AssignSkillFromBook(
                    skill);
            }
            catch (Exception exception)
            {
                LogSlotAssignmentFailure(
                    "skill",
                    skill.SkillName,
                    secondary,
                    index,
                    exception);
            }
        }


        private static void AssignItemSafely(
            Hotkeys hotkey,
            int inventoryIndex,
            bool secondary,
            int index)
        {
            try
            {
                if (GameData.PlayerInv == null ||
                    GameData.PlayerInv.ALLSLOTS == null ||
                    inventoryIndex < 0 ||
                    inventoryIndex >=
                        GameData.PlayerInv.ALLSLOTS.Count)
                {
                    return;
                }

                PrepareHotkeyVisuals(
                    hotkey);

                hotkey.AssignItemFrominv(
                    GameData.PlayerInv.ALLSLOTS[
                        inventoryIndex]);
            }
            catch (Exception exception)
            {
                LogSlotAssignmentFailure(
                    "item",
                    "inventory index " +
                    inventoryIndex,
                    secondary,
                    index,
                    exception);
            }
        }


        // ============================================================
        // UI MATERIAL COMPATIBILITY
        // ============================================================

        private static void PrepareHotkeyVisuals(
            Hotkeys hotkey)
        {
            if (hotkey == null)
            {
                return;
            }

            try
            {
                Image[] images =
                    hotkey.GetComponentsInChildren<Image>(
                        true);

                if (images == null)
                {
                    return;
                }

                for (int i = 0;
                     i < images.Length;
                     i++)
                {
                    Image image =
                        images[i];

                    if (image == null)
                    {
                        continue;
                    }

                    /*
                     * Some UI extensions clone image.material in an
                     * AssignSpellFromBook postfix. Inactive secondary hotkeys
                     * can legitimately still have a null explicit material at
                     * this stage, so seed it with Unity UI's default material.
                     */
                    if (image.material == null)
                    {
                        Material fallback =
                            image.defaultMaterial;

                        if (fallback == null)
                        {
                            fallback =
                                Graphic.defaultGraphicMaterial;
                        }

                        if (fallback != null)
                        {
                            image.material =
                                fallback;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogDebug(
                    "Blightcaller: hotkey visual preparation skipped: " +
                    exception.Message);
            }
        }


        // ============================================================
        // HOTKEY LOOKUP
        // ============================================================

        private static Hotkeys GetPrimaryHotkey(
            int index)
        {
            if (GameData.GM == null ||
                GameData.GM.HKManager == null ||
                GameData.GM.HKManager.FirstHotkeys == null ||
                index < 0 ||
                GameData.GM.HKManager.FirstHotkeys.Count <= index)
            {
                return null;
            }

            return
                GameData.GM.HKManager
                    .FirstHotkeys[index];
        }


        private static Hotkeys GetSecondaryHotkey(
            int index)
        {
            if (GameData.GM == null ||
                GameData.GM.HKManager == null ||
                GameData.GM.HKManager.SecondHotkeys == null ||
                index < 0 ||
                GameData.GM.HKManager.SecondHotkeys.Count <= index)
            {
                return null;
            }

            return
                GameData.GM.HKManager
                    .SecondHotkeys[index];
        }


        // ============================================================
        // BAR VISIBILITY
        // ============================================================

        private static void ForcePrimaryBarVisible()
        {
            for (int i = 0;
                 i < 10;
                 i++)
            {
                Hotkeys primary =
                    GetPrimaryHotkey(
                        i);

                if (primary != null)
                {
                    primary.gameObject.SetActive(
                        true);
                }

                Hotkeys secondary =
                    GetSecondaryHotkey(
                        i);

                if (secondary != null)
                {
                    secondary.gameObject.SetActive(
                        false);
                }
            }
        }


        // ============================================================
        // SPELL LOOKUP
        // ============================================================

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

            return
                BlightcallerCatalog.GetSpellById(
                    spellId);
        }


        // ============================================================
        // LOGGING
        // ============================================================

        private static void LogSlotAssignmentFailure(
            string contentType,
            string contentName,
            bool secondary,
            int index,
            Exception exception)
        {
            Plugin.NativeLog.LogWarning(
                "Blightcaller: failed to restore " +
                contentType +
                " [" +
                (contentName ?? "<unknown>") +
                "] to " +
                (secondary
                    ? "secondary"
                    : "primary") +
                " hotbar slot [" +
                (index + 1) +
                "]: " +
                exception);
        }
    }
}
