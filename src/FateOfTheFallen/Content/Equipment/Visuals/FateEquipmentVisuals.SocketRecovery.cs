using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed partial class FateEquipmentVisuals
    {
        // Erenshor sometimes leaves WeaponL/WeaponR inactive while restoring a
        // character or a SimPlayer. Parenting an item to that disabled holder
        // makes the whole custom mesh invisible, regardless of activeSelf.
        // A temporary visible proxy mirrors the *real* holder's world pose and
        // inherited scale (uniform rigs are exact); the item's calibrated local transform
        // remains unchanged. No gameplay equipment state is altered.
        private static Transform VR_GetHandParent(
            Transform socket, Transform actorRoot, ref Transform proxy, string label)
        {
            if (socket == null || actorRoot == null ||
                !actorRoot.gameObject.activeInHierarchy) return null;
            if (socket.gameObject.activeInHierarchy) return socket;

            Transform activeAncestor = socket.parent;
            while (activeAncestor != null && !activeAncestor.gameObject.activeInHierarchy)
                activeAncestor = activeAncestor.parent;

            // Do not create an apparently valid visual outside a disabled
            // character. Retry after native character initialization instead.
            if (activeAncestor == null || !activeAncestor.gameObject.activeInHierarchy)
                return null;

            if (proxy == null)
            {
                GameObject go = new GameObject("FOTF_CustomItemVisual_SocketProxy_" + label);
                proxy = go.transform;
                Plugin.NativeLog.LogInfo(
                    "[FateEquipmentVisuals] temporary active hand-socket proxy created [" +
                    label + "] while native weapon holder is inactive.");
            }
            if (proxy.parent != activeAncestor)
                proxy.SetParent(activeAncestor, false);
            VR_FollowHandSocket(proxy, socket);
            return proxy;
        }

        private static void VR_FollowHandSocket(Transform proxy, Transform socket)
        {
            if (proxy == null || socket == null || proxy.parent == null) return;
            proxy.position = socket.position;
            proxy.rotation = socket.rotation;
            // The temporary proxy occupies the actual holder's world pose,
            // not its parent's origin. Preserve relative import scale as well.
            Vector3 desired = socket.lossyScale;
            Vector3 actualParent = proxy.parent.lossyScale;
            proxy.localScale = new Vector3(
                VR_SafeScaleRatio(desired.x, actualParent.x),
                VR_SafeScaleRatio(desired.y, actualParent.y),
                VR_SafeScaleRatio(desired.z, actualParent.z));
        }

        private static float VR_SafeScaleRatio(float wanted, float inherited)
        {
            return Mathf.Abs(inherited) > 0.00001f ? wanted / inherited : 1f;
        }

        private static void VR_DestroyProxy(ref Transform proxy)
        {
            if (proxy == null) return;
            UnityEngine.Object.Destroy(proxy.gameObject);
            proxy = null;
        }
    }
}
