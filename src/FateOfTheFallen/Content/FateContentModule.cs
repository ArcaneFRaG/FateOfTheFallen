namespace FateOfTheFallen
{
    /// <summary>
    /// Lifecycle boundary for expansion content that is not owned by a
    /// playable class.
    /// </summary>
    internal static class FateContentModule
    {
        internal static void Initialize()
        {
            CustomItemRegistry.RegisterLazyResolver(
                FateOfTheFallenNotes.WeatheredNoteItemId.ToString(),
                ResolveWeatheredNote);
        }

        private static Item ResolveWeatheredNote(
            ItemDatabase database)
        {
            FateOfTheFallenNotes.Register(
                database);

            return FateOfTheFallenNotes.GetWeatheredNote();
        }
    }
}
