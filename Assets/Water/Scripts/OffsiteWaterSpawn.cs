using UnityEngine;

namespace FEMA_AR
{
    public class OffsiteWaterSpawn : MonoBehaviour
    {
        public Transform waterCamera = null;
        public GameObject waterPrefab = null;
        private Material waterMat = null;
        public GameObject planePrefab = null;

        void Start()
        {
            waterPrefab.SetActive(true);
            var waterRenderer = waterPrefab.GetComponent<FEMA_AR.WATER.WaterRenderer>();
            waterRenderer.arCamera = waterCamera.GetComponent<Camera>();
            waterMat = waterRenderer.waterMaterial;
            planePrefab.SetActive(true);
        }

        void Update(){}
    }
}