using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared native SpellDB registration helper for custom content.
    /// </summary>
    internal static class CustomSpellRegistry
    {
        internal static void Register(
            SpellDB database,
            Spell spell)
        {
            if (database == null ||
                database.SpellDatabase == null ||
                spell == null)
            {
                return;
            }

            Spell[] spells =
                database.SpellDatabase;

            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                Spell existing =
                    spells[i];

                if (existing == spell)
                {
                    return;
                }

                if (existing == null ||
                    string.IsNullOrEmpty(spell.Id) ||
                    !string.Equals(
                        existing.Id,
                        spell.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                spells[i] = spell;
                database.SpellDatabase = spells;
                return;
            }

            Spell[] expanded =
                new Spell[spells.Length + 1];

            Array.Copy(
                spells,
                expanded,
                spells.Length);

            expanded[spells.Length] =
                spell;

            database.SpellDatabase =
                expanded;
        }

        internal static void RegisterRange(
            SpellDB database,
            IEnumerable<Spell> spells)
        {
            if (database == null ||
                spells == null)
            {
                return;
            }

            foreach (Spell spell in spells)
            {
                Register(
                    database,
                    spell);
            }
        }
    }
}
