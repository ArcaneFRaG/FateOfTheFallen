using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        /// <summary>
        /// A rigid piece of an authored multipart armour model bound to a live
        /// humanoid bone.  The authored mesh itself is not skinned, so each
        /// logical armour plate stores the bone-relative pose it had when the
        /// item was first fitted.  LateUpdate then reapplies that pose to the
        /// animated Synty bone.  This gives shoulder/arm plates and individual
        /// finger lames proper animation without modifying the AssetBundle.
        /// </summary>
        private sealed class BA_RigidBinding
        {
            internal Transform Part;
            internal Transform Bone;
            internal Vector3 BoneLocalPosition;
            internal Quaternion BoneLocalRotation;
            internal Vector3 AuthoredLocalScale;
            internal bool ParentedToBone;
        }

        private sealed class BA_FingerChainCandidate
        {
            internal Transform[] Bones;
            internal Vector3 Direction;
            internal float RootDistance;
        }


        private static Transform BA_FindExactChild(GameObject root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate != null &&
                    string.Equals(candidate.name, exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void BA_AddBinding(
            List<BA_RigidBinding> bindings,
            GameObject visualRoot,
            string exactPartName,
            Transform bone)
        {
            if (bindings == null || visualRoot == null || bone == null)
            {
                return;
            }

            Transform part = BA_FindExactChild(visualRoot, exactPartName);
            if (part == null)
            {
                return;
            }

            Bounds bounds;
            if (!BA_TryGetRendererBounds(part, out bounds))
            {
                return;
            }

            // Every authored armour mesh was exported with its object origin at
            // the prefab root while the actual vertices sit elsewhere.  Bind a
            // pivot at the geometry centre, then parent that pivot directly to
            // the real animated Synty bone with worldPositionStays=true.
            //
            // This is deliberately different from the old per-frame matrix
            // follower.  Once the pivot is a child of the native bone, Unity's
            // own animation hierarchy drives it exactly like native equipment:
            // shoulder/arm plates inherit the upper-arm animation and finger
            // lames inherit the corresponding finger joint animation.
            GameObject pivotObject =
                new GameObject("FOTF_BindPivot_" + exactPartName);

            Transform pivot = pivotObject.transform;
            pivot.position = bounds.center;
            pivot.rotation = part.rotation;
            pivot.localScale = Vector3.one;

            // Put the authored geometry below the pivot without changing its
            // current fitted world pose.
            part.SetParent(pivot, true);

            // Parent the pivot to the actual animated bone, also preserving the
            // fitted pose and current body-item scale.  From this point onward
            // no LateUpdate transform correction is needed for this piece.
            pivot.SetParent(bone, true);

            BA_RigidBinding binding = new BA_RigidBinding();
            binding.Part = pivot;
            binding.Bone = bone;
            binding.BoneLocalPosition = pivot.localPosition;
            binding.BoneLocalRotation = pivot.localRotation;
            binding.AuthoredLocalScale = pivot.localScale;
            binding.ParentedToBone = true;
            bindings.Add(binding);
        }

        private static bool BA_TryGetRendererBounds(
            Transform part,
            out Bounds bounds)
        {
            bounds = new Bounds();
            if (part == null)
            {
                return false;
            }

            Renderer[] renderers =
                part.GetComponentsInChildren<Renderer>(true);

            bool found = false;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }


        private static void BA_ApplyBindings(List<BA_RigidBinding> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            // Direct-parent bindings are animated automatically by Unity. Keep
            // the legacy path for any future binding that intentionally stays
            // outside the bone hierarchy.
            for (int i = 0; i < bindings.Count; i++)
            {
                BA_RigidBinding binding = bindings[i];
                if (binding == null || binding.Part == null || binding.Bone == null)
                {
                    continue;
                }

                if (binding.ParentedToBone)
                {
                    continue;
                }

                binding.Part.position = binding.Bone.TransformPoint(binding.BoneLocalPosition);
                binding.Part.rotation = binding.Bone.rotation * binding.BoneLocalRotation;
                binding.Part.localScale = binding.AuthoredLocalScale;
            }
        }

        private static void BA_DestroyBindings(List<BA_RigidBinding> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                BA_RigidBinding binding = bindings[i];
                if (binding == null || binding.Part == null)
                {
                    continue;
                }

                // The pivot may have been reparented underneath the native
                // skeleton and is therefore no longer destroyed with the
                // original prefab root. Destroying the pivot also destroys the
                // custom mesh child that it owns.
                if (binding.Part.gameObject != null)
                {
                    UnityEngine.Object.Destroy(binding.Part.gameObject);
                }
            }

            bindings.Clear();
        }

        /// <summary>
        /// The Blender body pieces use thin overlapping plates.  Some of the
        /// older exported meshes contain winding that faces inward on one side.
        /// Built-in Standard culls those triangles, creating apparent holes.
        /// Body equipment is intentionally rendered double-sided so the plate
        /// geometry remains solid from every animation/camera angle.
        /// </summary>
        private static void BA_ForceDoubleSided(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; renderers != null && r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                for (int m = 0; materials != null && m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null)
                    {
                        continue;
                    }

                    if (material.HasProperty("_Cull"))
                    {
                        material.SetInt("_Cull", 0);
                    }

                    if (material.HasProperty("_CullMode"))
                    {
                        material.SetInt("_CullMode", 0);
                    }
                }
            }
        }

        // Standard does not expose a reliable runtime culling switch on all
        // Unity 2021 builds.  Some Carapace plates were exported with opposite
        // winding, so material _Cull changes alone can still leave faces
        // invisible.  Duplicate each triangle with reversed winding once when
        // the body visual spawns.  This makes the actual geometry two-sided and
        // is independent of the shader's culling implementation.
        // Runtime mesh mutation is deliberately disabled. Unity/Erenshor can
        // reject or crash on mesh readback/recalculation for AssetBundle meshes
        // that are not marked readable. Keep the method as a compatibility
        // no-op so callers are harmless; body-face fixes must be baked into the
        // source mesh/FBX or handled by a safe shader/material path instead.
        private static void BA_MakeMeshFiltersDoubleSided(GameObject visual)
        {
            // Intentionally no-op. Do not clone/read/write triangles here.
        }

        // modelFingerIndex is the authored Finger_0..3 order in the V7 Grasp.
        // The left model's thumb sits beside Finger_0, while the mirrored right
        // model's thumb sits beside Finger_3, so the anatomical order reverses.
        private static HumanBodyBones BA_GetFingerBone(
            bool left,
            int modelFingerIndex,
            int segment)
        {
            int anatomical = left ? modelFingerIndex : 3 - modelFingerIndex;

            if (left)
            {
                if (anatomical == 0)
                    return segment == 0 ? HumanBodyBones.LeftIndexProximal : (segment == 1 ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.LeftIndexDistal);
                if (anatomical == 1)
                    return segment == 0 ? HumanBodyBones.LeftMiddleProximal : (segment == 1 ? HumanBodyBones.LeftMiddleIntermediate : HumanBodyBones.LeftMiddleDistal);
                if (anatomical == 2)
                    return segment == 0 ? HumanBodyBones.LeftRingProximal : (segment == 1 ? HumanBodyBones.LeftRingIntermediate : HumanBodyBones.LeftRingDistal);
                return segment == 0 ? HumanBodyBones.LeftLittleProximal : (segment == 1 ? HumanBodyBones.LeftLittleIntermediate : HumanBodyBones.LeftLittleDistal);
            }

            if (anatomical == 0)
                return segment == 0 ? HumanBodyBones.RightIndexProximal : (segment == 1 ? HumanBodyBones.RightIndexIntermediate : HumanBodyBones.RightIndexDistal);
            if (anatomical == 1)
                return segment == 0 ? HumanBodyBones.RightMiddleProximal : (segment == 1 ? HumanBodyBones.RightMiddleIntermediate : HumanBodyBones.RightMiddleDistal);
            if (anatomical == 2)
                return segment == 0 ? HumanBodyBones.RightRingProximal : (segment == 1 ? HumanBodyBones.RightRingIntermediate : HumanBodyBones.RightRingDistal);
            return segment == 0 ? HumanBodyBones.RightLittleProximal : (segment == 1 ? HumanBodyBones.RightLittleIntermediate : HumanBodyBones.RightLittleDistal);
        }

        private static HumanBodyBones BA_GetThumbBone(bool left, int segment)
        {
            if (left)
            {
                return segment == 0 ? HumanBodyBones.LeftThumbProximal :
                    (segment == 1 ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.LeftThumbDistal);
            }

            return segment == 0 ? HumanBodyBones.RightThumbProximal :
                (segment == 1 ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.RightThumbDistal);
        }


        // Resolve a glove/finger binding against the humanoid avatar first and
        // then fall back to the concrete Synty transform hierarchy. Some
        // Erenshor avatars do not populate every HumanBodyBones finger entry
        // even though the finger transforms are present and animated.
        private static Transform BA_ResolveFingerTransform(
            Animator animator,
            Transform hand,
            bool left,
            int modelFingerIndex,
            int segment)
        {
            if (animator != null)
            {
                try
                {
                    Transform humanoid =
                        animator.GetBoneTransform(
                            BA_GetFingerBone(left, modelFingerIndex, segment));

                    if (humanoid != null)
                    {
                        return humanoid;
                    }
                }
                catch
                {
                }
            }

            int anatomical = left ? modelFingerIndex : 3 - modelFingerIndex;
            string finger = anatomical == 0 ? "index" :
                (anatomical == 1 ? "middle" :
                (anatomical == 2 ? "ring" : "little"));

            Transform named = BA_FindFingerTransformByName(hand, finger, segment);
            if (named != null)
            {
                return named;
            }

            return BA_FindFingerTransformByHierarchy(
                hand,
                left,
                modelFingerIndex,
                segment,
                false);
        }

        private static Transform BA_ResolveThumbTransform(
            Animator animator,
            Transform hand,
            bool left,
            int segment)
        {
            if (animator != null)
            {
                try
                {
                    Transform humanoid =
                        animator.GetBoneTransform(
                            BA_GetThumbBone(left, segment));

                    if (humanoid != null)
                    {
                        return humanoid;
                    }
                }
                catch
                {
                }
            }

            Transform named = BA_FindFingerTransformByName(hand, "thumb", segment);
            if (named != null)
            {
                return named;
            }

            return BA_FindFingerTransformByHierarchy(
                hand,
                left,
                0,
                segment,
                true);
        }

        private static Transform BA_FindFingerTransformByName(
            Transform hand,
            string finger,
            int segment)
        {
            if (hand == null || string.IsNullOrEmpty(finger))
            {
                return null;
            }

            Transform[] all = hand.GetComponentsInChildren<Transform>(true);
            if (all == null)
            {
                return null;
            }

            string[] fingerAliases;
            if (string.Equals(finger, "little", StringComparison.OrdinalIgnoreCase))
            {
                fingerAliases = new string[] { "little", "pinky", "pinkie" };
            }
            else
            {
                fingerAliases = new string[] { finger };
            }

            string[] segmentAliases =
                segment == 0
                    ? new string[] { "proximal", "prox", "1", "01" }
                    : (segment == 1
                        ? new string[] { "intermediate", "inter", "2", "02" }
                        : new string[] { "distal", "dist", "3", "03" });

            Transform best = null;
            int bestScore = -1;

            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null || candidate == hand)
                {
                    continue;
                }

                string normalized = BA_NormalizeBoneName(candidate.name);
                bool fingerMatch = false;
                for (int f = 0; f < fingerAliases.Length; f++)
                {
                    if (normalized.IndexOf(fingerAliases[f], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fingerMatch = true;
                        break;
                    }
                }

                if (!fingerMatch)
                {
                    continue;
                }

                int score = 10;
                for (int a = 0; a < segmentAliases.Length; a++)
                {
                    string alias = segmentAliases[a];
                    if (normalized.EndsWith(alias, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 8;
                        break;
                    }

                    if (normalized.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score += 3;
                    }
                }

                // Prefer the expected hierarchy depth: proximal is usually
                // nearest the hand, followed by intermediate then distal.
                int depth = 0;
                Transform parent = candidate.parent;
                while (parent != null && parent != hand && depth < 8)
                {
                    depth++;
                    parent = parent.parent;
                }

                int wantedDepth = segment + 1;
                score -= Math.Abs(depth - wantedDepth);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        // Last-resort finger discovery for Synty avatars whose Humanoid
        // Avatar omits finger mappings and whose bone names are generic.  Five
        // short 3-joint chains normally descend from the hand.  The thumb is
        // the chain whose direction differs most from the other four; the
        // remaining fingers are ordered by distance from that thumb root
        // (index -> middle -> ring -> little).  This runs only while equipping
        // the Grasp, never per frame.
        private static Transform BA_FindFingerTransformByHierarchy(
            Transform hand,
            bool left,
            int modelFingerIndex,
            int segment,
            bool wantThumb)
        {
            if (hand == null || segment < 0 || segment > 2)
            {
                return null;
            }

            Transform[] all = hand.GetComponentsInChildren<Transform>(true);
            if (all == null || all.Length == 0)
            {
                return null;
            }

            List<BA_FingerChainCandidate> candidates =
                new List<BA_FingerChainCandidate>();

            for (int i = 0; i < all.Length; i++)
            {
                Transform root = all[i];
                if (root == null || root == hand)
                {
                    continue;
                }

                // Finger proximal bones sit very close below the hand.  Avoid
                // walking arbitrarily deep helper/socket hierarchies.
                int depth = 0;
                Transform parent = root.parent;
                while (parent != null && parent != hand && depth < 5)
                {
                    depth++;
                    parent = parent.parent;
                }

                if (parent != hand || depth > 2)
                {
                    continue;
                }

                Transform[] chain = BA_BuildThreeJointChain(root);
                if (chain == null)
                {
                    continue;
                }

                float rootDistance = Vector3.Distance(hand.position, root.position);
                if (rootDistance > 0.45f)
                {
                    continue;
                }

                Vector3 direction = chain[2].position - chain[0].position;
                if (direction.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                BA_FingerChainCandidate candidate =
                    new BA_FingerChainCandidate();
                candidate.Bones = chain;
                candidate.Direction = direction.normalized;
                candidate.RootDistance = rootDistance;
                candidates.Add(candidate);
            }

            if (candidates.Count < 5)
            {
                return null;
            }

            // Prefer the five roots nearest the hand so weapon/socket helper
            // chains cannot displace the actual digits.
            candidates.Sort(delegate(BA_FingerChainCandidate a, BA_FingerChainCandidate b)
            {
                return a.RootDistance.CompareTo(b.RootDistance);
            });

            if (candidates.Count > 5)
            {
                candidates.RemoveRange(5, candidates.Count - 5);
            }

            int thumbIndex = -1;
            float lowestAgreement = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                float agreement = 0f;
                int comparisons = 0;

                for (int j = 0; j < candidates.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    agreement += Vector3.Dot(
                        candidates[i].Direction,
                        candidates[j].Direction);
                    comparisons++;
                }

                if (comparisons > 0)
                {
                    agreement /= comparisons;
                }

                if (agreement < lowestAgreement)
                {
                    lowestAgreement = agreement;
                    thumbIndex = i;
                }
            }

            if (thumbIndex < 0)
            {
                return null;
            }

            BA_FingerChainCandidate thumb = candidates[thumbIndex];
            if (wantThumb)
            {
                return thumb.Bones[segment];
            }

            List<BA_FingerChainCandidate> fingers =
                new List<BA_FingerChainCandidate>();

            for (int i = 0; i < candidates.Count; i++)
            {
                if (i != thumbIndex)
                {
                    fingers.Add(candidates[i]);
                }
            }

            fingers.Sort(delegate(BA_FingerChainCandidate a, BA_FingerChainCandidate b)
            {
                float da = Vector3.Distance(a.Bones[0].position, thumb.Bones[0].position);
                float db = Vector3.Distance(b.Bones[0].position, thumb.Bones[0].position);
                return da.CompareTo(db);
            });

            int anatomical = Mathf.Clamp(left ? modelFingerIndex : 3 - modelFingerIndex, 0, 3);
            if (anatomical >= fingers.Count)
            {
                return null;
            }

            return fingers[anatomical].Bones[segment];
        }

        private static Transform[] BA_BuildThreeJointChain(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] chain = new Transform[3];
            chain[0] = root;

            Transform current = root;
            for (int segment = 1; segment < 3; segment++)
            {
                Transform next = null;
                float bestDistance = -1f;

                for (int c = 0; c < current.childCount; c++)
                {
                    Transform child = current.GetChild(c);
                    if (child == null || child.GetComponent<Renderer>() != null)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(current.position, child.position);
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        next = child;
                    }
                }

                if (next == null)
                {
                    return null;
                }

                chain[segment] = next;
                current = next;
            }

            return chain;
        }

        private static string BA_NormalizeBoneName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(c))
                {
                    buffer[count++] = c;
                }
            }

            return new string(buffer, 0, count);
        }

        // Build a stable authored-hand basis without assuming that the Synty
        // Hand transform's local axes match the Blender FBX axes. The Grasp was
        // authored with +Y running from wrist toward elbow and +Z on the dorsal
        // side of the hand. We solve that orientation once, capture it relative
        // to the animated Hand bone, then simply inherit the real wrist motion.
        private static Quaternion BA_BuildAuthoredHandRotation(
            Transform hand,
            Transform lowerArm,
            Transform actorRoot)
        {
            // Export basis for the V7 Grasp:
            //   custom local +Z = wrist -> fingers
            //   custom local +Y = dorsal/back of hand
            // The old implementation incorrectly mapped +Y along the forearm,
            // which is exactly a 90-degree sideways hand error.
            Vector3 fingerAxis =
                lowerArm != null
                    ? hand.position - lowerArm.position
                    : (actorRoot != null ? -actorRoot.up : Vector3.forward);

            if (fingerAxis.sqrMagnitude < 0.000001f)
            {
                fingerAxis = actorRoot != null ? -actorRoot.up : Vector3.forward;
            }
            fingerAxis.Normalize();

            Vector3 dorsal =
                actorRoot != null
                    ? actorRoot.forward
                    : Vector3.up;

            dorsal = Vector3.ProjectOnPlane(dorsal, fingerAxis);
            if (dorsal.sqrMagnitude < 0.000001f && hand != null)
            {
                dorsal = Vector3.ProjectOnPlane(hand.up, fingerAxis);
            }
            if (dorsal.sqrMagnitude < 0.000001f)
            {
                dorsal = Vector3.ProjectOnPlane(Vector3.up, fingerAxis);
            }
            if (dorsal.sqrMagnitude < 0.000001f)
            {
                dorsal = Vector3.Cross(fingerAxis, Vector3.right);
            }
            dorsal.Normalize();

            return Quaternion.LookRotation(fingerAxis, dorsal);
        }
    }
}
