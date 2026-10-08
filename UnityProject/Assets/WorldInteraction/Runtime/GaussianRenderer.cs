using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldInteraction
{
    // Reference renderer: anisotropic SH0 splats, CPU per-camera depth sort.
    // Sort is within each object; interleaved objects are not globally sorted.
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class GaussianRenderer : MonoBehaviour
    {
        public TextAsset data;
        public Shader shader;
        public Gaussian[] splats;
        Mesh mesh;
        Material material;
        int[] order,indices;
        float[] depths;
        public int Count => splats?.Length??0;
        public void Initialize(Gaussian[] values)
        {
            if(values==null || values.Length==0 || values.Length>GaussianAsset.MaxCount)throw new ArgumentException("Invalid splat count");
            splats=values;
            if(mesh) Destroy(mesh);
            mesh=new Mesh {name="Anisotropic Gaussian quads", indexFormat=IndexFormat.UInt32};
            var vertices=new Vector3[Count*4]; var scales=new Vector3[vertices.Length];
            var rotations=new Vector4[vertices.Length];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];
            Vector2[] corners={new(-3,-3),new(-3,3),new(3,3),new(3,-3)};
            order=new int[Count];depths=new float[Count];indices=new int[Count*6];
            var bounds=new Bounds(values[0].center,Vector3.zero);
            for(int i=0;i<Count;i++)
            {
                var g=values[i];order[i]=i;
                float radius=3*Mathf.Max(g.scale.x,Mathf.Max(g.scale.y,g.scale.z));
                bounds.Encapsulate(new Bounds(g.center,Vector3.one*radius*2));
                for(int k=0;k<4;k++){int v=i*4+k;vertices[v]=g.center; scales[v]=g.scale;rotations[v]=new(g.rotation.x,g.rotation.y,g.rotation.z,g.rotation.w);colors[v]=g.color;uv[v]=corners[k];}
            }
            mesh.vertices=vertices;mesh.normals=scales;mesh.tangents=rotations;mesh.colors=colors;mesh.uv=uv;
            UpdateIndices();mesh.bounds=bounds;
            GetComponent<MeshFilter>().sharedMesh=mesh;
            if(!shader)shader=Shader.Find("WorldInteraction/AnisotropicGaussian");
            if(!shader)throw new InvalidOperationException("Gaussian shader missing");
            if(material)Destroy(material);
            material=new Material(shader);GetComponent<MeshRenderer>().sharedMaterial=material;
            GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            GetComponent<MeshRenderer>().receiveShadows=false;
        }
        void Start(){if(mesh)return;if(data)Initialize(GaussianAsset.Read(data.bytes));else if(splats?.Length>0)Initialize(splats);}
        void OnWillRenderObject()
        {
            var camera=Camera.current;if(!camera||!mesh)return;
            Matrix4x4 mv=camera.worldToCameraMatrix*transform.localToWorldMatrix;
            for(int i=0;i<Count;i++){order[i]=i;depths[i]=mv.MultiplyPoint3x4(splats[i].center).z;}
            Array.Sort(order,(a,b)=>depths[a].CompareTo(depths[b]));UpdateIndices();
        }
        void UpdateIndices()
        {
            for(int i=0;i<Count;i++){int v=order[i]*4,j=i*6;indices[j]=v;indices[j+1]=v+1;indices[j+2]=v+2;indices[j+3]=v;indices[j+4]=v+2;indices[j+5]=v+3;}
            mesh.SetIndices(indices,MeshTopology.Triangles,0,false);
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}
