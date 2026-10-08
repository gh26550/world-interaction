using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using CommonUsages=UnityEngine.XR.CommonUsages;

namespace WorldInteraction
{
    public sealed class PhysicsGrabber : MonoBehaviour
    {
        public XRNode hand=XRNode.RightHand;
        public bool desktop;
        public Camera view;
        public float reach=3,holdDistance=1.2f;
        public InteractionObject Held {get;private set;}
        Rigidbody anchor;ConfigurableJoint joint;
        bool pressed;
        void Awake()
        {
            var go=new GameObject("Grab anchor");anchor=go.AddComponent<Rigidbody>();anchor.isKinematic=true;anchor.useGravity=false;
        }
        void Update()
        {
            if(desktop && XRSettings.isDeviceActive){if(Held)Release();return;}
            var device=InputDevices.GetDeviceAtXRNode(hand);
            bool down=false;
            if(desktop && !XRSettings.isDeviceActive)
            {
                down=Keyboard.current?.eKey.isPressed??false;holdDistance=Mathf.Clamp(holdDistance+(Mouse.current?.scroll.ReadValue().y??0)*.001f,.2f,3);
            }
            else if(device.isValid)device.TryGetFeatureValue(CommonUsages.gripButton,out down);
            if(down&&!pressed)TryGrab();if(!down&&pressed)Release();pressed=down;
        }
        public void TryGrab()
        {
            if(Held)return;
            Transform origin=desktop&&!XRSettings.isDeviceActive&&view ? view.transform : transform;
            if(!Physics.Raycast(origin.position,origin.forward,out var hit,reach))return;
            var obj=hit.collider.GetComponentInParent<InteractionObject>();
            if(!obj||!obj.BeginHold())return;
            Held=obj;holdDistance=Mathf.Clamp(hit.distance,.2f,3);
            anchor.position=hit.point;anchor.rotation=origin.rotation;
            joint=obj.gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=anchor;
            joint.autoConfigureConnectedAnchor=false;joint.anchor=obj.transform.InverseTransformPoint(hit.point);joint.connectedAnchor=Vector3.zero;
            joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Free;
            joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Free;
            var drive=new JointDrive{positionSpring=1800,positionDamper=90,maximumForce=400};
            joint.xDrive=joint.yDrive=joint.zDrive=drive;joint.rotationDriveMode=RotationDriveMode.Slerp;joint.slerpDrive=drive;
            joint.targetRotation=Quaternion.identity;
        }
        void FixedUpdate()
        {
            if(!Held)return;
            Transform origin=desktop&&!XRSettings.isDeviceActive&&view ? view.transform : transform;
            anchor.MovePosition(origin.position+origin.forward*holdDistance);anchor.MoveRotation(origin.rotation);
        }
        public void Release(){if(joint)Destroy(joint);if(Held)Held.EndHold();Held=null;}
        void OnDisable(){Release();}
        void OnDestroy(){Release();if(anchor)Destroy(anchor.gameObject);}
    }
}
