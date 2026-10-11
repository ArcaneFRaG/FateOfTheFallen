using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// DoT-aware rotation brain for authored Blightcaller SimPlayers.
    /// Native movement/aggro is preserved; this component prevents the native
    /// random spell picker from repeatedly overwriting afflictions and chooses
    /// the Blightcaller spell priority explicitly.
    /// </summary>
    internal sealed class BlightcallerSimCombatController : MonoBehaviour
    {
        private const float DecisionInterval = 0.15f;
        private const float SuccessfulCastLockout = 0.35f;

        private SimPlayer _sim;
        private NPC _npc;
        private CastSpell _caster;
        private float _nextDecision;

        internal static void Install(
            SimPlayer sim)
        {
            if (!IsAuthoredBlightcaller(sim))
            {
                return;
            }

            BlightcallerSimCombatController controller =
                sim.GetComponent<BlightcallerSimCombatController>();

            if (controller == null)
            {
                controller =
                    sim.gameObject.AddComponent<BlightcallerSimCombatController>();
            }

            controller.Bind(sim);
        }

        internal static void TakeSpellOwnership(
            SimPlayer sim)
        {
            if (!IsAuthoredBlightcaller(sim))
            {
                return;
            }

            NPC npc = sim.GetThisNPC();

            if (npc == null ||
                npc.MyAttackSpells == null)
            {
                return;
            }

            npc.MyAttackSpells.RemoveAll(
                delegate(Spell spell)
                {
                    BlightcallerSpellDefinition definition;

                    return BlightcallerCatalog.TryGetDefinition(
                               spell,
                               out definition) &&
                           definition != null &&
                           !definition.HiddenEffect;
                });
        }

        private void Bind(
            SimPlayer sim)
        {
            _sim = sim;
            _npc = sim != null
                ? sim.GetThisNPC()
                : null;
            _caster = ResolveCaster(sim, _npc);
        }

        private void Update()
        {
            if (!IsAuthoredBlightcaller(_sim) ||
                Time.unscaledTime < _nextDecision)
            {
                return;
            }

            _nextDecision =
                Time.unscaledTime + DecisionInterval;

            if (_npc == null)
            {
                _npc = _sim.GetThisNPC();
            }

            if (_caster == null)
            {
                _caster = ResolveCaster(_sim, _npc);
            }

            if (_npc == null ||
                _caster == null ||
                _sim.MyStats == null ||
                _sim.MyStats.CurrentHP <= 0 ||
                WorldBossSimAvoidanceRuntime.ShouldOverrideFullSimAi(_sim) ||
                WorldBossSimAvoidanceRuntime.ShouldOverrideNpcAi(_npc) ||
                WorldBossSimAvoidanceRuntime.ShouldSuppressCaster(_caster))
            {
                return;
            }

            try
            {
                if (_caster.isCasting())
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            Character target =
                _npc.CurrentAggroTarget;

            if (target == null ||
                !target.Alive ||
                target.MyStats == null)
            {
                return;
            }

            Spell spell = ChooseSpell(target);

            if (spell == null)
            {
                return;
            }

            bool started = false;

            try
            {
                started =
                    _caster.StartSpell(
                        spell,
                        target.MyStats);
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "Blightcaller Sim AI: cast failed for [" +
                    GetSimName() +
                    "] spell [" +
                    spell.SpellName +
                    "]: " +
                    exception.Message);
            }

            if (!started)
            {
                return;
            }

            BlightcallerSpellDefinition definition;

            if (BlightcallerCatalog.TryGetDefinition(
                    spell,
                    out definition) &&
                definition != null)
            {
                _sim.SetSpellCooldownBySpell(
                    spell,
                    definition.CooldownSeconds);
            }

            _nextDecision =
                Time.unscaledTime +
                SuccessfulCastLockout;
        }

        private Spell ChooseSpell(
            Character target)
        {
            // Resistance/attack-roll debuffs are maintained before damage
            // effects so the subsequent afflictions benefit from them.
            Spell weakness = GetHighestFamilySpell(IsFesteringWeakness);
            if (CanMaintainStatus(weakness, target))
            {
                return weakness;
            }

            Spell curse = GetHighestFamilySpell(IsWitheringCurse);
            if (CanMaintainStatus(curse, target))
            {
                return curse;
            }

            // Each Blightcaller DoT family has its own SpellLine. Keep one
            // current rank of every family running, but never spam-refresh an
            // already active affliction.
            Spell withering = GetHighestFamilySpell(IsWitheringFamily);
            if (CanMaintainStatus(withering, target))
            {
                return withering;
            }

            Spell blight = GetHighestFamilySpell(IsBlightFamily);
            if (CanMaintainStatus(blight, target))
            {
                return blight;
            }

            Spell agony = GetHighestFamilySpell(IsAgonyFamily);
            if (CanMaintainStatus(agony, target))
            {
                return agony;
            }

            // Once the maintenance package is active, spend cast windows on
            // the strongest direct-damage options rather than overwriting DoTs.
            Spell soulRend = GetHighestFamilySpell(IsSoulRend);
            if (CanUse(soulRend))
            {
                return soulRend;
            }

            Spell shadow = GetHighestFamilySpell(IsShadowFamily);
            if (CanUse(shadow))
            {
                return shadow;
            }

            Spell bolt = GetHighestFamilySpell(IsBoltFamily);
            if (CanUse(bolt))
            {
                return bolt;
            }

            return null;
        }

        private bool CanMaintainStatus(
            Spell spell,
            Character target)
        {
            if (!CanUse(spell) ||
                target == null ||
                target.MyStats == null)
            {
                return false;
            }

            BlightcallerSpellDefinition definition;

            if (!BlightcallerCatalog.TryGetDefinition(
                    spell,
                    out definition) ||
                definition == null)
            {
                return false;
            }

            return !HasActiveEffect(
                target.MyStats,
                definition,
                spell);
        }

        private bool CanUse(
            Spell spell)
        {
            if (spell == null ||
                _sim == null ||
                _sim.MyStats == null)
            {
                return false;
            }

            BlightcallerSpellDefinition definition;

            if (!BlightcallerCatalog.TryGetDefinition(
                    spell,
                    out definition) ||
                definition == null ||
                definition.HiddenEffect ||
                definition.Level > _sim.MyStats.Level ||
                _sim.MyStats.CurrentMana < definition.ManaCost)
            {
                return false;
            }

            int index = _sim.GetSpellIndexInBook(spell);

            return index >= 0 &&
                   index < _sim.Spellbook.Count &&
                   _sim.GetSpellCooldownByIndex(index) <= 0f;
        }

        private Spell GetHighestFamilySpell(
            Func<BlightcallerSpellDefinition, bool> predicate)
        {
            if (_sim == null ||
                _sim.Spellbook == null ||
                predicate == null)
            {
                return null;
            }

            Spell best = null;
            int bestLevel = -1;

            for (int i = 0;
                 i < _sim.Spellbook.Count;
                 i++)
            {
                SimPlayerSpellSlot slot = _sim.Spellbook[i];
                BlightcallerSpellDefinition definition;

                if (slot == null ||
                    slot.spell == null ||
                    !BlightcallerCatalog.TryGetDefinition(
                        slot.spell,
                        out definition) ||
                    definition == null ||
                    definition.HiddenEffect ||
                    !predicate(definition) ||
                    definition.Level > _sim.MyStats.Level)
                {
                    continue;
                }

                if (definition.Level > bestLevel)
                {
                    best = slot.spell;
                    bestLevel = definition.Level;
                }
            }

            return best;
        }

        private static bool HasActiveEffect(
            Stats target,
            BlightcallerSpellDefinition definition,
            Spell castSpell)
        {
            if (target == null ||
                target.StatusEffects == null ||
                definition == null)
            {
                return false;
            }

            Spell effectSpell = castSpell;

            if (!string.IsNullOrEmpty(definition.AppliedEffectKey))
            {
                Spell resolved =
                    BlightcallerCatalog.GetSpell(
                        definition.AppliedEffectKey);

                if (resolved != null)
                {
                    effectSpell = resolved;
                }
            }

            if (effectSpell == null ||
                string.IsNullOrEmpty(effectSpell.Id))
            {
                return false;
            }

            for (int i = 0;
                 i < target.StatusEffects.Length;
                 i++)
            {
                StatusEffect status = target.StatusEffects[i];

                if (status != null &&
                    status.Effect != null &&
                    string.Equals(
                        status.Effect.Id,
                        effectSpell.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBoltFamily(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(
                definition,
                "blighted_bolt",
                "putrid_bolt",
                "festering_bolt",
                "apocalyptic_bolt");
        }

        private static bool IsShadowFamily(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(
                definition,
                "shadow_rend",
                "shadow_rupture",
                "shadow_devastation",
                "void_rend");
        }

        private static bool IsSoulRend(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(definition, "soul_rend");
        }

        private static bool IsWitheringFamily(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(
                definition,
                "withering_touch",
                "greater_withering_touch",
                "potent_withering_touch",
                "eternal_withering");
        }

        private static bool IsBlightFamily(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(
                definition,
                "blight",
                "greater_blight",
                "virulent_blight",
                "cataclysmic_blight");
        }

        private static bool IsAgonyFamily(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(
                definition,
                "agony",
                "greater_agony",
                "lingering_agony",
                "endless_agony");
        }

        private static bool IsFesteringWeakness(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(
                definition,
                "festering_weakness",
                "greater_festering_weakness",
                "devastating_festering_weakness");
        }

        private static bool IsWitheringCurse(
            BlightcallerSpellDefinition definition)
        {
            return KeyIs(
                definition,
                "withering_curse",
                "greater_withering_curse",
                "eternal_withering_curse");
        }

        private static bool KeyIs(
            BlightcallerSpellDefinition definition,
            params string[] keys)
        {
            if (definition == null ||
                string.IsNullOrEmpty(definition.Key) ||
                keys == null)
            {
                return false;
            }

            for (int i = 0;
                 i < keys.Length;
                 i++)
            {
                if (string.Equals(
                        definition.Key,
                        keys[i],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAuthoredBlightcaller(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null ||
                !BlightcallerCatalog.IsBlightcallerClass(
                    sim.MyStats.CharacterClass))
            {
                return false;
            }

            NPC npc = sim.GetThisNPC();

            return npc != null &&
                   FOTFSimPlayerRoster.IsClassMember(
                       npc.NPCName,
                       FOTFSimPlayerRoster.BlightcallerClassName);
        }

        private static CastSpell ResolveCaster(
            SimPlayer sim,
            NPC npc)
        {
            if (sim != null &&
                sim.MyStats != null &&
                sim.MyStats.Myself != null &&
                sim.MyStats.Myself.MySpells != null)
            {
                return sim.MyStats.Myself.MySpells;
            }

            if (npc != null)
            {
                Character character = npc.GetChar();

                if (character != null &&
                    character.MySpells != null)
                {
                    return character.MySpells;
                }

                CastSpell fromNpc = npc.GetComponent<CastSpell>();

                if (fromNpc != null)
                {
                    return fromNpc;
                }
            }

            return sim != null
                ? sim.GetComponent<CastSpell>()
                : null;
        }

        private string GetSimName()
        {
            if (_npc != null &&
                !string.IsNullOrEmpty(_npc.NPCName))
            {
                return _npc.NPCName;
            }

            return _sim != null
                ? _sim.name
                : "unknown";
        }
    }
}
