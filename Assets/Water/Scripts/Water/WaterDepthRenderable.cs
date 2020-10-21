using UnityEngine;

namespace FEMA_AR.WATER
{
    //Class to tag geometry to be rendered as "water bottom" e.g. riverbeds, or Water floors
    public class WaterDepthRenderable : MonoBehaviour
    {
        public delegate void WaterDepthEvent(WaterDepthRenderable wdr);
        public static event WaterDepthEvent OnUpdated;

        private void Start()
        {
            if (OnUpdated != null)
                OnUpdated(this);
        }

        void OnEnable()
        {
            if (OnUpdated != null)
                OnUpdated(this);

        }
        void OnDisable()
        {
            if (OnUpdated != null)
                OnUpdated(this);
        }

        //TODO: make this an updateable or cache-able structure...
    }
}
