using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace WorldInteraction.Editor
{
    public static class ImportedExperimentSetup
    {
        [MenuItem("World Interaction/Create Experiment From Selected World")]
        public static void Create()
        {
            var root=Selection.activeGameObject;if(!root)throw new System.InvalidOperationException("Select imported world root first");
            var objects=root.GetComponentsInChildren<InteractionObject>(true);if(objects.Length==0)throw new System.InvalidOperationException("No InteractionObjects in selection");
            if(Object.FindFirstObjectByType<ExperimentSession>())throw new System.InvalidOperationException("Use a new scene to avoid multiple experiments");
            var rigGO=new GameObject("XR Origin");Undo.RegisterCreatedObjectUndo(rigGO,"Create experiment");
            rigGO.transform.position=objects[0].transform.position+new Vector3(0,0,-2);
            var camGO=new GameObject("Main Camera");camGO.tag="MainCamera";camGO.transform.SetParent(rigGO.transform,false);camGO.transform.localPosition=new Vector3(0,1.65f,0);
            var cam=camGO.AddComponent<Camera>();cam.nearClipPlane=.03f;cam.clearFlags=CameraClearFlags.SolidColor;
            var rig=rigGO.AddComponent<PlayerRig>();rig.view=cam;
            rig.leftHand=Hand(rigGO.transform,XRNode.LeftHand);rig.rightHand=Hand(rigGO.transform,XRNode.RightHand);
            var desktop=camGO.AddComponent<PhysicsGrabber>();desktop.desktop=true;desktop.view=cam;
            var sessionGO=new GameObject("Experiment Session");Undo.RegisterCreatedObjectUndo(sessionGO,"Create experiment");
            var recorder=sessionGO.AddComponent<ExperimentRecorder>();var session=sessionGO.AddComponent<ExperimentSession>();session.objects=objects;session.recorder=recorder;session.view=cam;
            var marker=new GameObject("Placement Destination (move in editor)");marker.transform.position=objects.FirstOrDefault(o=>o.target)?.transform.position??Vector3.zero;session.destination=marker.transform;
            sessionGO.AddComponent<BenchmarkSweep>().session=session;
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,20,0);
            Selection.activeGameObject=sessionGO;
            Debug.Log("Review target/support flags, collision shapes, candidate calibration, player start, and destination before running.");
        }
        static Transform Hand(Transform parent,XRNode node){var go=new GameObject(node.ToString());go.transform.SetParent(parent,false);go.AddComponent<PhysicsGrabber>().hand=node;return go.transform;}
    }
}
