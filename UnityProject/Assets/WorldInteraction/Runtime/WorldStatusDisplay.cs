using UnityEngine;
namespace WorldInteraction
{
    [RequireComponent(typeof(TextMesh))]
    public sealed class WorldStatusDisplay : MonoBehaviour
    {
        public ExperimentSession session;float nextUpdate;
        void Update()
        {
            if(!session||Time.unscaledTime<nextUpdate)return;nextUpdate=Time.unscaledTime+.2f;
            GetComponent<TextMesh>().text="WORLD INTERACTION\n"+session.task+" / "+session.policy+"\n"+session.status+"\nGrip: grab   A: trial   B: task   X: policy";
        }
    }
}
