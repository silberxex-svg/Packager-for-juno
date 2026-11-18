
using ModApi.Craft;
using ModApi.Craft.Parts;
using ModApi.Craft.Parts.Attributes;
using ModApi.Craft.Parts.Input;
using ModApi.GameLoop;
using ModApi.GameLoop.Interfaces;
using UnityEngine;

[System.Serializable]
[DesignerPartModifier("Ball Joint")]
[PartModifierTypeId("SilverExe.BallJoint")]
public class BallJointData : PartModifierData<BallJointModifier>
{
    [PartModifierProperty(true, false)]
    public int AttachPointIndex = 0;

    [DesignerPropertySlider(0f, 90f, 19, Label = "Left/Right Range", Header = "Ball Joint Settings")]
    public float LeftRightRange = 45f;

    [DesignerPropertySlider(0f, 90f, 19, Label = "Forward/Backward Range")]
    public float ForwardBackwardRange = 45f;

    [DesignerPropertySlider(0f, 180f, 19, Label = "Speed (degrees/sec)")]
    public float MaxSpeed = 90f;

    [PartModifierProperty(true, false)]
    public float Strength = 400000f;
}

public class BallJointModifier :
    PartModifierScript<BallJointData>,
    IDesignerStart,
    IFlightStart,
    IFlightUpdate,
    IFlightFixedUpdate
{
    private IInputController? _leftRight;
    private IInputController? _forwardBackward;

    private ConfigurableJoint? _joint;
    private Rigidbody? _rigidBody;
    private Rigidbody? _connectedRigidBody;

    private float _targetX;
    private float _targetY;
    private float _currentX;
    private float _currentY;

    private float _lastLoggedX;
    private float _lastLoggedY;
    private bool _hasLoggedInput;

    private bool _loggedMissingJoint;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Debug.Log("[StickyPads] Ball Joint initialized.");
    }

    void IDesignerStart.DesignerStart(in DesignerFrameData frame)
    {
        GetControllers();
    }

    void IFlightStart.FlightStart(in FlightFrameData frame)
    {
        GetControllers();
        SetupJoint();
        _hasLoggedInput = false;
    }

    void IFlightUpdate.FlightUpdate(in FlightFrameData frame)
    {
        if (_joint == null)
            return;

        float inputX = _leftRight?.Value ?? 0f;
        float inputY = _forwardBackward?.Value ?? 0f;

        _targetX = Mathf.Clamp(inputX, -1f, 1f) *
            Mathf.Abs(Data.LeftRightRange);

        _targetY = Mathf.Clamp(inputY, -1f, 1f) *
            Mathf.Abs(Data.ForwardBackwardRange);

        if (!_hasLoggedInput ||
            !Mathf.Approximately(inputX, _lastLoggedX) ||
            !Mathf.Approximately(inputY, _lastLoggedY))
        {
            Debug.Log(
                $"[StickyPads] Ball Joint INPUT: " +
                $"LR={inputX:F3}, FB={inputY:F3} | " +
                $"TARGET: X={_targetX:F2}, Y={_targetY:F2}");

            _lastLoggedX = inputX;
            _lastLoggedY = inputY;
            _hasLoggedInput = true;
        }
    }

    void IFlightFixedUpdate.FlightFixedUpdate(in FlightFrameData frame)
    {
        if (_joint == null)
            return;

        float step = Mathf.Max(0f, Data.MaxSpeed) * frame.DeltaTime;

        _currentX = Mathf.MoveTowards(_currentX, _targetX, step);
        _currentY = Mathf.MoveTowards(_currentY, _targetY, step);

        _joint.targetRotation =
            Quaternion.AngleAxis(-_currentX, Vector3.right) *
            Quaternion.AngleAxis(-_currentY, Vector3.up);

        if (_rigidBody != null && _rigidBody.IsSleeping())
            _rigidBody.WakeUp();

        if (_connectedRigidBody != null &&
            _connectedRigidBody.IsSleeping())
            _connectedRigidBody.WakeUp();
    }

    private void GetControllers()
    {
        _leftRight = GetInputController("BallJointLR");
        _forwardBackward = GetInputController("BallJointFB");
    }

    public override void OnCraftLoaded(
        ICraftScript craftScript,
        bool movedToNewCraft)
    {
        base.OnCraftLoaded(craftScript, movedToNewCraft);
        CheckJoint();
    }

    public override void OnCraftStructureChanged(ICraftScript craftScript)
    {
        base.OnCraftStructureChanged(craftScript);
        CheckJoint();
    }

    private void CheckJoint()
    {
        if (PartScript?.BodyScript == null)
            return;

        Rigidbody body = PartScript.BodyScript.RigidBody;

        if (_joint == null ||
            _rigidBody != body ||
            _joint.connectedBody != _connectedRigidBody)
        {
            SetupJoint();
        }
    }

    private void SetupJoint()
    {
        _joint = null;
        _rigidBody = null;
        _connectedRigidBody = null;

        if (PartScript?.BodyScript == null)
            return;

        int index = Data.AttachPointIndex;

        if (index < 0 || index >= PartScript.Data.AttachPoints.Count)
        {
            Debug.LogError("[StickyPads] Ball Joint attach point index is invalid.");
            return;
        }

        AttachPoint attachPoint = PartScript.Data.AttachPoints[index];

        if (attachPoint.PartConnections.Count != 1)
            return;

        foreach (IBodyJoint bodyJoint in PartScript.BodyScript.Joints)
        {
            Joint found = bodyJoint.GetJointForAttachPoint(attachPoint);

            if (found is ConfigurableJoint configurable)
            {
                _joint = configurable;
                break;
            }
        }

        if (_joint == null)
        {
            if (!_loggedMissingJoint)
            {
                Debug.LogWarning(
                    "[StickyPads] Ball Joint could not find its ConfigurableJoint.");
                _loggedMissingJoint = true;
            }

            return;
        }

        _rigidBody = _joint.GetComponent<Rigidbody>();
        _connectedRigidBody = _joint.connectedBody;

        _joint.rotationDriveMode = RotationDriveMode.XYAndZ;

        _joint.angularXMotion = ConfigurableJointMotion.Limited;
        _joint.angularYMotion = ConfigurableJointMotion.Limited;
        _joint.angularZMotion = ConfigurableJointMotion.Locked;

        SoftJointLimit xLow = _joint.lowAngularXLimit;
        xLow.limit = -Mathf.Abs(Data.LeftRightRange);
        _joint.lowAngularXLimit = xLow;

        SoftJointLimit xHigh = _joint.highAngularXLimit;
        xHigh.limit = Mathf.Abs(Data.LeftRightRange);
        _joint.highAngularXLimit = xHigh;

        SoftJointLimit yLimit = _joint.angularYLimit;
        yLimit.limit = Mathf.Abs(Data.ForwardBackwardRange);
        _joint.angularYLimit = yLimit;

        JointDrive drive = new JointDrive
        {
            positionSpring = 400000f,
            positionDamper = 40000f,
            maximumForce = Mathf.Max(1f, Data.Strength)
        };

        _joint.angularXDrive = drive;
        _joint.angularYZDrive = drive;

        _currentX = 0f;
        _currentY = 0f;
        _targetX = 0f;
        _targetY = 0f;

        Debug.Log("[StickyPads] Ball Joint configured with two driven axes.");
    }
}
