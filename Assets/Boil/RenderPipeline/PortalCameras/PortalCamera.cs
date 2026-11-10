using System;
using UnityEngine;

namespace Boil.RenderPipeline.PortalCameras {
    public class PortalCamera : MonoBehaviour {
        public Transform linkedCameraTransform;
        public Transform mirrorTransform;
        //public float planeOffset = 0.0f; // можно смещать отражённую позицию вдоль нормали
        public Vector3 offset;
        
        void LateUpdate() {
            if (linkedCameraTransform == null || mirrorTransform == null)
                return;

            transform.position = linkedCameraTransform.position + offset;
            transform.rotation = linkedCameraTransform.rotation;
            
            /*Vector3 planePoint = mirrorTransform.position;
            Vector3 planeNormal = mirrorTransform.forward.normalized; // Z направлен к наблюдателю в вашем описании

            // плоскость в виде (a,b,c,d)
            float d = -Vector3.Dot(planeNormal, planePoint);
            Vector4 plane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, d);

            // матрица отражения
            Matrix4x4 reflectionMat = Matrix4x4.identity;
            CalculateReflectionMatrix(ref reflectionMat, plane);

            // отражённая позиция и базис (директория и ап)
            Vector3 camPos = linkedCameraTransform.position;
            Vector3 reflectedPos = reflectionMat.MultiplyPoint(camPos) + planeNormal * planeOffset;

            Vector3 camForward = linkedCameraTransform.forward;
            Vector3 camUp = linkedCameraTransform.up;
            Vector3 reflectedForward = reflectionMat.MultiplyVector(camForward);
            Vector3 reflectedUp = reflectionMat.MultiplyVector(camUp);

            // нормализация и защита от вырожденных случаев
            if (reflectedForward.sqrMagnitude < 1e-6f) reflectedForward = -mirrorTransform.forward;
            if (reflectedUp.sqrMagnitude < 1e-6f) reflectedUp = mirrorTransform.up;

            transform.position = reflectedPos;
            transform.rotation = Quaternion.LookRotation(reflectedForward.normalized, reflectedUp.normalized);*/
        }

        // Классический расчёт матрицы отражения по плоскости
        static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane) {
            reflectionMat.m00 = 1f - 2f * plane.x * plane.x;
            reflectionMat.m01 = -2f * plane.x * plane.y;
            reflectionMat.m02 = -2f * plane.x * plane.z;
            reflectionMat.m03 = -2f * plane.x * plane.w;

            reflectionMat.m10 = -2f * plane.y * plane.x;
            reflectionMat.m11 = 1f - 2f * plane.y * plane.y;
            reflectionMat.m12 = -2f * plane.y * plane.z;
            reflectionMat.m13 = -2f * plane.y * plane.w;

            reflectionMat.m20 = -2f * plane.z * plane.x;
            reflectionMat.m21 = -2f * plane.z * plane.y;
            reflectionMat.m22 = 1f - 2f * plane.z * plane.z;
            reflectionMat.m23 = -2f * plane.z * plane.w;

            reflectionMat.m30 = 0f;
            reflectionMat.m31 = 0f;
            reflectionMat.m32 = 0f;
            reflectionMat.m33 = 1f;
        }
    }
}