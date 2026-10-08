using System;
using UnityEngine;

namespace WorldInteraction
{
    [CreateAssetMenu(menuName="World Interaction/Task Requirement Profile")]
    public sealed class TaskProfile : ScriptableObject
    {
        public TaskKind task;
        public string description;
        public ObjectRequirement[] objects=Array.Empty<ObjectRequirement>();
        public InteractionRequirement Resolve(InteractionObject obj)
        {
            foreach(var entry in objects)if(entry.objectId==obj.objectId)return entry.requirement.Copy();
            return AssignmentEngine.ForTask(task,obj.target,obj.support);
        }
    }
    [Serializable] public class ObjectRequirement {public string objectId;public InteractionRequirement requirement=new();}
}
