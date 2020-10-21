using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FEMA_AR
{
    [RequireComponent(typeof(Camera))]
    public class ReflFalloff : MonoBehaviour
    {
        public Material waterMat;
        void Update()
        {
            float falloff = 0f;
            Vector3 camDirection = transform.InverseTransformDirection(Vector3.forward);
            if (camDirection.y > -0.5f && camDirection.y < 0.5f)
            {
                falloff = Mathf.Cos((camDirection.y * 180f) * Mathf.Deg2Rad);
            }
            waterMat.SetFloat("_RefractionFalloff", falloff);
        }
    }
}
