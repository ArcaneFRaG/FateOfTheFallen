using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Installs class-specific combat controllers on authored FOTF SimPlayers.
    /// Native SimPlayer movement, targeting, grouping and raid logic remain
    /// untouched; FOTF owns only custom-class spell choice/mechanics.
    /// </summary>
    internal static class FOTFSimCombatBridge
    {
        internal static void InstallFor(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null)
            {
                return;
            }

            FOTFSimPlayerDefinition definition;

            if (!FOTFSimPlayerRoster.TryGetDefinitionForSim(
                    sim,
                    out definition) ||
                definition == null)
            {
                return;
            }

            if (string.Equals(
                    definition.ClassName,
                    FOTFSimPlayerRoster.EmberweaverClassName,
                    StringComparison.OrdinalIgnoreCase))
            {
                EmberweaverSimCombatController.Install(
                    sim);

                EmberweaverSimCombatController.TakeSpellOwnership(
                    sim);

                return;
            }

            if (string.Equals(definition.ClassName,
                    FOTFSimPlayerRoster.LightbringerClassName,
                    StringComparison.OrdinalIgnoreCase))
            {
                LightbringerSimCombatController.Install(sim);
                LightbringerSimCombatController.TakeSpellOwnership(sim);
                return;
            }

            if (string.Equals(
                    definition.ClassName,
                    FOTFSimPlayerRoster.BlightcallerClassName,
                    StringComparison.OrdinalIgnoreCase))
            {
                BlightcallerSimCombatController.Install(
                    sim);

                BlightcallerSimCombatController.TakeSpellOwnership(
                    sim);
            }
        }
    }
}
