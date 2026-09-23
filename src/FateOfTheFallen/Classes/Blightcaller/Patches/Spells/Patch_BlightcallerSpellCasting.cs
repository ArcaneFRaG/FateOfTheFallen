using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(CastSpell),
        "StartSpell",
        new Type[]
        {
            typeof(Spell),
            typeof(Stats)
        })]
    internal static class Patch_BlightcallerCast
    {
        // ============================================================
        // CAST VALIDATION
        // ============================================================

        private static bool Prefix(
            CastSpell __instance,
            Spell _spell,
            ref Stats _target,
            ref bool __result)
        {
            try
            {
                BlightcallerSpellDefinition definition;


                if (!BlightcallerCatalog.TryGetDefinition(
                        _spell,
                        out definition))
                {
                    return true;
                }


                string denial;


                /*
                 * BlightcallerRuntime.PrepareCast() is already correctly
                 * caster-aware. It checks:
                 *
                 * __instance.MyChar.MyStats.CharacterClass
                 *
                 * rather than GameData.PlayerStats.
                 */
                if (BlightcallerRuntime.PrepareCast(
                        __instance,
                        _spell,
                        ref _target,
                        out denial))
                {
                    return true;
                }


                __result =
                    false;


                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: spell cast patch failed: " +
                    exception);


                return true;
            }
        }


        // ============================================================
        // POST CAST
        // ============================================================

        private static void Postfix(
            CastSpell __instance,
            Spell _spell,
            Stats _target,
            bool __result)
        {
            if (!__result)
            {
                return;
            }


            try
            {
                BlightcallerSpellDefinition definition;


                if (!BlightcallerCatalog.TryGetDefinition(
                        _spell,
                        out definition))
                {
                    return;
                }


                // ====================================================
                // DOT SCHEDULER FAST-PATH
                // ====================================================
                //
                // Projectile spells may not have resolved yet, so this
                // does not replace the normal Stats.Update tracking.
                //
                // It simply catches any Blightcaller effect already on
                // the target immediately.
                // ====================================================

                if (_target != null &&
                    _target.StatusEffects != null)
                {
                    BlightcallerDotScheduler
                        .TrackStatusEffects(
                            _target);
                }


                if (definition.HiddenEffect)
                {
                    return;
                }


                // ====================================================
                // ACTUAL CASTER
                // ====================================================

                Character caster =
                    __instance != null
                        ? __instance.MyChar
                        : null;


                if (caster == null ||
                    caster.MyStats == null)
                {
                    return;
                }


                if (!BlightcallerCatalog
                        .IsBlightcallerClass(
                            caster.MyStats.CharacterClass))
                {
                    return;
                }


                // ====================================================
                // PLAYER-ONLY COMBAT HANDLING
                // ====================================================
                //
                // SimPlayer Blightcallers must remain entirely within
                // native NPC combat handling.
                //
                // Never call the local PlayerCombat controller because
                // a SimPlayer successfully cast a Blightcaller spell.
                // ====================================================

                if (caster.MyStats ==
                        GameData.PlayerStats &&
                    GameData.PlayerCombat != null)
                {
                    GameData.PlayerCombat
                        .ForceAttackOn();
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: post-cast handling failed: " +
                    exception);
            }
        }
    }
}