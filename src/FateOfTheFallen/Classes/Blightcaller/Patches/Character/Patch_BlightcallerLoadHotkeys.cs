using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
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

                // ========================================================
                // ENSURE SECONDARY HOTBAR SAVE ARRAYS EXIST
                // ========================================================

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

                // ========================================================
                // RESTORE HOTBAR CONTENTS
                // ========================================================

                for (int i = 0; i <= 9; i++)
                {
                    // ====================================================
                    // PRIMARY ACTIONBAR
                    // ====================================================

                    if (GameData.CurrentCharacterSlot.HKSpells.Length > i &&
                        GameData.GM.HKManager.FirstHotkeys != null &&
                        GameData.GM.HKManager.FirstHotkeys.Count > i &&
                        GameData.GM.HKManager.FirstHotkeys[i] != null)
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
                                    GameData.SkillDatabase != null
                                        ? GameData.SkillDatabase.GetSkillByID(
                                            skillId)
                                        : null;

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
                            int inventoryIndex =
                                GameData.CurrentCharacterSlot.HKItems[i];

                            if (inventoryIndex != -1 &&
                                GameData.PlayerInv != null &&
                                GameData.PlayerInv.ALLSLOTS != null &&
                                inventoryIndex >= 0 &&
                                inventoryIndex <
                                GameData.PlayerInv.ALLSLOTS.Count)
                            {
                                GameData.GM.HKManager
                                    .FirstHotkeys[i]
                                    .AssignItemFrominv(
                                        GameData.PlayerInv.ALLSLOTS[
                                            inventoryIndex]);
                            }
                        }
                    }

                    // ====================================================
                    // SECONDARY ACTIONBAR
                    // ====================================================

                    if (GameData.CurrentCharacterSlot.SecHKSpells.Length > i &&
                        GameData.GM.HKManager.SecondHotkeys != null &&
                        GameData.GM.HKManager.SecondHotkeys.Count > i &&
                        GameData.GM.HKManager.SecondHotkeys[i] != null)
                    {
                        /*
                         * IMPORTANT:
                         *
                         * Do NOT temporarily activate the secondary
                         * hotkey GameObjects while restoring them.
                         *
                         * Previously we did:
                         *
                         *     SetActive(true)
                         *     restore slot
                         *     SetActive(false)
                         *
                         * That bypassed the native actionbar state
                         * handling and could result in actionbar 2
                         * becoming the selected bar after login.
                         */

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
                                    GameData.SkillDatabase != null
                                        ? GameData.SkillDatabase.GetSkillByID(
                                            skillId)
                                        : null;

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
                            int inventoryIndex =
                                GameData.CurrentCharacterSlot.SecHKItems[i];

                            if (inventoryIndex != -1 &&
                                GameData.PlayerInv != null &&
                                GameData.PlayerInv.ALLSLOTS != null &&
                                inventoryIndex >= 0 &&
                                inventoryIndex <
                                GameData.PlayerInv.ALLSLOTS.Count)
                            {
                                GameData.GM.HKManager
                                    .SecondHotkeys[i]
                                    .AssignItemFrominv(
                                        GameData.PlayerInv.ALLSLOTS[
                                            inventoryIndex]);
                            }
                        }
                    }
                }

                // ========================================================
                // FORCE PRIMARY ACTIONBAR AS DEFAULT AFTER LOGIN
                // ========================================================

                /*
                 * Because Blightcaller replaces the native LoadHotkeys()
                 * implementation, we must also establish the expected
                 * visual/default actionbar state ourselves.
                 *
                 * Bar 1:
                 *     visible / active
                 *
                 * Bar 2:
                 *     hidden / inactive
                 */

                for (int i = 0; i <= 9; i++)
                {
                    if (GameData.GM.HKManager.FirstHotkeys != null &&
                        GameData.GM.HKManager.FirstHotkeys.Count > i &&
                        GameData.GM.HKManager.FirstHotkeys[i] != null)
                    {
                        GameData.GM.HKManager
                            .FirstHotkeys[i]
                            .gameObject
                            .SetActive(true);
                    }

                    if (GameData.GM.HKManager.SecondHotkeys != null &&
                        GameData.GM.HKManager.SecondHotkeys.Count > i &&
                        GameData.GM.HKManager.SecondHotkeys[i] != null)
                    {
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

            return BlightcallerCatalog.GetSpellById(
                spellId);
        }
    }
}