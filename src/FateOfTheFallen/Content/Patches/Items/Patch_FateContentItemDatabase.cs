using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Registers non-class Fate of the Fallen content when the native
    /// item database is ready.
    /// </summary>
    [HarmonyPatch(
        typeof(ItemDatabase),
        "Start")]
    internal static class Patch_FateContentItemDatabaseStart
    {
        private static void Postfix(
            ItemDatabase __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                FateOfTheFallenQuests.Register();

                FateOfTheFallenNotes.Register(
                    __instance);

                Item weatheredNote =
                    FateOfTheFallenNotes.GetWeatheredNote();

                if (weatheredNote != null)
                {
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        __instance,
                        weatheredNote);
                }

                FateOfTheFallenNotes.LinkQuest();
                FateOfTheFallenQuests.LinkItems();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed during content ItemDatabase initialization: " +
                    exception);
            }
        }
    }
}
