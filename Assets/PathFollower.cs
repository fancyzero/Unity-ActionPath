using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFollower : MonoBehaviour {
    public GameObject pathOwner;
    public string pathComponentName;
    public float speed = 1;
    public float startOffset = 0;
    public float moveBeginTime;
    float lastFramePositin = 0;
    
    MasterPath GetPath()
    {
        List<MasterPath> paths = new List<MasterPath>();
        if (pathOwner == null)
            GetComponents(paths);
        else
            pathOwner.GetComponents(paths);
        var found = paths.Find(p => p.pathName == pathComponentName);
        return found;
    }

    // Use this for initialization
    void Start() {
        var path = GetPath();
        if (path != null && (path.cachedSections.Count == 0 || path.cachedSections[0].points.Count == 0))
        {
            path.CacheSections();
            Debug.LogWarning("path not cached , may cause performance problem");
        }
        moveBeginTime = Time.time;
    }

    // Update is called once per frame
    void Update() {
        Vector2 tangent = Vector2.zero;
        float t = speed * (Time.time - moveBeginTime) + startOffset;
        var pos = SampleCachedPath(t, out tangent);

        transform.position = pos;
        transform.rotation = Quaternion.FromToRotation(Vector3.down, tangent);
        foreach(var e in pathOwner.GetComponent<MasterPath>().eventPoints)
        {
            if (e.positionOnPath>=lastFramePositin && e.positionOnPath< (Time.time - moveBeginTime) + startOffset)
            {
                foreach( var evt in e.events)
                {
                    evt.OnTriggered(t,gameObject);
                }
            }
        }
        lastFramePositin = t;
    }


    public Vector2 SampleCachedPath( float t, out Vector2 tangent)
    {
        var path = GetPath();
        if (path == null)
            throw new System.Exception("path is null");
        var ret = path.Sample(t);
        tangent = ret.tangent;
        return ret.pos;

    }



}
