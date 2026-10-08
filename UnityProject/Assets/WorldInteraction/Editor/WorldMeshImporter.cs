using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldInteraction.Editor
{
    public static class WorldMeshImporter
    {
        [Serializable] class Manifest {public int version;public string name;public Entry[] objects;}
        [Serializable] class Entry {public string id,mesh,gaussians,category;public Vector3 position,size;public bool movable,target,support,qualityVerified,hiddenSurfacesComplete;public float contactErrorMm;}
        [MenuItem("World Interaction/Import Prepared WorldMesh Manifest")]
        public static void Import()
        {
            string path=EditorUtility.OpenFilePanel("Prepared WorldMesh manifest",Application.dataPath,"json");if(string.IsNullOrEmpty(path))return;ImportPath(path);
        }
        public static GameObject ImportPath(string path)
        {
            path=Path.GetFullPath(path);string dir=Path.GetDirectoryName(path);
            if(!path.StartsWith(Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Place prepared data under Assets/ImportedWorlds before import");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if(manifest.version!=1||manifest.objects==null||manifest.objects.Length==0)throw new InvalidDataException("Invalid manifest");
            var root=new GameObject(manifest.name);Undo.RegisterCreatedObjectUndo(root,"Import WorldMesh");
            string generated=Path.Combine(dir,"UnityAssets");Directory.CreateDirectory(generated);
            foreach(var entry in manifest.objects)
            {
                if(!System.Text.RegularExpressions.Regex.IsMatch(entry.id??"","^[a-zA-Z0-9_-]+$"))throw new InvalidDataException("Invalid object id");
                string meshPath=SafeChild(dir,entry.mesh);var mesh=ReadMesh(File.ReadAllBytes(meshPath));
                string assetPath=ToAssetPath(Path.Combine(generated,entry.id+".asset"));assetPath=AssetDatabase.GenerateUniqueAssetPath(assetPath);AssetDatabase.CreateAsset(mesh,assetPath);
                var go=new GameObject(entry.id);go.transform.SetParent(root.transform,false);go.transform.localPosition=entry.position;
                var body=go.AddComponent<Rigidbody>();body.isKinematic=!entry.movable;
                var obj=go.AddComponent<InteractionObject>();obj.objectId=entry.id;obj.category=entry.category;obj.movable=entry.movable;obj.target=entry.target;obj.support=entry.support;
                var visual=new GameObject("Mesh visual");visual.transform.SetParent(go.transform,false);visual.AddComponent<MeshFilter>().sharedMesh=mesh;
                var material=new Material(Shader.Find("WorldInteraction/VertexColor"));string matPath=AssetDatabase.GenerateUniqueAssetPath(ToAssetPath(Path.Combine(generated,entry.id+".mat")));AssetDatabase.CreateAsset(material,matPath);visual.AddComponent<MeshRenderer>().sharedMaterial=material;obj.meshVisual=visual;
                var collider=go.AddComponent<BoxCollider>();collider.center=mesh.bounds.center;collider.size=mesh.bounds.size;obj.accurateColliders=new Collider[]{collider};
                var proxy=go.AddComponent<BoxCollider>();proxy.center=mesh.bounds.center;proxy.size=mesh.bounds.size;proxy.enabled=false;obj.proxyColliders=new Collider[]{proxy};
                bool hasGs=!string.IsNullOrEmpty(entry.gaussians);
                if(hasGs){var gsgo=new GameObject("Gaussian visual");gsgo.transform.SetParent(go.transform,false);var gs=gsgo.AddComponent<GaussianRenderer>();gs.shader=Shader.Find("WorldInteraction/AnisotropicGaussian");gs.data=AssetDatabase.LoadAssetAtPath<TextAsset>(ToAssetPath(SafeChild(dir,entry.gaussians)));if(!gs.data)throw new InvalidDataException("Gaussian file missing");obj.gaussianVisual=gsgo;gsgo.SetActive(false);}
                obj.candidates=new[]{Candidate(Representation.Mesh,true,entry),Candidate(Representation.GS,hasGs,entry),Candidate(Representation.Hybrid,hasGs,entry)};
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);Selection.activeGameObject=root;return root;
        }
        static RepresentationCandidate Candidate(Representation r,bool available,Entry e)=>new(){representation=r,available=available,collision=r!=Representation.GS,grasp=r!=Representation.GS&&e.movable,move=r!=Representation.GS&&e.movable,qualityVerified=e.qualityVerified,contactErrorMm=e.contactErrorMm,hiddenSurfacesComplete=e.hiddenSurfacesComplete,cost=r==Representation.Mesh?3:r==Representation.GS?1:2};
        public static void ValidateFromCommandLine()
        {
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-worldManifest");if(index<0||index+1>=args.Length)throw new ArgumentException("Pass -worldManifest path");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=ImportPath(args[index+1]);Debug.Log("WORLDMESH_IMPORT_OK objects="+root.GetComponentsInChildren<InteractionObject>().Length);
        }
        static string ToAssetPath(string path)=>"Assets"+Path.GetFullPath(path)[Path.GetFullPath(Application.dataPath).Length..].Replace('\\','/');
        static string SafeChild(string dir,string relative)
        {
            string path=Path.GetFullPath(Path.Combine(dir,relative));if(!path.StartsWith(dir+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Asset path escapes manifest directory");return path;
        }
        public static Mesh ReadMesh(byte[] bytes)
        {
            using var r=new BinaryReader(new MemoryStream(bytes));if(new string(r.ReadChars(4))!="WIM1")throw new InvalidDataException("Expected WIM1 mesh");int n=r.ReadInt32(),k=r.ReadInt32();
            if(n<3||n>5000000||k<3||k%3!=0||bytes.Length!=12L+n*48L+k*4L)throw new InvalidDataException("Invalid mesh length");
            var v=new Vector3[n];var normals=new Vector3[n];var uv=new Vector2[n];var color=new Color[n];
            for(int i=0;i<n;i++){v[i]=new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());normals[i]=new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new(r.ReadSingle(),r.ReadSingle());color[i]=new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
            int[] triangles=new int[k];for(int i=0;i<k;i++){triangles[i]=r.ReadInt32();if(triangles[i]<0||triangles[i]>=n)throw new InvalidDataException("Invalid mesh index");}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32,vertices=v,normals=normals,uv=uv,colors=color,triangles=triangles};mesh.RecalculateBounds();return mesh;
        }
    }
}
