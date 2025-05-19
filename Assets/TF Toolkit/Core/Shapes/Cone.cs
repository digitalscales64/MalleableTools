using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TF_Toolkit
{
    //[System.Serializable]
    //public class Cone : Shape
    //{
    //    public float height;
    //    public float radius;

    //    public override bool IsInside(float x, float y, float z)
    //    {
    //        float max = height / 2;
    //        float min = -height / 2;

    //        if (y > max || y < min)
    //        {
    //            return false;
    //        }

    //        float radiusAtHeight = radius * Mathf.InverseLerp(max, min, y);

    //        return x * x + z * z <= radiusAtHeight * radiusAtHeight;
    //    }
    //    public override Vector3 ClosestPointOnSurface(float x, float y, float z)
    //    {
    //        float max = height / 2;
    //        float min = -height / 2;

    //        if (y <= min)
    //        {
    //            //its on the base
    //            if (!IsInsideBase2DRadius(vertex))
    //            {
    //                vertex.y = 0;
    //                vertex = vertex.normalized * radius;
    //            }
    //            vertex.y = min;
    //            return vertex;
    //        }

    //        //Project to 2d and solve closest point on line

        
    //        //        O
    //        //   P   /
    //        //      /
    //        //     /
    //        //    /   P
    //        //   O

        

    //        Vector2 topPoint = new Vector2(0, max);
    //        Vector2 bottomnPoint = new Vector2(radius, min);

    //        Vector2 P = new Vector2(Mathf.Abs(x), y);

    //        Vector2 closestPoint = ClosestPointOnLine(bottomnPoint, topPoint, P);

    //        if (closestPoint.magnitude > y - min)
    //        {
    //            //The point is closer to the bottomn.
    //            vertex.y = min;


    //            return vertex;
    //        }

    //        float closestHeight = closestPoint.y;
    //        float radiusAtClosestHeight = closestPoint.x;
    //        vertex.y = 0;
    //        vertex = vertex.normalized * radiusAtClosestHeight;
    //        vertex.y = closestHeight;
    //        return vertex;
    //    }

    //    public static Vector2 ClosestPointOnLine(Vector2 p1, Vector2 p2, Vector2 p)
    //    {
    //        //chatgpt

    //        Vector2 lineDirection = p2 - p1;
    //        Vector2 lineToPoint = p - p1;
    //        float t = Vector2.Dot(lineToPoint, lineDirection) / lineDirection.sqrMagnitude;
    //        return p1 + Mathf.Clamp01(t) * lineDirection;
    //    }

    //    bool IsInsideBase2DRadius(Vector3 vertex)
    //    {
    //        vertex.y = 0;
    //        vertex.Normalize();
    //        return vertex.magnitude < radius;
    //    }
    //}
}