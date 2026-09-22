using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
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
}
