namespace FateOfTheFallen
{
    internal static class BlightcallerRuntime
    {
        internal static bool PrepareCast(
            CastSpell caster,
            Spell spell,
            ref Stats target,
            out string denial)
        {
            denial = null;

            BlightcallerSpellDefinition definition;

            if (!BlightcallerCatalog.TryGetDefinition(
                    spell,
                    out definition))
            {
                return true;
            }

            if (definition.HiddenEffect)
            {
                return true;
            }

            if (caster == null)
            {
                denial = "The Blightcaller caster was unavailable.";
                return false;
            }

            if (caster.MyChar == null)
            {
                denial = "The Blightcaller character was unavailable.";
                return false;
            }

            if (caster.MyChar.MyStats == null)
            {
                denial = "The Blightcaller stats were unavailable.";
                return false;
            }

            if (!BlightcallerCatalog.IsBlightcallerClass(
                    caster.MyChar.MyStats.CharacterClass))
            {
                denial =
                    "Only a Blightcaller can cast that spell.";

                return false;
            }

            return true;
        }
    }
}