using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Cylinder : Shape
    {
        public float height;
        public float radius;

        public override bool IsInside(float x, float y, float z)
        {
            float top = height / 2;
            float bottomn = -top;

            if (y > top || y < bottomn)
            {
                return false;
            }
            return x * x + z * z <= radius * radius;
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            float xxzz = x * x + z * z;

            float top = height / 2;
            float bottomn = -top;

            bool isWithinRadius = xxzz <= radius * radius;
            bool isWithinHeight = y <= top && y >= bottomn;

            float multiplier;

            if (isWithinHeight && isWithinRadius)
            {
                //The point is inside
                float xxzzLength = Mathf.Sqrt(xxzz);

                float distanceY = top - Mathf.Abs(y);
                float distanceXZ = radius - xxzzLength;

                if (distanceXZ < distanceY)
                {
                    multiplier = radius / xxzzLength;
                    return new Vector3(
                        x * multiplier,
                        y,
                        z * multiplier
                    );
                }
                else
                {
                    if (y > 0)
                    {
                        return new Vector3(x, top, z);
                    }
                    else
                    {
                        return new Vector3(x, bottomn, z);
                    }
                }
            }

            //The point is outside
      
            if (isWithinHeight)
            {
                multiplier = radius / Mathf.Sqrt(xxzz);
    
                return new Vector3(
                    x * multiplier,
                    y,
                    z * multiplier
                );
            }

            if (y > top)
            {
                y = top;
            }
            else
            {
                y = bottomn;
            }


            if (isWithinRadius)
            {
                return new Vector3(x, y, z);
            }
         
            multiplier = radius / Mathf.Sqrt(xxzz);

            return new Vector3(
                x * multiplier,
                y,
                z * multiplier
            );
        }

        public override float DistanceToShape(float x, float y, float z)
        {
            float xxzz = x * x + z * z;

            float top = height / 2;

            float distY = Mathf.Abs(y) - top;

            bool isWithinRadius = xxzz <= radius * radius;
            bool isWithinHeight = distY < 0;

            if (isWithinHeight && isWithinRadius)
            {
                //The point is inside
                return Mathf.Max(
                    Mathf.Sqrt(xxzz) - radius,
                    distY
                );
            }

            //The point is outside
            if (isWithinHeight) return Mathf.Sqrt(xxzz) - radius;
            if (isWithinRadius) return distY;
      
            return Mathf.Sqrt(xxzz + distY * distY) - radius;
        }
    }
}
