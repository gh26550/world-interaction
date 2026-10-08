using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WorldInteraction.Tests
{
    public class AssignmentTests
    {
        RepresentationCandidate[] Candidates()=>new[]{
            new RepresentationCandidate{representation=Representation.Mesh,cost=3,contactErrorMm=0},
            new RepresentationCandidate{representation=Representation.Hybrid,cost=2,contactErrorMm=6},
            new RepresentationCandidate{representation=Representation.GS,cost=1,collision=false,grasp=false,move=false}
        };
        [TestCase(TaskKind.Observe,Representation.GS)]
        [TestCase(TaskKind.Transport,Representation.Hybrid)]
        [TestCase(TaskKind.PrecisionPlace,Representation.Mesh)]
        public void SameObjectGetsDifferentRepresentationForTask(TaskKind task,Representation expected)
        {
            var d=AssignmentEngine.Choose("cup",Candidates(),AssignmentEngine.ForTask(task,true,false),AssignmentPolicy.RequirementBased);
            Assert.That(d.feasible,Is.True);Assert.That(d.representation,Is.EqualTo(expected));
        }
        [Test] public void SupportsInheritContactRequirement(){Assert.That(AssignmentEngine.ForTask(TaskKind.PrecisionPlace,false,true).maxContactErrorMm,Is.EqualTo(2));}
        [Test] public void BaselineViolationsAreNotSilentlyRepaired(){var d=AssignmentEngine.Choose("cup",Candidates(),AssignmentEngine.ForTask(TaskKind.Transport,true,false),AssignmentPolicy.AggressiveGS);Assert.That(d.representation,Is.EqualTo(Representation.GS));Assert.That(d.feasible,Is.False);}
        [Test] public void UnknownCalibrationIsNotFeasible(){var candidates=Candidates();foreach(var c in candidates)c.qualityVerified=false;Assert.That(AssignmentEngine.Choose("x",candidates,new(),AssignmentPolicy.RequirementBased).feasible,Is.False);}
        [Test] public void MissingHiddenSurfaceRejectsMovableCandidate(){var c=Candidates()[1];c.hiddenSurfacesComplete=false;Assert.That(c.Violations(AssignmentEngine.ForTask(TaskKind.Transport,true,false)),Does.Contain("hidden surfaces incomplete"));}
        [Test] public void MissingAssetDoesNotReportSuccess(){Assert.That(AssignmentEngine.Choose("x",Array.Empty<RepresentationCandidate>(),new(),AssignmentPolicy.MeshOnly).feasible,Is.False);}
        [Test] public void TiesAreDeterministic(){var c=Candidates();foreach(var item in c)item.cost=1;Assert.That(AssignmentEngine.Choose("x",c,new(),AssignmentPolicy.RequirementBased).representation,Is.EqualTo(Representation.Mesh));}
        [Test] public void InvalidGaussianFilesRejected(){Assert.Throws<InvalidDataException>(()=>GaussianAsset.Read(new byte[]{87,73,71,49,255,255,255,127}));}
        [Test] public void GaussianRoundTripPreservesAnisotropy()
        {
            using var ms=new MemoryStream();using var w=new BinaryWriter(ms);w.Write(new[]{'W','I','G','1'});w.Write(1);
            foreach(float f in new[]{1f,2,3,.01f,.02f,.03f,0,0,0,1,.2f,.3f,.4f,.8f})w.Write(f);
            var g=GaussianAsset.Read(ms.ToArray())[0];Assert.That(g.scale,Is.EqualTo(new Vector3(.01f,.02f,.03f)));Assert.That(g.color.a,Is.EqualTo(.8f));
        }
        [Test] public void PercentilesUseSortedInterpolation(){Assert.That(ExperimentRecorder.Percentile(new double[]{40,10,30,20},.5),Is.EqualTo(25));}
    }
}
