using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(SkillDB), "Start")]
    internal static class Patch_LightbringerSkillDB
    {
        private static void Postfix(SkillDB __instance) { LightbringerSkillCatalog.Register(__instance); LightbringerAscensions.Register(); }
    }

    [HarmonyPatch(typeof(SkillDB), "GetSkillByID")]
    internal static class Patch_LightbringerSkillLookup
    {
        private static bool Prefix(string __0, ref Skill __result)
        {
            if (string.IsNullOrEmpty(__0) || !__0.StartsWith("FOTF_LB_SKILL_", StringComparison.OrdinalIgnoreCase))
                return true;
            Skill skill = LightbringerSkillCatalog.GetById(__0);
            if (skill == null) { LightbringerSkillCatalog.Register(GameData.SkillDatabase); skill = LightbringerSkillCatalog.GetById(__0); }
            if (skill == null) return true;
            __result = skill;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemInfoWindow), "DisplayItem", new Type[] { typeof(Item), typeof(Vector2), typeof(int) })]
    internal static class Patch_LightbringerItemTooltip
    {
        private static void Postfix(Item item, ItemInfoWindow __instance)
        {
            if (item == null || __instance == null) return;
            if (!LightbringerNativeHealing.CanUseItem(item)) return;
            // Also cover late-registered custom Druid-equippable loot.
            LightbringerNativeHealing.EnsureItemCompatibility(item);
            if (__instance.Usable != null &&
                (__instance.Usable.text ?? string.Empty).IndexOf("Lightbringer", StringComparison.OrdinalIgnoreCase) < 0)
                __instance.Usable.text += " Lightbringer ";
            if (item.TeachSkill == null || __instance.ReqLvl == null) return;
            int level = LightbringerSkillLearning.GetRequiredLevel(item.TeachSkill);
            TextMeshProUGUI label = __instance.ReqLvl.GetComponent<TextMeshProUGUI>();
            if (label != null && level > 0 &&
                (label.text ?? string.Empty).IndexOf("Lightbringer:", StringComparison.OrdinalIgnoreCase) < 0)
                label.text += "\nLightbringer: " + level;
        }
    }

    internal static class LightbringerSkillLearning
    {
        internal static int GetRequiredLevel(Skill skill)
        {
            if (skill == null) return 0;
            LightbringerSkillDefinition definition;
            if (LightbringerSkillCatalog.TryGet(skill.Id, out definition))
                return definition.RequiredLevel;
            // Native all-class skillbooks use Druid's native minimum level.
            int druid = ReadLevel(skill, "DruidRequiredLevel");
            if (druid > 0) return druid;
            // Only used for a book already verified to be native all-class.
            foreach (string name in new[] { "ArcanistRequiredLevel", "PaladinRequiredLevel",
                "ReaverRequiredLevel", "DuelistRequiredLevel", "StormcallerRequiredLevel" })
            {
                int n = ReadLevel(skill, name);
                if (n > 0) return n;
            }
            return 0;
        }

        private static int ReadLevel(object skill, string name)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = skill.GetType().GetField(name, flags);
            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(skill);
            PropertyInfo property = skill.GetType().GetProperty(name, flags);
            if (property != null && property.PropertyType == typeof(int) && property.CanRead)
                return (int)property.GetValue(skill, null);
            return 0;
        }
    }

    [HarmonyPatch(typeof(ItemIcon), "OnPointerUp")]
    internal static class Patch_LightbringerSkillbookLearning
    {
        private static bool Prefix(ItemIcon __instance, PointerEventData eventData)
        {
            if (__instance == null || __instance.MyItem == null ||
                __instance.MyItem.TeachSkill == null ||
                !LightbringerCatalog.IsCurrentPlayerLightbringer())
                return true;
            Item item = __instance.MyItem;
            if (!LightbringerNativeHealing.IsUniversalSkillbook(item) &&
                !LightbringerSkillCatalog.IsLightbringerSkill(item.TeachSkill))
                return true;
            if (GameData.PlayerStats == null || GameData.PlayerStats.Myself == null ||
                GameData.PlayerStats.Myself.MySkills == null)
                return true;
            try
            {
                int required = LightbringerSkillLearning.GetRequiredLevel(item.TeachSkill);
                if (required <= 0) return true;
                if (GameData.PlayerStats.Level < required)
                {
                    UpdateSocialLog.LogAdd(new ChatLogLine("You are not experienced enough to learn this skill yet...",
                        ChatLogLine.LogType.SystemMessages, "yellow"));
                    return false;
                }
                UseSkill skills = GameData.PlayerStats.Myself.MySkills;
                if (skills.KnownSkills == null) return true;
                foreach (Skill existing in skills.KnownSkills)
                    if (existing != null && existing.Id == item.TeachSkill.Id) return false;
                skills.KnownSkills.Add(item.TeachSkill);
                if (GameData.GM != null && GameData.GM.PlayerSkills != null &&
                    GameData.GM.PlayerSkills.KnownSkills != null &&
                    !GameData.GM.PlayerSkills.KnownSkills.Contains(item.TeachSkill))
                    GameData.GM.PlayerSkills.KnownSkills.Add(item.TeachSkill);
                UpdateSocialLog.LogAdd(new ChatLogLine("Learned skill: " + item.TeachSkill.SkillName,
                    ChatLogLine.LogType.SystemMessages, "lightblue"));
                try { skills.LearnSkill.Play(); } catch { }
                if (GameData.PlayerSkillBook != null && GameData.PlayerSkillBook.Skillbook.activeSelf)
                    GameData.PlayerSkillBook.UpdateSkillList(GameData.PlayerSkillBook.GetPage());
                if (GameData.PlayerInv != null) GameData.PlayerInv.RemoveItemFromInv(__instance);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("[Lightbringer] Skillbook learning failed: " + ex);
                return true;
            }
        }
    }
    [HarmonyPatch(typeof(CharSelectManager), "LoadSpellsAndSkills")]
    internal static class Patch_LightbringerRestoreLearnedSkills
    {
        private static void Postfix()
        {
            if (!LightbringerCharacterState.IsLightbringer(GameData.CurrentCharacterSlot) ||
                GameData.CurrentCharacterSlot.CharacterSkills == null ||
                GameData.GM == null || GameData.GM.PlayerSkills == null ||
                GameData.GM.PlayerSkills.KnownSkills == null)
                return;

            LightbringerSkillCatalog.Register(GameData.SkillDatabase);
            foreach (string id in GameData.CurrentCharacterSlot.CharacterSkills)
            {
                LightbringerSkillDefinition def;
                if (!LightbringerSkillCatalog.TryGet(id, out def) || def.RuntimeSkill == null)
                    continue;
                bool already = false;
                foreach (Skill known in GameData.GM.PlayerSkills.KnownSkills)
                    if (known != null && known.Id == id) { already = true; break; }
                if (!already) GameData.GM.PlayerSkills.KnownSkills.Add(def.RuntimeSkill);
            }
        }
    }
}
