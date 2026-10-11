using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// The single authoritative runtime owner for every Fate of the Fallen
    /// custom equipment model.  No boss/item family gets its own controller.
    /// Player and SimPlayer visuals are refreshed from this one MonoBehaviour,
    /// which prevents two systems from hiding, reparenting or rebuilding the
    /// same native equipment hierarchy.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        private const string ControllerName = "FOTF_EquipmentVisuals";
        private const float SharedSimScanInterval = 0.75f;
        private static FateEquipmentVisuals _instance;

        // SW, SB and Eventide previously performed their own global
        // FindObjectsOfType<SimPlayer>() scans. One shared snapshot is enough.
        private SimPlayer[] _sharedSimPlayers =
            new SimPlayer[0];

        private float _sharedNextSimScanTime;

        internal static void Initialize()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject obj = new GameObject(ControllerName);
            UnityEngine.Object.DontDestroyOnLoad(obj);
            _instance = obj.AddComponent<FateEquipmentVisuals>();

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] unified custom item visual controller installed. " +
                "Player and SimPlayer equipment are owned by one runtime.");

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] visual modes: Carapace=NATIVE_EVENTIDE_VOID, Grasp=SKINNED.");
        }

        internal static void Shutdown()
        {
            if (_instance == null)
            {
                return;
            }

            _instance.PW_Shutdown();
            _instance.PB_Shutdown();
            _instance.SW_Shutdown();
            _instance.SB_Shutdown();
            _instance.EV_Shutdown();
            _instance._sharedSimPlayers = new SimPlayer[0];

            if (_instance.gameObject != null)
            {
                UnityEngine.Object.Destroy(_instance.gameObject);
            }

            _instance = null;
        }

        internal static bool TryTuneEquippedVisual(
            string slotToken,
            string operation,
            string[] values,
            out string message)
        {
            if (_instance == null)
            {
                message = "FOTF unified equipment visual controller is not active.";
                return false;
            }

            if (string.Equals(slotToken, "chest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "c", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "head", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "hands", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "hand", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "gloves", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "waist", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "belt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slotToken, "w", StringComparison.OrdinalIgnoreCase))
            {
                return _instance.PB_TryTuneEquippedVisual(
                    slotToken, operation, values, out message);
            }

            return _instance.PW_TryTuneEquippedVisual(
                slotToken, operation, values, out message);
        }


        private SimPlayer[] GetCachedSimPlayers()
        {
            if (_sharedSimPlayers == null ||
                Time.unscaledTime >= _sharedNextSimScanTime)
            {
                _sharedNextSimScanTime =
                    Time.unscaledTime + SharedSimScanInterval;

                _sharedSimPlayers =
                    UnityEngine.Object.FindObjectsOfType<SimPlayer>();

                if (_sharedSimPlayers == null)
                {
                    _sharedSimPlayers =
                        new SimPlayer[0];
                }
            }

            return _sharedSimPlayers;
        }

        private void Update()
        {
            PW_Update();
            PB_Update();
            SW_Update();
            SB_Update();

            // Run after the normal equipment refresh paths so the native
            // Eventide pieces are active before we apply the FOTF palette.
            EV_Update();
        }

        private void LateUpdate()
        {
            PW_LateUpdate();
            PB_LateUpdate();
            SW_LateUpdate();
            SB_LateUpdate();

            // Run last so Erenshor's native Item colour application can still
            // drive the shoulder attachments, while HeavyPlate5 is restored to
            // the dedicated Carapace chest palette before the frame renders.
            EV_LateUpdate();
        }

        private void OnDestroy()
        {
            // Defensive restore in case Unity destroys the controller without
            // FateEquipmentVisuals.Shutdown() being called first.
            EV_Shutdown();

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
