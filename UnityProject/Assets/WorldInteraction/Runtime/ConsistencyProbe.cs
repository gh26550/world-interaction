using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldInteraction
{
    [Serializable] public class ConsistencyResult
    {
        public string objectId;public int samples,misses;public float rmsMm,maxMm,meanSignedMm;
        public string method="Mesh-reference vertex normals raycast against active colliders; not a perceptual GS surface metric";
    }
    public static class ConsistencyProbe
    {
        public static ConsistencyResult Measure(InteractionObject obj,int maxSamples=256,float rayHalfLength=.2f)
        {
            var result=new ConsistencyResult{objectId=obj.objectId};var errors=new List<float>();if(!obj.meshVisual)return result;
            var colliders=obj.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
            foreach(var filter in obj.meshVisual.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(!mesh||!mesh.isReadable)continue;
                var vertices=mesh.vertices;var normals=mesh.normals;if(vertices.Length!=normals.Length)continue;
                int step=Mathf.Max(1,Mathf.CeilToInt(vertices.Length/(float)maxSamples));
                for(int i=0;i<vertices.Length;i+=step)
                {
                    var p=filter.transform.TransformPoint(vertices[i]);var normal=filter.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
                    var ray=new Ray(p+normal*rayHalfLength,-normal);float nearest=float.PositiveInfinity;
                    foreach(var c in colliders)if(c.Raycast(ray,out var hit,rayHalfLength*2))nearest=Mathf.Min(nearest,hit.distance);
                    if(float.IsInfinity(nearest)){result.misses++;continue;}errors.Add((rayHalfLength-nearest)*1000);
                }
            }
            result.samples=errors.Count;if(errors.Count>0){result.rmsMm=Mathf.Sqrt(errors.Sum(e=>e*e)/errors.Count);result.maxMm=errors.Max(e=>Mathf.Abs(e));result.meanSignedMm=errors.Average();}return result;
        }
    }
}
