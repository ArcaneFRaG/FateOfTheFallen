using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed partial class FateEquipmentVisuals : MonoBehaviour
    {
        private sealed class MB_Binding
        {
            internal string PartName;
            internal string BoneName;
            internal string ScopeBoneName;
            internal Vector3 LocalEulerOffset;

            internal MB_Binding(
                string partName,
                string boneName,
                string scopeBoneName = null,
                Vector3? localEulerOffset = null)
            {
                PartName = partName;
                BoneName = boneName;
                ScopeBoneName = scopeBoneName;
                LocalEulerOffset = localEulerOffset ?? Vector3.zero;
            }
        }

        private static readonly MB_Binding[] MB_CarapaceBindings =
        {
            new MB_Binding("FOTF_Carapace_Torso", "Spine_03"),
            new MB_Binding(
                "FOTF_Carapace_Pauldron_L",
                "Shoulder_L",
                null,
                new Vector3(0f, 90f, 0f)),
            new MB_Binding(
                "FOTF_Carapace_Pauldron_R",
                "Shoulder_R",
                null,
                new Vector3(0f, 90f, 0f))
        };

        private static bool MB_UsesModularRig(string itemId, Item.SlotType slot)
        {
            // No current FOTF body item uses the rigid modular path.
            //
            // Carapace now reuses Erenshor's native Eventide Chestguard visual
            // and applies a runtime palette override. Grasp uses the skinned
            // native-hand path.
            return false;
        }

        private static bool MB_BindVisual(
            Transform actorRoot,
            GameObject visual,
            string itemId,
            Item.SlotType slot,
            string context)
        {
            if (actorRoot == null || visual == null)
            {
                return false;
            }

            // HARD CONTRACT: Grasp is a native skinned asset. It must never use
            // the rigid modular binder. Keeping this guard here prevents a future
            // caller from accidentally reintroducing the obsolete 24-piece path.
            if (slot == Item.SlotType.Hand &&
                string.Equals(
                    itemId,
                    NhalSeraEquipment.GraspId,
                    StringComparison.OrdinalIgnoreCase))
            {
                Plugin.NativeLog.LogError(
                    "[FateEquipmentVisuals] Grasp was incorrectly sent to the modular binder. " +
                    "This build expects the V16.1 skinned Grasp prefab.");
                return false;
            }

            MB_Binding[] bindings = null;
            if (slot == Item.SlotType.Chest &&
                string.Equals(itemId, NhalSeraEquipment.CarapaceId, StringComparison.OrdinalIgnoreCase))
            {
                bindings = MB_CarapaceBindings;
            }
            if (bindings == null || bindings.Length == 0)
            {
                return false;
            }

            // Do not force the prefab root scale to Vector3.one here.
            //
            // The Blender/FBX model may carry its unit conversion on the prefab
            // root (for example 0.01 for centimetres -> metres).  The modular
            // mesh vertices are already authored in target-bone local space, so
            // position/rotation are discarded when each part is attached, but
            // the imported effective scale must be preserved.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // Re-use the safe one-time preparation: activate hierarchy, disable
            // prefab animation/physics and enable renderers. No mesh mutation.
            SR_PrepareSkinnedVisual(visual);

            Transform skeletonRoot = SR_FindCharacterSkeletonRoot(actorRoot, visual.transform);
            if (skeletonRoot == null)
            {
                skeletonRoot = actorRoot;
            }

            FOTFModularOwnedParts owner = visual.GetComponent<FOTFModularOwnedParts>();
            if (owner == null)
            {
                owner = visual.AddComponent<FOTFModularOwnedParts>();
            }

            int bound = 0;
            List<string> missing = new List<string>();

            for (int i = 0; i < bindings.Length; i++)
            {
                MB_Binding binding = bindings[i];
                Transform part = MB_FindExactDescendant(visual.transform, binding.PartName);
                if (part == null)
                {
                    missing.Add("part:" + binding.PartName);
                    continue;
                }

                Transform bone = MB_ResolveBindingBone(
                    skeletonRoot,
                    binding,
                    itemId,
                    slot);

                if (bone == null)
                {
                    missing.Add("bone:" + binding.PartName + "->" + binding.BoneName);
                    continue;
                }

                if (!MB_HasRenderableMesh(part))
                {
                    missing.Add("mesh:" + binding.PartName);
                    continue;
                }

                owner.Add(part.gameObject);

                // Capture the model's EFFECTIVE imported scale before removing
                // the part from the prefab hierarchy. Unity may place FBX unit
                // conversion on the prefab root or another parent rather than
                // directly on this mesh transform.
                Vector3 importedWorldScale = part.lossyScale;
                Vector3 boneWorldScale = bone.lossyScale;
                Vector3 preservedLocalScale =
                    MB_GetScaleRelativeToBone(importedWorldScale, boneWorldScale);

                // Remember the centre of the rendered mesh in PART-LOCAL space.
                // This is essential for the pauldrons: applying a 90 degree
                // local rotation around the shoulder-bone origin otherwise
                // makes the entire pad orbit away from the shoulder.
                Vector3 localMeshCenter = MB_GetRenderableLocalCenter(part);

                part.SetParent(bone, false);
                part.localScale = preservedLocalScale;

                Quaternion appliedRotation =
                    Quaternion.Euler(binding.LocalEulerOffset);

                part.localRotation = appliedRotation;

                // Keep the visible mesh centre exactly where it would have been
                // with identity rotation, but rotate the armour about its own
                // centre. This fixes the correctly-rotated-but-displaced
                // Carapace pauldrons.
                Vector3 scaledCenter =
                    Vector3.Scale(localMeshCenter, preservedLocalScale);

                part.localPosition =
                    scaledCenter - (appliedRotation * scaledCenter);

                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] modular part bound [" + context +
                    "] Part=[" + binding.PartName +
                    "] Bone=[" + bone.name +
                    "] ImportedWorld=[" + MB_FormatVector(importedWorldScale) +
                    "] BoneWorld=[" + MB_FormatVector(boneWorldScale) +
                    "] AppliedLocalScale=[" + MB_FormatVector(preservedLocalScale) +
                    "] LocalEuler=[" + MB_FormatVector(binding.LocalEulerOffset) +
                    "] LocalPosition=[" + MB_FormatVector(part.localPosition) +
                    "] MeshCenter=[" + MB_FormatVector(localMeshCenter) +
                    "].");

                MB_SetLayerRecursive(part, bone.gameObject.layer);
                PB_DisablePhysics(part.gameObject);
                PB_RepairUnsupportedShaders(part.gameObject);
                PB_EnsureCustomVisible(part.gameObject);
                bound++;
            }

            // Anything left under the prefab root is a guide/wrapper and must
            // not render. Bound parts have already left this hierarchy.
            Renderer[] leftovers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; leftovers != null && i < leftovers.Length; i++)
            {
                Renderer renderer = leftovers[i];
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            if (missing.Count > 0 || bound != bindings.Length)
            {
                Plugin.NativeLog.LogError(
                    "[FateEquipmentVisuals] modular body bind failed [" + context + "] Bound=[" +
                    bound + "/" + bindings.Length + "] Missing=[" +
                    string.Join(", ", missing.ToArray()) + "].");
                return false;
            }

            Plugin.NativeLog.LogInfo(
                "[FateEquipmentVisuals] modular body bound [" + context + "] Parts=[" +
                bound + "] using native Synty bone parenting.");

            visual.SetActive(true);
            return true;
        }

        private static Transform MB_ResolveBindingBone(
            Transform skeletonRoot,
            MB_Binding binding,
            string itemId,
            Item.SlotType slot)
        {
            if (skeletonRoot == null || binding == null)
            {
                return null;
            }

            // Grasp is generated from the actual Synty hand surface, but the
            // precise digit bone names are discovered dynamically in Blender.
            // Do the same in game instead of assuming generic names such as
            // "Finger_01", which can resolve to the wrong chain or no chain at
            // all on the live Erenshor skeleton.
            if (slot == Item.SlotType.Hand &&
                string.Equals(
                    itemId,
                    NhalSeraEquipment.GraspId,
                    StringComparison.OrdinalIgnoreCase))
            {
                Transform graspBone =
                    MB_ResolveGraspBone(skeletonRoot, binding.PartName);

                if (graspBone != null)
                {
                    return graspBone;
                }
            }

            Transform scope = skeletonRoot;
            if (!string.IsNullOrEmpty(binding.ScopeBoneName))
            {
                scope = MB_FindBone(skeletonRoot, binding.ScopeBoneName);
                if (scope == null)
                {
                    return null;
                }
            }

            return MB_FindBone(scope, binding.BoneName);
        }

        private static Transform MB_ResolveGraspBone(
            Transform skeletonRoot,
            string partName)
        {
            if (skeletonRoot == null || string.IsNullOrEmpty(partName))
            {
                return null;
            }

            bool left = partName.IndexOf("_L", StringComparison.OrdinalIgnoreCase) >= 0;
            bool right = partName.IndexOf("_R", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!left && !right)
            {
                return null;
            }

            string handName = left ? "Hand_L" : "Hand_R";
            Transform hand = MB_FindBone(skeletonRoot, handName);
            if (hand == null)
            {
                return null;
            }

            if (partName.IndexOf("_Hand_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return hand;
            }

            string family = null;
            string requiredToken = null;
            bool excludeIndex = false;
            bool excludeThumb = false;

            if (partName.IndexOf("_Thumb_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                family = "Thumb";
                requiredToken = "thumb";
            }
            else if (partName.IndexOf("_Index_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                family = "Index";
                requiredToken = "indexfinger";
            }
            else if (partName.IndexOf("_Finger_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                family = "Finger";
                requiredToken = "finger";
                excludeIndex = true;
                excludeThumb = true;
            }

            if (requiredToken == null)
            {
                return null;
            }

            int segment = MB_ParseTrailingSegment(partName);
            if (segment <= 0)
            {
                return null;
            }

            Transform root = MB_FindDigitRoot(
                hand,
                requiredToken,
                excludeIndex,
                excludeThumb);

            if (root == null)
            {
                Plugin.NativeLog.LogError(
                    "[FateEquipmentVisuals] could not resolve " + family +
                    " root under [" + hand.name + "] for modular Grasp part [" +
                    partName + "].");
                return null;
            }

            Transform result = MB_FollowDigitChain(root, segment - 1, requiredToken);

            if (result != null)
            {
                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] Grasp bone resolved Part=[" +
                    partName + "] -> [" + result.name + "].");
            }

            return result;
        }

        private static int MB_ParseTrailingSegment(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int end = value.Length - 1;
            while (end >= 0 && char.IsDigit(value[end]))
            {
                end--;
            }

            if (end == value.Length - 1)
            {
                return 0;
            }

            int parsed;
            return int.TryParse(value.Substring(end + 1), out parsed) ? parsed : 0;
        }

        private static Transform MB_FindDigitRoot(
            Transform hand,
            string requiredToken,
            bool excludeIndex,
            bool excludeThumb)
        {
            if (hand == null)
            {
                return null;
            }

            Queue<KeyValuePair<Transform, int>> queue =
                new Queue<KeyValuePair<Transform, int>>();

            for (int i = 0; i < hand.childCount; i++)
            {
                Transform child = hand.GetChild(i);
                if (MB_IsCleanDigitBone(child))
                {
                    queue.Enqueue(
                        new KeyValuePair<Transform, int>(child, 1));
                }
            }

            while (queue.Count > 0)
            {
                KeyValuePair<Transform, int> entry = queue.Dequeue();
                Transform candidate = entry.Key;
                int depth = entry.Value;

                if (candidate == null || depth > 2)
                {
                    continue;
                }

                string key = SR_NormalizeBoneName(candidate.name);

                bool matches =
                    !string.IsNullOrEmpty(key) &&
                    key.IndexOf(requiredToken, StringComparison.OrdinalIgnoreCase) >= 0;

                if (excludeIndex &&
                    key.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches = false;
                }

                if (excludeThumb &&
                    key.IndexOf("thumb", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches = false;
                }

                if (matches)
                {
                    return candidate;
                }

                if (depth < 2)
                {
                    for (int i = 0; i < candidate.childCount; i++)
                    {
                        Transform child = candidate.GetChild(i);
                        if (MB_IsCleanDigitBone(child))
                        {
                            queue.Enqueue(
                                new KeyValuePair<Transform, int>(
                                    child,
                                    depth + 1));
                        }
                    }
                }
            }

            return null;
        }

        private static Transform MB_FollowDigitChain(
            Transform root,
            int steps,
            string familyToken)
        {
            Transform current = root;

            for (int step = 0; step < steps; step++)
            {
                if (current == null)
                {
                    return null;
                }

                Transform preferred = null;
                Transform fallback = null;

                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (!MB_IsCleanDigitBone(child))
                    {
                        continue;
                    }

                    if (fallback == null)
                    {
                        fallback = child;
                    }

                    string key = SR_NormalizeBoneName(child.name);
                    if (!string.IsNullOrEmpty(key) &&
                        key.IndexOf(
                            familyToken,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        preferred = child;
                        break;
                    }
                }

                current = preferred ?? fallback;
            }

            return current;
        }

        private static bool MB_IsCleanDigitBone(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            string key = SR_NormalizeBoneName(candidate.name);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            string[] ignored =
            {
                "ik", "weapon", "prop", "socket", "helper",
                "twist", "end", "nub", "attach"
            };

            for (int i = 0; i < ignored.Length; i++)
            {
                if (key.IndexOf(
                        ignored[i],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3 MB_GetRenderableLocalCenter(Transform part)
        {
            if (part == null)
            {
                return Vector3.zero;
            }

            MeshFilter[] filters =
                part.GetComponentsInChildren<MeshFilter>(true);

            if (filters != null)
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (filter == null || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    Vector3 worldCenter =
                        filter.transform.TransformPoint(
                            filter.sharedMesh.bounds.center);

                    return part.InverseTransformPoint(worldCenter);
                }
            }

            SkinnedMeshRenderer[] skinned =
                part.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (skinned != null)
            {
                for (int i = 0; i < skinned.Length; i++)
                {
                    SkinnedMeshRenderer renderer = skinned[i];
                    if (renderer == null || renderer.sharedMesh == null)
                    {
                        continue;
                    }

                    Vector3 worldCenter =
                        renderer.transform.TransformPoint(
                            renderer.sharedMesh.bounds.center);

                    return part.InverseTransformPoint(worldCenter);
                }
            }

            return Vector3.zero;
        }

        private static Vector3 MB_GetScaleRelativeToBone(
            Vector3 importedWorldScale,
            Vector3 boneWorldScale)
        {
            return new Vector3(
                MB_SafeScaleRatio(importedWorldScale.x, boneWorldScale.x),
                MB_SafeScaleRatio(importedWorldScale.y, boneWorldScale.y),
                MB_SafeScaleRatio(importedWorldScale.z, boneWorldScale.z));
        }

        private static float MB_SafeScaleRatio(float imported, float target)
        {
            float importedMagnitude = Mathf.Abs(imported);
            float targetMagnitude = Mathf.Abs(target);

            if (float.IsNaN(importedMagnitude) ||
                float.IsInfinity(importedMagnitude) ||
                importedMagnitude < 0.000001f)
            {
                return 1f;
            }

            if (float.IsNaN(targetMagnitude) ||
                float.IsInfinity(targetMagnitude) ||
                targetMagnitude < 0.000001f)
            {
                return importedMagnitude;
            }

            float ratio = importedMagnitude / targetMagnitude;

            if (float.IsNaN(ratio) ||
                float.IsInfinity(ratio) ||
                ratio < 0.000001f)
            {
                return 1f;
            }

            return ratio;
        }

        private static string MB_FormatVector(Vector3 value)
        {
            return
                "(" +
                value.x.ToString("0.######") + ", " +
                value.y.ToString("0.######") + ", " +
                value.z.ToString("0.######") +
                ")";
        }

        private static Transform MB_FindExactDescendant(Transform root, string exactName)
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

        private static Transform MB_FindBone(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            string targetKey = SR_NormalizeBoneName(targetName);
            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            // Pass 1: exact display-name or exact normalized-name match only.
            for (int i = 0; all != null && i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                string candidateKey = SR_NormalizeBoneName(candidate.name);
                if (string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase) ||
                    candidateKey == targetKey)
                {
                    return candidate;
                }
            }

            // Pass 2: permissive fallback for export prefixes/suffixes only when
            // no exact candidate exists. This prevents Finger_01 from resolving
            // to IndexFinger_01 simply because the latter ends with "finger01".
            for (int i = 0; all != null && i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                string candidateKey = SR_NormalizeBoneName(candidate.name);
                if (!string.IsNullOrEmpty(candidateKey) &&
                    !string.IsNullOrEmpty(targetKey) &&
                    (candidateKey.StartsWith(targetKey, StringComparison.OrdinalIgnoreCase) ||
                     candidateKey.EndsWith(targetKey, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool MB_HasRenderableMesh(Transform part)
        {
            if (part == null)
            {
                return false;
            }

            MeshFilter[] filters = part.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; filters != null && i < filters.Length; i++)
            {
                if (filters[i] != null && filters[i].sharedMesh != null && filters[i].sharedMesh.vertexCount > 0)
                {
                    return true;
                }
            }

            SkinnedMeshRenderer[] skinned = part.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; skinned != null && i < skinned.Length; i++)
            {
                if (skinned[i] != null && skinned[i].sharedMesh != null && skinned[i].sharedMesh.vertexCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MB_SetLayerRecursive(Transform root, int layer)
        {
            if (root == null)
            {
                return;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    all[i].gameObject.layer = layer;
                }
            }
        }
    }
    internal sealed class FOTFModularOwnedParts : MonoBehaviour
    {
        internal readonly List<GameObject> Parts = new List<GameObject>();

        internal void Add(GameObject part)
        {
            if (part != null && !Parts.Contains(part))
            {
                Parts.Add(part);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < Parts.Count; i++)
            {
                GameObject part = Parts[i];
                if (part != null)
                {
                    UnityEngine.Object.Destroy(part);
                }
            }
            Parts.Clear();
        }
    }

}
