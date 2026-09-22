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
}
