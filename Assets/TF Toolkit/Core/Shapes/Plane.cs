using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Plane : Shape
    {
        public Vector3 point;
        public Vector3 normal;

        //behind is considered inside
        public override bool IsInside(float x, float y, float z)
        {
            return DistanceToShape(x, y, z) <= 0;
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            float distance = DistanceToShape(x, y, z);

            return new Vector3(
                x - distance * normal.x,
                y - distance * normal.y,
                z - distance * normal.z
            );
        }

        public override float DistanceToShape(float x, float y, float z)
        {
            return (x - point.x) * normal.x + (y - point.y) * normal.y + (z - point.z) * normal.z;
        }
    }
}