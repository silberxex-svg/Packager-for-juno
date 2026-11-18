using ModApi.Craft.Parts;
using ModApi.Craft.Parts.Attributes;
using UnityEngine;
using Assets.Scripts;
using Assets.Scripts.Craft.Parts;
using Assets.Scripts.Craft.Parts.Modifiers.Eva;
using Assets.Scripts.Craft.Parts.Modifiers.Fuselage;

[System.Serializable]
[DesignerPartModifier("Grapple")]
[PartModifierTypeId("SilverExe.Grapple")]
public class GrappleData : PartModifierData<GrappleModifier>
{
    [PartModifierProperty(true, false)]
    public float MaxDistance = 750f;
}

public class GrappleModifier : PartModifierScript<GrappleData>
{
    private GrapplingHookScript? _grapplingHook;
    private bool _hierarchyLogged;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Debug.Log("[StickyPads] Grapple initialized. Range: " + Data.MaxDistance);

        if (!_hierarchyLogged)
        {
            _hierarchyLogged = true;
            LogPartHierarchy();
        }
    }

    private void LogActivationTransforms()
    {
        Transform t = base.transform;

        Debug.Log("[StickyPads] ===== ACTIVATION TRANSFORMS =====");
        Debug.Log("[StickyPads] Root: " + t.name);
        Debug.Log("[StickyPads] World Position: " + t.position);
        Debug.Log("[StickyPads] World Rotation: " + t.rotation.eulerAngles);
        Debug.Log("[StickyPads] World Forward: " + t.forward);
        Debug.Log("[StickyPads] World Right: " + t.right);
        Debug.Log("[StickyPads] World Up: " + t.up);

        Debug.Log("[StickyPads] AttachPointScripts count: " +
            (PartScript.AttachPointScripts == null
                ? "null"
                : PartScript.AttachPointScripts.Count.ToString()));

        if (PartScript.AttachPointScripts != null)
        {
            foreach (AttachPointScript script in PartScript.AttachPointScripts)
            {
                if (script == null)
                    continue;

                AttachPoint point = script.AttachPoint;

                Debug.Log(
                    "[StickyPads] ACTIVATION ATTACHPOINT: " +
                    script.name +
                    " | Local Position: " + script.transform.localPosition +
                    " | World Position: " + script.transform.position +
                    " | Point Position: " + point.Position +
                    " | Position Offset: " + point.PositionOffset
                );
            }
        }

        Debug.Log("[StickyPads] ===== PARENT CHAIN =====");

        Transform current = t;
        int depth = 0;

        while (current != null)
        {
            Debug.Log(
                "[StickyPads] PARENT LEVEL " + depth +
                " | Name: " + current.name +
                " | Local Position: " + current.localPosition +
                " | World Position: " + current.position +
                " | Local Rotation: " + current.localRotation.eulerAngles +
                " | World Rotation: " + current.rotation.eulerAngles +
                " | Children: " + current.childCount
            );

            if (depth == 1)
            {
                Debug.Log("[StickyPads] ===== IMMEDIATE PARENT CHILDREN =====");

                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);

                    Debug.Log(
                        "[StickyPads] PARENT CHILD: " + child.name +
                        " | Local Position: " + child.localPosition +
                        " | World Position: " + child.position +
                        " | Local Rotation: " + child.localRotation.eulerAngles +
                        " | World Rotation: " + child.rotation.eulerAngles +
                        " | Children: " + child.childCount
                    );

                    if (child.name.StartsWith("Strut-"))
                    {
                        LogStrutColliderBounds(child);
                    }

                    LogTransformChildren(child, 2);
                }

                Debug.Log("[StickyPads] ===== IMMEDIATE PARENT CHILDREN END =====");
            }

            current = current.parent;
            depth++;
        }

        Debug.Log("[StickyPads] ===== PARENT CHAIN END =====");
        Debug.Log("[StickyPads] ===== ACTIVATION TRANSFORMS END =====");
    }

    private void LogStrutColliderBounds(Transform strut)
    {
        Debug.Log("[StickyPads] ===== STRUT MEASUREMENTS: " + strut.name + " =====");
        Debug.Log("[StickyPads] Strut Position: " + strut.position);
        Debug.Log("[StickyPads] Strut Forward: " + strut.forward);
        Debug.Log("[StickyPads] Strut Right: " + strut.right);
        Debug.Log("[StickyPads] Strut Up: " + strut.up);
        Debug.Log("[StickyPads] Strut Lossy Scale: " + strut.lossyScale);

        Collider[] colliders = strut.GetComponentsInChildren<Collider>(true);

        Debug.Log("[StickyPads] Strut Collider Count: " + colliders.Length);

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            Bounds bounds = collider.bounds;

            Debug.Log(
                "[StickyPads] STRUT COLLIDER: " + collider.name +
                " | Type: " + collider.GetType().Name +
                " | Object: " + collider.transform.name +
                " | World Center: " + bounds.center +
                " | World Extents: " + bounds.extents +
                " | World Min: " + bounds.min +
                " | World Max: " + bounds.max +
                " | Local Position: " + collider.transform.localPosition +
                " | Lossy Scale: " + collider.transform.lossyScale
            );

            BoxCollider box = collider as BoxCollider;

            if (box != null)
            {
                Debug.Log(
                    "[StickyPads] STRUT BOX: " + box.name +
                    " | Local Center: " + box.center +
                    " | Local Size: " + box.size
                );
            }

            MeshCollider mesh = collider as MeshCollider;

            if (mesh != null && mesh.sharedMesh != null)
            {
                Bounds meshBounds = mesh.sharedMesh.bounds;

                Debug.Log(
                    "[StickyPads] STRUT MESH: " + mesh.name +
                    " | Mesh Bounds Center: " + meshBounds.center +
                    " | Mesh Bounds Extents: " + meshBounds.extents +
                    " | Mesh Bounds Size: " + meshBounds.size
                );
            }
        }

        Debug.Log("[StickyPads] ===== STRUT MEASUREMENTS END =====");
    }

    private void LogTransformChildren(Transform parent, int depth)
    {
        if (depth > 5)
            return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            Debug.Log(
                "[StickyPads] " + new string(' ', depth * 2) +
                "CHILD: " + child.name +
                " | Local Position: " + child.localPosition +
                " | World Position: " + child.position +
                " | Local Rotation: " + child.localRotation.eulerAngles +
                " | World Rotation: " + child.rotation.eulerAngles +
                " | Children: " + child.childCount
            );

            LogTransformChildren(child, depth + 1);
        }
    }

    private void LogPartHierarchy()
    {
        Debug.Log("[StickyPads] ===== GRAPPLE HIERARCHY START =====");

        Transform root = PartScript.Transform;
        LogTransform(root, 0);

        Debug.Log("[StickyPads] ===== ATTACHMENT POINTS =====");

        if (PartScript.AttachPointScripts != null)
        {
            foreach (AttachPointScript script in PartScript.AttachPointScripts)
            {
                if (script == null)
                    continue;

                AttachPoint point = script.AttachPoint;

                Debug.Log(
                    "[StickyPads] AttachPoint: " +
                    script.name +
                    " | Position: " + point.Position +
                    " | Offset: " + point.PositionOffset +
                    " | Scale: " + point.Scale +
                    " | Transform: " + script.transform.name
                );
            }
        }

        Debug.Log("[StickyPads] ===== COLLIDERS =====");

        if (PartScript.Colliders != null)
        {
            foreach (PartColliderScript colliderScript in PartScript.Colliders)
            {
                if (colliderScript == null || colliderScript.Collider == null)
                    continue;

                Collider collider = colliderScript.Collider;

                Debug.Log(
                    "[StickyPads] Collider: " +
                    collider.name +
                    " | Type: " + collider.GetType().Name +
                    " | Object: " + collider.transform.name +
                    " | Local Position: " + collider.transform.localPosition +
                    " | Local Scale: " + collider.transform.localScale
                );
            }
        }

        Debug.Log("[StickyPads] ===== GRAPPLE HIERARCHY END =====");
    }

    private void LogTransform(Transform current, int depth)
    {
        string indent = new string(' ', depth * 2);

        Debug.Log(
            "[StickyPads] " + indent +
            current.name +
            " | Local Position: " + current.localPosition +
            " | Local Scale: " + current.localScale +
            " | Children: " + current.childCount
        );

        for (int i = 0; i < current.childCount; i++)
        {
            LogTransform(current.GetChild(i), depth + 1);
        }
    }

    public override void OnActivated()
    {
        base.OnActivated();
        ReleaseGrapple();

        LogActivationTransforms();

        var body = PartScript.BodyScript?.RigidBody;

        if (body == null)
        {
            Debug.LogWarning("[StickyPads] Grapple has no rigidbody.");
            return;
        }

        Vector3 grappleOrigin =
            base.transform.position - base.transform.forward * 0.35f;

        Ray ray = new Ray(
            grappleOrigin,
            base.transform.TransformDirection(
                Quaternion.Euler(-90f, 0f, 0f) * Vector3.forward
            )
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            Data.MaxDistance,
            -1543503872,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Debug.Log("[StickyPads] Forward ray found " + hits.Length + " collider(s).");

        foreach (RaycastHit hit in hits)
        {
            Rigidbody hitBody = hit.collider.GetComponentInParent<Rigidbody>();

            if (hitBody == body)
            {
                Debug.Log("[StickyPads] Skipping own craft collider: "
                    + hit.collider.name);
                continue;
            }

            if (hitBody == null)
            {
                hitBody = hit.collider.gameObject.AddComponent<Rigidbody>();
                hitBody.isKinematic = true;
            }

            PartScript hitPart =
                hit.collider.GetComponentInParent<PartScript>();

            _grapplingHook = GrapplingHookScript.Connect(
                body,
                PartScript,
                grappleOrigin,
                hitBody,
                hitPart,
                hit.point
            );

            if (_grapplingHook != null)
            {
                _grapplingHook.LineOffsetFrom = new Vector3(0f, 0.35f, 0f);

                Debug.Log("[StickyPads] Grapple connected to: "
                    + hit.collider.name);

                return;
            }
        }

        Debug.Log("[StickyPads] No external target found.");
    }

    public override void OnDeactivated()
    {
        base.OnDeactivated();
        ReleaseGrapple();
        Debug.Log("[StickyPads] Grapple deactivated.");
    }

    private void ReleaseGrapple()
    {
        if (_grapplingHook != null)
        {
            _grapplingHook.RemoveImmediately();
            _grapplingHook = null;
        }
    }

    public override void OnPartDestroyed()
    {
        ReleaseGrapple();
        base.OnPartDestroyed();
    }

    protected override void OnDisposed()
    {
        ReleaseGrapple();
        base.OnDisposed();
    }
}