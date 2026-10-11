using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 6 interaction layer for the permanent Ancient Warding Shrine.
    ///
    /// The shrine itself remains available whenever the expansion gate is open.
    /// This runtime only adds a click target over the central warding seal and
    /// awards item 1704 while The Pattern Beneath is active.
    /// </summary>
    internal static class Quest6ShrineInteractionRuntime
    {
        private const string InteractionObjectName =
            "FOTF_Q6_WardingSealInteraction";

        private const string LogPrefix =
            "[Quest6Shrine] ";

        private const int PollFrames =
            30;

        private static bool _initialized;
        private static Coroutine _monitorRoutine;
        private static GameObject _configuredShrine;
        private static Quest6ShrineInteractable _interactable;


        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized =
                true;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            Scene scene =
                SceneManager.GetActiveScene();

            if (FOTFExpansionGate.IsUnlocked &&
                IsAzure(
                    scene))
            {
                StartMonitor(
                    scene);
            }
        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized =
                false;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            StopMonitor();

            _configuredShrine =
                null;

            _interactable =
                null;
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Ignore the temporary additive Reliquary source scene used by the
            // native statue cache while Azure remains the active gameplay scene.
            if (mode == LoadSceneMode.Additive &&
                SceneManager.GetActiveScene() != scene)
            {
                return;
            }

            StopMonitor();

            _configuredShrine =
                null;

            _interactable =
                null;

            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !IsAzure(
                    scene))
            {
                return;
            }

            StartMonitor(
                scene);
        }


        private static bool IsAzure(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    AncientWardingShrineRuntime.SceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void StartMonitor(
            Scene scene)
        {
            if (_monitorRoutine != null)
            {
                return;
            }

            _monitorRoutine =
                CoroutineHost.Start(
                    Monitor(
                        scene));
        }


        private static void StopMonitor()
        {
            if (_monitorRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                _monitorRoutine);

            _monitorRoutine =
                null;
        }


        private static IEnumerator Monitor(
            Scene scene)
        {
            while (_initialized &&
                   scene.IsValid() &&
                   scene.isLoaded &&
                   IsAzure(
                       SceneManager.GetActiveScene()))
            {
                ThePatternBeneathQuest.Register();
                ThePatternBeneathQuest.EnsureChainState();

                GameObject shrine =
                    AncientWardingShrineRuntime.CurrentShrine;

                if (shrine != null &&
                    shrine != _configuredShrine)
                {
                    ConfigureShrine(
                        shrine);
                }

                if (_interactable != null)
                {
                    _interactable.RefreshState();
                }

                for (int frame = 0;
                     frame < PollFrames;
                     frame++)
                {
                    if (!_initialized ||
                        !scene.IsValid() ||
                        !scene.isLoaded)
                    {
                        _monitorRoutine =
                            null;

                        yield break;
                    }

                    yield return null;
                }
            }

            _monitorRoutine =
                null;
        }


        private static void ConfigureShrine(
            GameObject shrine)
        {
            if (shrine == null)
            {
                return;
            }

            Transform interactionAnchor =
                FindChildRecursive(
                    shrine.transform,
                    "InteractionAnchor");

            if (interactionAnchor == null)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "cannot configure Quest 6 interaction because InteractionAnchor is missing.");

                return;
            }

            Transform existing =
                FindChildRecursive(
                    shrine.transform,
                    InteractionObjectName);

            GameObject interactionObject;

            if (existing != null)
            {
                interactionObject =
                    existing.gameObject;
            }
            else
            {
                interactionObject =
                    new GameObject(
                        InteractionObjectName);

                interactionObject.transform.position =
                    interactionAnchor.position;

                interactionObject.transform.rotation =
                    Quaternion.identity;

                interactionObject.transform.localScale =
                    Vector3.one;

                // Keep the collider physically neutral even if the imported
                // shrine root carries an FBX compensation scale/rotation.
                interactionObject.transform.SetParent(
                    shrine.transform,
                    true);
            }

            SphereCollider collider =
                interactionObject.GetComponent<SphereCollider>();

            if (collider == null)
            {
                collider =
                    interactionObject.AddComponent<SphereCollider>();
            }

            collider.center =
                Vector3.zero;

            collider.radius =
                1.35f;

            collider.isTrigger =
                true;

            collider.enabled =
                true;

            Quest6ShrineInteractable interactable =
                interactionObject.GetComponent<Quest6ShrineInteractable>();

            if (interactable == null)
            {
                interactable =
                    interactionObject.AddComponent<Quest6ShrineInteractable>();
            }

            interactable.Configure(
                shrine.transform,
                interactionAnchor,
                collider);

            _configuredShrine =
                shrine;

            _interactable =
                interactable;

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "central seal interaction armed at [" +
                interactionAnchor.position +
                "].");
        }


        private static Transform FindChildRecursive(
            Transform root,
            string childName)
        {
            if (root == null ||
                string.IsNullOrEmpty(
                    childName))
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    childName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform found =
                    FindChildRecursive(
                        root.GetChild(
                            i),
                        childName);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }


    internal sealed class Quest6ShrineInteractable :
        MonoBehaviour
    {
        private const float InteractionRange =
            6.0f;

        private Transform _shrineRoot;
        private Transform _interactionAnchor;
        private Collider _collider;
        private bool _harvested;


        internal void Configure(
            Transform shrineRoot,
            Transform interactionAnchor,
            Collider interactionCollider)
        {
            _shrineRoot =
                shrineRoot;

            _interactionAnchor =
                interactionAnchor;

            _collider =
                interactionCollider;

            _harvested =
                ThePatternBeneathContent.PlayerHasFragment() ||
                ThePatternBeneathQuest.IsComplete();

            RefreshState();
        }


        internal void RefreshState()
        {
            bool spent =
                ThePatternBeneathContent.PlayerHasFragment() ||
                ThePatternBeneathQuest.IsComplete();

            _harvested =
                spent;

            if (_collider != null)
            {
                // Before Q6 is active, leave the seal clickable for flavour.
                // Once its fragment has been recovered, the shrine becomes
                // mechanically inert until a quest reset removes that state.
                _collider.enabled =
                    !spent;
            }
        }


        private void OnMouseDown()
        {
            TryExamine();
        }


        private void TryExamine()
        {
            ThePatternBeneathQuest.Register();
            ThePatternBeneathQuest.EnsureChainState();

            if (_harvested ||
                ThePatternBeneathContent.PlayerHasFragment() ||
                ThePatternBeneathQuest.IsComplete())
            {
                RefreshState();
                return;
            }

            if (!FOTFExpansionGate.IsUnlocked)
            {
                return;
            }

            if (!ThePatternBeneathQuest.IsActive())
            {
                AddSystemMessage(
                    "The ancient warding seal is fractured, but its purpose is not yet clear.",
                    "yellow");

                return;
            }

            PlayerControl player =
                GameData.PlayerControl;

            if (player == null)
            {
                return;
            }

            Vector3 point =
                _interactionAnchor != null
                    ? _interactionAnchor.position
                    : transform.position;

            float distance =
                Vector3.Distance(
                    player.transform.position,
                    point);

            if (distance >
                InteractionRange)
            {
                AddSystemMessage(
                    "You are too far away to examine the ancient warding seal.",
                    "yellow");

                return;
            }

            if (!ThePatternBeneathContent.TryGiveFragment(
                    true))
            {
                return;
            }

            _harvested =
                true;

            RefreshState();

            Plugin.NativeLog.LogInfo(
                "[Quest6Shrine] player examined the Ancient Warding Shrine and recovered Fractured Warding Fragment [1704].");
        }


        private static void AddSystemMessage(
            string text,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        text,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? ""));
            }
            catch
            {
            }
        }
    }
}
