using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldInteraction
{
    public sealed class SceneGaussianBaseline : MonoBehaviour
    {
        GameObject combined;
        public void Clear(){if(combined){combined.SetActive(false);Destroy(combined);}combined=null;}
        public void Build(InteractionObject[] objects)
        {
            Clear();var all=new List<Gaussian>();Shader shader=null;
            foreach(var o in objects)
            {
                if(!o.gaussianVisual)throw new InvalidOperationException("Scene GS requires a Gaussian asset for every assigned object");
                foreach(var renderer in o.gaussianVisual.GetComponentsInChildren<GaussianRenderer>(true))
                {
                    shader=renderer.shader;var splats=renderer.splats;
                    if(splats==null&&renderer.data)splats=GaussianAsset.Read(renderer.data.bytes);
                    if(splats==null)throw new InvalidOperationException("Gaussian data missing");
                    Vector3 scale=renderer.transform.lossyScale;
                    if(Mathf.Abs(scale.x-scale.y)>.0001f||Mathf.Abs(scale.x-scale.z)>.0001f)throw new InvalidOperationException("Scene aggregation requires uniform transform scale");
                    foreach(var original in splats){var g=original;g.center=renderer.transform.TransformPoint(g.center);g.rotation=renderer.transform.rotation*g.rotation;g.scale*=Mathf.Abs(scale.x);all.Add(g);}
                }
            }
            if(all.Count>GaussianAsset.MaxCount)throw new InvalidOperationException("Scene exceeds reference renderer splat limit");
            combined=new GameObject("Scene-level GS baseline");var gs=combined.AddComponent<GaussianRenderer>();gs.shader=shader;gs.Initialize(all.ToArray());
            foreach(var o in objects)o.gaussianVisual.SetActive(false);
        }
        void OnDestroy(){Clear();}
    }
}
