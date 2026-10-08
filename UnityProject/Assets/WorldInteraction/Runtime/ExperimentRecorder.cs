using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace WorldInteraction
{
    [Serializable] public class ExperimentSummary
    {
        public string sessionId,startedUtc,task,policy,device,unityVersion,scene,assignmentJson;
        public bool success;public string endReason;
        public int frames,grabs,releases,impacts,drops,requirementViolations;
        public double durationSeconds,p50FrameMs,p95FrameMs,p99FrameMs,budgetExceededFraction;
        public double meanCpuFrameMs,meanGpuFrameMs;
        public long allocatedMemoryBytes;
        public string memoryMeaning="Unity total allocated CPU memory; not VRAM";
        public string gpuTimingMeaning="FrameTimingManager samples; -1 means unavailable";
        public float frameBudgetMs,placementErrorMm;
    }
    public sealed class ExperimentRecorder : MonoBehaviour
    {
        public float frameBudgetMs=13.8889f,warmupSeconds=2;
        public bool Recording {get;private set;}
        public string LastPath {get;private set;}="";
        public ExperimentSummary Summary {get;private set;}
        readonly List<double> frames=new();
        readonly FrameTiming[] timing=new FrameTiming[1];
        StreamWriter writer;double started,measuredStart;double cpuSum,gpuSum;int cpuCount,gpuCount;
        static string F(double d)=>d.ToString("R",CultureInfo.InvariantCulture);
        public void Begin(TaskKind task,AssignmentPolicy policy,InteractionObject[] objects)
        {
            if(Recording)throw new InvalidOperationException("Trial already running");
            string id=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+"-"+Guid.NewGuid().ToString("N")[..8];
            LastPath=Path.Combine(Application.persistentDataPath,"experiments",id);Directory.CreateDirectory(LastPath);
            Summary=new ExperimentSummary {sessionId=id,startedUtc=DateTime.UtcNow.ToString("O"),task=task.ToString(),policy=policy.ToString(),device=SystemInfo.deviceModel+" / "+SystemInfo.graphicsDeviceName,unityVersion=Application.unityVersion,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,frameBudgetMs=frameBudgetMs,requirementViolations=objects.Count(o=>o.decision==null||!o.decision.feasible),meanCpuFrameMs=-1,meanGpuFrameMs=-1};
            File.WriteAllText(Path.Combine(LastPath,"assignments.json"),JsonUtility.ToJson(new DecisionList{objects=objects.Select(o=>new ObjectSnapshot{id=o.objectId,requirement=o.requirement,decision=o.decision,candidates=o.candidates}).ToArray()},true));
            File.WriteAllText(Path.Combine(LastPath,"consistency.json"),JsonUtility.ToJson(new ConsistencyList{objects=objects.Select(o=>ConsistencyProbe.Measure(o)).ToArray()},true));
            writer=new StreamWriter(Path.Combine(LastPath,"events.jsonl"));frames.Clear();cpuSum=gpuSum=0;cpuCount=gpuCount=0;
            started=Time.realtimeSinceStartupAsDouble;measuredStart=started+warmupSeconds;Recording=true;Event("start","");
        }
        [Serializable] class ObjectSnapshot{public string id;public InteractionRequirement requirement;public AssignmentDecision decision;public RepresentationCandidate[] candidates;}
        [Serializable] class DecisionList{public ObjectSnapshot[] objects;}
        [Serializable] class ConsistencyList{public ConsistencyResult[] objects;}
        [Serializable] class EventRow{public double time;public string type,objectId;public Vector3 position;}
        public void Event(string type,string id,Vector3 position=default)
        {
            if(!Recording)return;
            writer.WriteLine(JsonUtility.ToJson(new EventRow{time=Time.realtimeSinceStartupAsDouble-started,type=type,objectId=id,position=position}));
            switch(type){case "grab":Summary.grabs++;break;case "release":Summary.releases++;break;case "impact":Summary.impacts++;break;case "drop":Summary.drops++;break;}
        }
        void Update()
        {
            if(!Recording)return;FrameTimingManager.CaptureFrameTimings();
            if(Time.realtimeSinceStartupAsDouble<measuredStart)return;
            frames.Add(Time.unscaledDeltaTime*1000);
            if(FrameTimingManager.GetLatestTimings(1,timing)>0){if(timing[0].cpuFrameTime>0){cpuSum+=timing[0].cpuFrameTime;cpuCount++;}if(timing[0].gpuFrameTime>0){gpuSum+=timing[0].gpuFrameTime;gpuCount++;}}
        }
        public static double Percentile(IEnumerable<double> values,double p)
        {
            var sorted=values.OrderBy(v=>v).ToArray();if(sorted.Length==0)return 0;
            double index=(sorted.Length-1)*p;int lo=(int)Math.Floor(index),hi=(int)Math.Ceiling(index);
            return sorted[lo]+(sorted[hi]-sorted[lo])*(index-lo);
        }
        public void End(bool success,string reason,float placementErrorMm=-1)
        {
            if(!Recording)return;Event("end",reason);Recording=false;writer.Dispose();writer=null;
            Summary.success=success;Summary.endReason=reason;Summary.durationSeconds=Time.realtimeSinceStartupAsDouble-started;
            Summary.frames=frames.Count;Summary.p50FrameMs=Percentile(frames,.5);Summary.p95FrameMs=Percentile(frames,.95);Summary.p99FrameMs=Percentile(frames,.99);
            Summary.budgetExceededFraction=frames.Count==0?0:(double)frames.Count(v=>v>frameBudgetMs)/frames.Count;
            Summary.meanCpuFrameMs=cpuCount>0?cpuSum/cpuCount:-1;Summary.meanGpuFrameMs=gpuCount>0?gpuSum/gpuCount:-1;
            Summary.allocatedMemoryBytes=Profiler.GetTotalAllocatedMemoryLong();Summary.placementErrorMm=placementErrorMm;
            File.WriteAllText(Path.Combine(LastPath,"summary.json"),JsonUtility.ToJson(Summary,true));
            File.WriteAllLines(Path.Combine(LastPath,"frames.csv"),new[]{"frame,interval_ms"}.Concat(frames.Select((v,i)=>i+","+F(v))));
        }
        void OnApplicationPause(bool pause){if(pause)End(false,"application paused");}
        void OnDestroy(){End(false,"scene closed");}
    }
}
