using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Rotation brain for authored Emberweaver SimPlayers. Native SimPlayer
    /// movement/aggro remains active; this controller owns only custom spell
    /// selection and delegates class mechanics to EmberweaverSimCombatRuntime.
    /// </summary>
    internal sealed class EmberweaverSimCombatController : MonoBehaviour
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
            if (sim == null ||
                !EmberweaverSimCombatRuntime.IsAuthoredEmberweaver(sim))
            {
                return;
            }

            EmberweaverSimCombatController controller =
                sim.GetComponent<EmberweaverSimCombatController>();

            if (controller == null)
            {
                controller =
                    sim.gameObject.AddComponent<EmberweaverSimCombatController>();
            }

            controller.Bind(sim);
        }

        internal static void TakeSpellOwnership(
            SimPlayer sim)
        {
            if (sim == null ||
                !EmberweaverSimCombatRuntime.IsAuthoredEmberweaver(sim))
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
                    EmberweaverSpellDefinition definition;

                    return EmberweaverSpellCatalog.TryGetDefinition(
                               spell,
                               out definition) &&
                           definition != null;
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

            EmberweaverSimCombatRuntime.Register(sim);
        }

        private void Update()
        {
            if (_sim == null ||
                !_sim.gameObject.activeInHierarchy ||
                !EmberweaverSimCombatRuntime.IsAuthoredEmberweaver(_sim))
            {
                return;
            }

            EmberweaverSimCombatRuntime.Update(_sim);

            if (Time.unscaledTime < _nextDecision)
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

            Spell spell =
                ChooseSpell(target);

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
                    "Emberweaver Sim AI: cast failed for [" +
                    GetSimName() +
                    "] spell [" +
                    spell.SpellName +
                    "]: " +
                    exception.Message);
            }

            if (started)
            {
                _nextDecision =
                    Time.unscaledTime +
                    SuccessfulCastLockout;
            }
        }

        private Spell ChooseSpell(
            Character target)
        {
            if (_sim == null ||
                target == null)
            {
                return null;
            }

            Spell pyro = GetHighestFamilySpell(IsPyroclasm);
            Spell combust = GetHighestFamilySpell(IsCombust);
            Spell scorch = GetHighestFamilySpell(IsScorch);
            Spell surge = GetHighestFamilySpell(IsSurge);
            Spell kindle = GetHighestFamilySpell(IsKindle);
            Spell bolt = GetHighestFamilySpell(IsBolt);

            int referenceDamage =
                GetReferenceDirectDamage(
                    pyro,
                    surge,
                    kindle,
                    bolt);

            // Flashfire is always consumed immediately on the strongest
            // available Pyroclasm rank. Its cooldown and mana restrictions are
            // intentionally ignored because the proc resets/makes it free.
            if (pyro != null &&
                EmberweaverSimCombatRuntime.IsFlashfireReady(_sim) &&
                CanUse(pyro, true, true))
            {
                return pyro;
            }

            // Avoid wasting Combust at low Heat. The AI holds it for 8-10
            // Heat and a meaningful reservoir, matching the class's intended
            // cash-out gameplay instead of firing it as soon as 5 Heat exists.
            if (combust != null &&
                EmberweaverSimCombatRuntime.ShouldAICombust(
                    _sim,
                    target,
                    referenceDamage) &&
                CanUse(combust, false, false))
            {
                return combust;
            }

            // Scorch is an accelerator rather than a filler. Use it when a
            // real Wildfire bank exists, not immediately into an empty target.
            if (scorch != null &&
                EmberweaverSimCombatRuntime.ShouldAIScorch(
                    _sim,
                    target,
                    referenceDamage) &&
                CanUse(scorch, false, false))
            {
                return scorch;
            }

            if (surge != null &&
                CanUse(surge, false, false))
            {
                return surge;
            }

            if (kindle != null &&
                CanUse(kindle, false, false))
            {
                return kindle;
            }

            if (pyro != null &&
                CanUse(pyro, false, false))
            {
                return pyro;
            }

            if (bolt != null &&
                CanUse(bolt, false, false))
            {
                return bolt;
            }

            return null;
        }

        private bool CanUse(
            Spell spell,
            bool ignoreCooldown,
            bool ignoreMana)
        {
            if (spell == null ||
                _sim == null ||
                _sim.MyStats == null)
            {
                return false;
            }

            EmberweaverSpellDefinition definition;

            if (!EmberweaverSpellCatalog.TryGetDefinition(
                    spell,
                    out definition) ||
                definition == null ||
                definition.RequiredLevel > _sim.MyStats.Level)
            {
                return false;
            }

            if (!EmberweaverSimCombatRuntime.IsSpellReady(
                    _sim,
                    spell,
                    ignoreCooldown))
            {
                return false;
            }

            if (!ignoreMana &&
                _sim.MyStats.CurrentMana < definition.ManaCost)
            {
                return false;
            }

            return true;
        }

        private Spell GetHighestFamilySpell(
            Func<EmberweaverSpellDefinition, bool> predicate)
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
                EmberweaverSpellDefinition definition;

                if (slot == null ||
                    slot.spell == null ||
                    !EmberweaverSpellCatalog.TryGetDefinition(
                        slot.spell,
                        out definition) ||
                    definition == null ||
                    !predicate(definition) ||
                    definition.RequiredLevel > _sim.MyStats.Level)
                {
                    continue;
                }

                if (definition.RequiredLevel > bestLevel)
                {
                    best = slot.spell;
                    bestLevel = definition.RequiredLevel;
                }
            }

            return best;
        }

        private static int GetReferenceDirectDamage(
            params Spell[] spells)
        {
            int best = 1;

            if (spells == null)
            {
                return best;
            }

            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                EmberweaverSpellDefinition definition;

                if (spells[i] != null &&
                    EmberweaverSpellCatalog.TryGetDefinition(
                        spells[i],
                        out definition) &&
                    definition != null)
                {
                    best = Mathf.Max(best, definition.Damage);
                }
            }

            return best;
        }

        private static bool IsPyroclasm(
            EmberweaverSpellDefinition definition)
        {
            return definition != null &&
                   definition.IsPyroclasm;
        }

        private static bool IsCombust(
            EmberweaverSpellDefinition definition)
        {
            return definition != null &&
                   definition.IsCombust;
        }

        private static bool IsScorch(
            EmberweaverSpellDefinition definition)
        {
            return definition != null &&
                   definition.IsScorchFamily;
        }

        private static bool IsKindle(
            EmberweaverSpellDefinition definition)
        {
            return definition != null &&
                   definition.IsKindleFamily;
        }

        private static bool IsSurge(
            EmberweaverSpellDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return string.Equals(definition.Id, EmberweaverSpellCatalog.FlameSurgeId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(definition.Id, EmberweaverSpellCatalog.BlazingSurgeId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(definition.Id, EmberweaverSpellCatalog.CataclysmicSurgeId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBolt(
            EmberweaverSpellDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return string.Equals(definition.Id, EmberweaverSpellCatalog.EmberBoltId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(definition.Id, EmberweaverSpellCatalog.SearingBoltId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(definition.Id, EmberweaverSpellCatalog.InfernoBoltId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(definition.Id, EmberweaverSpellCatalog.CataclysmicBoltId, StringComparison.OrdinalIgnoreCase);
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

        private void OnDestroy()
        {
            EmberweaverSimCombatRuntime.Unregister(_sim);
        }
    }
}
