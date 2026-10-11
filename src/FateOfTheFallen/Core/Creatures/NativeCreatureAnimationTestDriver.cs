using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Temporary manual animation test driver for the custom
    /// Blight-Touched Colossus visual.
    ///
    /// This intentionally does not move the GameObject. Walk/run play in
    /// place because root motion is disabled by NativeCreatureAnimationBridge.
    ///
    /// Controls:
    /// Numpad 1 = Idle
    /// Numpad 2 = Walk
    /// Numpad 3 = Run
    /// Numpad 4 = Melee swing
    /// Numpad 5 = Die
    /// Numpad 6 = Revive
    /// </summary>
    internal sealed class NativeCreatureAnimationTestDriver : MonoBehaviour
    {
        private Animator _animator;
        private bool _readyLogged;

        private void Update()
        {
            if (!EnsureAnimatorReady())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetIdle();
            }

            if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                SetWalk();
            }

            if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                SetRun();
            }

            if (Input.GetKeyDown(KeyCode.Keypad4))
            {
                TriggerMelee();
            }

            if (Input.GetKeyDown(KeyCode.Keypad5))
            {
                TriggerDeath();
            }

            if (Input.GetKeyDown(KeyCode.Keypad6))
            {
                TriggerRevive();
            }
        }

        private bool EnsureAnimatorReady()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();

                if (_animator == null)
                {
                    _animator = GetComponentInChildren<Animator>(true);
                }
            }

            if (_animator == null ||
                _animator.runtimeAnimatorController == null)
            {
                return false;
            }

            if (!_readyLogged)
            {
                _readyLogged = true;

            }

            return true;
        }

        private void SetIdle()
        {
            ResetMovement();
            SetBoolIfPresent("Dead", false);
            SetBoolIfPresent("Stunned", false);

        }

        private void SetWalk()
        {
            SetBoolIfPresent("Dead", false);
            SetBoolIfPresent("Running", false);
            SetBoolIfPresent("Backing", false);
            SetBoolIfPresent("Walking", true);

        }

        private void SetRun()
        {
            SetBoolIfPresent("Dead", false);
            SetBoolIfPresent("Walking", false);
            SetBoolIfPresent("Backing", false);
            SetFloatIfPresent("RunSpeedModifier", 1f);
            SetBoolIfPresent("Running", true);

        }

        private void TriggerMelee()
        {
            ResetMovement();
            SetBoolIfPresent("Dead", false);
            SetIntegerIfPresent("AttackIndex", 0);
            ResetTriggerIfPresent("MeleeSwing");
            SetTriggerIfPresent("MeleeSwing");

        }

        private void TriggerDeath()
        {
            ResetMovement();
            ResetTriggerIfPresent("Die");
            SetBoolIfPresent("Dead", true);
            SetTriggerIfPresent("Die");

        }

        private void TriggerRevive()
        {
            ResetMovement();
            SetBoolIfPresent("Dead", false);
            ResetTriggerIfPresent("Revive");
            SetTriggerIfPresent("Revive");

        }

        private void ResetMovement()
        {
            SetBoolIfPresent("Patrol", false);
            SetBoolIfPresent("Walking", false);
            SetBoolIfPresent("Running", false);
            SetBoolIfPresent("Backing", false);
            SetBoolIfPresent("pivotLeft", false);
            SetBoolIfPresent("pivotRight", false);
            SetBoolIfPresent("strafeLeft", false);
            SetBoolIfPresent("strafeRight", false);
        }

        private void SetBoolIfPresent(string parameterName, bool value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
            {
                _animator.SetBool(parameterName, value);
            }
        }

        private void SetFloatIfPresent(string parameterName, float value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Float))
            {
                _animator.SetFloat(parameterName, value);
            }
        }

        private void SetIntegerIfPresent(string parameterName, int value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Int))
            {
                _animator.SetInteger(parameterName, value);
            }
        }

        private void SetTriggerIfPresent(string parameterName)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Trigger))
            {
                _animator.SetTrigger(parameterName);
            }
        }

        private void ResetTriggerIfPresent(string parameterName)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Trigger))
            {
                _animator.ResetTrigger(parameterName);
            }
        }

        private bool HasParameter(
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter[] parameters = _animator.parameters;

            if (parameters == null)
            {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];

                if (parameter.type == parameterType &&
                    string.Equals(
                        parameter.name,
                        parameterName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
