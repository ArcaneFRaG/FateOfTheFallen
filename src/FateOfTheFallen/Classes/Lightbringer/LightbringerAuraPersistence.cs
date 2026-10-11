using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    // ============================================================
    // EMBERWEAVER AURA PERSISTENCE
    // ============================================================

    internal static class LightbringerAuraPersistence
    {
        // ========================================================
        // CURRENT CHARACTER CHECK
        // ========================================================

        internal static bool IsCurrentLightbringer()
        {
            /*
             * Prefer the saved class identity.
             *
             * During the character-load process Erenshor may
             * temporarily have the runtime CharacterClass set to
             * one of the native classes, so CurrentCharacterSlot
             * is the most reliable first check.
             */

            if (GameData.CurrentCharacterSlot != null &&
                string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    LightbringerConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            /*
             * Runtime fallback.
             */

            if (GameData.PlayerStats != null &&
                GameData.PlayerStats.CharacterClass != null)
            {
                return LightbringerCatalog.IsLightbringerClass(
                    GameData.PlayerStats.CharacterClass);
            }

            return false;
        }


        // ========================================================
        // PREPARE AURA FOR NATIVE SAVE
        // ========================================================

        internal static void PrepareForNativeSave()
        {
            if (!IsCurrentLightbringer())
            {
                return;
            }

            Inventory inventory =
                GameData.PlayerInv;

            SaveGameData saveData =
                GameData.CurrentCharacterSlot;

            if (inventory == null ||
                saveData == null ||
                inventory.AuraSlot == null)
            {
                return;
            }

            Item aura =
                inventory.AuraSlot.MyItem;

            /*
             * If the slot is empty, leave it to the native save
             * logic to clear AuraItem normally.
             */

            if (aura == null ||
                aura == inventory.Empty)
            {
                return;
            }

            /*
             * Only intervene for our own custom Aura items.
             */

            if (!LightbringerAuras.IsLightbringerAuraItem(
                    aura))
            {
                return;
            }

            /*
             * Native Erenshor tracks the equipped Aura in two
             * places:
             *
             *     Inventory.AuraItem
             *     SaveGameData.AuraItem
             *
             * Keep both synchronized before SaveGameData executes.
             */

            inventory.AuraItem =
                aura;

            saveData.AuraItem =
                aura.Id;

            
        }


        // ========================================================
        // RESTORE SAVED AURA
        // ========================================================

        internal static bool TryRestoreFromSave(
            string source)
        {
            if (!IsCurrentLightbringer())
            {
                return false;
            }

            SaveGameData saveData =
                GameData.CurrentCharacterSlot;

            Inventory inventory =
                GameData.PlayerInv;

            if (saveData == null ||
                inventory == null ||
                inventory.AuraSlot == null)
            {
                return false;
            }

            string savedAuraId =
                saveData.AuraItem;

            if (string.IsNullOrEmpty(
                    savedAuraId))
            {
                return false;
            }

            // ====================================================
            // RESOLVE CUSTOM AURA
            // ====================================================

            /*
             * Go directly through our custom item registry.
             *
             * At this point the important thing is that
             * LightbringerAuras.Register() has already run.
             */

            Item savedAura =
                CustomItemRegistry.GetItemById(
                    savedAuraId);

            /*
             * Backward-compatible fallback for the five current
             * Lightbringer Aura IDs.
             *
             * This means existing characters using 1930-1934
             * remain valid even if the registration implementation
             * changes later.
             */

            if (savedAura == null)
            {
                switch (savedAuraId)
                {
                    case "1930":
                        savedAura =
                            LightbringerAuras.GetItem(
                                LightbringerAuras.FirstLightKey);
                        break;

                    case "1931":
                        savedAura =
                            LightbringerAuras.GetItem(
                                LightbringerAuras.DawnsEmbraceKey);
                        break;

                    case "1932":
                        savedAura =
                            LightbringerAuras.GetItem(
                                LightbringerAuras.SanctuaryKey);
                        break;

                    case "1933":
                        savedAura =
                            LightbringerAuras.GetItem(
                                LightbringerAuras.RadiantCovenantKey);
                        break;

                    case "1934":
                        savedAura =
                            LightbringerAuras.GetItem(
                                LightbringerAuras.EternalDawnKey);
                        break;
                }
            }

            if (savedAura == null)
            {
                /*
                 * This normally means this restore attempt occurred
                 * before LightbringerAuras.Register().
                 *
                 * Do not erase the saved ID. A later restore hook
                 * will try again.
                 */

               

                return false;
            }

            if (!LightbringerAuras.IsLightbringerAuraItem(
                    savedAura))
            {
                return false;
            }

            Item currentAura =
                inventory.AuraSlot.MyItem;

            // ====================================================
            // ALREADY RESTORED
            // ====================================================

            if (currentAura == savedAura)
            {
                inventory.AuraItem =
                    savedAura;

                if (GameData.PlayerStats != null)
                {
                    GameData.PlayerStats.CheckAuras();
                }

                return true;
            }

            // ====================================================
            // DO NOT OVERWRITE A LIVE AURA
            // ====================================================

            /*
             * This is important because TryRestoreFromSave() can be
             * called from more than one load hook.
             *
             * If something is already legitimately equipped, don't
             * replace it with the original save value.
             */

            if (currentAura != null &&
                currentAura != inventory.Empty)
            {
                return false;
            }

            // ====================================================
            // RESTORE SLOT
            // ====================================================

            inventory.AuraSlot.MyItem =
                savedAura;

            inventory.AuraSlot.Quantity =
                1;

            inventory.AuraItem =
                savedAura;

            inventory.AuraSlot.UpdateSlotImage();

            // ====================================================
            // RE-ACTIVATE AURA EFFECT
            // ====================================================

            if (GameData.PlayerStats != null)
            {
                GameData.PlayerStats.CheckAuras();
            }

            

            return true;
        }
    }


    // ============================================================
    // NATIVE SAVE BRIDGE
    // ============================================================

    [HarmonyPatch(
        typeof(GameManager),
        "SaveGameData",
        new Type[]
        {
            typeof(bool)
        })]
    internal static class Patch_LightbringerAuraNativeSave
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            try
            {
                LightbringerAuraPersistence
                    .PrepareForNativeSave();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Lightbringer: Aura native-save synchronization failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // INVENTORY LOAD RETRY
    // ============================================================

    [HarmonyPatch(
        typeof(Inventory),
        "Start")]
    internal static class Patch_LightbringerAuraEquipmentLoad
    {
        [HarmonyPostfix]
        private static void Postfix(
            Inventory __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                if (__instance != GameData.PlayerInv)
                {
                    return;
                }

                /*
                 * This may be too early if the SpellDB has not yet
                 * registered the Lightbringer Aura items.
                 *
                 * That's okay. TryRestoreFromSave() deliberately
                 * fails harmlessly and we'll retry after
                 * LightbringerAuras.Register().
                 */

                LightbringerAuraPersistence
                    .TryRestoreFromSave(
                        "Inventory.Start");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Lightbringer: Aura restoration after Inventory.Start failed: " +
                    exception);
            }
        }
    }
}