using System.Collections.Generic;
using UnityEngine;

namespace WorldInteraction
{
    public sealed class DemoBootstrap : MonoBehaviour
    {
        public Shader gaussianShader;
        void Start()
        {
            if(FindFirstObjectByType<ExperimentSession>())return;
            Application.targetFrameRate=72;Time.fixedDeltaTime=1f/90f;
            var light=new GameObject("Key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-25,0);
            RenderSettings.ambientLight=new Color(.5f,.53f,.6f);
            StaticBox("Floor",new(0,-.08f,1),new(8,.16f,8),new(.13f,.17f,.2f));
            StaticBox("Back wall",new(0,1.5f,4),new(8,3,.1f),new(.22f,.3f,.32f));
            var objects=new List<InteractionObject>();
            objects.Add(Box("table",new(0,.7f,1.2f),new(2.4f,.12f,1.1f),new(.5f,.34f,.21f),false,false,true));
            objects.Add(Box("target-block",new(-.65f,.87f,1.15f),new(.18f,.22f,.18f),new(.1f,.85f,.68f),true,true,false));
            objects.Add(Box("display-block",new(.1f,1,1.5f),new(.3f,.48f,.25f),new(.9f,.48f,.19f),false,false,false));
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)StaticBox("Table leg",new(x*.95f,.32f,1.2f+z*.4f),new(.08f,.64f,.08f),new(.25f,.2f,.17f));
            var zone=new GameObject("Placement target");zone.transform.position=new(.65f,.87f,1.15f);
            StaticBox("Target marking",new(.65f,.762f,1.15f),new(.23f,.002f,.23f),new(.2f,.55f,.9f));
            // Physical guides permit the .18 m target with 15 mm clearance on each side.
            foreach(float sign in new[]{-1f,1f})StaticBox("Precision guide",new(.65f+sign*.12f,.8f,1.15f),new(.02f,.08f,.25f),new(.3f,.4f,.55f));
            var rigGO=new GameObject("XR Origin");rigGO.transform.position=new(0,0,-1.1f);
            var cameraGO=new GameObject("Main Camera");cameraGO.tag="MainCamera";cameraGO.transform.SetParent(rigGO.transform,false);cameraGO.transform.localPosition=new(0,1.65f,0);
            var camera=cameraGO.AddComponent<Camera>();camera.nearClipPlane=.03f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new(.08f,.12f,.15f);
            var rig=rigGO.AddComponent<PlayerRig>();rig.view=camera;
            rig.leftHand=Hand("Left hand",rigGO.transform,XRNodeLeft:true,camera);
            rig.rightHand=Hand("Right hand",rigGO.transform,XRNodeLeft:false,camera);
            var record=new GameObject("Recorder").AddComponent<ExperimentRecorder>();
            var session=new GameObject("Experiment Session").AddComponent<ExperimentSession>();session.objects=objects.ToArray();session.destination=zone.transform;session.view=camera;session.recorder=record;
            var desktop=cameraGO.AddComponent<PhysicsGrabber>();desktop.desktop=true;desktop.view=camera;
            var labelGO=new GameObject("VR instructions");labelGO.transform.position=new(0,2.1f,2.5f);
            var label=labelGO.AddComponent<TextMesh>();label.text="WORLD INTERACTION\nGrip: grab / release   A: trial   B: task\nPlace the green block in the blue target";label.fontSize=48;label.characterSize=.016f;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;
            labelGO.AddComponent<WorldStatusDisplay>().session=session;session.gameObject.AddComponent<BenchmarkSweep>().session=session;
        }
        Transform Hand(string name,Transform parent,bool XRNodeLeft,Camera camera)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var grab=go.AddComponent<PhysicsGrabber>();grab.hand=XRNodeLeft?UnityEngine.XR.XRNode.LeftHand:UnityEngine.XR.XRNode.RightHand;grab.view=camera;
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;line.SetPositions(new[]{Vector3.zero,Vector3.forward*1.2f});line.startWidth=.003f;line.endWidth=.001f;line.material=new Material(Shader.Find("Sprites/Default"));line.startColor=line.endColor=Color.cyan;
            return go.transform;
        }
        InteractionObject Box(string id,Vector3 position,Vector3 size,Color color,bool movable,bool target,bool support)
        {
            var root=new GameObject(id);root.transform.position=position;var body=root.AddComponent<Rigidbody>();body.mass=.4f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;body.isKinematic=!movable;
            var obj=root.AddComponent<InteractionObject>();obj.objectId=id;obj.target=target;obj.support=support;obj.movable=movable;obj.category=support?"structure":"prop";
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Mesh visual";visual.transform.SetParent(root.transform,false);visual.transform.localScale=size;Destroy(visual.GetComponent<Collider>());visual.GetComponent<Renderer>().material.color=color;obj.meshVisual=visual;
            var low=Instantiate(visual,root.transform);low.name="LOD visual (box already minimal)";low.SetActive(false);obj.lowMeshVisual=low;
            var exact=root.AddComponent<BoxCollider>();exact.size=size;obj.accurateColliders=new Collider[]{exact};
            var proxy=root.AddComponent<BoxCollider>();proxy.size=size+Vector3.one*.012f;proxy.enabled=false;obj.proxyColliders=new Collider[]{proxy};
            var gsGO=new GameObject("Gaussian visual");gsGO.transform.SetParent(root.transform,false);var gs=gsGO.AddComponent<GaussianRenderer>();gs.shader=gaussianShader;gs.Initialize(BoxSplats(size,color));obj.gaussianVisual=gsGO;gsGO.SetActive(false);
            obj.candidates=new[]{
                new RepresentationCandidate{representation=Representation.Mesh,cost=3,contactErrorMm=0,grasp=movable,move=movable},
                new RepresentationCandidate{representation=Representation.GS,cost=1,collision=false,grasp=false,move=false,validatedMinDistance=.3f,visualQuality=.85f},
                new RepresentationCandidate{representation=Representation.Hybrid,cost=2,contactErrorMm=6,visualQuality=.85f,grasp=movable,move=movable}
            };
            obj.CaptureInitial();return obj;
        }
        public static Gaussian[] BoxSplats(Vector3 size,Color color,int resolution=24)
        {
            var list=new List<Gaussian>();
            for(int axis=0;axis<3;axis++)foreach(float side in new[]{-1f,1f})
            {
                int u=(axis+1)%3,v=(axis+2)%3;var normal=Vector3.zero;normal[axis]=side;
                var up=Vector3.zero;up[v]=1;var q=Quaternion.LookRotation(normal,up);
                float sigmaU=size[u]/(resolution-1)*.55f,sigmaV=size[v]/(resolution-1)*.55f;
                for(int i=0;i<resolution;i++)for(int j=0;j<resolution;j++)
                {
                    var p=Vector3.zero;p[axis]=side*size[axis]*.5f;p[u]=(i/(float)(resolution-1)-.5f)*size[u];p[v]=(j/(float)(resolution-1)-.5f)*size[v];
                    var tint=color*(axis==1&&side>0?1:axis==0?.85f:.72f);tint.a=.92f;
                    list.Add(new Gaussian{center=p,scale=new(sigmaU,sigmaV,.0008f),rotation=q,color=tint});
                }
            }
            return list.ToArray();
        }
        static void StaticBox(string name,Vector3 p,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().material.color=color;
        }
    }
}
