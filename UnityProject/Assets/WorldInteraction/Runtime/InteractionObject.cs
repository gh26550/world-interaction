using System;
using UnityEngine;

namespace WorldInteraction
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class InteractionObject : MonoBehaviour
    {
        public string objectId = "object", category = "prop";
        public bool target, support, movable = true;
        public GameObject meshVisual, lowMeshVisual, gaussianVisual;
        public Collider[] accurateColliders = Array.Empty<Collider>();
        public Collider[] proxyColliders = Array.Empty<Collider>();
        public RepresentationCandidate[] candidates = Array.Empty<RepresentationCandidate>();
        public Representation manualRepresentation = Representation.Mesh;
        public InteractionRequirement requirement = new();
        public AssignmentDecision decision;
        public bool held { get; private set; }
        public Rigidbody Body => GetComponent<Rigidbody>();
        public Vector3 InitialPosition { get; private set; }
        public Quaternion InitialRotation { get; private set; }
        public event Action<string, InteractionObject> Changed;
        bool initialized;
        public void CaptureInitial() { InitialPosition=transform.position; InitialRotation=transform.rotation; initialized=true; }
        void Start() { if (!initialized) CaptureInitial(); }
        public void Apply(AssignmentDecision next, bool lod=false)
        {
            if (held) throw new InvalidOperationException("Cannot change representation while held");
            decision=next;
            bool mesh=next.representation==Representation.Mesh;
            if (meshVisual) meshVisual.SetActive(mesh && !(lod && lowMeshVisual));
            if (lowMeshVisual) lowMeshVisual.SetActive(mesh && lod);
            if (gaussianVisual) gaussianVisual.SetActive(!mesh);
            foreach (var c in accurateColliders) if(c) c.enabled=mesh;
            foreach (var c in proxyColliders) if(c) c.enabled=next.representation==Representation.Hybrid;
            Body.isKinematic=!movable || next.representation==Representation.GS;
            Body.useGravity=movable && next.representation!=Representation.GS;
            Changed?.Invoke("assignment", this);
        }
        public bool CanGrab => movable && !held && decision!=null && decision.representation!=Representation.GS && requirement.grasp;
        public bool BeginHold()
        {
            if (!CanGrab) return false;
            held=true; Changed?.Invoke("grab",this); return true;
        }
        public void EndHold() { if(!held)return; held=false; Changed?.Invoke("release",this); }
        public void ResetPose()
        {
            if (held) throw new InvalidOperationException("Release object before reset");
            transform.SetPositionAndRotation(InitialPosition,InitialRotation);
            Body.position=InitialPosition; Body.rotation=InitialRotation;
            if(!Body.isKinematic){ Body.linearVelocity=Vector3.zero; Body.angularVelocity=Vector3.zero; }
        }
        void OnCollisionEnter(Collision collision)
        {
            Changed?.Invoke(collision.relativeVelocity.magnitude>.8f ? "impact" : "contact",this);
        }
    }
}
