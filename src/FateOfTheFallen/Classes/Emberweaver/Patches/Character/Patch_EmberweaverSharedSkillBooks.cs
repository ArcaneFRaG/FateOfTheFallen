using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    /// <summary>
    /// Native skillbook learning only understands Erenshor's native classes.
    /// Emberweaver deliberately shares Arcane Proficiency, Multifocus, and
    /// Arcane Recovery with the Arcanist/Blightcaller caster archetype.
    /// Handle those native skillbooks explicitly instead of making
    /// Emberweaver eligible for every Arcanist skill.
    /// </summary>
    [HarmonyPatch(
        typeof(ItemIcon),
        "OnPointerUp")]
    internal static class Patch_EmberweaverSharedSkillBooks
    {
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
                !EmberweaverCatalog.IsEmberweaverClass(
                    GameData.PlayerStats.CharacterClass))
            {
                return true;
            }

            Skill skill =
                __instance.MyItem.TeachSkill;

            if (!ArcanistDerivedCompatibility
                    .IsApprovedSharedSkill(
                        skill))
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

                if (useSkill == null ||
                    useSkill.KnownSkills == null)
                {
                    return true;
                }

                if (ContainsSkill(
                        useSkill.KnownSkills,
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

                if (GameData.PlayerStats.Myself != null &&
                    GameData.PlayerStats.Myself.MySkills != null &&
                    GameData.PlayerStats.Myself.MySkills.KnownSkills != null &&
                    !ContainsSkill(
                        GameData.PlayerStats.Myself.MySkills.KnownSkills,
                        skill))
                {
                    GameData.PlayerStats.Myself.MySkills.KnownSkills.Add(
                        skill);
                }

                if (GameData.GM != null &&
                    GameData.GM.PlayerSkills != null &&
                    GameData.GM.PlayerSkills.KnownSkills != null &&
                    !ContainsSkill(
                        GameData.GM.PlayerSkills.KnownSkills,
                        skill))
                {
                    GameData.GM.PlayerSkills.KnownSkills.Add(
                        skill);
                }

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Learned skill: " +
                        skill.SkillName,
                        ChatLogLine.LogType.SystemMessages,
                        "lightblue"));

                try
                {
                    useSkill.LearnSkill.Play();
                }
                catch
                {
                }

                try
                {
                    GameData.PlayerAud.PlayOneShot(
                        GameData.Misc.NewSkill,
                        0.6f *
                        GameData.SFXVol *
                        GameData.MasterVol);
                }
                catch
                {
                }

                if (GameData.PlayerSkillBook != null &&
                    GameData.PlayerSkillBook.Skillbook.activeSelf)
                {
                    GameData.PlayerSkillBook.UpdateSkillList(
                        GameData.PlayerSkillBook.GetPage());
                }

                if (GameData.HKMngr != null &&
                    GameData.HKMngr.FindBlankHotkey() != null)
                {
                    GameData.HKMngr.AssignNewSkillToHK(
                        skill,
                        GameData.HKMngr.FindBlankHotkey());
                }

                if (GameData.PlayerInv != null)
                {
                    GameData.PlayerInv.RemoveItemFromInv(
                        __instance);
                }

                return false;
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: shared Arcanist skillbook handling failed",
                    exception);

                return true;
            }
        }

        private static bool ContainsSkill(
            IList<Skill> skills,
            Skill wanted)
        {
            if (skills == null ||
                wanted == null)
            {
                return false;
            }

            for (int i = 0;
                 i < skills.Count;
                 i++)
            {
                Skill existing =
                    skills[i];

                if (existing == wanted)
                {
                    return true;
                }

                if (existing == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(
                        existing.Id) &&
                    !string.IsNullOrEmpty(
                        wanted.Id) &&
                    string.Equals(
                        existing.Id,
                        wanted.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
