using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace WorldInteraction.Tests
{
    public class InteractionTests
    {
        [UnitySetUp] public IEnumerator Setup(){SceneManager.LoadScene("InteractionLab");yield return null;yield return null;}
        [UnityTest] public IEnumerator RepresentationKeepsPoseAndBlocksWhileHeld()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExperimentSession>();var obj=session.Target;
            yield return new WaitForFixedUpdate();Vector3 p=obj.transform.position;
            Assert.That(obj.BeginHold(),Is.True);Assert.Throws<InvalidOperationException>(()=>obj.Apply(new AssignmentDecision{representation=Representation.Mesh}));obj.EndHold();
            obj.Apply(new AssignmentDecision{representation=Representation.Mesh,feasible=true});Assert.That(Vector3.Distance(p,obj.transform.position),Is.LessThan(.0001f));
            Assert.That(obj.accurateColliders.All(c=>c.enabled),Is.True);Assert.That(obj.proxyColliders.All(c=>!c.enabled),Is.True);
        }
        [UnityTest] public IEnumerator TaskAndPolicyStayUnchangedDuringGrab()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExperimentSession>();var task=session.task;var policy=session.policy;
            Assert.That(session.Target.BeginHold(),Is.True);session.NextTask();session.NextPolicy();
            Assert.That(session.task,Is.EqualTo(task));Assert.That(session.policy,Is.EqualTo(policy));session.Target.EndHold();yield return null;
        }
        [UnityTest] public IEnumerator GravityRespectsSupportAndLogsTrial()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExperimentSession>();Assert.That(session.Begin(),Is.True);
            yield return new WaitForSeconds(.8f);
            Assert.That(session.Target.transform.position.y,Is.GreaterThan(.82f));Assert.That(session.Target.transform.position.y,Is.LessThan(.94f));
            session.recorder.End(false,"test finish");Assert.That(File.Exists(Path.Combine(session.recorder.LastPath,"summary.json")),Is.True);
            Assert.That(File.Exists(Path.Combine(session.recorder.LastPath,"assignments.json")),Is.True);
        }
        [UnityTest] public IEnumerator PhysicsGrabUsesJointAndRelease()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExperimentSession>();
            var go=new GameObject("test hand");go.transform.position=session.Target.transform.position-Vector3.forward*.5f;
            var grab=go.AddComponent<PhysicsGrabber>();grab.TryGrab();Assert.That(grab.Held,Is.EqualTo(session.Target));Assert.That(session.Target.GetComponent<ConfigurableJoint>(),Is.Not.Null);
            grab.Release();yield return null;Assert.That(session.Target.held,Is.False);UnityEngine.Object.Destroy(go);
        }
        [UnityTest] public IEnumerator SceneGSCanBeReplacedByMeshWithoutLosingObjects()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExperimentSession>();session.task=TaskKind.Observe;session.policy=AssignmentPolicy.SceneGS;
            Assert.That(session.Apply(),Is.True);yield return null;Assert.That(GameObject.Find("Scene-level GS baseline"),Is.Not.Null);
            session.policy=AssignmentPolicy.MeshOnly;Assert.That(session.Apply(),Is.True);yield return null;Assert.That(session.objects.All(o=>o.meshVisual.activeSelf),Is.True);
        }
        [UnityTest] public IEnumerator StablePlacementCompletesTask()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExperimentSession>();session.task=TaskKind.PrecisionPlace;session.policy=AssignmentPolicy.RequirementBased;Assert.That(session.Begin(),Is.True);
            var target=session.Target;Assert.That(target.BeginHold(),Is.True);target.EndHold();target.Body.position=session.destination.position;target.Body.linearVelocity=Vector3.zero;
            yield return new WaitForSeconds(2);
            Assert.That(session.recorder.Recording,Is.False);Assert.That(session.recorder.Summary.success,Is.True);
        }
        [UnityTest] public IEnumerator GaussianRendersAndSavePreview()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExperimentSession>();session.policy=AssignmentPolicy.UniformHybrid;session.Apply();
            yield return new WaitForSeconds(.3f);
            var rt=new RenderTexture(1280,720,24);session.view.targetTexture=rt;session.view.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            var pixels=image.GetPixels32();Assert.That(pixels.Count(p=>p.g>p.r*1.4f&&p.g>80),Is.GreaterThan(10),"Green Gaussian target should be visible");
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../results"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"unity-preview.png"),image.EncodeToPNG());
            session.view.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);
        }
    }
}
