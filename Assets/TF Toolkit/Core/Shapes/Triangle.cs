using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    [System.Serializable]
    public class Triangle : Shape
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public override bool IsInside(float x, float y, float z)
        {
            return false;
        }
        public override Vector3 ClosestPointOnSurface(float x, float y, float z)
        {
            Vector3 point = new Vector3(x, y, z);

            // Vectors from a to b and a to c
            Vector3 ab = b - a;
            Vector3 ac = c - a;

            // Vector from a to the point
            Vector3 ap = point - a;

            // Check if the point is in the vertex region outside A
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            // Vector from b to the point
            Vector3 bp = point - b;

            // Check if the point is in the vertex region outside B
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            // Check if the point is in edge region of AB, and if so return projection of point onto AB
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return a + ab * v;
            }

            // Vector from c to the point
            Vector3 cp = point - c;

            // Check if the point is in the vertex region outside C
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            // Check if the point is in edge region of AC, and if so return projection of point onto AC
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return a + ac * w;
            }

            // Check if the point is in edge region of BC, and if so return projection of point onto BC
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return b + (c - b) * w;
            }

            // The point is inside the face region. Compute Q through its barycentric coordinates (u, v, w)
            float denom = 1.0f / (va + vb + vc);
            float v2 = vb * denom;
            float w2 = vc * denom;

            return a + ab * v2 + ac * w2;
        }
    }
}