using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// First encounter-mechanics proof of concept for FOTF raids.
    ///
    /// A live raid boss periodically creates a visible soak telegraph. FOTF
    /// temporarily takes ownership of a small set of SimPlayers by suppressing
    /// only SimPlayer.InRaidLogic(), moves those Sims into assigned soak
    /// positions, holds them through impact, applies one shared damage event,
    /// then releases them back to native raid AI.
    ///
    /// This is intentionally a reusable controller rather than boss-specific
    /// hardcoding. Later mechanics can use the same temporary AI-override model.
    /// </summary>
    internal static class RaidSoakMechanicRuntime
    {
        internal const string MechanicName =
            "Blighted Convergence";

        private const string CoordinatorOwner =
            "RaidSoak";

        internal const int RequiredSimPlayers =
            4;

        internal const float SoakRadius =
            5f;

        internal const float TelegraphSeconds =
            10f;

        internal const float InitialDelaySeconds =
            8f;

        internal const float RepeatDelaySeconds =
            20f;

        internal const int TotalSplitDamage =
            12000;

        // The authored boss room is 50 x 50 Unity units. The soak centre is
        // constrained so the full 5m-radius telegraph remains inside the room
        // with additional wall clearance. This prevents valid NavMesh samples
        // from leaking into the corridor while preserving long-distance runs.
        private const float MinSoakSpawnDistance =
            18f;

        private const float BossRoomWallBuffer =
            2f;

        private const int SoakPlacementAttempts =
            32;

        private const float SoakPlacementSampleRadius =
            2.5f;

        private const float AssignmentRingRadius =
            1.75f;

        private const float ArrivalTolerance =
            0.85f;

        // Once a Sim reaches its assigned soak point, the mechanic owns that
        // position until impact. Native NPC logic can otherwise reacquire a
        // target or nudge the NavMeshAgent back toward combat after arrival.
        private const float HoldDriftTolerance =
            0.20f;

        private const float FallbackMoveSpeed =
            8f;

        private sealed class SoakAssignment
        {
            internal SimPlayer Sim;
            internal NPC Npc;
            internal Character SavedAggroTarget;
            internal NavMeshAgent Agent;
            internal Vector3 TargetPosition;
            internal int InstanceId;
            internal bool HasArrived;
        }

        private static readonly List<SoakAssignment> ActiveAssignments =
            new List<SoakAssignment>();

        private static readonly HashSet<int> SuppressedSimIds =
            new HashSet<int>();

        private static NPC _boss;
        private static GameObject _zoneObject;
        private static Material _zoneMaterial;
        private static int _generation;
        private static bool _mechanicActive;


        internal static void AttachBoss(
            NPC boss)
        {
            CancelActiveMechanic(
                "boss attach");

            _generation++;
            _boss =
                boss;

            if (_boss == null)
            {
                return;
            }

            int generation =
                _generation;

            CoroutineHost.Start(
                RunBossMechanicLoop(
                    generation));

        }


        internal static void Stop(
            string reason)
        {
            _generation++;
            _boss =
                null;

            CancelActiveMechanic(
                reason);
        }


        internal static void CancelActiveMechanic(
            string reason)
        {
            if (_mechanicActive ||
                ActiveAssignments.Count > 0 ||
                _zoneObject != null)
            {
            }

            ReleaseAssignments();
            DestroyZone();

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);

            _mechanicActive =
                false;
        }


        internal static bool ShouldSuppressNativeRaidLogic(
            SimPlayer sim)
        {
            if (sim == null ||
                SuppressedSimIds.Count == 0)
            {
                return false;
            }

            return SuppressedSimIds.Contains(
                sim.GetInstanceID());
        }


        private static IEnumerator RunBossMechanicLoop(
            int generation)
        {
            // Wait for the boss to enter real combat before starting the
            // encounter-mechanic cadence.
            while (IsControllerCurrent(
                       generation))
            {
                if (IsBossEngaged())
                {
                    break;
                }

                yield return null;
            }

            if (!IsControllerCurrent(
                    generation))
            {
                yield break;
            }

            yield return WaitRealtimeWhileCurrent(
                InitialDelaySeconds,
                generation);

            while (IsControllerCurrent(
                       generation))
            {
                if (!IsBossEngaged())
                {
                    // A disengaged but living boss is normally a wipe/reset.
                    // Do not fire mechanics until native combat is active again.
                    yield return null;
                    continue;
                }

                yield return ExecuteSoak(
                    generation);

                if (!IsControllerCurrent(
                        generation))
                {
                    yield break;
                }

                yield return WaitRealtimeWhileCurrent(
                    RepeatDelaySeconds,
                    generation);
            }
        }


        private static IEnumerator ExecuteSoak(
            int generation)
        {
            if (!IsControllerCurrent(
                    generation) ||
                _mechanicActive)
            {
                yield break;
            }

            Vector3 soakCenter;

            if (!TryResolveSoakCenter(
                    out soakCenter))
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] soak mechanic skipped because no safe NavMesh point could be resolved near the boss.");

                yield break;
            }

            List<SimPlayer> selected =
                SelectSoakers(
                    soakCenter,
                    RequiredSimPlayers);

            if (selected.Count == 0)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] [" +
                    MechanicName +
                    "] skipped because no living raid SimPlayers were available.");

                yield break;
            }

            if (!RaidMechanicCoordinator.TryAcquire(
                    CoordinatorOwner))
            {

                yield break;
            }

            _mechanicActive =
                true;

            CreateZone(
                soakCenter);

            BuildAssignments(
                selected,
                soakCenter);


            float impactAt =
                Time.realtimeSinceStartup +
                TelegraphSeconds;

            while (IsControllerCurrent(
                       generation) &&
                   _mechanicActive &&
                   Time.realtimeSinceStartup < impactAt)
            {
                UpdateAssignmentMovement();
                yield return null;
            }

            if (!IsControllerCurrent(
                    generation) ||
                !_mechanicActive)
            {
                CancelActiveMechanic(
                    "controller invalidated before impact");

                yield break;
            }

            ApplySoakImpact(
                soakCenter);

            ReleaseAssignments();
            DestroyZone();

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);

            _mechanicActive =
                false;

        }


        private static IEnumerator WaitRealtimeWhileCurrent(
            float seconds,
            int generation)
        {
            float end =
                Time.realtimeSinceStartup +
                seconds;

            while (IsControllerCurrent(
                       generation) &&
                   Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }


        private static bool IsControllerCurrent(
            int generation)
        {
            if (generation != _generation ||
                _boss == null ||
                !CustomRaidLoader.IsRaidPocScene(
                    SceneManager.GetActiveScene()))
            {
                return false;
            }

            Character bossCharacter =
                _boss.GetChar();

            Stats bossStats =
                _boss.GetComponent<Stats>();

            return
                bossCharacter != null &&
                bossCharacter.Alive &&
                bossStats != null &&
                bossStats.CurrentHP > 0;
        }


        private static bool IsBossEngaged()
        {
            if (_boss == null)
            {
                return false;
            }

            if (_boss.CurrentAggroTarget != null)
            {
                return true;
            }

            Stats stats =
                _boss.GetComponent<Stats>();

            return
                stats != null &&
                stats.CurrentMaxHP > 0 &&
                stats.CurrentHP < stats.CurrentMaxHP;
        }


        private static bool TryResolveSoakCenter(
            out Vector3 center)
        {
            center =
                Vector3.zero;

            if (_boss == null)
            {
                return false;
            }

            Vector3 roomCenter;

            if (!RaidBossRoomRuntime.TryGetCenter(
                    out roomCenter))
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] [" +
                    MechanicName +
                    "] could not resolve the 50x50 boss-room centre from Boss_01.");

                return false;
            }

            Vector3 bossPosition =
                _boss.transform.position;

            float requiredRoomMargin =
                SoakRadius + BossRoomWallBuffer;

            float usableHalfExtent =
                RaidBossRoomRuntime.HalfRoomSize - requiredRoomMargin;

            // Sample inside the safe square rather than by radial distance from
            // the moving boss. Because the centre stays at least SoakRadius +
            // wall buffer from every room edge, the entire telegraph remains in
            // the boss room even at corner-biased placements.
            for (int attempt = 0;
                 attempt < SoakPlacementAttempts;
                 attempt++)
            {
                float localX =
                    UnityEngine.Random.Range(
                        -usableHalfExtent,
                        usableHalfExtent);

                float localZ =
                    UnityEngine.Random.Range(
                        -usableHalfExtent,
                        usableHalfExtent);

                Vector3 desired =
                    roomCenter +
                    new Vector3(
                        localX,
                        0f,
                        localZ);

                float desiredDistance =
                    Vector3.Distance(
                        bossPosition,
                        desired);

                if (desiredDistance <
                    MinSoakSpawnDistance)
                {
                    continue;
                }

                NavMeshHit hit;

                if (!NavMesh.SamplePosition(
                        desired,
                        out hit,
                        SoakPlacementSampleRadius,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                if (!RaidBossRoomRuntime.ContainsPoint(
                        hit.position,
                        requiredRoomMargin))
                {
                    continue;
                }

                float resolvedBossDistance =
                    Vector3.Distance(
                        bossPosition,
                        hit.position);

                if (resolvedBossDistance <
                    MinSoakSpawnDistance)
                {
                    continue;
                }

                if (!HasCompleteNavMeshPath(
                        bossPosition,
                        hit.position))
                {
                    continue;
                }

                center =
                    hit.position;


                return true;
            }

            Plugin.NativeLog.LogWarning(
                "[RaidPoC] [" +
                MechanicName +
                "] could not resolve a reachable soak centre inside the 50x50 boss room with [" +
                requiredRoomMargin.ToString("0.0") +
                "m] edge clearance and minimum boss distance [" +
                MinSoakSpawnDistance.ToString("0") +
                "m].");

            return false;
        }


        private static bool HasCompleteNavMeshPath(
            Vector3 from,
            Vector3 to)
        {
            NavMeshHit startHit;
            NavMeshHit endHit;

            if (!NavMesh.SamplePosition(
                    from,
                    out startHit,
                    4f,
                    NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(
                    to,
                    out endHit,
                    4f,
                    NavMesh.AllAreas))
            {
                return false;
            }

            NavMeshPath path =
                new NavMeshPath();

            if (!NavMesh.CalculatePath(
                    startHit.position,
                    endHit.position,
                    NavMesh.AllAreas,
                    path))
            {
                return false;
            }

            return path.status ==
                   NavMeshPathStatus.PathComplete;
        }


        private static List<SimPlayer> SelectSoakers(
            Vector3 center,
            int desiredCount)
        {
            List<SimPlayer> candidates =
                new List<SimPlayer>();

            RaidManager manager =
                GameData.RaidManager;

            if (manager == null ||
                manager.Raiders == null)
            {
                return candidates;
            }

            Character bossTarget =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            // First pass: avoid pulling the current boss target/tank out of
            // position if enough other Sims are alive.
            foreach (RaidMemberSlot slot in manager.Raiders)
            {
                SimPlayer sim =
                    slot != null
                        ? slot.AssignedAvatar
                        : null;

                if (!IsValidLivingRaidSim(
                        sim))
                {
                    continue;
                }

                NPC npc =
                    sim.GetThisNPC();

                Character character =
                    npc != null
                        ? npc.GetChar()
                        : null;

                if (bossTarget != null &&
                    character == bossTarget)
                {
                    continue;
                }

                if (!HasCompleteNavMeshPath(
                        sim.transform.position,
                        center))
                {
                    continue;
                }

                AddUnique(
                    candidates,
                    sim);
            }

            // If the raid is small, allow the current boss target as a last
            // resort so the mechanic can still be demonstrated.
            if (candidates.Count < desiredCount)
            {
                foreach (RaidMemberSlot slot in manager.Raiders)
                {
                    SimPlayer sim =
                        slot != null
                            ? slot.AssignedAvatar
                            : null;

                    if (IsValidLivingRaidSim(
                            sim) &&
                        HasCompleteNavMeshPath(
                            sim.transform.position,
                            center))
                    {
                        AddUnique(
                            candidates,
                            sim);
                    }
                }
            }

            // For this test deliberately prefer the farthest reachable Sims.
            // The original PoC selected the closest Sims, which proved the
            // control handoff but did not prove sustained mechanic movement.
            candidates.Sort(
                delegate(
                    SimPlayer a,
                    SimPlayer b)
                {
                    float da =
                        (a.transform.position - center).sqrMagnitude;

                    float db =
                        (b.transform.position - center).sqrMagnitude;

                    return db.CompareTo(
                        da);
                });

            if (candidates.Count > desiredCount)
            {
                candidates.RemoveRange(
                    desiredCount,
                    candidates.Count - desiredCount);
            }

            return candidates;
        }


        private static bool IsValidLivingRaidSim(
            SimPlayer sim)
        {
            if (sim == null ||
                !sim.InRaid ||
                sim.MyStats == null ||
                sim.MyStats.CurrentHP <= 0)
            {
                return false;
            }

            NPC npc =
                sim.GetThisNPC();

            Character character =
                npc != null
                    ? npc.GetChar()
                    : null;

            return
                npc != null &&
                character != null &&
                character.Alive;
        }


        private static void AddUnique(
            List<SimPlayer> sims,
            SimPlayer sim)
        {
            if (sim == null)
            {
                return;
            }

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                if (sims[i] == sim)
                {
                    return;
                }
            }

            sims.Add(
                sim);
        }


        private static void BuildAssignments(
            List<SimPlayer> selected,
            Vector3 center)
        {
            ReleaseAssignments();

            int count =
                selected.Count;

            for (int i = 0;
                 i < count;
                 i++)
            {
                SimPlayer sim =
                    selected[i];

                if (!IsValidLivingRaidSim(
                        sim))
                {
                    continue;
                }

                float angle =
                    count > 1
                        ? ((Mathf.PI * 2f) * i / count)
                        : 0f;

                Vector3 desired =
                    center +
                    new Vector3(
                        Mathf.Cos(angle) * AssignmentRingRadius,
                        0f,
                        Mathf.Sin(angle) * AssignmentRingRadius);

                NavMeshHit hit;

                Vector3 resolved =
                    NavMesh.SamplePosition(
                        desired,
                        out hit,
                        3f,
                        NavMesh.AllAreas)
                        ? hit.position
                        : center;

                NPC npc =
                    sim.GetThisNPC();

                SoakAssignment assignment =
                    new SoakAssignment
                    {
                        Sim = sim,
                        Npc = npc,
                        SavedAggroTarget =
                            npc != null
                                ? npc.CurrentAggroTarget
                                : null,
                        Agent =
                            sim.GetComponent<NavMeshAgent>(),
                        TargetPosition = resolved,
                        InstanceId = sim.GetInstanceID(),
                        HasArrived = false
                    };

                ActiveAssignments.Add(
                    assignment);

                SuppressedSimIds.Add(
                    assignment.InstanceId);

                // Remove the live combat target while the mechanic owns the
                // Sim so it does not continue trying to attack while moving.
                // The exact Character reference is restored on release.
                if (npc != null)
                {
                    npc.CurrentAggroTarget =
                        null;
                }

                float initialTravelDistance =
                    Vector3.Distance(
                        sim.transform.position,
                        resolved);

            }
        }


        private static void UpdateAssignmentMovement()
        {
            for (int i = 0;
                 i < ActiveAssignments.Count;
                 i++)
            {
                SoakAssignment assignment =
                    ActiveAssignments[i];

                SimPlayer sim =
                    assignment.Sim;

                if (!IsValidLivingRaidSim(
                        sim))
                {
                    continue;
                }

                // Keep combat ownership cleared for the entire mechanic window,
                // not just on assignment. Some native NPC paths can reacquire
                // the boss after the Sim reaches the soak and pull it back out.
                NPC npc =
                    assignment.Npc;

                if (npc != null)
                {
                    npc.CurrentAggroTarget =
                        null;
                }

                float distance =
                    Vector3.Distance(
                        sim.transform.position,
                        assignment.TargetPosition);

                NavMeshAgent agent =
                    assignment.Agent;

                // Arrival is latched. Once a Sim has reached its mechanic slot,
                // it stays under a hard positional hold until soak impact.
                if (!assignment.HasArrived &&
                    distance <= ArrivalTolerance)
                {
                    assignment.HasArrived =
                        true;

                }

                if (assignment.HasArrived)
                {
                    HoldAssignmentAtTarget(
                        assignment,
                        sim,
                        agent,
                        distance);

                    continue;
                }

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    try
                    {
                        agent.isStopped =
                            false;

                        agent.SetDestination(
                            assignment.TargetPosition);

                        continue;
                    }
                    catch
                    {
                        // Fall through to simple transform movement. The native
                        // agent may transiently leave the NavMesh during raid
                        // scene setup or other game-side movement.
                    }
                }

                sim.transform.position =
                    Vector3.MoveTowards(
                        sim.transform.position,
                        assignment.TargetPosition,
                        FallbackMoveSpeed * Time.deltaTime);
            }
        }


        private static void HoldAssignmentAtTarget(
            SoakAssignment assignment,
            SimPlayer sim,
            NavMeshAgent agent,
            float distance)
        {
            bool correctedWithAgent =
                false;

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                try
                {
                    agent.isStopped =
                        true;

                    agent.velocity =
                        Vector3.zero;

                    if (agent.hasPath)
                    {
                        agent.ResetPath();
                    }

                    if (distance > HoldDriftTolerance)
                    {
                        correctedWithAgent =
                            agent.Warp(
                                assignment.TargetPosition);
                    }
                }
                catch
                {
                    correctedWithAgent =
                        false;
                }
            }

            // Warp is preferred because it keeps NavMeshAgent state coherent. If
            // the agent cannot warp (or is temporarily off-mesh), pin the actual
            // Sim transform so native combat movement cannot pull it out again.
            if (distance > HoldDriftTolerance &&
                !correctedWithAgent)
            {
                sim.transform.position =
                    assignment.TargetPosition;
            }
        }


        private static void ApplySoakImpact(
            Vector3 center)
        {
            List<Character> occupants =
                FindLivingRaidCharactersInside(
                    center,
                    SoakRadius);

            if (occupants.Count == 0)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] [" +
                    MechanicName +
                    "] SOAK IMPACT found no living raid members inside the zone. No split recipients were available.");

                return;
            }

            int damageEach =
                Mathf.CeilToInt(
                    (float)TotalSplitDamage /
                    occupants.Count);

            Character bossCharacter =
                _boss != null
                    ? _boss.GetChar()
                    : null;

            for (int i = 0;
                 i < occupants.Count;
                 i++)
            {
                Character target =
                    occupants[i];

                if (target == null ||
                    !target.Alive ||
                    target.MyStats == null ||
                    target.MyStats.CurrentHP <= 0)
                {
                    continue;
                }

                try
                {
                    // Combat Meter source context is frame-scoped and consumed
                    // by the immediately following damage call, so set it per
                    // recipient for multi-target split damage.
                    RaidMechanicDamageAttribution.SetNamedSource(
                        bossCharacter,
                        MechanicName);

                    target.DamageMe(
                        damageEach,
                        false,
                        GameData.DamageType.Poison,
                        bossCharacter,
                        false,
                        false,
                        0);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] soak damage failed for [" +
                        target.name +
                        "]: " +
                        exception);
                }
            }

        }


        private static List<Character> FindLivingRaidCharactersInside(
            Vector3 center,
            float radius)
        {
            List<Character> result =
                new List<Character>();

            float radiusSqr =
                radius * radius;

            PlayerControl player =
                GameData.PlayerControl;

            if (player != null &&
                player.Myself != null)
            {
                AddCharacterIfInside(
                    result,
                    player.Myself,
                    center,
                    radiusSqr);
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

                    if (!IsValidLivingRaidSim(
                            sim))
                    {
                        continue;
                    }

                    NPC npc =
                        sim.GetThisNPC();

                    Character character =
                        npc != null
                            ? npc.GetChar()
                            : null;

                    AddCharacterIfInside(
                        result,
                        character,
                        center,
                        radiusSqr);
                }
            }

            return result;
        }


        private static void AddCharacterIfInside(
            List<Character> result,
            Character character,
            Vector3 center,
            float radiusSqr)
        {
            if (character == null ||
                !character.Alive ||
                character.MyStats == null ||
                character.MyStats.CurrentHP <= 0 ||
                (character.transform.position - center).sqrMagnitude > radiusSqr)
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


        private static void ReleaseAssignments()
        {
            for (int i = 0;
                 i < ActiveAssignments.Count;
                 i++)
            {
                SoakAssignment assignment =
                    ActiveAssignments[i];

                SuppressedSimIds.Remove(
                    assignment.InstanceId);

                SimPlayer sim =
                    assignment.Sim;

                if (sim == null)
                {
                    continue;
                }

                NavMeshAgent agent =
                    assignment.Agent;

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    try
                    {
                        agent.isStopped =
                            false;

                        if (agent.hasPath)
                        {
                            agent.ResetPath();
                        }
                    }
                    catch
                    {
                        // Native AI will repair movement state on the next
                        // InRaidLogic tick after the suppression is removed.
                    }
                }

                NPC npc =
                    assignment.Npc;

                if (npc != null &&
                    assignment.SavedAggroTarget != null &&
                    assignment.SavedAggroTarget.Alive)
                {
                    npc.CurrentAggroTarget =
                        assignment.SavedAggroTarget;
                }
            }

            ActiveAssignments.Clear();
            SuppressedSimIds.Clear();
        }


        private static void CreateZone(
            Vector3 center)
        {
            DestroyZone();

            _zoneObject =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);

            _zoneObject.name =
                "FOTF_RaidSoak_PoC";

            _zoneObject.transform.position =
                center +
                (Vector3.up * 0.05f);

            _zoneObject.transform.localScale =
                new Vector3(
                    SoakRadius * 2f,
                    0.025f,
                    SoakRadius * 2f);

            Collider collider =
                _zoneObject.GetComponent<Collider>();

            if (collider != null)
            {
                collider.enabled =
                    false;

                UnityEngine.Object.Destroy(
                    collider);
            }

            Renderer renderer =
                _zoneObject.GetComponent<Renderer>();

            if (renderer != null)
            {
                Shader shader =
                    Shader.Find(
                        "Sprites/Default");

                if (shader == null)
                {
                    shader =
                        Shader.Find(
                            "Unlit/Color");
                }

                if (shader != null)
                {
                    _zoneMaterial =
                        new Material(
                            shader);

                    _zoneMaterial.color =
                        new Color(
                            0.48f,
                            0.12f,
                            0.72f,
                            0.38f);

                    _zoneMaterial.renderQueue =
                        3000;

                    renderer.material =
                        _zoneMaterial;
                }
            }
        }


        private static void DestroyZone()
        {
            if (_zoneObject != null)
            {
                UnityEngine.Object.Destroy(
                    _zoneObject);

                _zoneObject =
                    null;
            }

            if (_zoneMaterial != null)
            {
                UnityEngine.Object.Destroy(
                    _zoneMaterial);

                _zoneMaterial =
                    null;
            }
        }


        private static string GetSimName(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return "<null>";
            }

            NPC npc =
                sim.GetThisNPC();

            if (npc != null &&
                !string.IsNullOrEmpty(
                    npc.NPCName))
            {
                return npc.NPCName;
            }

            return sim.name;
        }
    }


    /// <summary>
    /// Native SimPlayer.Update() enters InRaidLogic() when a Sim is actively
    /// raiding. During a mechanic override we skip only this AI method; Update,
    /// stats, animations, damage/death and the rest of the SimPlayer lifecycle
    /// remain native and active.
    /// </summary>
    [HarmonyPatch(
        typeof(SimPlayer),
        "InRaidLogic")]
    internal static class Patch_RaidSoakSuppressNativeRaidLogic
    {
        private static bool Prefix(
            SimPlayer __instance)
        {
            return
                !RaidSoakMechanicRuntime
                    .ShouldSuppressNativeRaidLogic(
                        __instance) &&
                !RaidAvoidableAoeMechanicRuntime
                    .ShouldSuppressNativeRaidLogic(
                        __instance) &&
                !RaidPhaseTwoInterruptRuntime
                    .ShouldSuppressNativeRaidLogic(
                        __instance);
        }
    }
}
