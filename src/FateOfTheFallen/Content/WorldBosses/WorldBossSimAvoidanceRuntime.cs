using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Reusable temporary movement override for open-world boss mechanics.
    ///
    /// Only threatened native raid SimPlayers are overridden. Their live
    /// combat target is saved, native InRaidLogic is suppressed while they
    /// move, and the exact previous target is restored after impact.
    /// The human player is never moved automatically.
    /// </summary>
    internal static class WorldBossSimAvoidanceRuntime
    {
        internal enum ReleaseMode
        {
            ResumeEncounter,
            ReturnToFollow
        }

        private const float SafePadding =
            2.0f;

        private const float ConeExtraAngle =
            14f;

        private const float ArrivalTolerance =
            0.85f;

        private const float HoldDriftTolerance =
            0.30f;

        private const float MechanicStoppingDistance =
            0.05f;

        private const float StallVelocityThreshold =
            0.05f;

        private const float StallFinishDistance =
            1.25f;

        private const float FallbackMoveSpeed =
            10f;

        // Directed boss mechanics are short reaction checks. Native casts and
        // class AI can keep a NavMeshAgent stationary even after SetDestination
        // is issued, so directed assignments use a guaranteed per-frame
        // NavMesh step at at least this speed.
        private const float DirectedMoveMinimumSpeed =
            13.5f;

        private const float NavMeshSampleRadius =
            3.0f;

        // Open-world terrain can differ substantially in Y across only a few
        // horizontal metres. Mechanic destinations are therefore solved in XZ
        // first and projected vertically onto the real walkable surface.
        private const float TerrainProbeUp =
            14f;

        private const float TerrainProbeDown =
            30f;

        private const float TerrainNavSampleRadius =
            1.35f;

        private const float MaxTerrainHorizontalSnap =
            1.50f;

        private sealed class AvoidAssignment
        {
            internal SimPlayer Sim;
            internal NPC Npc;
            internal Character Character;
            internal Transform MovementTransform;
            internal Character SavedAggroTarget;
            internal NavMeshAgent Agent;
            internal CastSpell Caster;
            internal Animator Animator;
            internal Vector3 TargetPosition;
            internal string MechanicType;
            internal int InstanceId;
            internal bool HasArrived;
            internal bool ForceDirectedMovement;

            internal bool SavedNpcEnabled;
            internal bool FullAiOverrideActive;

            internal float SavedStoppingDistance;
            internal bool SavedAutoBraking;
            internal bool AgentMovementStateCaptured;

        }

        private static readonly List<AvoidAssignment> ActiveAssignments =
            new List<AvoidAssignment>();

        private static readonly HashSet<int> FullAiOverrideSimIds =
            new HashSet<int>();

        // Do NOT disable the NPC MonoBehaviour itself. In the open world that
        // component participates in native lifecycle/combat state and toggling
        // enabled off/on can leave Sims in a non-reactive state afterwards.
        // Instead we temporarily skip only NPC.Update() while the mechanic owns
        // that Sim.
        private static readonly HashSet<int> FullAiOverrideNpcIds =
            new HashSet<int>();

        private static readonly HashSet<int> SuppressedCasterIds =
            new HashSet<int>();

        private static readonly FieldInfo SpellVesselSourceField =
            AccessTools.Field(
                typeof(SpellVessel),
                "SpellSource");


        private static readonly FieldInfo SimTrainingField =
            AccessTools.Field(
                typeof(SimPlayer),
                "training");

        private static readonly FieldInfo SimAllowForcedPullPathField =
            AccessTools.Field(
                typeof(SimPlayer),
                "allowForcedPullPath");

        private static int _postCombatRecoveryGeneration;
        private static string _activeMechanicType;


        internal static bool HasActiveAssignments
        {
            get
            {
                return ActiveAssignments.Count > 0;
            }
        }


        internal static bool AreAllActiveAssignmentsArrived
        {
            get
            {
                if (ActiveAssignments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < ActiveAssignments.Count; i++)
                {
                    AvoidAssignment assignment =
                        ActiveAssignments[i];

                    if (assignment != null &&
                        !assignment.HasArrived)
                    {
                        return false;
                    }
                }

                return true;
            }
        }


        internal static bool IsActiveMechanic(
            string mechanicType)
        {
            return ActiveAssignments.Count > 0 &&
                !string.IsNullOrEmpty(mechanicType) &&
                string.Equals(
                    _activeMechanicType,
                    mechanicType,
                    StringComparison.Ordinal);
        }


        internal static List<SimPlayer> GetLivingRaidSimsSnapshot()
        {
            return GetLivingRaidSims();
        }


        internal static void ForceReleaseAll(
            string reason)
        {
            if (ActiveAssignments.Count > 0)
            {
            }

            ReleaseAssignments(
                reason ?? "forced cleanup",
                ReleaseMode.ReturnToFollow);

            string cleanupReason =
                reason ?? "forced cleanup";

            if (!IsBossAttachCleanup(
                    cleanupReason))
            {
                ResetOfficialRosterToFollow(
                    cleanupReason);

                _postCombatRecoveryGeneration++;

                CoroutineHost.Start(
                    PostCombatRecoveryRoutine(
                        _postCombatRecoveryGeneration,
                        cleanupReason));
            }
            else
            {
            }
        }


        private static bool IsBossAttachCleanup(
            string reason)
        {
            return
                !string.IsNullOrEmpty(
                    reason) &&
                reason.IndexOf(
                    "boss attach",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }


        internal static bool ShouldOverrideFullSimAi(
            SimPlayer sim)
        {
            if (sim == null ||
                FullAiOverrideSimIds.Count == 0)
            {
                return false;
            }

            return FullAiOverrideSimIds.Contains(
                sim.GetInstanceID());
        }


        internal static bool ShouldOverrideNpcAi(
            NPC npc)
        {
            if (npc == null)
            {
                return false;
            }

            return FullAiOverrideNpcIds.Contains(
                npc.GetInstanceID());
        }


        internal static bool ShouldSuppressCaster(
            CastSpell caster)
        {
            return caster != null &&
                SuppressedCasterIds.Contains(
                    caster.GetInstanceID());
        }


        internal static void BeginConeAvoidance(
            NPC boss,
            Vector3 origin,
            Vector3 forward,
            float range,
            float angleDegrees)
        {
            ReleaseAssignments(
                "new cone mechanic");

            if (boss == null)
            {
                return;
            }

            forward.y =
                0f;

            if (forward.sqrMagnitude <=
                0.0001f)
            {
                forward =
                    boss.transform.forward;
            }

            forward.y =
                0f;

            if (forward.sqrMagnitude <=
                0.0001f)
            {
                forward =
                    Vector3.forward;
            }

            forward.Normalize();

            List<SimPlayer> sims =
                GetLivingRaidSims();

            float halfAngle =
                angleDegrees *
                0.5f;

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                SimPlayer sim =
                    sims[i];

                Transform movementTransform =
                    ResolveMovementTransform(
                        sim);

                if (movementTransform == null)
                {
                    continue;
                }

                Vector3 simPosition =
                    movementTransform.position;

                Vector3 delta =
                    simPosition -
                    origin;

                delta.y =
                    0f;

                float distance =
                    delta.magnitude;

                if (distance <=
                        0.05f ||
                    distance >
                        range)
                {
                    continue;
                }

                float signedAngle =
                    Vector3.SignedAngle(
                        forward,
                        delta.normalized,
                        Vector3.up);

                if (Mathf.Abs(
                        signedAngle) >
                    halfAngle)
                {
                    continue;
                }

                float side =
                    signedAngle >=
                            0f
                        ? 1f
                        : -1f;

                // Choose the SHORTEST practical escape across the nearest cone
                // edge. The previous implementation rotated the Sim around the
                // boss at the same radius, which could create 10-14m travel
                // paths during a 3s telegraph.
                //
                // We keep the Sim at approximately its current boss radius and
                // move only far enough laterally to cross the closest edge plus
                // a small angular safety margin.
                float targetAngle =
                    side *
                    (halfAngle +
                     ConeExtraAngle);

                Vector3 targetDirection =
                    Quaternion.Euler(
                        0f,
                        targetAngle,
                        0f) *
                    forward;

                float escapeRadius =
                    Mathf.Clamp(
                        distance,
                        2.5f,
                        Mathf.Max(
                            2.5f,
                            range -
                            0.25f));

                Vector3 edgePoint =
                    origin +
                    targetDirection.normalized *
                    escapeRadius;

                Vector3 current =
                    simPosition;

                Vector3 lateral =
                    edgePoint -
                    current;

                lateral.y =
                    0f;

                // Cap excessively long requested sidesteps. If the exact edge
                // projection is far away, step in that direction only as far as
                // needed to clear quickly, then NavMesh resolution handles the
                // final walkable position.
                const float maxConeEscapeTravel =
                    7.5f;

                Vector3 desired =
                    lateral.magnitude >
                            maxConeEscapeTravel
                        ? current +
                          lateral.normalized *
                          maxConeEscapeTravel
                        : edgePoint;

                Vector3 resolved;

                if (!TryResolveReachableConePoint(
                        simPosition,
                        desired,
                        origin,
                        forward,
                        range,
                        halfAngle,
                        out resolved))
                {
                    Plugin.NativeLog.LogWarning(
                        "[WorldBossAI] could not resolve a COMPLETE safe cone path for Sim=[" +
                        GetSimName(
                            sim) +
                        "]. Leaving Sim unassigned rather than sending it toward a partial NavMesh path.");

                    continue;
                }

                AddAssignment(
                    sim,
                    resolved,
                    "cone",
                    true);
            }

            Plugin.NativeLog.LogInfo(
                "[WorldBossAI] cone avoidance armed. SimsInRoster=[" +
                sims.Count +
                "] Assigned=[" +
                ActiveAssignments.Count +
                "] Range=[" +
                range.ToString("0.0") +
                "] HalfAngle=[" +
                halfAngle.ToString("0.0") +
                "].");
        }


        internal static void BeginCircleAvoidance(
            List<Vector3> centers,
            float radius)
        {
            ReleaseAssignments(
                "new circle mechanic");

            if (centers == null ||
                centers.Count == 0)
            {
                return;
            }

            List<SimPlayer> sims =
                GetLivingRaidSims();

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                SimPlayer sim =
                    sims[i];

                Transform movementTransform =
                    ResolveMovementTransform(
                        sim);

                if (movementTransform == null)
                {
                    continue;
                }

                Vector3 current =
                    movementTransform.position;

                int threateningCenter =
                    -1;

                float nearestSqr =
                    float.PositiveInfinity;

                for (int c = 0;
                     c < centers.Count;
                     c++)
                {
                    Vector3 delta =
                        current -
                        centers[c];

                    delta.y =
                        0f;

                    float sqr =
                        delta.sqrMagnitude;

                    if (sqr <=
                            radius *
                            radius &&
                        sqr <
                            nearestSqr)
                    {
                        threateningCenter =
                            c;

                        nearestSqr =
                            sqr;
                    }
                }

                if (threateningCenter <
                    0)
                {
                    continue;
                }

                Vector3 center =
                    centers[
                        threateningCenter];

                Vector3 away =
                    current -
                    center;

                away.y =
                    0f;

                if (away.sqrMagnitude <=
                    0.0001f)
                {
                    // Deterministic spread direction based on raid slot order,
                    // avoiding every assigned Sim choosing the same escape line.
                    float angle =
                        (i * 137.5f) %
                        360f;

                    away =
                        Quaternion.Euler(
                            0f,
                            angle,
                            0f) *
                        Vector3.forward;
                }

                away.Normalize();

                Vector3 desired =
                    center +
                    away *
                    (radius +
                     SafePadding);

                // If that point happens to overlap another Bloom circle, keep
                // walking the same direction until it is outside all circles.
                for (int guard = 0;
                     guard < 5 &&
                     IsInsideAnyCircle(
                         desired,
                         centers,
                         radius +
                         0.35f);
                     guard++)
                {
                    desired +=
                        away *
                        SafePadding;
                }

                Vector3 resolved;

                if (!TryResolveReachableCirclePoint(
                        current,
                        desired,
                        centers,
                        radius,
                        out resolved))
                {
                    Plugin.NativeLog.LogWarning(
                        "[WorldBossAI] could not resolve a COMPLETE safe circle path for Sim=[" +
                        GetSimName(
                            sim) +
                        "]. Leaving Sim unassigned rather than sending it toward a partial NavMesh path.");

                    continue;
                }

                AddAssignment(
                    sim,
                    resolved,
                    "circle",
                    true);
            }

            Plugin.NativeLog.LogInfo(
                "[WorldBossAI] circle avoidance armed. SimsInRoster=[" +
                sims.Count +
                "] Assigned=[" +
                ActiveAssignments.Count +
                "] HazardCount=[" +
                centers.Count +
                "].");
        }


        /// <summary>
        /// Generic mechanic-owned Sim movement used by Nhal'Sera and future
        /// world bosses. Destinations are projected to reachable NavMesh points
        /// and then run through the same full AI override / restoration path as
        /// the proven cone and circle avoidance logic.
        /// </summary>
        internal static void BeginDirectedMovement(
            Dictionary<SimPlayer, Vector3> destinations,
            string mechanicType)
        {
            ReleaseAssignments(
                "new directed mechanic: " +
                (mechanicType ?? "world boss mechanic"));

            if (destinations == null ||
                destinations.Count == 0)
            {
                return;
            }

            _activeMechanicType =
                mechanicType ?? "directed";

            int assignmentsBefore =
                ActiveAssignments.Count;

            foreach (KeyValuePair<SimPlayer, Vector3> pair in destinations)
            {
                SimPlayer sim = pair.Key;

                if (!IsValidLivingRaidSim(sim))
                {
                    continue;
                }

                Transform movementTransform =
                    ResolveMovementTransform(
                        sim);

                Vector3 current =
                    movementTransform != null
                        ? movementTransform.position
                        : sim.transform.position;

                Vector3 resolved;

                if (!TryAcceptReachableCandidate(
                        current,
                        pair.Value,
                        delegate(Vector3 point)
                        {
                            return true;
                        },
                        out resolved))
                {
                    resolved = ResolveNavMeshPoint(
                        pair.Value,
                        current);
                }

                AddAssignment(
                    sim,
                    resolved,
                    mechanicType ?? "directed",
                    true);
            }

            Plugin.NativeLog.LogInfo(
                "[WorldBossAI] directed movement armed. Mechanic=[" +
                (_activeMechanicType ?? "<null>") +
                "] Requested=[" +
                destinations.Count +
                "] Assigned=[" +
                (ActiveAssignments.Count -
                 assignmentsBefore) +
                "].");
        }


        /// <summary>
        /// Sends every living official party/raid Sim - tanks included - to a
        /// random currently-valid safe anchor. The choice is deterministic for
        /// a supplied seed so one mechanic start does not reshuffle every frame.
        /// </summary>
        internal static void BeginRandomAnchorMovement(
            IList<Vector3> anchors,
            int seed,
            string mechanicType)
        {
            ReleaseAssignments(
                "new anchor mechanic: " +
                (mechanicType ?? "world boss anchors"));

            if (anchors == null ||
                anchors.Count == 0)
            {
                return;
            }

            _activeMechanicType =
                mechanicType ?? "anchor";

            List<SimPlayer> sims =
                GetLivingRaidSims();

            if (sims.Count == 0)
            {
                return;
            }

            System.Random random =
                new System.Random(
                    seed);

            for (int i = 0; i < sims.Count; i++)
            {
                SimPlayer sim =
                    sims[i];

                if (!IsValidLivingRaidSim(
                        sim))
                {
                    continue;
                }

                int anchorIndex =
                    random.Next(
                        anchors.Count);

                Vector3 center =
                    anchors[anchorIndex];

                // Small per-Sim offset avoids piling the whole raid onto one
                // exact point while keeping everyone comfortably inside the
                // 7m Veil safe radius.
                float angle =
                    (float)(
                        random.NextDouble() *
                        Math.PI *
                        2.0);

                float radius =
                    0.75f +
                    (float)random.NextDouble() *
                    1.50f;

                Vector3 desired =
                    center +
                    new Vector3(
                        Mathf.Sin(angle) * radius,
                        0f,
                        Mathf.Cos(angle) * radius);

                Vector3 current =
                    sim.transform.position;

                Vector3 resolved;

                if (!TryAcceptReachableCandidate(
                        current,
                        desired,
                        delegate(Vector3 point)
                        {
                            return true;
                        },
                        out resolved))
                {
                    resolved =
                        ResolveNavMeshPoint(
                            center,
                            current);
                }

                AddAssignment(
                    sim,
                    resolved,
                    mechanicType ?? "anchor");
            }
        }


        internal static void UpdateMovement()
        {
            for (int i = 0;
                 i < ActiveAssignments.Count;
                 i++)
            {
                AvoidAssignment assignment =
                    ActiveAssignments[i];

                SimPlayer sim =
                    assignment.Sim;

                if (!IsValidLivingRaidSim(
                        sim))
                {
                    continue;
                }

                // FOTF has exclusive AI ownership during the mechanic:
                // SimPlayer.Update is skipped by Harmony and NPC.Update is
                // disabled on assignment. Only this controller may issue
                // movement/cast decisions until release.
                NPC npc =
                    assignment.Npc;

                if (npc != null)
                {
                    npc.CurrentAggroTarget =
                        null;
                }

                // Existing native casts are intentionally allowed to finish.
                // Destroying SpellVessel objects bypasses Erenshor's normal cast
                // completion path and can leave CastSpell.isCasting() stuck true.
                // New casts are still blocked by SuppressedCasterIds.

                Transform movementTransform =
                    assignment.MovementTransform != null
                        ? assignment.MovementTransform
                        : ResolveMovementTransform(
                            sim);

                if (movementTransform == null)
                {
                    continue;
                }

                assignment.MovementTransform =
                    movementTransform;

                float distance =
                    Vector3.Distance(
                        movementTransform.position,
                        assignment.TargetPosition);

                NavMeshAgent agent =
                    assignment.Agent;

                if (!assignment.HasArrived &&
                    distance <=
                        ArrivalTolerance)
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

                if (assignment.ForceDirectedMovement)
                {
                    ForceDirectedStep(
                        assignment,
                        sim,
                        agent);

                    continue;
                }

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    try
                    {
                        // Native FollowPlayer() can leave a settled Sim at
                        // speed=0 / isStopped=true. Because the mechanic
                        // temporarily suppresses InRaidLogic() or
                        // GroupedWithPlayerLogic(), the normal native code
                        // that wakes the agent and restores actualRunSpeed
                        // does not get a chance to run.
                        //
                        // Restore exactly Erenshor's normal run speed here.
                        // This is NOT a mechanic speed boost.
                        if (sim.MyStats != null)
                        {
                            agent.speed =
                                sim.MyStats.actualRunSpeed;
                        }

                        agent.stoppingDistance =
                            MechanicStoppingDistance;

                        SetMechanicMoveAnimation(
                            assignment,
                            true);

                        agent.isStopped =
                            false;

                        agent.SetDestination(
                            assignment.TargetPosition);

                        // Unity can decide an agent has effectively arrived
                        // while still slightly short of the requested point.
                        // If it has stopped moving near the endpoint but is
                        // outside our actual arrival tolerance, finish the
                        // final short segment directly so the Sim cannot run
                        // in place on the hazard boundary.
                        if (!agent.pathPending &&
                            agent.velocity.sqrMagnitude <=
                                StallVelocityThreshold *
                                StallVelocityThreshold &&
                            distance >
                                ArrivalTolerance &&
                            distance <=
                                StallFinishDistance)
                        {
                            Vector3 finished =
                                Vector3.MoveTowards(
                                    sim.transform.position,
                                    assignment.TargetPosition,
                                    FallbackMoveSpeed *
                                    Time.deltaTime);

                            if (agent.isOnNavMesh)
                            {
                                agent.Warp(
                                    finished);
                            }
                            else
                            {
                                sim.transform.position =
                                    finished;
                            }
                        }

                        continue;
                    }
                    catch
                    {
                        // Fall through to the same transform fallback used by
                        // the proven test-raid controller.
                    }
                }

                Transform fallbackTransform =
                    assignment.MovementTransform != null
                        ? assignment.MovementTransform
                        : ResolveMovementTransform(
                            sim);

                if (fallbackTransform != null)
                {
                    assignment.MovementTransform =
                        fallbackTransform;

                    fallbackTransform.position =
                        Vector3.MoveTowards(
                            fallbackTransform.position,
                            assignment.TargetPosition,
                            FallbackMoveSpeed *
                            Time.deltaTime);
                }
            }
        }


        private static void ForceDirectedStep(
            AvoidAssignment assignment,
            SimPlayer sim,
            NavMeshAgent agent)
        {
            if (assignment == null || sim == null)
            {
                return;
            }

            Transform movementTransform =
                assignment.MovementTransform != null
                    ? assignment.MovementTransform
                    : ResolveMovementTransform(
                        sim);

            if (movementTransform == null)
            {
                return;
            }

            assignment.MovementTransform =
                movementTransform;

            Vector3 current =
                movementTransform.position;

            Vector3 target =
                assignment.TargetPosition;

            float speed = DirectedMoveMinimumSpeed;
            if (sim.MyStats != null)
            {
                speed = Mathf.Max(
                    speed,
                    sim.MyStats.actualRunSpeed);
            }

            Vector3 next =
                Vector3.MoveTowards(
                    current,
                    target,
                    speed * Mathf.Max(0f, Time.deltaTime));

            Vector3 resolved = next;
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    next,
                    out hit,
                    1.75f,
                    NavMesh.AllAreas))
            {
                resolved = hit.position;
            }

            bool movedWithAgent = false;

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                try
                {
                    agent.speed = speed;
                    agent.stoppingDistance = MechanicStoppingDistance;
                    agent.isStopped = false;

                    if (agent.hasPath)
                    {
                        agent.ResetPath();
                    }

                    // Warp is deliberate here. Short boss mechanics cannot
                    // wait for class/cast state to relinquish NavMeshAgent
                    // control. Per-frame increments keep the visual movement
                    // smooth while guaranteeing the mechanic wins ownership.
                    movedWithAgent = agent.Warp(resolved);
                }
                catch
                {
                    movedWithAgent = false;
                }
            }

            if (!movedWithAgent)
            {
                movementTransform.position = resolved;
            }

            SetMechanicMoveAnimation(assignment, true);

            float remaining =
                Vector3.Distance(
                    movementTransform.position,
                    assignment.TargetPosition);

            if (remaining <= ArrivalTolerance)
            {
                assignment.HasArrived = true;
            }
        }


        internal static void ReleaseAssignments(
            string reason,
            ReleaseMode mode =
                ReleaseMode.ResumeEncounter)
        {
            if (ActiveAssignments.Count >
                0)
            {
            }

            for (int i = 0;
                 i < ActiveAssignments.Count;
                 i++)
            {
                AvoidAssignment assignment =
                    ActiveAssignments[i];

                if (assignment == null)
                {
                    continue;
                }

                EndFullAiOverride(
                    assignment,
                    mode);
            }

            ActiveAssignments.Clear();
            FullAiOverrideSimIds.Clear();
            FullAiOverrideNpcIds.Clear();
            SuppressedCasterIds.Clear();
            _activeMechanicType = null;
        }


        private static void AddAssignment(
            SimPlayer sim,
            Vector3 targetPosition,
            string mechanicType,
            bool forceDirectedMovement = false)
        {
            if (!IsValidLivingRaidSim(
                    sim))
            {
                return;
            }

            NPC npc =
                sim.GetThisNPC();

            if (npc == null)
            {
                return;
            }

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    npc.GetComponent<Character>();

                if (character == null)
                {
                    character =
                        npc.GetComponentInChildren<Character>(
                            true);
                }
            }

            Transform movementTransform =
                character != null
                    ? character.transform
                    : (npc != null
                        ? npc.transform
                        : sim.transform);

            NavMeshAgent agent =
                character != null
                    ? character.GetComponent<NavMeshAgent>()
                    : null;

            if (agent == null &&
                npc != null)
            {
                agent =
                    npc.GetComponent<NavMeshAgent>();

                if (agent == null)
                {
                    agent =
                        npc.GetComponentInChildren<NavMeshAgent>(
                            true);
                }
            }

            if (agent == null)
            {
                agent =
                    sim.GetComponent<NavMeshAgent>();

                if (agent == null)
                {
                    agent =
                        sim.GetComponentInChildren<NavMeshAgent>(
                            true);
                }
            }

            Animator animator =
                character != null
                    ? character.GetMyAnim()
                    : null;

            if (animator == null &&
                npc != null)
            {
                animator =
                    npc.GetComponentInChildren<Animator>(
                        true);
            }

            if (animator == null)
            {
                animator =
                    sim.GetComponentInChildren<Animator>(
                        true);
            }

            AvoidAssignment assignment =
                new AvoidAssignment
                {
                    Sim =
                        sim,

                    Npc =
                        npc,

                    Character =
                        character,

                    MovementTransform =
                        movementTransform,

                    SavedAggroTarget =
                        npc.CurrentAggroTarget,

                    Agent =
                        agent,

                    Caster =
                        ResolveCaster(
                            sim,
                            npc),

                    Animator =
                        animator,

                    TargetPosition =
                        targetPosition,

                    MechanicType =
                        mechanicType,

                    InstanceId =
                        sim.GetInstanceID(),

                    HasArrived =
                        false,

                    ForceDirectedMovement =
                        forceDirectedMovement
                };

            ActiveAssignments.Add(
                assignment);

            BeginFullAiOverride(
                assignment);

            if (assignment.Caster != null)
            {
                // Block NEW casts while the mechanic owns this Sim, but do not
                // destroy a cast that was already in progress. Let native
                // SpellVessel completion clear CastSpell.isCasting() normally.
                SuppressedCasterIds.Add(
                    assignment.Caster.GetInstanceID());
            }

        }


        private static void HoldAssignmentAtTarget(
            AvoidAssignment assignment,
            SimPlayer sim,
            NavMeshAgent agent,
            float distance)
        {
            if (assignment == null ||
                sim == null)
            {
                return;
            }

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

                    if (distance >
                        HoldDriftTolerance)
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

            // This is the exact hold philosophy used in the test raid:
            // prefer NavMeshAgent.Warp for coherence, otherwise pin the
            // SimPlayer transform so native combat movement cannot drag it
            // out again before impact.
            if (distance >
                    HoldDriftTolerance &&
                !correctedWithAgent)
            {
                Transform movementTransform =
                    assignment.MovementTransform != null
                        ? assignment.MovementTransform
                        : ResolveMovementTransform(
                            sim);

                if (movementTransform != null)
                {
                    assignment.MovementTransform =
                        movementTransform;

                    movementTransform.position =
                        assignment.TargetPosition;
                }
            }
        }


        private static void BeginFullAiOverride(
            AvoidAssignment assignment)
        {
            if (assignment == null ||
                assignment.Sim == null)
            {
                return;
            }

            SimPlayer sim =
                assignment.Sim;

            NPC npc =
                assignment.Npc;

            NavMeshAgent agent =
                assignment.Agent;

            FullAiOverrideSimIds.Add(
                assignment.InstanceId);

            if (npc != null)
            {
                assignment.SavedNpcEnabled =
                    npc.enabled;

                npc.CurrentAggroTarget =
                    null;

                // Keep the native component ENABLED. We suppress only its
                // Update() method through a scoped Harmony prefix below.
                // This avoids disturbing open-world NPC lifecycle state.
                FullAiOverrideNpcIds.Add(
                    npc.GetInstanceID());
            }

            if (agent != null)
            {
                try
                {
                    assignment.SavedStoppingDistance =
                        agent.stoppingDistance;

                    assignment.SavedAutoBraking =
                        agent.autoBraking;

                    assignment.AgentMovementStateCaptured =
                        true;

                    if (sim.MyStats != null)
                    {
                        // Use the Sim's real native run speed, not a mechanic
                        // speed boost.
                        agent.speed =
                            sim.MyStats.actualRunSpeed;
                    }

                    // Native follow/combat movement can use a relatively large
                    // stopping distance. That is fine for formation movement
                    // but not for mechanic escapes where the target is only a
                    // short distance beyond the hazard boundary.
                    agent.stoppingDistance =
                        MechanicStoppingDistance;

                    agent.autoBraking =
                        true;

                    if (agent.enabled &&
                        agent.isOnNavMesh)
                    {
                        if (agent.hasPath)
                        {
                            agent.ResetPath();
                        }

                        agent.velocity =
                            Vector3.zero;

                        agent.isStopped =
                            false;
                    }
                }
                catch
                {
                    assignment.AgentMovementStateCaptured =
                        false;

                    // Per-frame movement has a transform fallback.
                }
            }

            SetMechanicMoveAnimation(
                assignment,
                true);

            assignment.FullAiOverrideActive =
                true;

        }


        private static void EndFullAiOverride(
            AvoidAssignment assignment,
            ReleaseMode mode)
        {
            if (assignment == null)
            {
                return;
            }

            SimPlayer sim =
                assignment.Sim;

            NavMeshAgent agent =
                assignment.Agent;

            NPC npc =
                assignment.Npc;

            if (agent != null)
            {
                try
                {
                    if (assignment.AgentMovementStateCaptured)
                    {
                        agent.stoppingDistance =
                            assignment.SavedStoppingDistance;

                        agent.autoBraking =
                            assignment.SavedAutoBraking;
                    }

                    if (agent.enabled &&
                        agent.isOnNavMesh)
                    {
                        if (agent.hasPath)
                        {
                            agent.ResetPath();
                        }

                        agent.velocity =
                            Vector3.zero;

                        // Wake the agent for the first native-AI frame.
                        if (sim != null &&
                            sim.MyStats != null)
                        {
                            agent.speed =
                                sim.MyStats.actualRunSpeed;
                        }

                        agent.isStopped =
                            false;
                    }
                }
                catch
                {
                }

                assignment.AgentMovementStateCaptured =
                    false;
            }

            SetMechanicMoveAnimation(
                assignment,
                false);

            if (npc != null)
            {
                try
                {
                    // NPC remained enabled for the entire mechanic. Removing
                    // it from FullAiOverrideNpcIds below is what resumes its
                    // native Update() loop.
                    if (mode ==
                        ReleaseMode.ResumeEncounter)
                    {
                        Character saved =
                            assignment.SavedAggroTarget;

                        if (saved != null &&
                            saved.Alive &&
                            saved.MyStats != null &&
                            saved.MyStats.CurrentHP > 0)
                        {
                            npc.CurrentAggroTarget =
                                saved;
                        }
                        else
                        {
                            npc.CurrentAggroTarget =
                                null;
                        }
                    }
                    else
                    {
                        // Boss death/reset/despawn is not a normal mechanic
                        // completion. Never restore the old boss target.
                        npc.CurrentAggroTarget =
                            null;

                        npc.retreat =
                            false;
                    }
                }
                catch
                {
                }
            }

            if (mode ==
                    ReleaseMode.ReturnToFollow &&
                sim != null)
            {
                try
                {
                    // Raid/group AI gives GuardSpot precedence over
                    // FollowPlayer(). Clear it using Erenshor's own API.
                    sim.FreeFollow();

                    sim.IgnoreAllCombat =
                        false;

                    NavMeshAgent followAgent =
                        assignment.Agent;

                    if (followAgent != null &&
                        sim.MyStats != null &&
                        followAgent.enabled &&
                        followAgent.isOnNavMesh)
                    {
                        followAgent.speed =
                            sim.MyStats.actualRunSpeed;

                        followAgent.isStopped =
                            false;

                        followAgent.velocity =
                            Vector3.zero;

                        if (followAgent.hasPath)
                        {
                            followAgent.ResetPath();
                        }

                        if (GameData.PlayerControl != null)
                        {
                            Vector3 followPoint =
                                GameData.GetSafeNavMeshPoint(
                                    GameData.PlayerControl.transform.position,
                                    2f,
                                    0.25f,
                                    4f,
                                    0.5f);

                            followAgent.SetDestination(
                                followPoint);
                        }
                    }

                    SetMechanicMoveAnimation(
                        assignment,
                        true);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[WorldBossAI] post-boss follow reset failed for Sim=[" +
                        GetSimName(
                            sim) +
                        "]: " +
                        exception.Message);
                }
            }

            if (npc != null)
            {
                FullAiOverrideNpcIds.Remove(
                    npc.GetInstanceID());
            }

            FullAiOverrideSimIds.Remove(
                assignment.InstanceId);

            if (assignment.Caster != null)
            {
                SuppressedCasterIds.Remove(
                    assignment.Caster.GetInstanceID());
            }

            assignment.FullAiOverrideActive =
                false;

        }


        private static void SetMechanicMoveAnimation(
            AvoidAssignment assignment,
            bool moving)
        {
            if (assignment == null)
            {
                return;
            }

            Animator animator =
                assignment.Animator;

            if (animator == null)
            {
                return;
            }

            try
            {
                animator.SetBool(
                    "Walking",
                    moving);

                animator.SetBool(
                    "Patrol",
                    false);
            }
            catch
            {
                // Animator layouts can differ by class/equipment.
            }
        }


        private static Transform ResolveMovementTransform(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return null;
            }

            NPC npc =
                sim.GetThisNPC();

            if (npc != null)
            {
                Character character =
                    npc.GetChar();

                if (character == null)
                {
                    character =
                        npc.GetComponent<Character>();

                    if (character == null)
                    {
                        character =
                            npc.GetComponentInChildren<Character>(
                                true);
                    }
                }

                if (character != null)
                {
                    return
                        character.transform;
                }

                return
                    npc.transform;
            }

            Character directCharacter =
                sim.GetComponent<Character>();

            if (directCharacter == null)
            {
                directCharacter =
                    sim.GetComponentInChildren<Character>(
                        true);
            }

            return
                directCharacter != null
                    ? directCharacter.transform
                    : sim.transform;
        }


        private static CastSpell ResolveCaster(
            SimPlayer sim,
            NPC npc)
        {
            CastSpell caster =
                null;

            if (npc != null)
            {
                Character character =
                    npc.GetChar();

                if (character != null &&
                    character.MySpells != null)
                {
                    caster =
                        character.MySpells;
                }

                if (caster == null)
                {
                    caster =
                        npc.GetComponent<CastSpell>();
                }

                if (caster == null)
                {
                    caster =
                        npc.GetComponentInChildren<CastSpell>(
                            true);
                }
            }

            if (caster == null &&
                sim != null)
            {
                caster =
                    sim.GetComponent<CastSpell>();

                if (caster == null)
                {
                    caster =
                        sim.GetComponentInChildren<CastSpell>(
                            true);
                }
            }

            return
                caster;
        }


        private static bool TryResolveReachableConePoint(
            Vector3 from,
            Vector3 desired,
            Vector3 origin,
            Vector3 forward,
            float range,
            float halfAngle,
            out Vector3 resolved)
        {
            if (TryAcceptReachableCandidate(
                    from,
                    desired,
                    delegate(Vector3 point)
                    {
                        return !IsPointInsideCone(
                            point,
                            origin,
                            forward,
                            range,
                            halfAngle);
                    },
                    out resolved))
            {
                return true;
            }

            // Search around the desired point for a nearby walkable point that
            // is both OUTSIDE the hazard and connected to the Sim's current
            // NavMesh island.
            float[] radii =
                new float[]
                {
                    0.75f,
                    1.5f,
                    2.25f,
                    3.0f,
                    4.0f,
                    5.0f
                };

            for (int r = 0;
                 r < radii.Length;
                 r++)
            {
                float searchRadius =
                    radii[r];

                for (int step = 0;
                     step < 16;
                     step++)
                {
                    float angle =
                        step *
                        (360f / 16f);

                    Vector3 candidate =
                        desired +
                        Quaternion.Euler(
                            0f,
                            angle,
                            0f) *
                        Vector3.forward *
                        searchRadius;

                    if (TryAcceptReachableCandidate(
                            from,
                            candidate,
                            delegate(Vector3 point)
                            {
                                return !IsPointInsideCone(
                                    point,
                                    origin,
                                    forward,
                                    range,
                                    halfAngle);
                            },
                            out resolved))
                    {
                        return true;
                    }
                }
            }

            resolved =
                from;

            return false;
        }


        private static bool TryResolveReachableCirclePoint(
            Vector3 from,
            Vector3 desired,
            List<Vector3> centers,
            float radius,
            out Vector3 resolved)
        {
            if (TryAcceptReachableCandidate(
                    from,
                    desired,
                    delegate(Vector3 point)
                    {
                        return !IsInsideAnyCircle(
                            point,
                            centers,
                            radius +
                            SafePadding * 0.35f);
                    },
                    out resolved))
            {
                return true;
            }

            float[] radii =
                new float[]
                {
                    0.75f,
                    1.5f,
                    2.25f,
                    3.0f,
                    4.0f,
                    5.0f
                };

            for (int r = 0;
                 r < radii.Length;
                 r++)
            {
                float searchRadius =
                    radii[r];

                for (int step = 0;
                     step < 16;
                     step++)
                {
                    float angle =
                        step *
                        (360f / 16f);

                    Vector3 candidate =
                        desired +
                        Quaternion.Euler(
                            0f,
                            angle,
                            0f) *
                        Vector3.forward *
                        searchRadius;

                    if (TryAcceptReachableCandidate(
                            from,
                            candidate,
                            delegate(Vector3 point)
                            {
                                return !IsInsideAnyCircle(
                                    point,
                                    centers,
                                    radius +
                                    SafePadding * 0.35f);
                            },
                            out resolved))
                    {
                        return true;
                    }
                }
            }

            resolved =
                from;

            return false;
        }


        private delegate bool SafePointPredicate(
            Vector3 point);


        private static bool TryAcceptReachableCandidate(
            Vector3 from,
            Vector3 candidate,
            SafePointPredicate predicate,
            out Vector3 resolved)
        {
            float ignoredPathLength;

            return TryProjectCandidateToTerrainNavMesh(
                from,
                candidate,
                predicate,
                out resolved,
                out ignoredPathLength);
        }


        private static bool TryProjectCandidateToTerrainNavMesh(
            Vector3 from,
            Vector3 candidate,
            SafePointPredicate predicate,
            out Vector3 resolved,
            out float pathLength)
        {
            resolved =
                from;

            pathLength =
                float.PositiveInfinity;

            Vector3 startPoint;

            if (!TryResolveStartOnNavMesh(
                    from,
                    out startPoint))
            {
                return false;
            }

            bool found =
                false;

            Vector3 bestPoint =
                from;

            float bestPathLength =
                float.PositiveInfinity;

            // First try the real collision surface directly beneath the
            // requested X/Z. This is the important open-world terrain step:
            // candidate.y is not trusted.
            Vector3 groundPoint;

            if (TryProjectXZToGround(
                    candidate,
                    from.y,
                    out groundPoint))
            {
                TryEvaluateTerrainCandidate(
                    startPoint,
                    candidate,
                    groundPoint,
                    predicate,
                    ref found,
                    ref bestPoint,
                    ref bestPathLength);
            }

            // Also sweep several Y probes at the SAME requested X/Z. This
            // handles places where the walkable NavMesh is slightly above the
            // rendered terrain, and areas whose terrain collider is unusual.
            float referenceY =
                Mathf.Max(
                    from.y,
                    candidate.y);

            float[] yOffsets =
                new float[]
                {
                    0f,
                    1.5f,
                    -1.5f,
                    3f,
                    -3f,
                    5f,
                    -5f,
                    8f,
                    -8f,
                    12f,
                    -12f
                };

            for (int i = 0;
                 i < yOffsets.Length;
                 i++)
            {
                Vector3 probe =
                    new Vector3(
                        candidate.x,
                        referenceY +
                        yOffsets[i],
                        candidate.z);

                NavMeshHit hit;

                if (!NavMesh.SamplePosition(
                        probe,
                        out hit,
                        TerrainNavSampleRadius,
                        NavMesh.AllAreas))
                {
                    continue;
                }

                TryEvaluateTerrainCandidate(
                    startPoint,
                    candidate,
                    hit.position,
                    predicate,
                    ref found,
                    ref bestPoint,
                    ref bestPathLength);
            }

            if (!found)
            {
                return false;
            }

            resolved =
                bestPoint;

            pathLength =
                bestPathLength;

            return true;
        }


        private static void TryEvaluateTerrainCandidate(
            Vector3 startPoint,
            Vector3 requestedXZ,
            Vector3 candidate,
            SafePointPredicate predicate,
            ref bool found,
            ref Vector3 bestPoint,
            ref float bestPathLength)
        {
            Vector2 requestedPlanar =
                new Vector2(
                    requestedXZ.x,
                    requestedXZ.z);

            Vector2 resolvedPlanar =
                new Vector2(
                    candidate.x,
                    candidate.z);

            // Do not let a spherical SamplePosition call silently move us to a
            // different ledge/polygon several metres away. Terrain correction
            // is allowed to change Y substantially, but X/Z must remain close
            // to the requested mechanic escape point.
            if (Vector2.Distance(
                    requestedPlanar,
                    resolvedPlanar) >
                MaxTerrainHorizontalSnap)
            {
                return;
            }

            if (predicate != null &&
                !predicate(
                    candidate))
            {
                return;
            }

            NavMeshPath path =
                new NavMeshPath();

            if (!NavMesh.CalculatePath(
                    startPoint,
                    candidate,
                    NavMesh.AllAreas,
                    path) ||
                path.status !=
                    NavMeshPathStatus.PathComplete)
            {
                return;
            }

            float length =
                GetPathLength(
                    path);

            if (float.IsInfinity(
                    length) ||
                float.IsNaN(
                    length))
            {
                return;
            }

            if (!found ||
                length <
                    bestPathLength)
            {
                found =
                    true;

                bestPoint =
                    candidate;

                bestPathLength =
                    length;
            }
        }


        private static bool TryResolveStartOnNavMesh(
            Vector3 from,
            out Vector3 startPoint)
        {
            NavMeshHit startHit;

            if (NavMesh.SamplePosition(
                    from,
                    out startHit,
                    NavMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                startPoint =
                    startHit.position;

                return true;
            }

            startPoint =
                from;

            return false;
        }


        private static bool TryProjectXZToGround(
            Vector3 candidate,
            float referenceY,
            out Vector3 groundPoint)
        {
            float topY =
                Mathf.Max(
                    candidate.y,
                    referenceY) +
                TerrainProbeUp;

            Vector3 rayOrigin =
                new Vector3(
                    candidate.x,
                    topY,
                    candidate.z);

            RaycastHit[] hits =
                Physics.RaycastAll(
                    rayOrigin,
                    Vector3.down,
                    TerrainProbeUp +
                    TerrainProbeDown,
                    ~0,
                    QueryTriggerInteraction.Ignore);

            if (hits != null &&
                hits.Length > 0)
            {
                Array.Sort(
                    hits,
                    delegate(
                        RaycastHit a,
                        RaycastHit b)
                    {
                        return a.distance.CompareTo(
                            b.distance);
                    });

                for (int i = 0;
                     i < hits.Length;
                     i++)
                {
                    Collider collider =
                        hits[i].collider;

                    if (collider == null)
                    {
                        continue;
                    }

                    // Ignore characters/NPC bodies. We want the world surface,
                    // not the top of a creature standing on the requested X/Z.
                    if (collider.GetComponentInParent<Character>() != null ||
                        collider.GetComponentInParent<NPC>() != null)
                    {
                        continue;
                    }

                    Vector3 surface =
                        hits[i].point;

                    NavMeshHit navHit;

                    if (NavMesh.SamplePosition(
                            surface +
                            Vector3.up *
                            0.25f,
                            out navHit,
                            TerrainNavSampleRadius,
                            NavMesh.AllAreas))
                    {
                        Vector2 requestedPlanar =
                            new Vector2(
                                candidate.x,
                                candidate.z);

                        Vector2 navPlanar =
                            new Vector2(
                                navHit.position.x,
                                navHit.position.z);

                        if (Vector2.Distance(
                                requestedPlanar,
                                navPlanar) <=
                            MaxTerrainHorizontalSnap)
                        {
                            groundPoint =
                                navHit.position;

                            return true;
                        }
                    }
                }
            }

            groundPoint =
                candidate;

            return false;
        }


        private static float GetPathLength(
            NavMeshPath path)
        {
            if (path == null ||
                path.corners == null ||
                path.corners.Length <
                    2)
            {
                return 0f;
            }

            float length =
                0f;

            for (int i = 1;
                 i < path.corners.Length;
                 i++)
            {
                length +=
                    Vector3.Distance(
                        path.corners[i - 1],
                        path.corners[i]);
            }

            return length;
        }


        private static bool HasCompleteNavMeshPath(
            Vector3 from,
            Vector3 to)
        {
            Vector3 startPoint;

            if (!TryResolveStartOnNavMesh(
                    from,
                    out startPoint))
            {
                return false;
            }

            NavMeshHit endHit;

            if (!NavMesh.SamplePosition(
                    to,
                    out endHit,
                    TerrainNavSampleRadius,
                    NavMesh.AllAreas))
            {
                return false;
            }

            NavMeshPath path =
                new NavMeshPath();

            if (!NavMesh.CalculatePath(
                    startPoint,
                    endHit.position,
                    NavMesh.AllAreas,
                    path))
            {
                return false;
            }

            return path.status ==
                   NavMeshPathStatus.PathComplete;
        }


        private static float GetCompletePathLength(
            Vector3 from,
            Vector3 to)
        {
            Vector3 startPoint;

            if (!TryResolveStartOnNavMesh(
                    from,
                    out startPoint))
            {
                return -1f;
            }

            NavMeshHit endHit;

            if (!NavMesh.SamplePosition(
                    to,
                    out endHit,
                    TerrainNavSampleRadius,
                    NavMesh.AllAreas))
            {
                return -1f;
            }

            NavMeshPath path =
                new NavMeshPath();

            if (!NavMesh.CalculatePath(
                    startPoint,
                    endHit.position,
                    NavMesh.AllAreas,
                    path) ||
                path.status !=
                    NavMeshPathStatus.PathComplete)
            {
                return -1f;
            }

            return GetPathLength(
                path);
        }


        private static bool IsPointInsideCone(
            Vector3 point,
            Vector3 origin,
            Vector3 forward,
            float range,
            float halfAngle)
        {
            Vector3 delta =
                point -
                origin;

            delta.y =
                0f;

            float distance =
                delta.magnitude;

            if (distance <=
                    0.05f ||
                distance >
                    range)
            {
                return false;
            }

            Vector3 planarForward =
                forward;

            planarForward.y =
                0f;

            if (planarForward.sqrMagnitude <=
                0.0001f)
            {
                planarForward =
                    Vector3.forward;
            }

            planarForward.Normalize();

            float angle =
                Mathf.Abs(
                    Vector3.SignedAngle(
                        planarForward,
                        delta.normalized,
                        Vector3.up));

            return angle <=
                   halfAngle;
        }


        private static Vector3 ResolveNavMeshPoint(
            Vector3 desired,
            Vector3 fallback)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    desired,
                    out hit,
                    NavMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                return
                    hit.position;
            }

            if (NavMesh.SamplePosition(
                    fallback,
                    out hit,
                    NavMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                return
                    hit.position;
            }

            return
                fallback;
        }


        private static bool IsInsideAnyCircle(
            Vector3 point,
            List<Vector3> centers,
            float radius)
        {
            float radiusSqr =
                radius *
                radius;

            for (int i = 0;
                 i < centers.Count;
                 i++)
            {
                Vector3 delta =
                    point -
                    centers[i];

                delta.y =
                    0f;

                if (delta.sqrMagnitude <=
                    radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }


        private static IEnumerator PostCombatRecoveryRoutine(
            int generation,
            string reason)
        {
            for (int frame = 0;
                 frame < 6 &&
                 generation ==
                    _postCombatRecoveryGeneration;
                 frame++)
            {
                ResetOfficialRosterToFollow(
                    reason +
                    " recovery-frame-" +
                    frame);

                yield return null;
            }
        }


        private static void ResetOfficialRosterToFollow(
            string reason)
        {
            List<SimPlayer> sims =
                GetLivingRaidSims();

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                SimPlayer sim =
                    sims[i];

                if (sim == null)
                {
                    continue;
                }

                try
                {
                    NPC npc =
                        sim.GetThisNPC();

                    CastSpell caster =
                        ResolveCaster(
                            sim,
                            npc);

                    if (!sim.enabled)
                    {
                        sim.enabled =
                            true;
                    }

                    if (npc != null)
                    {
                        npc.CurrentAggroTarget =
                            null;

                        npc.retreat =
                            false;

                        if (!npc.enabled)
                        {
                            npc.enabled =
                                true;
                        }

                        FullAiOverrideNpcIds.Remove(
                            npc.GetInstanceID());
                    }

                    FullAiOverrideSimIds.Remove(
                        sim.GetInstanceID());

                    sim.RunningAway =
                        false;

                    sim.PullerPulling =
                        false;

                    sim.pursuing =
                        false;

                    sim.assistCall =
                        false;

                    sim.InCampWhilePulling =
                        false;

                    sim.PullTarget =
                        null;

                    sim.CurrentPullPhase =
                        SimPlayer.PullPhases.NotPulling;

                    sim.suspendGuard =
                        null;

                    sim.IgnoreAllCombat =
                        false;

                    // Native GroupFollow() explicitly clears freeRoamAzure
                    // before FreeFollow(). SimPlayer.Update() gives
                    // freeRoamAzure precedence over InRaid and otherwise sends
                    // the Sim into SoloPlayLogic instead of InRaidLogic.
                    sim.freeRoamAzure =
                        false;

                    sim.FreeFollow();

                    if (sim.MyStats != null &&
                        sim.MyStats.Myself != null &&
                        sim.MyStats.Myself.MyCharmedNPC != null)
                    {
                        sim.MyStats.Myself.MyCharmedNPC.CurrentAggroTarget =
                            null;
                    }

                    if (SimTrainingField != null)
                    {
                        SimTrainingField.SetValue(
                            sim,
                            false);
                    }

                    if (SimAllowForcedPullPathField != null)
                    {
                        SimAllowForcedPullPathField.SetValue(
                            sim,
                            false);
                    }

                    // Do not destroy SpellVessels here. If a Sim is currently
                    // casting, native CastSpell/SpellVessel code must be allowed
                    // to finish and clear isCasting() itself.

                    NavMeshAgent agent =
                        sim.GetComponent<NavMeshAgent>();

                    if (agent != null &&
                        sim.MyStats != null &&
                        agent.enabled &&
                        agent.isOnNavMesh)
                    {
                        agent.speed =
                            sim.MyStats.actualRunSpeed;

                        agent.velocity =
                            Vector3.zero;

                        if (agent.hasPath)
                        {
                            agent.ResetPath();
                        }

                        agent.isStopped =
                            false;

                        if (GameData.PlayerControl != null)
                        {
                            Vector3 followPoint =
                                GameData.GetSafeNavMeshPoint(
                                    GameData.PlayerControl.transform.position,
                                    2f,
                                    0.25f,
                                    4f,
                                    0.5f);

                            agent.SetDestination(
                                followPoint);
                        }
                    }

                    Animator animator =
                        sim.GetComponent<Animator>();

                    if (animator != null)
                    {
                        animator.SetBool(
                            "Walking",
                            true);

                        animator.SetBool(
                            "Patrol",
                            false);
                    }

                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[WorldBossAI] POST-COMBAT STATE RECOVERY failed Sim=[" +
                        GetSimName(
                            sim) +
                        "]: " +
                        exception.Message);
                }
            }
        }


        private static List<SimPlayer> GetLivingRaidSims()
        {
            // World-boss mechanics are allowed to control only Sims that are
            // explicitly members of the player's current party or raid.
            // We deliberately do NOT scan the scene for friendly SimPlayers.
            List<SimPlayer> result =
                new List<SimPlayer>();

            int partyCount =
                0;

            int raidCount =
                0;

            // Normal open-world party membership.
            if (GameData.GroupMembers != null)
            {
                for (int i = 0;
                     i < GameData.GroupMembers.Length;
                     i++)
                {
                    SimPlayerTracking tracking =
                        GameData.GroupMembers[i];

                    SimPlayer sim =
                        tracking != null
                            ? tracking.MyAvatar
                            : null;

                    if (!IsValidLivingRaidSim(
                            sim))
                    {
                        continue;
                    }

                    if (AddUniqueSim(
                            result,
                            sim))
                    {
                        partyCount++;
                    }
                }
            }

            // Native player raid membership. This is the authoritative source
            // for world-boss raid Sims when Erenshor has promoted the party to
            // a raid, even outside a dedicated raid scene.
            RaidManager manager =
                GameData.RaidManager;

            if (manager != null &&
                manager.Raiders != null)
            {
                foreach (RaidMemberSlot slot in
                         manager.Raiders)
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

                    if (AddUniqueSim(
                            result,
                            sim))
                    {
                        raidCount++;
                    }
                }
            }


            return result;
        }


        private static bool AddUniqueSim(
            List<SimPlayer> result,
            SimPlayer sim)
        {
            if (result == null ||
                sim == null)
            {
                return false;
            }

            for (int i = 0;
                 i < result.Count;
                 i++)
            {
                if (result[i] == sim)
                {
                    return false;
                }
            }

            result.Add(
                sim);

            return true;
        }


        private static bool IsValidLivingRaidSim(
            SimPlayer sim)
        {
            // Do not require sim.InRaid here. In an open-world player party,
            // native SimPlayer.Update() uses GroupedWithPlayerLogic() instead
            // of InRaidLogic(). Membership is enforced by GetLivingRaidSims()
            // using GameData.GroupMembers / RaidManager.Raiders.
            if (sim == null ||
                sim.gameObject == null ||
                !sim.gameObject.activeInHierarchy)
            {
                return false;
            }

            NPC npc =
                sim.GetThisNPC();

            Character character =
                npc != null
                    ? npc.GetChar()
                    : null;

            return npc != null &&
                character != null &&
                character.Alive &&
                character.MyStats != null &&
                character.MyStats.CurrentHP >
                    0;
        }


        private static string GetSimName(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return
                    "<null>";
            }

            NPC npc =
                sim.GetThisNPC();

            if (npc != null &&
                !string.IsNullOrEmpty(
                    npc.NPCName))
            {
                return
                    npc.NPCName;
            }

            return
                sim.name;
        }
    }


    /// <summary>
    /// Suppress only native raid-combat decision making for Sims currently
    /// owned by a world-boss movement mechanic. Their normal Update, damage,
    /// death, animation and stat lifecycle remain native.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_WorldBossSimAvoidanceSuppressSpellStarts
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] methods =
                typeof(CastSpell).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (method == null ||
                    method.Name !=
                        "StartSpell")
                {
                    continue;
                }

                if (method.ReturnType !=
                    typeof(bool))
                {
                    continue;
                }

                yield return
                    method;
            }
        }


        [HarmonyPrefix]
        private static bool Prefix(
            CastSpell __instance,
            ref bool __result)
        {
            if (!WorldBossSimAvoidanceRuntime
                    .ShouldSuppressCaster(
                        __instance))
            {
                return true;
            }

            __result =
                false;

            return false;
        }
    }



    /// <summary>
    /// While a mechanic owns a Sim, suppress only that Sim NPC's native
    /// decision tick. The NPC MonoBehaviour remains enabled at all times.
    /// This is deliberately non-destructive: once its id is removed from the
    /// override set, native NPC.Update() resumes on the very next frame.
    /// </summary>
    [HarmonyPatch(
        typeof(NPC),
        "Update")]
    internal static class Patch_WorldBossSimAvoidanceNpcAiOverride
    {
        [HarmonyPrefix]
        private static bool Prefix(
            NPC __instance)
        {
            return
                !WorldBossSimAvoidanceRuntime
                    .ShouldOverrideNpcAi(
                        __instance);
        }
    }



    /// <summary>
    /// While a world-boss mechanic owns a Sim, skip the SimPlayer's entire
    /// native Update AI tick. Character, Stats, NavMeshAgent, Animator and
    /// damage/death components remain alive; FOTF exclusively drives the
    /// mechanic movement until release.
    /// </summary>
    [HarmonyPatch(
        typeof(SimPlayer),
        "Update")]
    internal static class Patch_WorldBossSimAvoidanceFullAiOverride
    {
        [HarmonyPrefix]
        private static bool Prefix(
            SimPlayer __instance)
        {
            return
                !WorldBossSimAvoidanceRuntime
                    .ShouldOverrideFullSimAi(
                        __instance);
        }
    }

}
