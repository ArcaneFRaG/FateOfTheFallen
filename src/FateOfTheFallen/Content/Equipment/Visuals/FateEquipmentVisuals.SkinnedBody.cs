using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        // Carapace and Grasp are now authored as proper Synty-skinned FBX assets.
        // They must be instantiated once and have their imported skin bones
        // redirected to the live player/SimPlayer skeleton.  Do NOT reposition
        // individual plates/fingers at runtime; the native animation skeleton
        // drives all deformation after this one-time remap.
        private static bool SR_UsesNativeSkinnedRig(
            string itemId,
            Item.SlotType slot)
        {
            // Grasp must remain a true skinned mesh. The hand/finger shell is
            // extracted from the native Synty body and retains the native
            // vertex weights, then this runtime redirects the imported FBX
            // bones onto the live player/SimPlayer skeleton.
            //
            // Carapace remains on the rigid modular path.
            return
                slot == Item.SlotType.Hand &&
                !string.IsNullOrEmpty(itemId) &&
                string.Equals(
                    itemId,
                    NhalSeraEquipment.GraspId,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void SR_PrepareSkinnedVisual(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            Transform[] transforms =
                visual.GetComponentsInChildren<Transform>(true);

            for (int i = 0;
                 transforms != null && i < transforms.Length;
                 i++)
            {
                Transform transform = transforms[i];
                if (transform != null && !transform.gameObject.activeSelf)
                {
                    transform.gameObject.SetActive(true);
                }
            }

            // The prefab's imported Animator/Animation must never drive its
            // private export skeleton after the SkinnedMeshRenderer bones have
            // been redirected to the live Erenshor character skeleton.
            Animator[] animators =
                visual.GetComponentsInChildren<Animator>(true);

            for (int i = 0;
                 animators != null && i < animators.Length;
                 i++)
            {
                if (animators[i] != null)
                {
                    animators[i].enabled = false;
                }
            }

            Animation[] animations =
                visual.GetComponentsInChildren<Animation>(true);

            for (int i = 0;
                 animations != null && i < animations.Length;
                 i++)
            {
                if (animations[i] != null)
                {
                    animations[i].enabled = false;
                }
            }

            Collider[] colliders =
                visual.GetComponentsInChildren<Collider>(true);

            for (int i = 0;
                 colliders != null && i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody[] rigidbodies =
                visual.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0;
                 rigidbodies != null && i < rigidbodies.Length;
                 i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = true;
                body.detectCollisions = false;
            }

            Renderer[] renderers =
                visual.GetComponentsInChildren<Renderer>(true);

            for (int i = 0;
                 renderers != null && i < renderers.Length;
                 i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = true;

                SkinnedMeshRenderer skinned =
                    renderer as SkinnedMeshRenderer;

                if (skinned != null)
                {
                    skinned.updateWhenOffscreen = true;
                }
            }

            visual.SetActive(true);
        }

        private static bool SR_RemapSkinnedVisual(
            Transform actorRoot,
            GameObject visual,
            string context,
            out int rendererCount,
            out int mappedBoneCount,
            out int missingBoneCount)
        {
            rendererCount = 0;
            mappedBoneCount = 0;
            missingBoneCount = 0;

            if (actorRoot == null || visual == null)
            {
                return false;
            }

            // The remade FBXs are authored in the Synty bind pose.  Their root
            // therefore sits directly on the character root with no old
            // hand/chest calibration offsets or import-scale corrections.
            Transform visualTransform = visual.transform;
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;

            Dictionary<string, Transform> exact =
                new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, Transform> normalized =
                new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

            Transform searchRoot =
                SR_FindCharacterSkeletonRoot(actorRoot, visual.transform);

            Transform[] targets =
                searchRoot != null
                    ? searchRoot.GetComponentsInChildren<Transform>(true)
                    : actorRoot.GetComponentsInChildren<Transform>(true);

            for (int i = 0;
                 targets != null && i < targets.Length;
                 i++)
            {
                Transform target = targets[i];
                if (target == null || SR_IsInsideVisual(target, visual.transform))
                {
                    continue;
                }

                if (!exact.ContainsKey(target.name))
                {
                    exact[target.name] = target;
                }

                string key = SR_NormalizeBoneName(target.name);
                if (!string.IsNullOrEmpty(key) && !normalized.ContainsKey(key))
                {
                    normalized[key] = target;
                }
            }

            SkinnedMeshRenderer[] renderers =
                visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                Plugin.NativeLog.LogError(
                    "[FateEquipmentVisuals] skinned body prefab [" +
                    context +
                    "] contains no SkinnedMeshRenderer. Re-export the remade FBX with its Armature modifier / skin weights.");
                return false;
            }

            HashSet<string> missingNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Keep the FBX's ORIGINAL skin bones and bindposes. The V16.1
            // Blender export already contains a self-consistent Synty armature.
            // Instead of replacing renderer.bones (which mixes Blender bindposes
            // with Erenshor transforms), drive those imported bones so their
            // WORLD transforms follow the matching live Erenshor bones.
            FOTFSkinnedBoneFollower follower =
                visual.GetComponent<FOTFSkinnedBoneFollower>();

            if (follower == null)
            {
                follower = visual.AddComponent<FOTFSkinnedBoneFollower>();
            }

            follower.ClearMappings();

            for (int r = 0; r < renderers.Length; r++)
            {
                SkinnedMeshRenderer renderer = renderers[r];
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                rendererCount++;

                Transform[] sourceBones = renderer.bones;
                int rendererMapped = 0;

                for (int b = 0;
                     sourceBones != null && b < sourceBones.Length;
                     b++)
                {
                    Transform sourceBone = sourceBones[b];
                    Transform liveBone =
                        sourceBone != null
                            ? SR_FindTargetBoneForSkinnedSource(
                                sourceBone,
                                searchRoot != null ? searchRoot : actorRoot,
                                visual.transform,
                                exact,
                                normalized)
                            : null;

                    if (liveBone != null)
                    {
                        follower.AddMapping(sourceBone, liveBone);
                        mappedBoneCount++;
                        rendererMapped++;
                    }
                    else
                    {
                        missingBoneCount++;

                        if (sourceBone != null)
                        {
                            missingNames.Add(sourceBone.name);
                        }
                    }
                }

                // IMPORTANT:
                // Do NOT assign renderer.bones to the live skeleton and do NOT
                // rebuild bindposes from the current animation pose.
                //
                // The previous live-bindpose pass made the current pose the bind
                // pose, which cancels skin deformation and leaves the glove at
                // its authored T-pose hand coordinates (the floating gloves seen
                // beside the character).
                //
                // The renderer now keeps its original FBX bones + bindposes.
                // FOTFSkinnedBoneFollower moves those imported bones onto the
                // live Erenshor bones every LateUpdate instead.
                renderer.updateWhenOffscreen = true;
                renderer.enabled = true;

                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] skinned source rig retained [" +
                    context +
                    "] Renderer=[" +
                    renderer.name +
                    "] DrivenBones=[" +
                    rendererMapped +
                    "/" +
                    (sourceBones != null ? sourceBones.Length : 0) +
                    "].");
            }

            if (missingBoneCount == 0)
            {
                follower.SyncNow();
                follower.enabled = true;

                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] Grasp source rig animation bridge active [" +
                    context +
                    "] Mode=[PreserveBindPositionScale_CopyLocalRotation].");
            }
            else
            {
                follower.enabled = false;
            }

            if (missingNames.Count > 0)
            {
                Plugin.NativeLog.LogWarning(
                    "[FateEquipmentVisuals] skinned bone remap [" +
                    context +
                    "] missing [" +
                    string.Join(", ", new List<string>(missingNames).ToArray()) +
                    "]. The FBX bone names must match the Synty runtime skeleton.");
            }

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] skinned body remap [" +
                context +
                "] Renderers=[" +
                rendererCount +
                "] MappedBones=[" +
                mappedBoneCount +
                "] MissingBones=[" +
                missingBoneCount +
                "].");

            // A partially remapped skinned glove is not usable. Missing even a
            // single weighted finger bone can collapse geometry across the body
            // or leave one hand invisible. Fail the bind instead of suppressing
            // the native hands and showing corrupted geometry.
            return
                rendererCount > 0 &&
                mappedBoneCount > 0 &&
                missingBoneCount == 0;
        }

        private static Transform SR_FindCharacterSkeletonRoot(
            Transform actorRoot,
            Transform visualRoot)
        {
            if (actorRoot == null)
            {
                return null;
            }

            Animator[] animators =
                actorRoot.GetComponentsInChildren<Animator>(true);

            Animator best = null;
            int bestScore = -1;

            for (int i = 0;
                 animators != null && i < animators.Length;
                 i++)
            {
                Animator animator = animators[i];
                if (animator == null ||
                    SR_IsInsideVisual(animator.transform, visualRoot))
                {
                    continue;
                }

                int score = 0;
                try
                {
                    if (animator.isHuman)
                    {
                        score += 100;
                    }
                }
                catch
                {
                }

                Transform[] all =
                    animator.GetComponentsInChildren<Transform>(true);

                for (int t = 0;
                     all != null && t < all.Length;
                     t++)
                {
                    Transform candidate = all[t];
                    if (candidate == null || SR_IsInsideVisual(candidate, visualRoot))
                    {
                        continue;
                    }

                    string key = SR_NormalizeBoneName(candidate.name);
                    if (key == "hips" || key == "spine01" || key == "spine02" ||
                        key == "spine03" || key == "handl" || key == "handr")
                    {
                        score += 5;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = animator;
                }
            }

            return best != null
                ? best.transform
                : actorRoot;
        }

        private static bool SR_IsInsideVisual(
            Transform candidate,
            Transform visualRoot)
        {
            if (candidate == null || visualRoot == null)
            {
                return false;
            }

            return candidate == visualRoot || candidate.IsChildOf(visualRoot);
        }

        private static Transform SR_FindTargetBoneForSkinnedSource(
            Transform sourceBone,
            Transform liveSkeletonRoot,
            Transform visualRoot,
            Dictionary<string, Transform> exact,
            Dictionary<string, Transform> normalized)
        {
            if (sourceBone == null)
            {
                return null;
            }

            // Blender cannot keep duplicate bone names inside one armature.
            // The native Synty skeleton uses the same digit names beneath both
            // Hand_L and Hand_R, so Blender renames the second branch to names
            // such as Thumb_01.001. Unity's live skeleton still has Thumb_01
            // under each hand. We must therefore resolve finger bones by BOTH
            // their clean name and which hand branch owns them.
            string sideHandName = SR_FindSourceHandAncestorName(sourceBone);

            if (!string.IsNullOrEmpty(sideHandName) &&
                liveSkeletonRoot != null)
            {
                Transform liveHand =
                    SR_FindTargetBone(
                        sideHandName,
                        exact,
                        normalized);

                if (liveHand != null)
                {
                    string cleanSourceName =
                        SR_StripBlenderNumericSuffix(sourceBone.name);

                    Transform scoped =
                        SR_FindDescendantBoneByName(
                            liveHand,
                            cleanSourceName);

                    if (scoped != null)
                    {
                        return scoped;
                    }

                    // Some FBX importers add namespace/prefix text in addition
                    // to Blender's numeric suffix. Try the normalized clean key
                    // while remaining strictly inside the correct hand branch.
                    string cleanKey =
                        SR_NormalizeBoneName(cleanSourceName);

                    scoped =
                        SR_FindDescendantBoneByNormalizedName(
                            liveHand,
                            cleanKey);

                    if (scoped != null)
                    {
                        return scoped;
                    }
                }
            }

            // Non-hand bones (Root/Hips/Spine/etc.) are globally unique and can
            // use the normal mapping path.
            return
                SR_FindTargetBone(
                    SR_StripBlenderNumericSuffix(sourceBone.name),
                    exact,
                    normalized);
        }

        private static string SR_FindSourceHandAncestorName(
            Transform sourceBone)
        {
            Transform current = sourceBone;

            while (current != null)
            {
                string cleanName =
                    SR_StripBlenderNumericSuffix(current.name);

                string key =
                    SR_NormalizeBoneName(cleanName);

                if (key == "handl")
                {
                    return "Hand_L";
                }

                if (key == "handr")
                {
                    return "Hand_R";
                }

                current = current.parent;
            }

            return null;
        }

        private static Transform SR_FindDescendantBoneByName(
            Transform root,
            string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; all != null && i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                string cleanCandidate =
                    SR_StripBlenderNumericSuffix(candidate.name);

                if (string.Equals(
                    cleanCandidate,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform SR_FindDescendantBoneByNormalizedName(
            Transform root,
            string targetKey)
        {
            if (root == null || string.IsNullOrEmpty(targetKey))
            {
                return null;
            }

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; all != null && i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                string cleanCandidate =
                    SR_StripBlenderNumericSuffix(candidate.name);

                string candidateKey =
                    SR_NormalizeBoneName(cleanCandidate);

                if (candidateKey == targetKey)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string SR_StripBlenderNumericSuffix(
            string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 5)
            {
                return value;
            }

            int dot = value.LastIndexOf('.');
            if (dot < 0 || dot + 4 != value.Length)
            {
                return value;
            }

            if (char.IsDigit(value[dot + 1]) &&
                char.IsDigit(value[dot + 2]) &&
                char.IsDigit(value[dot + 3]))
            {
                return value.Substring(0, dot);
            }

            return value;
        }

        private static Transform SR_FindTargetBone(
            string sourceName,
            Dictionary<string, Transform> exact,
            Dictionary<string, Transform> normalized)
        {
            if (string.IsNullOrEmpty(sourceName))
            {
                return null;
            }

            Transform target;
            if (exact != null && exact.TryGetValue(sourceName, out target))
            {
                return target;
            }

            string key = SR_NormalizeBoneName(sourceName);
            if (!string.IsNullOrEmpty(key) &&
                normalized != null &&
                normalized.TryGetValue(key, out target))
            {
                return target;
            }

            // Blender/FBX exporters can prefix names with an armature or
            // namespace. After normalization, allow a suffix match for the
            // distinctive Synty bone token as a final fallback.
            if (!string.IsNullOrEmpty(key) && normalized != null && key.Length >= 4)
            {
                foreach (KeyValuePair<string, Transform> pair in normalized)
                {
                    if (pair.Key.EndsWith(key, StringComparison.OrdinalIgnoreCase) ||
                        key.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
            }

            return null;
        }

        private static string SR_NormalizeBoneName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int cut = Math.Max(
                value.LastIndexOf(':'),
                Math.Max(value.LastIndexOf('|'), value.LastIndexOf('/')));

            if (cut >= 0 && cut + 1 < value.Length)
            {
                value = value.Substring(cut + 1);
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
    }

    /// <summary>
    /// Drives the imported Grasp FBX armature from Erenshor's live Synty
    /// skeleton while leaving the SkinnedMeshRenderer's original bone array and
    /// bindposes intact.
    ///
    /// This avoids both failure modes we hit previously:
    ///  - swapping in live bones while retaining Blender bindposes -> spikes
    ///  - rebuilding bindposes from the CURRENT animated pose -> floating T-pose
    /// </summary>
    internal sealed class FOTFSkinnedBoneFollower : MonoBehaviour
    {
        private sealed class BonePair
        {
            internal Transform Source;
            internal Transform Target;
            internal int Depth;

            // Preserve the imported FBX bind hierarchy exactly. The Grasp mesh
            // bindposes were authored against these source-bone translations
            // and scales, so runtime animation must never overwrite them.
            internal Vector3 SourceBindLocalPosition;
            internal Vector3 SourceBindLocalScale;
        }

        private readonly List<BonePair> _pairs =
            new List<BonePair>();

        internal void ClearMappings()
        {
            _pairs.Clear();
        }

        internal void AddMapping(
            Transform source,
            Transform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            for (int i = 0; i < _pairs.Count; i++)
            {
                if (_pairs[i].Source == source)
                {
                    _pairs[i].Target = target;
                    return;
                }
            }

            _pairs.Add(
                new BonePair
                {
                    Source = source,
                    Target = target,
                    Depth = GetDepth(source),
                    SourceBindLocalPosition = source.localPosition,
                    SourceBindLocalScale = source.localScale
                });

            _pairs.Sort(
                delegate (BonePair a, BonePair b)
                {
                    return a.Depth.CompareTo(b.Depth);
                });
        }

        internal void SyncNow()
        {
            // The imported V16.1 Grasp skeleton and mesh are a matched skinned
            // FBX pair. Its bindposes are valid ONLY while the imported source
            // skeleton keeps its original local translations and scales.
            //
            // Previous passes copied live WORLD position/rotation/scale onto
            // every source bone. That changes the imported skeleton's bone
            // lengths / scale basis relative to its bindposes and is why one
            // glove remained displaced while the other collapsed into a curved
            // black shape.
            //
            // Both rigs originate from the same Synty skeleton, so the correct
            // animation bridge is:
            //   - keep source bind localPosition
            //   - keep source bind localScale
            //   - copy the live bone's LOCAL ROTATION only
            //
            // Parent rotations then naturally move the imported hand/finger
            // bones to the same animated locations as the character without
            // destroying the skin's authored bind geometry.
            for (int i = 0; i < _pairs.Count; i++)
            {
                BonePair pair = _pairs[i];

                if (pair == null ||
                    pair.Source == null ||
                    pair.Target == null)
                {
                    continue;
                }

                Quaternion targetRotation =
                    pair.Target.localRotation;

                // Bind position and scale are constant. Writing them on every
                // LateUpdate dirties the entire imported glove hierarchy.
                // Only copy animation rotation, and only when it changed.
                if (Mathf.Abs(
                        Quaternion.Dot(
                            pair.Source.localRotation,
                            targetRotation)) <
                    0.9999995f)
                {
                    pair.Source.localRotation =
                        targetRotation;
                }
            }
        }

        private void LateUpdate()
        {
            SyncNow();
        }

        private static int GetDepth(Transform value)
        {
            int depth = 0;
            Transform current = value;

            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }


    }
}
