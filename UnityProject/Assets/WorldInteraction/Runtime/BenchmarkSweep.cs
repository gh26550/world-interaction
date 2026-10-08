using System.Collections;
using UnityEngine;

namespace WorldInteraction
{
    public sealed class BenchmarkSweep : MonoBehaviour
    {
        public ExperimentSession session;
        [Min(3)] public float secondsPerCondition=8;
        public bool Running {get;private set;}
        public AssignmentPolicy[] conditions={AssignmentPolicy.MeshOnly,AssignmentPolicy.MeshLOD,AssignmentPolicy.UniformHybrid,AssignmentPolicy.RequirementBased,AssignmentPolicy.AggressiveGS,AssignmentPolicy.SceneGS};
        [ContextMenu("Run Fixed View Rendering Benchmark")]
        public void Run(){if(!Application.isPlaying||Running)return;StartCoroutine(Sweep());}
        IEnumerator Sweep()
        {
            if(!session)session=FindFirstObjectByType<ExperimentSession>();if(!session||session.recorder.Recording)yield break;
            foreach(var grab in FindObjectsByType<PhysicsGrabber>(FindObjectsSortMode.None))grab.Release();
            Running=true;var oldPolicy=session.policy;bool oldAllow=session.allowInfeasibleBaseline;session.allowInfeasibleBaseline=true;session.autoComplete=false;
            var rig=FindFirstObjectByType<PlayerRig>();if(rig)rig.enabled=false;
            foreach(var policy in conditions)
            {
                session.policy=policy;
                if(!session.Begin())continue;
                session.recorder.Event("benchmark_fixed_view","no user task; do not interpret success rate");
                yield return new WaitForSecondsRealtime(secondsPerCondition);
                session.recorder.End(false,"render-only benchmark");
            }
            session.policy=oldPolicy;session.allowInfeasibleBaseline=oldAllow;session.autoComplete=true;if(rig)rig.enabled=true;session.Apply();Running=false;
        }
    }
}
