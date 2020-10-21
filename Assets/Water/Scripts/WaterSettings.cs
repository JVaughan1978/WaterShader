using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FEMA_AR.WATER;

namespace FEMA_AR
{
    public class WaterSettings : MonoBehaviour
    {
        public GameObject waterRoot;
        public WaveSpectrum waveSpectrum;
        public Material waterMaterial;
        public float tweenTime = 1.0f;

        [Range(0f, 1f)]
        public float percent = 0.75f;
        private float _percent = -1f;

        [Serializable]
        public struct WaterLevel
        {
            public string name; //name for the Inspector
            public float percent; //percent this corresponds to from UI
            public Vector3 waterPos; //height relative to camera
            public float wspec_weight; //
            public float wspec_chop;
            public float wspec_windSpeed;
            public float wshad_drawDist;
            public float wshad_alpha;
        };
        public WaterLevel[] waterLevels;
        public WaterLevel _waterLevel;

        private void OnEnable()
        {
            //DragFloodMarker.OnDragged += SetPercent;
        }

        private void OnDisable()
        {
            //DragFloodMarker.OnDragged -= SetPercent;
        }

        void SetPercent(float newPercent)
        {
            percent = newPercent;
        }

        void Start()
        {
            WaterLevel startLevel = waterLevels[0];
            //AnimateTo(startLevel, 0f);
        }

        void Update()
        {
            if (waterRoot == null || waveSpectrum == null || waterMaterial == null)
                return;

            if (percent != _percent)
            {
                WaterLevel nextLevel = _waterLevel;
                for (int i = 0; i < waterLevels.Length; i++)
                {
                    if (Mathf.Approximately(percent, waterLevels[i].percent))
                    {
                        nextLevel = waterLevels[i];
                    }
                }
                //AnimateTo(nextLevel, tweenTime);
                _percent = percent;
            }
        }
            /*
            if (_animating)
            {
                waterRoot.transform.localPosition = _waterLevel.waterPos;
                waveSpectrum.weight = _waterLevel.wspec_weight;
                waveSpectrum.chop = _waterLevel.wspec_chop;
                waveSpectrum.windSpeed = _waterLevel.wspec_windSpeed;
                Color c = waterMaterial.GetColor("_Diffuse");
                c.a = _waterLevel.wshad_alpha;
                waterMaterial.SetColor("_Diffuse", c);
                waterMaterial.SetFloat("_Distance", _waterLevel.wshad_drawDist);
            }
        }
        bool _animating = false;
        void AnimateTo(WaterLevel waterLevel, float speed = 0f)
        {
            if (_animating)
            {
                LeanTween.cancel(gameObject);
            }

            WaterLevel _tweenStart = _waterLevel;
            LeanTween.value(gameObject, 0f, 1f, speed)
            .setEase(LeanTweenType.easeOutCubic)
            .setOnStart(() =>
            {
                _animating = true;
            })
            .setOnUpdate((float val) =>
            {
                _waterLevel.percent = Mathf.Lerp(_tweenStart.percent, waterLevel.percent, val);
                _waterLevel.waterPos = Vector3.Lerp(_tweenStart.waterPos, waterLevel.waterPos, val);
                _waterLevel.wspec_weight = Mathf.Lerp(_tweenStart.wspec_weight, waterLevel.wspec_weight, val);
                _waterLevel.wspec_chop = Mathf.Lerp(_tweenStart.wspec_chop, waterLevel.wspec_chop, val);
                _waterLevel.wspec_windSpeed = Mathf.Lerp(_tweenStart.wspec_windSpeed, waterLevel.wspec_windSpeed, val);
                _waterLevel.wshad_drawDist = Mathf.Lerp(_tweenStart.wshad_drawDist, waterLevel.wshad_drawDist, val);
                _waterLevel.wshad_alpha = Mathf.Lerp(_tweenStart.wshad_alpha, waterLevel.wshad_alpha, val);
            })
            .setOnComplete(() =>
            {
                _animating = false;
            });
        }
        */
    }
}
