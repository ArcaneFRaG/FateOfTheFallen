using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Development input host for the Blight-Touched Colossus custom creature.
    ///
    /// This host is already proven to receive F10/F11 in Erenshor, so it also
    /// explicitly initializes CustomCreatureRuntime. That removes any ambiguity
    /// about FateContentModule call ordering while we prove the new creature.
    ///
    /// F10 = spawn/re-spawn Moldermaw.
    /// F11 = destroy the current custom creature.
    /// F12 = spawn/re-spawn Nhal'Sera for prefab/rig validation.
    /// </summary>
    internal static class BlightTouchedColossusVisualTestRuntime
    {
        private const string ControllerObjectName =
            "FOTF_BlightTouchedColossusDevelopmentInput";

        private static BlightTouchedColossusVisualTestController
            _controller;


        internal static void Initialize()
        {
            if (_controller != null)
            {
                return;
            }


            GameObject controllerObject =
                new GameObject(
                    ControllerObjectName);

            UnityEngine.Object.DontDestroyOnLoad(
                controllerObject);

            _controller =
                controllerObject.AddComponent<
                    BlightTouchedColossusVisualTestController>();


            try
            {

                CustomCreatureRuntime.Initialize();

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBossDevInput] CustomCreatureRuntime.Initialize() THREW: " +
                    exception);
            }

        }


        internal static void Shutdown()
        {
            if (_controller == null)
            {
                return;
            }

            GameObject controllerObject =
                _controller.gameObject;

            _controller =
                null;

            if (controllerObject != null)
            {
                UnityEngine.Object.Destroy(
                    controllerObject);
            }
        }
    }


    internal sealed class BlightTouchedColossusVisualTestController :
        MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(
                    KeyCode.F10))
            {

                try
                {
                    CustomCreatureRuntime
                        .SpawnDevelopmentColossus();

                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "[WorldBossDevInput] F10 CustomCreatureRuntime call THREW: " +
                        exception);
                }
            }

            if (Input.GetKeyDown(
                    KeyCode.F11))
            {

                try
                {
                    CustomCreatureRuntime
                        .DestroyCurrent();
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "[WorldBossDevInput] F11 CustomCreatureRuntime call THREW: " +
                        exception);
                }
            }

            if (Input.GetKeyDown(
                    KeyCode.F12))
            {
                try
                {
                    CustomCreatureRuntime
                        .SpawnDevelopmentNhalSera();
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "[WorldBossDevInput] F12 NhalSera development spawn THREW: " +
                        exception);
                }
            }
        }
    }
}
