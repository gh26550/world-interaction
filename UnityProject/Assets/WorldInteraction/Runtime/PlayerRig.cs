using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using CommonUsages=UnityEngine.XR.CommonUsages;

namespace WorldInteraction
{
    public sealed class PlayerRig : MonoBehaviour
    {
        public Camera view;
        public Transform leftHand,rightHand;
        public float speed=1.5f;
        CharacterController capsule;
        float yaw,pitch;bool lastPrimary,lastSecondary,lastLeftPrimary,originReady;
        float trackingYOffset;
        void Start(){capsule=gameObject.AddComponent<CharacterController>();capsule.radius=.2f;capsule.height=1.7f;capsule.center=new(0,.85f,0);}
        void Update()
        {
            if(XRSettings.isDeviceActive)
            {
                if(!originReady)
                {
                    var inputs=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(inputs);
                    foreach(var input in inputs)if(input.running){trackingYOffset=input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)?0:1.65f;originReady=true;}
                }
                PoseNode(XRNode.CenterEye,view.transform);PoseNode(XRNode.LeftHand,leftHand);PoseNode(XRNode.RightHand,rightHand);
                var d=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);d.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 axis);Move(axis);
                var r=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                r.TryGetFeatureValue(CommonUsages.primaryButton,out bool a);r.TryGetFeatureValue(CommonUsages.secondaryButton,out bool b);
                var session=FindFirstObjectByType<ExperimentSession>();
                if(a&&!lastPrimary&&session)session.ToggleTrial();if(b&&!lastSecondary&&session)session.NextTask();lastPrimary=a;lastSecondary=b;
                d.TryGetFeatureValue(CommonUsages.primaryButton,out bool x);if(x&&!lastLeftPrimary&&session)session.NextPolicy();lastLeftPrimary=x;
            }
            else
            {
                var mouse=Mouse.current;
                if(mouse!=null&&mouse.rightButton.isPressed){var delta=mouse.delta.ReadValue();yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.12f,-80,80);view.transform.localRotation=Quaternion.Euler(pitch,yaw,0);}
                var keys=Keyboard.current;
                if(keys!=null)Move(new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0)));
            }
        }
        void Move(Vector2 axis)
        {
            if(axis.sqrMagnitude>1)axis.Normalize();Vector3 forward=view.transform.forward;forward.y=0;forward.Normalize();
            Vector3 right=Vector3.Cross(Vector3.up,forward);capsule.Move((forward*axis.y+right*axis.x)*speed*Time.deltaTime+Vector3.down*2*Time.deltaTime);
        }
        void PoseNode(XRNode node,Transform t)
        {
            if(!t)return;var d=InputDevices.GetDeviceAtXRNode(node);
            if(d.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p))t.localPosition=p+Vector3.up*trackingYOffset;
            if(d.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q))t.localRotation=q;
        }
    }
}
