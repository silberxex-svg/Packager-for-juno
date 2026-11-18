
using ModApi.Craft.Parts;
using ModApi.Craft.Parts.Attributes;
using UnityEngine;

[System.Serializable]
[DesignerPartModifier("Sticky Pad")]
[PartModifierTypeId("SilverExe.StickyPad")]
public class StickyPadData : PartModifierData<StickyPadModifier>
{
    [PartModifierProperty(true, false)]
    public bool Enabled = true;

    [PartModifierProperty(true, false)]
    public bool StickyMode = true;

    [PartModifierProperty(true, false)]
    public float Strength = 5000f;

    [PartModifierProperty(true, false)]
    public float GripRange = 1f;
}

public class StickyPadModifier : PartModifierScript<StickyPadData>
{
    private bool _active;
    private FixedJoint? _joint;

    private bool _loggedCollision;
    private bool _loggedMissingBody;
    private bool _loggedJoint;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _active = Data.Enabled;

        Debug.Log("[StickyPads] Pad initialized. Enabled: " + _active);
    }

    public override void OnModifiersCreated()
    {
        base.OnModifiersCreated();

        var owner = PartScript;

        Debug.Log("[StickyPads] OnModifiersCreated received.");
        Debug.Log("[StickyPads] Owner PartScript null: " +
            (owner == null));

        if (owner != null)
        {
            Debug.Log("[StickyPads] Final modifier count: " +
                owner.Modifiers.Count);

            Debug.Log("[StickyPads] This pad is registered: " +
                owner.Modifiers.Contains(this));
        }
    }

    public override void OnActivated()
    {
        base.OnActivated();

        _active = true;
        Data.Enabled = true;

        Debug.Log("[StickyPads] Pad activated.");
    }

    public override void OnDeactivated()
    {
        base.OnDeactivated();

        _active = false;
        Data.Enabled = false;
        Release();

        Debug.Log("[StickyPads] Pad deactivated.");
    }

    public override bool OnCollision(IPartFlightCollision collision)
    {
        if (!_loggedCollision)
        {
            Debug.Log("[StickyPads] Collision callback received.");
            Debug.Log("[StickyPads] Collision belongs to this pad: " +
                (collision.PartScript == PartScript));

            _loggedCollision = true;
        }

        if (!_active || !Data.StickyMode || _joint != null)
            return false;

        var ownBody = collision.PartScript?.BodyScript?.RigidBody;

        if (ownBody == null)
        {
            if (!_loggedMissingBody)
            {
                Debug.LogWarning(
                    "[StickyPads] Collision received, but own rigidbody is NULL.");
                _loggedMissingBody = true;
            }

            return false;
        }

        var otherBody =
            collision.OtherPartScript?.BodyScript?.RigidBody;

        if (otherBody == ownBody)
            return false;

        Vector3 contactPoint = collision.Contact.point;

        _joint = ownBody.gameObject.AddComponent<FixedJoint>();

        _joint.autoConfigureConnectedAnchor = false;
        _joint.anchor =
            ownBody.transform.InverseTransformPoint(contactPoint);

        if (otherBody != null)
        {
            _joint.connectedBody = otherBody;
            _joint.connectedAnchor =
                otherBody.transform.InverseTransformPoint(contactPoint);
        }
        else
        {
            _joint.connectedBody = null;
            _joint.connectedAnchor = contactPoint;
        }

        _joint.breakForce = Mathf.Max(0.1f, Data.Strength);
        _joint.breakTorque = Mathf.Infinity;
        _joint.enableCollision = false;

        if (!_loggedJoint)
        {
            Debug.Log(
                "[StickyPads] FixedJoint created successfully. Other rigidbody: " +
                (otherBody != null));

            _loggedJoint = true;
        }

        return false;
    }

    private void Release()
    {
        if (_joint != null)
        {
            UnityEngine.Object.Destroy(_joint);
            _joint = null;
        }
    }

    public override void OnPartDestroyed()
    {
        Release();
        base.OnPartDestroyed();
    }

    protected override void OnDisposed()
    {
        Release();
        base.OnDisposed();
    }
}
