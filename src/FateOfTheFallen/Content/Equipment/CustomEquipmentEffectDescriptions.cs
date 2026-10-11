using HarmonyLib;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Central presentation layer for FOTF item procs/equip effects.
    ///
    /// Each proc-bearing item already owns a unique custom Spell object.  r23
    /// turns those tooltip proxies into complete presentation definitions so
    /// Erenshor's native right-click spell panel no longer shows level 0,
    /// missing icons or an unrelated default spell line.  The complete effect
    /// explanation is also injected into the right-click detail body beneath
    /// the native Cast Time line, rather than polluting Item.Lore.
    ///
    /// Combat remains owned by the dedicated equipment-effect runtimes.  These
    /// presentation spells are tooltip carriers only where the native proc
    /// trigger does not match the real custom trigger.
    /// </summary>
    internal static class CustomEquipmentEffectDescriptions
    {
        private const string LegacyMarker = "\n\nEquip Effect: ";
        internal const string DetailMarker = "\n\n<color=#7CFF8C>PROC EFFECT</color>\n";

        internal static void Apply(Item item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id))
                return;

            StripLegacyLoreInjection(item);

            string description = GetDescription(item.Id);
            if (string.IsNullOrEmpty(description))
                return;

            if (item.WeaponProcOnHit != null)
                ConfigurePresentationSpell(item, item.WeaponProcOnHit, description);

            if (item.WandEffect != null)
                ConfigurePresentationSpell(item, item.WandEffect, description);

            if (item.BowEffect != null)
                ConfigurePresentationSpell(item, item.BowEffect, description);
        }

        internal static string GetDescriptionForItem(Item item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id))
                return null;

            return GetDescription(item.Id);
        }

        private static void ConfigurePresentationSpell(
            Item item,
            Spell spell,
            string description)
        {
            if (item == null || spell == null)
                return;

            // Actual combat proc spells (for example Moldermaw's three native
            // CheckProc spells and Fallen Spark) already have authoritative
            // gameplay fields.  Never rewrite their damage/aggro/line/cooldown
            // merely because a tooltip was opened.  Only tooltip-only proxy
            // spells need the full synthetic presentation definition below.
            bool tooltipOnly =
                !string.IsNullOrEmpty(spell.Id) &&
                spell.Id.StartsWith(
                    "FOTF_TOOLTIP_",
                    StringComparison.OrdinalIgnoreCase);

            spell.SpellDesc = description;
            if (spell.SpellIcon == null)
                spell.SpellIcon = item.ItemIcon;

            if (!tooltipOnly)
                return;

            // Shared sane defaults.  These eliminate the uninitialized enum
            // values that previously rendered as Dru_Poison_DOT / level 0.
            spell.RequiredLevel = Mathf.Max(1, item.ItemLevel);
            spell.ManaCost = 0;
            spell.Aggro = 0;
            spell.SpellChargeTime = 0f;
            spell.SpellRange = 100f;
            spell.TargetDamage = 0;
            spell.TargetHealing = 0;
            spell.SpellDurationInTicks = 0;
            spell.Cooldown = 0f;
            spell.StatusEffectToApply = null;
            spell.SelfOnly = false;
            spell.CrowdControlSpell = false;
            spell.SimUsable = false;
            spell.SimsNeedHelpToLearn = true;
            spell.Line = Spell.SpellLine.Generic;
            spell.Type = Spell.SpellType.Beneficial;
            spell.MyDamageType = GameData.DamageType.Magic;

            spell.UsedBy = item.Classes != null
                ? new List<Class>(item.Classes)
                : new List<Class>();

            string id = item.Id;

            // Moldermaw
            if (Id(id, MoldermawEquipment.SporeboundScepterId))
                SetDamage(spell, 2200, GameData.DamageType.Magic);
            else if (Id(id, MoldermawEquipment.RotcleaverId))
                SetDamage(spell, 4700, GameData.DamageType.Physical);
            else if (Id(id, MoldermawEquipment.BulwarkId))
                SetBeneficial(spell, 0f);
            else if (Id(id, MoldermawEquipment.BloomId))
                SetHealing(spell, 750, 0f);
            else if (Id(id, MoldermawEquipment.SporepiercerId))
                SetDamage(spell, 2800, GameData.DamageType.Poison);

            // Nhal'Sera
            else if (Id(id, NhalSeraEquipment.LightlessScepterId))
                SetDamage(spell, 3400, GameData.DamageType.Void, 5f);
            else if (Id(id, NhalSeraEquipment.CarapaceId))
                SetBeneficial(spell, 45f);
            else if (Id(id, NhalSeraEquipment.CrownId))
                SetHealing(spell, 900, 20f);
            else if (Id(id, NhalSeraEquipment.GraspId))
                SetBeneficial(spell, 20f);
            else if (Id(id, NhalSeraEquipment.GirdleId))
                SetBeneficial(spell, 30f);

            // Varkesh
            else if (Id(id, VarkeshEquipment.SoulbinderGrimoireId))
                SetDamage(spell, 3600, GameData.DamageType.Magic);
            else if (Id(id, VarkeshEquipment.GravetideReaverId))
                SetDamage(spell, 5000, GameData.DamageType.Physical);
            else if (Id(id, VarkeshEquipment.HeartOfTheDeathlessId))
                SetBeneficial(spell, 90f);
            else if (Id(id, VarkeshEquipment.LanternOfDepartedSoulsId))
                SetHealing(spell, 0, 12f);
            else if (Id(id, VarkeshEquipment.BandOfTheGravewindId))
                SetBeneficial(spell, 15f);

            // Gorath
            else if (Id(id, GorathEquipment.EmberOfTheDemonKingId))
                SetDamage(spell, 2400, GameData.DamageType.Elemental);
            else if (Id(id, GorathEquipment.HellforgedTalonId))
                SetDamage(spell, 4400, GameData.DamageType.Physical);
            else if (Id(id, GorathEquipment.InfernalBastionId))
                SetBeneficial(spell, 0f);
            else if (Id(id, GorathEquipment.AshenCovenantId))
                SetBeneficial(spell, 0f);
            else if (Id(id, GorathEquipment.StormfireId))
                SetDamage(spell, 2600, GameData.DamageType.Elemental);

            // Standalone/pre-raid custom equipment
            else if (Id(id, FallenScepter.ItemId))
                SetDamage(spell, 1000, GameData.DamageType.Magic);
        }

        private static void SetDamage(
            Spell spell,
            int damage,
            GameData.DamageType damageType,
            float cooldown = 0f)
        {
            spell.Type = Spell.SpellType.Damage;
            spell.Line = Spell.SpellLine.Direct_Damage;
            spell.TargetDamage = damage;
            spell.TargetHealing = 0;
            spell.MyDamageType = damageType;
            spell.Cooldown = cooldown;
        }

        private static void SetHealing(
            Spell spell,
            int healing,
            float cooldown)
        {
            spell.Type = Spell.SpellType.Heal;
            spell.Line = Spell.SpellLine.Generic;
            spell.TargetDamage = 0;
            spell.TargetHealing = healing;
            spell.MyDamageType = GameData.DamageType.Magic;
            spell.Cooldown = cooldown;
        }

        private static void SetBeneficial(
            Spell spell,
            float cooldown)
        {
            spell.Type = Spell.SpellType.Beneficial;
            spell.Line = Spell.SpellLine.Generic;
            spell.TargetDamage = 0;
            spell.TargetHealing = 0;
            spell.MyDamageType = GameData.DamageType.Magic;
            spell.Cooldown = cooldown;
        }

        private static void StripLegacyLoreInjection(Item item)
        {
            if (item == null || string.IsNullOrEmpty(item.Lore))
                return;

            int markerIndex = item.Lore.IndexOf(LegacyMarker, StringComparison.Ordinal);
            if (markerIndex >= 0)
                item.Lore = item.Lore.Substring(0, markerIndex).TrimEnd();
        }

        private static string GetDescription(string id)
        {
            // Moldermaw
            if (Id(id, MoldermawEquipment.SporeboundScepterId))
                return "20% chance on a successful scepter hit: deal 2,200 Magic damage and apply Blighted Eruption for 8 seconds, dealing 500 Magic damage every 2 seconds (2,000 total). Reprocs refresh the infection rather than stacking.";

            if (Id(id, MoldermawEquipment.RotcleaverId))
                return "15% chance on a physical weapon hit: deal 4,700 additional Physical damage and apply Festering Wound for 8 seconds. While wounded, the target takes 6% increased melee damage from you. Reprocs refresh the wound rather than stacking.";

            if (Id(id, MoldermawEquipment.BulwarkId))
                return "90% chance on a successful Bash: force the target's attention for 6,800 threat and gain Stonebark for 6 seconds. Stonebark grants +100 Armor and 8% Physical damage reduction.";

            if (Id(id, MoldermawEquipment.BloomId))
                return "Blooming Reservoir grants +400 Mana while equipped. Direct heals have a 15% chance to apply Lingering Bloom, healing 250 HP every 2 seconds for 6 seconds (750 total). Reprocs refresh rather than stack.";

            if (Id(id, MoldermawEquipment.SporepiercerId))
                return "18% chance on a ranged hit: deal 2,800 Poison damage and apply Blighted Spores for 8 seconds, dealing 450 Poison damage every 2 seconds (1,800 total). Reprocs refresh the spores rather than stacking.";

            // Nhal'Sera
            if (Id(id, NhalSeraEquipment.LightlessScepterId))
                return "18% chance from offensive spell damage or a successful scepter hit: deal 3,400 Void damage and restore 150 Mana. 5 second internal cooldown.";

            if (Id(id, NhalSeraEquipment.CarapaceId))
                return "When you fall below 35% Health, gain +250 Armor and 20% damage reduction against Physical and Void damage for 10 seconds. 45 second internal cooldown.";

            if (Id(id, NhalSeraEquipment.CrownId))
                return "15% chance on a direct heal: heal every living member of your group for 900 and grant +8 Magic Resist and +8 Void Resist for 8 seconds. 20 second internal cooldown.";

            if (Id(id, NhalSeraEquipment.GraspId))
                return "15% chance on a physical weapon hit: gain +12% Weapon Haste, +8% Armor Penetration and +20 Dexterity for 8 seconds. 20 second internal cooldown.";

            if (Id(id, NhalSeraEquipment.GirdleId))
                return "10% chance from damaging spells or direct heals: grant Mana Regen V to every living member of your group for 12 seconds. Mana Regen V restores 5 Mana per level every 3 seconds. 30 second internal cooldown.";

            // Varkesh
            if (Id(id, VarkeshEquipment.SoulbinderGrimoireId))
                return "15% chance from damage spells: Soul Rend deals 3,600 Magic damage and restores 180 Mana.";

            if (Id(id, VarkeshEquipment.GravetideReaverId))
                return "10% chance on a one-handed physical weapon hit: Death's Momentum deals 5,000 additional Physical damage and grants 12% attack speed for 6 seconds.";

            if (Id(id, VarkeshEquipment.HeartOfTheDeathlessId))
                return "When you cross below 25% Health, Refuse Death grants +150 Armor, 15% damage reduction and 25% increased healing received for 8 seconds. 90 second cooldown.";

            if (Id(id, VarkeshEquipment.LanternOfDepartedSoulsId))
                return "Directly healing an ally below 35% Health increases that heal by 25% and grants Soulkeeper's Grace, increasing your healing by 10% for 6 seconds. 12 second internal cooldown.";

            if (Id(id, VarkeshEquipment.BandOfTheGravewindId))
                return "12% chance on a ranged hit: gain Gravewind for 8 seconds, granting 12% faster ranged attacks, +10 Dexterity and +10 Agility. While Gravewind is active, ranged attacks have a 20% chance to deal 1,200 Magic damage. 15 second internal cooldown.";

            // Gorath
            if (Id(id, GorathEquipment.EmberOfTheDemonKingId))
                return "20% chance from damage spells: Hellfire deals 2,400 Elemental damage and burns for 500 Elemental damage every 2 seconds for 8 seconds. Repeated procs refresh the burn and increase periodic damage by 10%, up to 3 additional stacks.";

            if (Id(id, GorathEquipment.HellforgedTalonId))
                return "15% chance on a physical weapon hit: Infernal Frenzy deals 4,400 additional Physical damage and grants 10% attack speed for 6 seconds.";

            if (Id(id, GorathEquipment.InfernalBastionId))
                return "90% chance on a successful Bash: Demonic Challenge generates 7,200 threat and grants Infernal Resolve, reducing all damage taken by 5% for 6 seconds.";

            if (Id(id, GorathEquipment.AshenCovenantId))
                return "15% chance when casting a beneficial spell: refund 30% of its Mana cost and gain Ashen Grace, increasing healing by 8% for 6 seconds.";

            if (Id(id, GorathEquipment.StormfireId))
                return "20% chance on a ranged hit: deal 2,600 Elemental damage and burn for 475 Elemental damage every 2 seconds for 8 seconds. Repeated procs refresh the burn and increase periodic damage by 10%, up to 3 additional stacks. Spell damage against your Stormfire target has a 15% chance to trigger Arc Discharge for 1,000 Elemental damage (3 second cooldown).";

            if (Id(id, FallenScepter.ItemId))
                return "5% chance on a weapon hit: Fallen Spark deals 1,000 Magic damage at no Mana cost.";

            return null;
        }

        private static bool Id(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The native RMB proc panel does not render Spell.SpellDesc by itself.
    /// Append that description to the native Cast Time block only while that
    /// right-click detail text exists, preserving the native panel/layout.
    /// </summary>
    internal sealed class CustomEquipmentProcDetailPresenter : MonoBehaviour
    {
        private ItemInfoWindow _window;
        private string _description;
        private TextMeshProUGUI[] _texts;
        private float _nextTextRefresh;
        private float _nextRewrite;

        internal void Track(Item item, ItemInfoWindow window)
        {
            _window = window;
            _description = CustomEquipmentEffectDescriptions.GetDescriptionForItem(item);
            _texts = _window != null
                ? _window.GetComponentsInChildren<TextMeshProUGUI>(true)
                : null;
            _nextTextRefresh = Time.unscaledTime + 0.35f;
            Rewrite();
        }

        internal void Clear()
        {
            RemoveInjectedText();
            _window = null;
            _description = null;
            _texts = null;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextRewrite)
                return;

            _nextRewrite = Time.unscaledTime + 0.10f;
            Rewrite();
        }

        private void Rewrite()
        {
            if (_window == null || string.IsNullOrEmpty(_description))
                return;

            if (_texts == null || Time.unscaledTime >= _nextTextRefresh)
            {
                _texts = _window.GetComponentsInChildren<TextMeshProUGUI>(true);
                _nextTextRefresh = Time.unscaledTime + 0.35f;
            }

            if (_texts == null)
                return;

            for (int i = 0; i < _texts.Length; i++)
            {
                TextMeshProUGUI text = _texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                    continue;

                int markerIndex = text.text.IndexOf(CustomEquipmentEffectDescriptions.DetailMarker, StringComparison.Ordinal);
                string baseText = markerIndex >= 0
                    ? text.text.Substring(0, markerIndex)
                    : text.text;

                // The expanded native proc panel contains a Cast Time line;
                // the normal item tooltip does not.  This keeps the effect
                // explanation exclusively inside the Hold RMB detail region.
                if (baseText.IndexOf("Cast Time", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string desired = baseText.TrimEnd() + CustomEquipmentEffectDescriptions.DetailMarker + _description;
                if (string.Equals(text.text, desired, StringComparison.Ordinal))
                    continue;

                text.text = desired;
                text.ForceMeshUpdate();
            }
        }

        private void RemoveInjectedText()
        {
            if (_texts == null)
                return;

            for (int i = 0; i < _texts.Length; i++)
            {
                TextMeshProUGUI text = _texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                    continue;

                int markerIndex = text.text.IndexOf(CustomEquipmentEffectDescriptions.DetailMarker, StringComparison.Ordinal);
                if (markerIndex < 0)
                    continue;

                text.text = text.text.Substring(0, markerIndex).TrimEnd();
                text.ForceMeshUpdate();
            }
        }
    }

    [HarmonyPatch(
        typeof(ItemInfoWindow),
        "DisplayItem",
        new Type[]
        {
            typeof(Item),
            typeof(Vector2),
            typeof(int)
        })]
    internal static class Patch_CustomEquipmentEffectDescriptions
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Item item)
        {
            CustomEquipmentEffectDescriptions.Apply(item);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Item item, ItemInfoWindow __instance)
        {
            if (__instance == null)
                return;

            CustomEquipmentProcDetailPresenter presenter =
                __instance.GetComponent<CustomEquipmentProcDetailPresenter>();

            string description = CustomEquipmentEffectDescriptions.GetDescriptionForItem(item);
            if (string.IsNullOrEmpty(description))
            {
                if (presenter != null)
                    presenter.Clear();
                return;
            }

            if (presenter == null)
            {
                presenter =
                    __instance.gameObject.AddComponent<CustomEquipmentProcDetailPresenter>();
            }

            presenter.Track(item, __instance);
        }
    }
}
