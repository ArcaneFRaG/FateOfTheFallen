using HarmonyLib;
using System;
using System.Collections.Generic;

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
                    GameData.GM.PlayerSpells.KnownSpells == null ||
                    GameData.GM.PlayerSkills == null ||
                    GameData.GM.PlayerSkills.MyAscensions == null)
                {
                    return true;
                }


                // ============================================================
                // RESTORE KNOWN SPELLS
                // ============================================================

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

                        if (spell.RequiredLevel > 0 &&
                            !GameData.GM.PlayerSpells.KnownSpells.Contains(
                                spell))
                        {
                            GameData.GM.PlayerSpells.KnownSpells.Add(
                                spell);
                        }
                    }
                }


                // ============================================================
                // RESTORE KNOWN SKILLS
                // ============================================================

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


                // ============================================================
                // RESTORE ASCENSIONS
                // ============================================================
                //
                // IMPORTANT:
                //
                // The previous implementation appended every saved Ascension
                // entry to MyAscensions every time LoadSpellsAndSkills ran.
                //
                // Repeated world loads / logouts could therefore grow the
                // runtime Ascension list indefinitely.
                //
                // AAScreen/GetRank also searches this list, so a bloated list
                // causes increasingly expensive Ascension-tab refreshes.
                //
                // Rebuild the runtime list from the save data instead.
                // While doing so, deduplicate by Ascension ID so an already
                // bloated save cannot reproduce the leak.
                // ============================================================

                int runtimeCountBefore =
                    GameData.GM.PlayerSkills.MyAscensions.Count;

                int savedCount =
                    GameData.CurrentCharacterSlot.Ascensions != null
                        ? GameData.CurrentCharacterSlot.Ascensions.Count
                        : 0;

                Dictionary<string, int> ascensionLevels =
                    new Dictionary<string, int>(
                        StringComparer.Ordinal);

                if (GameData.CurrentCharacterSlot.Ascensions != null)
                {
                    foreach (
                        AscensionSkillEntry ascension
                        in GameData.CurrentCharacterSlot.Ascensions)
                    {
                        if (ascension == null ||
                            string.IsNullOrEmpty(
                                ascension.id))
                        {
                            continue;
                        }

                        int existingLevel;

                        if (ascensionLevels.TryGetValue(
                                ascension.id,
                                out existingLevel))
                        {
                            /*
                             * If a corrupted/bloated save contains the same
                             * Ascension more than once, keep the highest rank.
                             */
                            if (ascension.level > existingLevel)
                            {
                                ascensionLevels[ascension.id] =
                                    ascension.level;
                            }

                            continue;
                        }

                        ascensionLevels.Add(
                            ascension.id,
                            ascension.level);
                    }
                }

                // ============================================================
                // REPAIR THE SAVE-SLOT ASCENSION LIST
                // ============================================================
                //
                // The save itself may already contain millions of duplicate
                // entries. Rebuilding only MyAscensions fixes runtime behaviour,
                // but leaves the enormous saved list alive in memory and ready
                // to be serialized again.
                //
                // Collapse the current save-slot list to one entry per ID too.
                // TrimExcess() is important here: Clear() alone would retain the
                // huge backing array/capacity from the corrupted list.
                // ============================================================

                if (GameData.CurrentCharacterSlot.Ascensions != null)
                {
                    GameData.CurrentCharacterSlot.Ascensions.Clear();

                    foreach (
                        KeyValuePair<string, int> ascension
                        in ascensionLevels)
                    {
                        GameData.CurrentCharacterSlot.Ascensions.Add(
                            new AscensionSkillEntry(
                                ascension.Key,
                                ascension.Value));
                    }

                    GameData.CurrentCharacterSlot.Ascensions.TrimExcess();
                }


                // ============================================================
                // REBUILD RUNTIME ASCENSIONS
                // ============================================================

                GameData.GM.PlayerSkills.MyAscensions.Clear();

                foreach (
                    KeyValuePair<string, int> ascension
                    in ascensionLevels)
                {
                    GameData.GM.PlayerSkills.MyAscensions.Add(
                        new AscensionSkillEntry(
                            ascension.Key,
                            ascension.Value));
                }

                int runtimeCountAfter =
                    GameData.GM.PlayerSkills.MyAscensions.Count;

                int repairedSavedCount =
                    GameData.CurrentCharacterSlot.Ascensions != null
                        ? GameData.CurrentCharacterSlot.Ascensions.Count
                        : 0;

                /*
                 * Diagnostic warning:
                 *
                 * Log only when corruption was actually repaired.
                 */
                if (savedCount > repairedSavedCount)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller: repaired persisted Ascension duplication. " +
                        "Runtime before=" +
                        runtimeCountBefore +
                        ", saved before=" +
                        savedCount +
                        ", saved after=" +
                        repairedSavedCount +
                        ", runtime after=" +
                        runtimeCountAfter +
                        ".");
                }


                // ============================================================
                // ASCENSION POINTS
                // ============================================================

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
