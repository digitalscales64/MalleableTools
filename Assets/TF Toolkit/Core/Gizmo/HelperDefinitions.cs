using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelperDefinitions : ScriptableObject
{
    public Material OpaqueRed;
    public Material OpaqueGreen;
    public Material OpaqueBlue;
    public Material TransparentRed;
    public Material TransparentGreen;
    public Material TransparentBlue;

    public Mesh Capsule;
    public Mesh Circle;
    public Mesh Cone;
    public Mesh Cube;
    public Mesh Cylinder;
    public Mesh CylinderWithoutCaps;
    public Mesh HalfSphereWithoutCap;
    public Mesh Plane;
    public Mesh Sphere;







    private static HelperDefinitions _instance;
    public static HelperDefinitions instance { 
        get {
            if (_instance == null)
            {
#if UNITY_PIPELINE_HDRP
                _instance = Resources.Load<HelperDefinitions>("TF_Toolklit helpers HDRP");
#elif UNITY_PIPELINE_URP
                _instance = Resources.Load<HelperDefinitions>("TF_Toolklit helpers URP");
#else
                _instance = Resources.Load<HelperDefinitions>("TF_Toolklit helpers Standard");
#endif
            }
            return _instance;
        } 
        private set 
        { 
        }
    }
}
