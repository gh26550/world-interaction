using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldInteraction
{
    public enum Representation { Mesh, GS, Hybrid }
    public enum TaskKind { Observe, Transport, PrecisionPlace }
    public enum AssignmentPolicy { RequirementBased, MeshOnly, MeshLOD, UniformHybrid, CategoryDistance, ManualHybrid, AggressiveGS, SceneGS }

    [Serializable]
    public class InteractionRequirement
    {
        public bool collision, grasp, move, articulation, revealHidden;
        [Min(0)] public float maxContactErrorMm = 20;
        [Min(0)] public float minViewDistance = 1;
        [Range(0, 1)] public float minVisualQuality = .65f;
        public bool NeedsPhysics => collision || grasp || move || articulation;
        public InteractionRequirement Copy() => (InteractionRequirement)MemberwiseClone();
    }

    [Serializable]
    public class RepresentationCandidate
    {
        public Representation representation;
        public bool available = true, collision = true, grasp = true, move = true, articulation;
        public bool hiddenSurfacesComplete = true, qualityVerified = true;
        [Min(0)] public float contactErrorMm;
        [Range(0,1)] public float visualQuality = 1;
        [Min(0)] public float validatedMinDistance = .15f;
        [Min(0)] public float cost = 1;
        public string costSource = "heuristic—not a measured runtime";
        public string[] Violations(InteractionRequirement r)
        {
            var errors = new List<string>();
            if (!available) errors.Add("asset unavailable");
            if (!qualityVerified) errors.Add("quality/contact calibration unverified");
            if (r.collision && !collision) errors.Add("collision required");
            if (r.grasp && !grasp) errors.Add("grasp required");
            if (r.move && !move) errors.Add("motion required");
            if (r.articulation && !articulation) errors.Add("articulation required");
            if (r.NeedsPhysics && contactErrorMm > r.maxContactErrorMm) errors.Add("contact error exceeds tolerance");
            if (r.revealHidden && !hiddenSurfacesComplete) errors.Add("hidden surfaces incomplete");
            if (visualQuality < r.minVisualQuality || validatedMinDistance > r.minViewDistance) errors.Add("view quality/distance unmet");
            return errors.ToArray();
        }
    }

    [Serializable]
    public class AssignmentDecision
    {
        public string objectId;
        public Representation representation;
        public bool feasible;
        public string reason;
        public string[] violations;
        public float cost;
        public string costSource;
    }

    public static class AssignmentEngine
    {
        public static AssignmentDecision Choose(string id, IEnumerable<RepresentationCandidate> candidates,
            InteractionRequirement requirement, AssignmentPolicy policy, Representation manual = Representation.Mesh,
            string category = "prop", float distance = 1)
        {
            var all = candidates.Where(c => c != null && c.available).ToArray();
            if (all.Length == 0) return new AssignmentDecision { objectId=id, feasible=false, reason="No available representation", violations=new[]{"asset unavailable"} };
            Representation wanted = policy switch {
                AssignmentPolicy.UniformHybrid => Representation.Hybrid,
                AssignmentPolicy.ManualHybrid => manual,
                AssignmentPolicy.CategoryDistance => category == "structure" || distance < .6f ? Representation.Mesh : Representation.GS,
                AssignmentPolicy.AggressiveGS or AssignmentPolicy.SceneGS => Representation.GS,
                _ => Representation.Mesh
            };
            var candidate = policy == AssignmentPolicy.RequirementBased
                ? all.Where(c => c.Violations(requirement).Length == 0).OrderBy(c => c.cost).ThenBy(c => c.representation).FirstOrDefault()
                : all.FirstOrDefault(c => c.representation == wanted);
            bool unavailable = candidate == null;
            candidate ??= all.FirstOrDefault(c=>c.representation==Representation.Mesh) ?? all[0];
            var violations = candidate.Violations(requirement).ToList();
            if (unavailable) violations.Add(policy == AssignmentPolicy.RequirementBased ? "No candidate meets all requirements" : "Requested baseline asset unavailable");
            return new AssignmentDecision {
                objectId=id, representation=candidate.representation, feasible=violations.Count==0,
                violations=violations.ToArray(), cost=candidate.cost, costSource=candidate.costSource,
                reason=violations.Count==0 ? (policy==AssignmentPolicy.RequirementBased ? "Lowest declared cost among feasible candidates" : "Baseline/manual assignment") : string.Join("; ",violations)
            };
        }

        public static InteractionRequirement ForTask(TaskKind task, bool target, bool support)
        {
            var r = new InteractionRequirement();
            if (task == TaskKind.Observe) { r.minViewDistance = target ? .3f : 1; return r; }
            r.collision = target || support;
            if (target) { r.grasp=true; r.move=true; r.revealHidden=true; r.minViewDistance=.25f; }
            r.maxContactErrorMm = task==TaskKind.PrecisionPlace && (target || support) ? 2 : 20;
            return r;
        }
    }
}
