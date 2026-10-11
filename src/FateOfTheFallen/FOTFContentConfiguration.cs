using Lunaris.Config;

namespace FateOfTheFallen
{
    /// <summary>
    /// Visible in the Lunaris plugin options menu. Changes are captured on plugin
    /// initialization; reload FOTF / restart Erenshor to apply changes safely.
    /// Existing character classes and item registrations are intentionally retained.
    /// </summary>
    public sealed class FOTFContentConfiguration
    {
        [ConfigAttribute("Allow New Blightcallers", "Classes", "Show Blightcaller during NEW character creation. Existing Blightcallers remain playable. Reload FOTF to apply.")]
        public bool NewBlightcallers { get; set; } = true;

        [ConfigAttribute("Allow New Emberweavers", "Classes", "Show Emberweaver during NEW character creation. Existing Emberweavers remain playable. Reload FOTF to apply.")]
        public bool NewEmberweavers { get; set; } = true;

        [ConfigAttribute("Allow New Lightbringers", "Classes", "Show Lightbringer during NEW character creation. Existing Lightbringers remain playable. Reload FOTF to apply.")]
        public bool NewLightbringers { get; set; } = true;

        [ConfigAttribute("Class Spell Vendors", "Classes", "Spawn FOTF class spell / aura vendors. Existing spells and inventory items remain registered. Reload FOTF to apply.")]
        public bool ClassVendors { get; set; } = true;

        [ConfigAttribute("FOTF SimPlayer Roster", "Classes", "Add the 36 authored FOTF Sims to the native guild roster. Existing authored Sim saves are not deleted. Reload FOTF to apply.")]
        public bool CustomSimPlayers { get; set; } = true;

        [ConfigAttribute("Questline and Sereth", "Expansion", "Enable Archivist Sereth, quest interactions, and the 14-quest story. Disable before starting a questline; inventory/save data is preserved. Reload FOTF to apply.")]
        public bool Questline { get; set; } = true;

        [ConfigAttribute("World Boss Encounters", "Expansion", "Enable world-boss spawning. Individual encounter switches below also apply. Disabling bosses stops quest progression which requires their drops. Reload FOTF to apply.")]
        public bool WorldBosses { get; set; } = true;

        [ConfigAttribute("Moldermaw", "World Bosses", "Spawn Moldermaw. Requires World Boss Encounters. Reload FOTF to apply.")]
        public bool Moldermaw { get; set; } = true;

        [ConfigAttribute("Nhal'Sera", "World Bosses", "Spawn Nhal'Sera. Requires World Boss Encounters. Reload FOTF to apply.")]
        public bool NhalSera { get; set; } = true;

        [ConfigAttribute("Varkesh", "World Bosses", "Spawn Varkesh. Requires World Boss Encounters. Reload FOTF to apply.")]
        public bool Varkesh { get; set; } = true;

        [ConfigAttribute("Gorath", "World Bosses", "Spawn Gorath. Requires World Boss Encounters. Reload FOTF to apply.")]
        public bool Gorath { get; set; } = true;

        [ConfigAttribute("Additional Zones", "Expansion", "Enable FOTF's experimental custom-zone loader. Reload FOTF to apply.")]
        public bool CustomZones { get; set; } = true;

        [ConfigAttribute("Custom Raid Scenes", "Expansion", "Enable FOTF custom raid scene loader. Reload FOTF to apply.")]
        public bool CustomRaids { get; set; } = true;

        [ConfigAttribute("Native Absorption Indicators", "Presentation", "Enable the Lightbringer native Erenshor HP-bar absorb badges. Per-frame controls remain available via /fotfabsorbui. Reload FOTF to apply.")]
        public bool NativeAbsorbIndicators { get; set; } = true;
    }

    internal static class FOTFContentOptions
    {
        // Snapshot: changes in the Lunaris settings menu never live-disable a
        // class/spell that a loaded save is using or destroy an active boss.
        internal static FOTFContentConfiguration Active { get; private set; } = new FOTFContentConfiguration();

        internal static void Capture(FOTFContentConfiguration options)
        {
            Active = options == null ? new FOTFContentConfiguration() : new FOTFContentConfiguration
            {
                NewBlightcallers = options.NewBlightcallers,
                NewEmberweavers = options.NewEmberweavers,
                NewLightbringers = options.NewLightbringers,
                ClassVendors = options.ClassVendors,
                CustomSimPlayers = options.CustomSimPlayers,
                Questline = options.Questline,
                WorldBosses = options.WorldBosses,
                Moldermaw = options.Moldermaw,
                NhalSera = options.NhalSera,
                Varkesh = options.Varkesh,
                Gorath = options.Gorath,
                CustomZones = options.CustomZones,
                CustomRaids = options.CustomRaids,
                NativeAbsorbIndicators = options.NativeAbsorbIndicators
            };
        }

        internal static bool SpawnMoldermaw { get { return Active.WorldBosses && Active.Moldermaw; } }
        internal static bool SpawnNhalSera { get { return Active.WorldBosses && Active.NhalSera; } }
        internal static bool SpawnVarkesh { get { return Active.WorldBosses && Active.Varkesh; } }
        internal static bool SpawnGorath { get { return Active.WorldBosses && Active.Gorath; } }
        internal static bool AnyBoss { get { return SpawnMoldermaw || SpawnNhalSera || SpawnVarkesh || SpawnGorath; } }
    }
}
