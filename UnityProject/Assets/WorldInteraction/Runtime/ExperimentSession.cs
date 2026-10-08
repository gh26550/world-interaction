using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldInteraction
{
    public sealed class ExperimentSession : MonoBehaviour
    {
        public InteractionObject[] objects;
        public TaskKind task=TaskKind.Transport;
        public AssignmentPolicy policy=AssignmentPolicy.RequirementBased;
        public TaskProfile[] taskProfiles=Array.Empty<TaskProfile>();
        public Transform destination;
        public Vector3 destinationHalfExtents=new(.3f,.25f,.3f);
        public ExperimentRecorder recorder;
        public Camera view;
        public bool allowInfeasibleBaseline;
        [NonSerialized] public bool autoComplete=true;
        public string status="Ready";
        float settled,observed;
        bool dropped;
        SceneGaussianBaseline sceneBaseline;
        public InteractionObject Target => objects?.FirstOrDefault(o=>o.target);
        void Start()
        {
            if(objects==null||objects.Length==0)objects=FindObjectsByType<InteractionObject>(FindObjectsSortMode.None);
            foreach(var o in objects){o.CaptureInitial();o.Changed+=OnObjectEvent;}Apply();
        }
        public bool Apply()
        {
            if(recorder.Recording || objects.Any(o=>o.held)){status="End trial and release objects before assignment";return false;}
            if(sceneBaseline)sceneBaseline.Clear();
            foreach(var o in objects)
            {
                var profile=taskProfiles.FirstOrDefault(p=>p && p.task==task);
                o.requirement=profile?profile.Resolve(o):AssignmentEngine.ForTask(task,o.target,o.support);
                float distance=view?Vector3.Distance(o.transform.position,view.transform.position):1;
                var decision=AssignmentEngine.Choose(o.objectId,o.candidates,o.requirement,policy,o.manualRepresentation,o.category,distance);
                o.Apply(decision,policy==AssignmentPolicy.MeshLOD);
            }
            if(policy==AssignmentPolicy.SceneGS)
            {
                sceneBaseline??=gameObject.AddComponent<SceneGaussianBaseline>();
                try{sceneBaseline.Build(objects);}catch(Exception e){status=e.Message;return false;}
            }
            status=objects.All(o=>o.decision.feasible)?"All declared requirements satisfied":"Requirement violations: inspect assignments";return true;
        }
        public void NextTask()
        {
            if(recorder.Recording || objects.Any(o=>o.held))return;task=(TaskKind)(((int)task+1)%3);Apply();
        }
        public void NextPolicy(){if(recorder.Recording || objects.Any(o=>o.held))return;policy=(AssignmentPolicy)(((int)policy+1)%Enum.GetValues(typeof(AssignmentPolicy)).Length);Apply();}
        public void ToggleTrial(){if(recorder.Recording)recorder.End(false,"manual stop");else Begin();}
        public bool Begin()
        {
            if(!Apply())return false;
            if(objects.Any(o=>!o.decision.feasible)&&!allowInfeasibleBaseline){status="Blocked: unmet requirements (explicit baseline override available)";return false;}
            foreach(var o in objects)o.ResetPose();Physics.SyncTransforms();
            if(policy==AssignmentPolicy.SceneGS)sceneBaseline.Build(objects);
            settled=observed=0;dropped=false;
            recorder.Begin(task,policy,objects);status="Trial running";return true;
        }
        void OnObjectEvent(string kind,InteractionObject o)=>recorder.Event(kind,o.objectId,o.transform.position);
        void Update()
        {
            if(Keyboard.current?.spaceKey.wasPressedThisFrame??false)ToggleTrial();if(Keyboard.current?.tKey.wasPressedThisFrame??false)NextTask();
            if(!recorder.Recording||!Target||!autoComplete)return;
            if(task==TaskKind.Observe)
            {
                var direction=Target.transform.position-view.transform.position;
                bool looking=Vector3.Angle(view.transform.forward,direction)<8 && direction.magnitude<2;
                bool visible=Physics.Raycast(view.transform.position,direction.normalized,out var hit, direction.magnitude+.2f) && hit.collider.GetComponentInParent<InteractionObject>()==Target;
                // GS has no collider: use screen-angle observation only, explicitly logged.
                if(Target.decision.representation==Representation.GS)visible=true;
                observed=looking&&visible?observed+Time.deltaTime:0;
                if(observed>=2){recorder.End(true,"target observed for 2 seconds");status="Observation complete";}
                return;
            }
            if(!Target.held && Target.transform.position.y<.2f&&!dropped){recorder.Event("drop",Target.objectId,Target.transform.position);dropped=true;}
            if(!destination)return;
            Vector3 local=destination.InverseTransformPoint(Target.transform.position);
            float tolerance=task==TaskKind.PrecisionPlace?.015f:.2f;
            bool inside=Mathf.Abs(local.x)<=tolerance&&Mathf.Abs(local.z)<=tolerance&&Mathf.Abs(local.y)<destinationHalfExtents.y;
            bool still=Target.Body.linearVelocity.magnitude<.05f&&Target.Body.angularVelocity.magnitude<.2f;
            settled=inside&&still&&!Target.held&&recorder.Summary.grabs>0?settled+Time.deltaTime:0;
            if(settled>1){float error=new Vector2(local.x,local.z).magnitude*1000;recorder.End(true,"stable placement",error);status="Placement complete";}
        }
        void OnGUI()
        {
            if(UnityEngine.XR.XRSettings.isDeviceActive)return;
            GUILayout.BeginArea(new Rect(16,16,430,Screen.height-32),GUI.skin.box);
            GUILayout.Label("WORLD INTERACTION / Unity research workbench");
            GUILayout.Label("Task: "+task+"  |  Policy: "+policy);
            GUI.enabled=!recorder.Recording && !objects.Any(o=>o.held);
            if(GUILayout.Button("Next task [T]"))NextTask();
            int next=GUILayout.SelectionGrid((int)policy,Enum.GetNames(typeof(AssignmentPolicy)),2);
            if(next!=(int)policy){policy=(AssignmentPolicy)next;Apply();}
            allowInfeasibleBaseline=GUILayout.Toggle(allowInfeasibleBaseline,"Allow infeasible baseline (violations are logged)");
            foreach(var o in objects)
            {
                GUILayout.Label(o.objectId+" → "+o.decision?.representation+" / "+o.decision?.reason);
                if(policy==AssignmentPolicy.ManualHybrid){int m=GUILayout.SelectionGrid((int)o.manualRepresentation,new[]{"Mesh","GS","Hybrid"},3);if(m!=(int)o.manualRepresentation){o.manualRepresentation=(Representation)m;Apply();}}
            }
            GUI.enabled=true;
            if(GUILayout.Button(recorder.Recording?"End trial [Space]":"Begin trial [Space]"))ToggleTrial();
            GUILayout.Label(status);GUILayout.Label("WASD move · right mouse look · E hold/release · wheel reach");
            GUILayout.Label("Quest: grip grab · A start/stop · B task · left stick move");
            GUILayout.Label("Reference GS: SH0, per-object sorting. Demo costs are illustrative.");
            if(!string.IsNullOrEmpty(recorder.LastPath))GUILayout.Label("Logs: "+recorder.LastPath);
            GUILayout.EndArea();
        }
        void OnDestroy(){if(objects!=null)foreach(var o in objects)if(o)o.Changed-=OnObjectEvent;}
    }
}
