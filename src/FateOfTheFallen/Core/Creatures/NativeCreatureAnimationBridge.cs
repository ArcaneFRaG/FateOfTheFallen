using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class NativeCreatureAnimationBridge
    {
        private const string LogPrefix = "[FOTF Creature Animation] ";

        private static RuntimeAnimatorController _capturedController;
        private static string _capturedControllerName;
        private static string _capturedSourceObjectName;
        private static string _capturedSourceNpcName;
        private static bool _loggedControllerDiagnostics;

        internal static bool HasCapturedController => _capturedController != null;
        internal static string CapturedControllerName => _capturedControllerName ?? "<none>";
        internal static string CapturedSourceName =>
            !string.IsNullOrEmpty(_capturedSourceNpcName)
                ? _capturedSourceNpcName
                : (_capturedSourceObjectName ?? "<none>");

        internal static bool TryCaptureBestNativeController()
        {
            Animator[] animators = UnityEngine.Object.FindObjectsOfType<Animator>(true);

            if (animators == null || animators.Length == 0)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix + "no Animator components were found in the loaded scene.");
                return false;
            }

            Animator bestAnimator = null;
            NPC bestNpc = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];

                if (!IsUsableNativeSource(animator))
                    continue;

                NPC npc = ResolveNpc(animator);
                int score = ScoreSource(animator, npc);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestAnimator = animator;
                bestNpc = npc;
            }

            if (bestAnimator == null)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "could not find a usable native NPC humanoid Animator. " +
                    "Use this in a normal Erenshor world zone containing a living humanoid NPC/enemy.");
                return false;
            }

            return Capture(bestAnimator, bestNpc);
        }

        internal static bool TryCaptureFrom(NPC sourceNpc)
        {
            if (sourceNpc == null)
                return false;

            Animator animator = sourceNpc.GetComponent<Animator>();

            if (animator == null)
                animator = sourceNpc.GetComponentInChildren<Animator>(true);

            if (!IsUsableNativeSource(animator))
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "NPC [" + GetNpcName(sourceNpc) +
                    "] does not expose a usable humanoid Animator/controller.");
                return false;
            }

            return Capture(animator, sourceNpc);
        }

        private static bool Capture(Animator animator, NPC npc)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            _capturedController = animator.runtimeAnimatorController;
            _capturedControllerName = _capturedController.name;
            _capturedSourceObjectName =
                animator.gameObject != null ? animator.gameObject.name : "<unknown>";
            _capturedSourceNpcName = GetNpcName(npc);


            LogControllerDiagnostics(animator, npc);
            return true;
        }

        internal static bool TryCaptureAndApply(GameObject targetRoot)
        {
            if (!HasCapturedController && !TryCaptureBestNativeController())
                return false;

            return TryApply(targetRoot);
        }

        internal static bool TryApply(GameObject targetRoot)
        {
            if (targetRoot == null)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix + "cannot apply controller: target root was null.");
                return false;
            }

            Animator targetAnimator = targetRoot.GetComponent<Animator>();

            if (targetAnimator == null)
                targetAnimator = targetRoot.GetComponentInChildren<Animator>(true);

            if (targetAnimator == null)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "custom creature [" + targetRoot.name +
                    "] contains no Animator.");
                return false;
            }

            return TryApply(targetAnimator);
        }

        internal static bool TryApply(Animator targetAnimator)
        {
            if (targetAnimator == null)
                return false;

            if (!HasCapturedController)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix + "no native controller has been captured yet.");
                return false;
            }

            Avatar targetAvatar = targetAnimator.avatar;

            if (targetAvatar == null)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "target Animator [" + targetAnimator.name +
                    "] has no Avatar. The Colossus FBX must remain configured as Humanoid.");
                return false;
            }

            if (!targetAvatar.isValid)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "target Avatar [" + targetAvatar.name + "] is invalid.");
                return false;
            }

            if (!targetAvatar.isHuman)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "target Avatar [" + targetAvatar.name + "] is not Humanoid.");
                return false;
            }

            RuntimeAnimatorController controller =
                CloneControllerForTarget(_capturedController);

            if (controller == null)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "failed to clone/resolve captured controller [" +
                    CapturedControllerName + "].");
                return false;
            }

            try
            {
                targetAnimator.runtimeAnimatorController = controller;
                targetAnimator.applyRootMotion = false;
                targetAnimator.enabled = true;

                targetAnimator.Rebind();
                targetAnimator.Update(0f);


                LogTargetParameters(targetAnimator);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "failed applying native controller to [" +
                    targetAnimator.name + "]: " + exception);
                return false;
            }
        }

        private static RuntimeAnimatorController CloneControllerForTarget(
            RuntimeAnimatorController source)
        {
            if (source == null)
                return null;

            AnimatorOverrideController sourceOverride =
                source as AnimatorOverrideController;

            if (sourceOverride == null)
                return source;

            RuntimeAnimatorController baseController =
                sourceOverride.runtimeAnimatorController;

            if (baseController == null)
                return source;

            AnimatorOverrideController clone =
                new AnimatorOverrideController(baseController);

            List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>(
                    sourceOverride.overridesCount);

            sourceOverride.GetOverrides(overrides);
            clone.ApplyOverrides(overrides);
            clone.name = "FOTF_" + sourceOverride.name + "_Clone";

            return clone;
        }

        private static bool IsUsableNativeSource(Animator animator)
        {
            if (animator == null ||
                animator.gameObject == null ||
                animator.runtimeAnimatorController == null)
            {
                return false;
            }

            string objectName = animator.gameObject.name ?? string.Empty;

            if (objectName.IndexOf(
                    "BlightTouchedColossus",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf(
                    "FOTF_",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (!animator.gameObject.scene.IsValid() ||
                !animator.gameObject.scene.isLoaded)
            {
                return false;
            }

            Avatar avatar = animator.avatar;

            if (avatar == null ||
                !avatar.isValid ||
                !avatar.isHuman)
            {
                return false;
            }

            return true;
        }

        private static int ScoreSource(Animator animator, NPC npc)
        {
            int score = 0;

            if (npc != null)
            {
                score += 1000;

                Character character = npc.GetComponent<Character>();

                if (character != null && character.Alive)
                    score += 100;
            }

            if (animator.isActiveAndEnabled)
                score += 50;

            if (animator.avatar != null && animator.avatar.isHuman)
                score += 25;

            AnimationClip[] clips =
                animator.runtimeAnimatorController.animationClips;

            if (clips != null)
                score += Mathf.Min(clips.Length, 25);

            return score;
        }

        private static NPC ResolveNpc(Animator animator)
        {
            if (animator == null)
                return null;

            NPC npc = animator.GetComponent<NPC>();
            if (npc != null)
                return npc;

            npc = animator.GetComponentInParent<NPC>();
            if (npc != null)
                return npc;

            return animator.GetComponentInChildren<NPC>(true);
        }

        private static void LogControllerDiagnostics(Animator animator, NPC npc)
        {
            if (_loggedControllerDiagnostics || animator == null)
                return;

            _loggedControllerDiagnostics = true;


            try
            {
                AnimatorControllerParameter[] parameters = animator.parameters;

                if (parameters != null)
                {
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        AnimatorControllerParameter parameter = parameters[i];

                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "failed reading Animator parameters: " +
                    exception.Message);
            }

            try
            {
                AnimationClip[] clips =
                    animator.runtimeAnimatorController.animationClips;

                if (clips != null)
                {
                    HashSet<string> seen =
                        new HashSet<string>(StringComparer.Ordinal);

                    for (int i = 0; i < clips.Length; i++)
                    {
                        AnimationClip clip = clips[i];

                        if (clip == null || !seen.Add(clip.name))
                            continue;

                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "failed reading controller clips: " +
                    exception.Message);
            }

            if (npc != null)
                LogNpcAnimationClipFields(npc);

        }

        private static void LogNpcAnimationClipFields(NPC npc)
        {
            try
            {
                FieldInfo[] fields =
                    npc.GetType().GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];

                    if (!typeof(AnimationClip).IsAssignableFrom(field.FieldType))
                        continue;

                    AnimationClip clip =
                        field.GetValue(npc) as AnimationClip;

                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "failed reflecting NPC AnimationClip fields: " +
                    exception.Message);
            }
        }

        private static void LogTargetParameters(Animator targetAnimator)
        {
            try
            {
                AnimatorControllerParameter[] parameters =
                    targetAnimator.parameters;

            }
            catch
            {
            }
        }

        internal static void ClearCapturedController()
        {
            _capturedController = null;
            _capturedControllerName = null;
            _capturedSourceObjectName = null;
            _capturedSourceNpcName = null;
            _loggedControllerDiagnostics = false;

        }

        private static string GetNpcName(NPC npc)
        {
            if (npc == null)
                return null;

            try
            {
                if (!string.IsNullOrEmpty(npc.NPCName))
                    return npc.NPCName;
            }
            catch
            {
            }

            return
                npc.gameObject != null
                    ? npc.gameObject.name
                    : null;
        }

        private static string GetAvatarDescription(Avatar avatar)
        {
            if (avatar == null)
                return "<null>";

            return
                avatar.name +
                "; Valid=" + avatar.isValid +
                "; Human=" + avatar.isHuman;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }

    /// <summary>
    /// First-pass runtime probe. Add this component AFTER instantiating a
    /// custom creature prefab in Erenshor. It retries until a native NPC
    /// controller is available, applies it once, then disables itself.
    ///
    /// Do not place this script inside the AssetBundle prefab in the separate
    /// Unity project. Add it from the FOTF runtime assembly instead:
    ///
    /// customCreature.AddComponent<NativeCreatureAnimationProbe>();
    /// </summary>
    internal sealed class NativeCreatureAnimationProbe : MonoBehaviour
    {
        private const float RetryInterval = 0.50f;

        private float _nextAttemptTime;
        private bool _applied;

        private void Update()
        {
            if (_applied ||
                Time.unscaledTime < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime =
                Time.unscaledTime + RetryInterval;

            _applied =
                NativeCreatureAnimationBridge.TryCaptureAndApply(
                    gameObject);

            if (_applied)
            {

                enabled = false;
            }
        }
    }
}
