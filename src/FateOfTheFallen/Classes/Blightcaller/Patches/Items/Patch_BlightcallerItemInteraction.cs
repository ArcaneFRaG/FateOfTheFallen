using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FateOfTheFallen
{
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
}
