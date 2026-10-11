using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Phase 2 add PoC.
    ///
    /// Exactly one Blightbound Herald is spawned when Phase 2 begins. It does
    /// not respawn during the pull. While alive it repeatedly deals heavy,
    /// named raid-wide pulse damage. SimPlayer target selection remains under
    /// native raid/group assignment control except for explicit mechanics such
    /// as the temporary interrupt assignment. Wipe/boss death destroys the add
    /// and the next pull may spawn a fresh add in Phase 2.
    /// </summary>
    internal static class RaidPhaseTwoAddRuntime
    {
        internal const string AddDisplayName =
            "Blightbound Herald [PoC]";

        internal const string PulseMechanicName =
            "Blighted Pulse";

        internal const int AddHP =
            4000000;

        internal const int AddBaseAttackDamage =
            2500;

        internal const float AddScaleMultiplier =
            1.5f;

        internal const float PulseInitialDelaySeconds =
            2f;

        internal const float PulseIntervalSeconds =
            4f;

        internal const int PulseDamagePerVictim =
            4000;

        private const float SpawnDistanceFromCenter =
            14f;

        private const float SpawnRoomMargin =
            4f;

        private const float NavMeshSampleRadius =
            4f;

        private const int SpawnPositionAttempts =
            24;

        private static NPC _boss;
        private static NPC _add;
        private static int _generation;
        private static bool _phaseActive;
        private static bool _addDefeated;


        internal static void StartPhaseTwo(
            NPC boss)
        {
            Stop(
                "phase 2 restart");

            _generation++;

            _boss =
                boss;

            _phaseActive =
                boss != null;

            _addDefeated =
                false;

            if (!_phaseActive)
            {
                return;
            }

            int generation =
                _generation;

            CoroutineHost.Start(
                SpawnAddAndRunPulses(
                    generation));
        }


        internal static void Stop(
            string reason)
        {
            _generation++;

            _phaseActive =
                false;

            RaidPhaseTwoInterruptRuntime.Stop(
                reason);

            _addDefeated =
                false;

            NPC add =
                _add;

            _add =
                null;

            _boss =
                null;

            if (add != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(
                        add.gameObject);
                }
                catch
                {
                    // Scene unload may already have destroyed the add.
                }
            }
        }


        internal static void NotifyAddDeath(
            NPC add,
            string source)
        {
            if (_addDefeated)
            {
                return;
            }

            if (_add != null &&
                add != null &&
                add != _add)
            {
                return;
            }

            RaidPhaseTwoInterruptRuntime.Stop(
                "phase 2 add defeated");

            _addDefeated =
                true;

            // Do not force SimPlayers back to the boss. Native raid/group
            // assignment logic owns target selection after the add dies.
            _add =
                null;

        }


        internal static void NotifyAddDeath(
            string source)
        {
            NotifyAddDeath(
                _add,
                source);
        }


        private static IEnumerator SpawnAddAndRunPulses(
            int generation)
        {
            Scene scene =
                SceneManager.GetActiveScene();

            if (!IsCurrent(
                    generation) ||
                !CustomRaidLoader.IsRaidPocScene(
                    scene))
            {
                yield break;
            }

            Vector3 spawnPosition;

            if (!TryResolveSpawnPosition(
                    out spawnPosition))
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] Phase 2 add could not resolve a safe spawn point inside the 50x50 boss room.");

                yield break;
            }

            GameObject markerObject =
                new GameObject(
                    "FOTF_Phase2AddSpawnMarker");

            markerObject.transform.position =
                spawnPosition;

            Vector3 roomCenter;

            if (RaidBossRoomRuntime.TryGetCenter(
                    out roomCenter))
            {
                Vector3 facing =
                    roomCenter - spawnPosition;

                facing.y =
                    0f;

                if (facing.sqrMagnitude > 0.001f)
                {
                    markerObject.transform.rotation =
                        Quaternion.LookRotation(
                            facing.normalized,
                            Vector3.up);
                }
            }

            SceneManager.MoveGameObjectToScene(
                markerObject,
                scene);

            NPC spawned =
                null;

            yield return BlightedThrallRuntime.CloneRaidTrashAtMarker(
                scene,
                null,
                markerObject.transform,
                "Phase2_Add_01",
                npc =>
                {
                    spawned =
                        npc;
                });

            UnityEngine.Object.Destroy(
                markerObject);

            if (!IsCurrent(
                    generation) ||
                spawned == null)
            {
                if (spawned != null)
                {
                    UnityEngine.Object.Destroy(
                        spawned.gameObject);
                }

                yield break;
            }

            ConfigureAdd(
                spawned);

            _add =
                spawned;

            RaidPhaseTwoInterruptRuntime.Begin(
                spawned);


            yield return WaitRealtimeWhileCurrent(
                PulseInitialDelaySeconds,
                generation);

            while (IsCurrent(
                       generation) &&
                   !_addDefeated &&
                   IsAddAlive())
            {
                if (!RaidPhaseTwoInterruptRuntime.IsCasting)
                {
                    ApplyPulse();
                }

                yield return WaitRealtimeWhileCurrent(
                    PulseIntervalSeconds,
                    generation);
            }
        }


        private static void ConfigureAdd(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            GameObject addObject =
                npc.gameObject;

            addObject.name =
                AddDisplayName;

            npc.NPCName =
                AddDisplayName;

            npc.BaseAtkDmg =
                AddBaseAttackDamage;

            npc.NoDmgCap =
                true;

            npc.noClick =
                false;

            npc.retreat =
                false;

            addObject.transform.localScale =
                addObject.transform.localScale *
                AddScaleMultiplier;

            Stats stats =
                addObject.GetComponent<Stats>();

            if (stats != null)
            {
                stats.MyName =
                    AddDisplayName;

                stats.BaseHP =
                    AddHP;

                stats.BaseArmorPenPercentage =
                    200f;

                stats.BaseMHAtkDelay =
                    120f;

                stats.OverrideHPforNPC =
                    true;

                stats.CalcStats();

                stats.ArmorPenPercentage =
                    200f;

                stats.CurrentHP =
                    stats.CurrentMaxHP;
            }

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    addObject.GetComponent<Character>();
            }

            if (character != null)
            {
                character.Alive =
                    true;
            }

            BlightedThrallBehaviour behaviour =
                addObject.GetComponent<BlightedThrallBehaviour>();

            if (behaviour != null)
            {
                // Keep this add focused on the Phase 2 pulse rather than the
                // Thrall-specific Regrowth/Rupturing Growth test abilities.
                behaviour.SetRaidBossMode();
            }

            RaidPhaseAddIdentity identity =
                addObject.GetComponent<RaidPhaseAddIdentity>();

            if (identity == null)
            {
                identity =
                    addObject.AddComponent<RaidPhaseAddIdentity>();
            }

            identity.InstanceName =
                "Phase2_Add_01";

            Character preferredTarget =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            if (preferredTarget == null ||
                !preferredTarget.Alive)
            {
                PlayerControl player =
                    GameData.PlayerControl;

                preferredTarget =
                    player != null
                        ? player.Myself
                        : null;
            }

            if (preferredTarget != null &&
                preferredTarget.Alive)
            {
                npc.CurrentAggroTarget =
                    preferredTarget;
            }

            BlightedThrallRuntime.RefreshRaidNpcNamePlate(
                npc);
        }


        private static bool TryResolveSpawnPosition(
            out Vector3 position)
        {
            position =
                Vector3.zero;

            Vector3 center;

            if (!RaidBossRoomRuntime.TryGetCenter(
                    out center))
            {
                return false;
            }

            for (int attempt = 0;
                 attempt < SpawnPositionAttempts;
                 attempt++)
            {
                float angle =
                    UnityEngine.Random.Range(
                        0f,
                        360f) *
                    Mathf.Deg2Rad;

                Vector3 direction =
                    new Vector3(
                        Mathf.Sin(angle),
                        0f,
                        Mathf.Cos(angle));

                Vector3 candidate =
                    center +
                    (direction * SpawnDistanceFromCenter);

                candidate =
                    RaidBossRoomRuntime.ClampPoint(
                        candidate,
                        SpawnRoomMargin);

                NavMeshHit hit;

                if (!NavMesh.SamplePosition(
                        candidate,
                        out hit,
                        NavMeshSampleRadius,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                if (!RaidBossRoomRuntime.ContainsPoint(
                        hit.position,
                        SpawnRoomMargin))
                {
                    continue;
                }

                position =
                    hit.position;

                return true;
            }

            return false;
        }


        private static void ApplyPulse()
        {
            NPC add =
                _add;

            if (add == null)
            {
                return;
            }

            Character source =
                add.GetChar();

            List<Character> victims =
                FindLivingRaidCharacters();

            int hits =
                0;

            for (int i = 0;
                 i < victims.Count;
                 i++)
            {
                Character target =
                    victims[i];

                if (target == null ||
                    !target.Alive ||
                    target.MyStats == null ||
                    target.MyStats.CurrentHP <= 0)
                {
                    continue;
                }

                try
                {
                    RaidMechanicDamageAttribution.SetNamedSource(
                        source,
                        PulseMechanicName);

                    target.DamageMe(
                        PulseDamagePerVictim,
                        false,
                        GameData.DamageType.Poison,
                        source,
                        false,
                        false,
                        0);

                    hits++;
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] [" +
                        PulseMechanicName +
                        "] pulse damage failed for [" +
                        target.name +
                        "]: " +
                        exception);
                }
            }

        }


        private static List<Character> FindLivingRaidCharacters()
        {
            List<Character> result =
                new List<Character>();

            PlayerControl player =
                GameData.PlayerControl;

            if (player != null &&
                player.Myself != null)
            {
                AddUniqueLivingCharacter(
                    result,
                    player.Myself);
            }

            RaidManager manager =
                GameData.RaidManager;

            if (manager != null &&
                manager.Raiders != null)
            {
                foreach (RaidMemberSlot slot in manager.Raiders)
                {
                    SimPlayer sim =
                        slot != null
                            ? slot.AssignedAvatar
                            : null;

                    if (sim == null)
                    {
                        continue;
                    }

                    NPC npc =
                        sim.GetThisNPC();

                    Character character =
                        npc != null
                            ? npc.GetChar()
                            : null;

                    AddUniqueLivingCharacter(
                        result,
                        character);
                }
            }

            return result;
        }


        private static void AddUniqueLivingCharacter(
            List<Character> result,
            Character character)
        {
            if (character == null ||
                !character.Alive ||
                character.MyStats == null ||
                character.MyStats.CurrentHP <= 0)
            {
                return;
            }

            for (int i = 0;
                 i < result.Count;
                 i++)
            {
                if (result[i] == character)
                {
                    return;
                }
            }

            result.Add(
                character);
        }


        private static bool IsAddAlive()
        {
            NPC add =
                _add;

            if (add == null)
            {
                return false;
            }

            Character character =
                add.GetChar();

            Stats stats =
                add.GetComponent<Stats>();

            return
                character != null &&
                character.Alive &&
                stats != null &&
                stats.CurrentHP > 0;
        }


        private static IEnumerator WaitRealtimeWhileCurrent(
            float seconds,
            int generation)
        {
            float end =
                Time.realtimeSinceStartup +
                seconds;

            while (IsCurrent(
                       generation) &&
                   !_addDefeated &&
                   Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }


        private static bool IsCurrent(
            int generation)
        {
            return
                generation == _generation &&
                _phaseActive &&
                _boss != null &&
                CustomRaidLoader.IsRaidPocScene(
                    SceneManager.GetActiveScene());
        }
    }


    /// <summary>
    /// Stable identity for the single Phase 2 add. This prevents the
    /// Blighted Thrall shell's generic death callback from treating it as one of
    /// the three persistent 44-minute trash lifecycle slots.
    /// </summary>
    internal sealed class RaidPhaseAddIdentity :
        MonoBehaviour
    {
        internal string InstanceName;
    }
}
