using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Sphere : Shape
    {
        public float radius;
 
        public override bool IsInside(float x, float y, float z)
        {
            return radius * radius <= x * x + y * y + z * z;
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            float length = Mathf.Sqrt(x * x + y * y + z * z);
            float multiplier = radius / length;
            return new Vector3(
                x * multiplier,
                y * multiplier,
                z * multiplier
            );
        }
        public override float DistanceToShape(float x, float y, float z)
        {
            return Mathf.Sqrt(x * x + y * y + z * z) - radius;
        }
    }
}


