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
}
