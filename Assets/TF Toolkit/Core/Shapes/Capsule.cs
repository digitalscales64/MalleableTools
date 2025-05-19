using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Capsule : Shape
    {
        public float height;
        public float radius;

        public override bool IsInside(float x, float y, float z)
        {
            float top = height / 2;
            float bottomn = -height / 2;

            if (y >= top)
            {
                y -= top;
                return x * x + y * y + z * z < radius * radius;
            }
            else if (y <= bottomn)
            {
                y -= bottomn;
                return x * x + y * y + z * z < radius * radius;
            }
            else
            {
                //between
                return x * x + z * z < radius * radius;
            }
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            float top = height / 2;
            float bottomn = -top;
            float multiplier;
            float xxzz = x * x + z * z;

            if (y > top)
            {
                y -= top;
                multiplier = radius / Mathf.Sqrt(xxzz + y * y);
                y = y * multiplier + top;
            }
            else if (y < bottomn)
            {
                y -= bottomn;
                multiplier = radius / Mathf.Sqrt(xxzz + y * y);
                y = y * multiplier + bottomn;
            }
            else
            {
                //between
                multiplier = radius / Mathf.Sqrt(xxzz);
                
            }
            x *= multiplier;
            z *= multiplier;
            return new Vector3(x, y, z);
        }

        public override float DistanceToShape(float x, float y, float z)
        {
            float top = height / 2;
            float bottomn = -top;

            float xxzz = x * x + z * z;

            if (y > top)
            {
                y -= top;
                return Mathf.Sqrt(xxzz + y * y) - radius;
            }
            else if (y < bottomn)
            {
                y -= bottomn;
                return Mathf.Sqrt(xxzz + y * y) - radius;
            }
            else
            {
                //between
                return Mathf.Sqrt(xxzz) - radius;
            }
        }
    }
}