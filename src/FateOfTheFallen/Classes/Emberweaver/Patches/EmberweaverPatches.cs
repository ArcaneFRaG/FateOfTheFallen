using HarmonyLib;
using System;
using TMPro;
using UnityEngine;

namespace FateOfTheFallen
{
    // ============================================================
    // CLASS DATABASE / PERSISTENCE
    // ============================================================

    [HarmonyPatch(
        typeof(ClassDB),
        "Awake")]
    internal static class Patch_EmberweaverClassDatabase
    {
        private static void Postfix()
        {
            EmberweaverCatalog.TryEnsureClass();
        }
    }


    [HarmonyPatch(
        typeof(ItemDatabase),
        "Start")]
    internal static class Patch_EmberweaverItemDatabaseStart
    {
        private static void Postfix(
            ItemDatabase __instance)
        {
            EmberweaverModule.RegisterItemDatabase(
                __instance);
        }
    }


    [HarmonyPatch(
        typeof(CharSelectManager),
        "SelectSlot")]
    internal static class Patch_EmberweaverCharSelectSlot
    {
        private static void Prefix(
            CharSelectManager __instance)
        {
            EmberweaverCharacterState
                .RestoreIfCurrentSlotIsEmberweaver(
                    __instance);
        }

        private static void Postfix(
            CharSelectManager __instance)
        {
            EmberweaverCharacterState
                .RestoreIfCurrentSlotIsEmberweaver(
                    __instance);
        }
    }


    [HarmonyPatch(
        typeof(CharSelectManager),
        "Play")]
    internal static class Patch_EmberweaverCharSelectPlay
    {
        private static void Prefix(
            CharSelectManager __instance)
        {
            EmberweaverCharacterState
                .RestoreIfCurrentSlotIsEmberweaver(
                    __instance);
        }
    }


    [HarmonyPatch(
        typeof(CharSelectManager),
        "PlayAlternate")]
    internal static class Patch_EmberweaverCharSelectPlayAlternate
    {
        private static void Prefix(
            CharSelectManager __instance)
        {
            EmberweaverCharacterState
                .RestoreIfCurrentSlotIsEmberweaver(
                    __instance);
        }
    }


    [HarmonyPatch(
        typeof(CharSelectManager),
        "SaveChar")]
    internal static class Patch_EmberweaverCharSelectSave
    {
        private static void Prefix()
        {
            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver())
            {
                return;
            }

            /*
             * The FOTF character creator sets Emberweaver as manager.selClass
             * and as the runtime Class. Preserve that identity through the
             * native save path and apply the data-driven starting loadout.
             */
            EmberweaverCharacterState.RestoreRuntimeClass();
            EmberweaverStartingLoadout.GrantStartingLoadout();
        }

        private static void Postfix()
        {
            EmberweaverCharacterState
                .ForceCurrentSaveIdentityIfSelected();
        }
    }


    // ============================================================
    // ITEM TOOLTIP ELIGIBILITY
    // ============================================================

    [HarmonyPatch(
        typeof(ItemInfoWindow),
        "DisplayItem",
        new Type[]
        {
            typeof(Item),
            typeof(Vector2),
            typeof(int)
        })]
    internal static class Patch_EmberweaverItemInfoWindow
    {
        private static void Postfix(
            Item item,
            ItemInfoWindow __instance)
        {
            if (item == null ||
                __instance == null)
            {
                return;
            }

            try
            {
                AddEmberweaverClassEligibility(
                    item,
                    __instance);

                AddEmberweaverTeachingRequirement(
                    item,
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: item-tooltip eligibility patch failed",
                    exception);
            }
        }

        private static void AddEmberweaverTeachingRequirement(
            Item item,
            ItemInfoWindow instance)
        {
            int requiredLevel =
                0;

            EmberweaverSpellDefinition spellDefinition;

            if (EmberweaverTeachingItems.TryGetSpellDefinition(
                    item,
                    out spellDefinition))
            {
                requiredLevel =
                    spellDefinition.RequiredLevel;
            }
            else
            {
                EmberweaverSkillDefinition skillDefinition;

                if (EmberweaverTeachingItems.TryGetSkillDefinition(
                        item,
                        out skillDefinition))
                {
                    requiredLevel =
                        skillDefinition.RequiredLevel;
                }
                else if (item.TeachSkill != null &&
                         ArcanistDerivedCompatibility
                             .IsApprovedSharedSkill(
                                 item.TeachSkill))
                {
                    // Shared native Arcanist skillbooks use the Arcanist
                    // requirement as Emberweaver's requirement too.
                    requiredLevel =
                        item.TeachSkill.ArcanistRequiredLevel;
                }
            }

            if (requiredLevel <= 0 ||
                instance.ReqLvl == null)
            {
                return;
            }

            TextMeshProUGUI text =
                instance.ReqLvl.GetComponent<TextMeshProUGUI>();

            if (text == null)
            {
                return;
            }

            string current =
                text.text ??
                string.Empty;

            if (current.IndexOf(
                    "Emberweaver:",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            string line =
                "Emberweaver: " +
                requiredLevel +
                "\n";

            int headerIndex =
                current.IndexOf(
                    "Required Level:",
                    StringComparison.OrdinalIgnoreCase);

            if (headerIndex >= 0)
            {
                int insertAt =
                    current.IndexOf(
                        '\n',
                        headerIndex);

                if (insertAt >= 0)
                {
                    text.text =
                        current.Insert(
                            insertAt + 1,
                            line);

                    return;
                }
            }

            text.text =
                current +
                (current.EndsWith("\n") || current.Length == 0
                    ? string.Empty
                    : "\n") +
                "Required Level:\n" +
                line;
        }

        private static void AddEmberweaverClassEligibility(
            Item item,
            ItemInfoWindow instance)
        {
            if (!ArcanistDerivedCompatibility
                    .CanEmberweaverUseItem(
                        item))
            {
                return;
            }

            TextMeshProUGUI usable =
                instance.Usable;

            if (usable == null)
            {
                return;
            }

            string current =
                usable.text ??
                string.Empty;

            if (current.IndexOf(
                    EmberweaverConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                usable.text =
                    EmberweaverConstants.DisplayName;

                return;
            }

            usable.text =
                current.TrimEnd() +
                " " +
                EmberweaverConstants.DisplayName +
                " ";
        }
    }
}
