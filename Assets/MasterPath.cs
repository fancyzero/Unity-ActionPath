using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[CreateAssetMenu(menuName = "PixelFighter2/Create MasterPath")]

[System.Serializable]
public class MasterPath :MonoBehaviour{

    public enum SampleSpace
    {
        local,
        world,
    }
    public bool looped = true;
    public bool worldSpace = false;
    public float simplifyTorrence = 0.999f;
    static int sectionSamples = 100;
    public string pathName = "haha";
    public enum ContorlPointType
    {
        CurveControl,
        Action,
    }
    [System.Serializable]
    public class ControlPoint
    {
        public Vector2 pos;
        public Vector2 inTangent;
        public Vector2 outTangent;
        public ContorlPointType type;
    }
    [System.Serializable]
    public class EventPoint
    {
        public float positionOnPath;//length from begining
        public Vector3 cachedPosition;
        [SerializeField]
        public List<EventBase> events = new List<EventBase>();
    }
    [System.Serializable]
    public class PathPoint
    {
        public int action;
        public Vector2 pos;
        public float length;
    }

    [System.Serializable]
    public class CachedSection
    {
        [SerializeField]
        public List<PathPoint> points = new List<PathPoint>();
        public float StartingLength()
        {
            if (points.Count == 0)
                return 0;
            return points[0].length;
        }
        public float EndingLength()
        {
            if (points.Count == 0)
                return 0;
            return points[points.Count - 1].length;
        }
    }
    [SerializeField]
    public List<EventPoint> eventPoints = new List<EventPoint>();
    [SerializeField]
    protected List<ControlPoint> controlPoints = new List<ControlPoint>();
    [SerializeField]
    public List<CachedSection> cachedSections = new List<CachedSection>();

    struct ProjectToSegmentResult
    {
        public int segmentIndex;
        public float length;
        public Vector3 projectedPos;
    }


    public Vector3 LocalToWorld(Vector3 pos)
    {
        if (worldSpace)
            return pos;
        else
            return transform.TransformPoint(pos);

    }
    public Vector3 WorldToLocal( Vector3 pos)
    {
        if (worldSpace)
            return pos;
        else
            return transform.InverseTransformPoint(pos);
    }

    public void ClearControlPoints()
    {
        controlPoints.Clear();
        CacheSections();
    }

    public void ClearEventPoints()
    {
        eventPoints.Clear();
        foreach( var cmp in GetComponents<EventBase>())
        {
            DestroyImmediate(cmp);
        }
    }
    

    public int GetControlPointsCount()
    {
        return controlPoints.Count;
    }
    public ControlPoint GetControlPoint(int index, SampleSpace space = SampleSpace.local)
    {
        var p = controlPoints[index];
        var cp = new ControlPoint();
        cp.pos = LocalToWorld(p.pos);
        cp.inTangent = LocalToWorld(p.inTangent);
        cp.outTangent = LocalToWorld(p.outTangent);
        cp.type = p.type;
        return cp;
    }

    public void SetControlPointPosition(int index, Vector3 pos, SampleSpace space = SampleSpace.local)
    {
        if (space == SampleSpace.world && !worldSpace)
        {
            var posToIntan = controlPoints[index].inTangent - controlPoints[index].pos;
            var posToOuttan = controlPoints[index].outTangent - controlPoints[index].pos ;
            controlPoints[index].pos = WorldToLocal(pos);
            controlPoints[index].inTangent = controlPoints[index].pos + posToIntan;
            controlPoints[index].outTangent = controlPoints[index].pos + posToOuttan;
        }
        else
        {
            var posToIntan = controlPoints[index].inTangent - controlPoints[index].pos ;
            var posToOuttan = controlPoints[index].outTangent - controlPoints[index].pos ;
            controlPoints[index].pos = pos;
            controlPoints[index].inTangent = controlPoints[index].pos + posToIntan;
            controlPoints[index].outTangent = controlPoints[index].pos + posToOuttan;
        }
    }

    public void SetControlPointInTangent(int index, Vector3 pos, SampleSpace space = SampleSpace.local)
    {
        if (space == SampleSpace.world && !worldSpace)
            controlPoints[index].inTangent = WorldToLocal(pos);
        else
            controlPoints[index].inTangent = pos;
    }

    public void SetControlPointOutTangent(int index, Vector3 pos, SampleSpace space = SampleSpace.local)
    {
        if (space == SampleSpace.world && !worldSpace)
            controlPoints[index].outTangent = WorldToLocal(pos);
        else
            controlPoints[index].outTangent = pos;
    }

    public Vector2 SamplePoint(int a, int b, float t, SampleSpace space = SampleSpace.local)
    {
        var p0 = controlPoints[a];
        var p1 = controlPoints[b];
        float t_3 = t * t * t;
        float t_2 = t * t;
        int noInTangent = controlPoints[a].type == ContorlPointType.CurveControl ? 1 : 0;
        int noOutTangent = controlPoints[b].type == ContorlPointType.CurveControl ? 1 : 0;

        var ret =  (2 * t_3 - 3 * t_2 + 1) * p0.pos + 
            (t_3 - 2 * t_2 + t) * (p0.outTangent-p0.pos) * noOutTangent + 
            (-2 * t_3 + 3 * t_2) * p1.pos +
            (t_3 - t_2) * (p1.inTangent - p1.pos) * noInTangent;

        if (!worldSpace && space == SampleSpace.world)
            ret = transform.TransformPoint(ret);

        return ret;
    }

    List<PathPoint> SampleSection(float baseLength, int a, int b, SampleSpace space = SampleSpace.local)
    {
        List<PathPoint> points = new List<PathPoint>();
        float length = baseLength;
        int samples = sectionSamples;
        float step = 1.0f / samples;
        for (int i = 0; i < samples + 1; i++)
        {  
            var p0 = SamplePoint(a, b, i * step,space);
            if (i > 0)
            {
                length += (p0 - points[points.Count - 1].pos).magnitude;
            }
            points.Add(new PathPoint { pos = p0, length = length });
        }
        return points;
    }

    public void CacheSections()
    {
        float oldLength = CachedPathLength();
        cachedSections = new List<MasterPath.CachedSection>();
        float baseLength = 0;
        int lastPointIndex = looped? controlPoints.Count:controlPoints.Count - 1;


        for (int i = 0; i < lastPointIndex; i++)
        {
            var sampledPoints = SampleSection(baseLength, i, (i+1) % controlPoints.Count);

            CachedSection sec = new CachedSection
            {
                points = sampledPoints
            };
            cachedSections.Add(sec);
            baseLength = sec.EndingLength();

        }
        Simplify();
        CacheEventPoints(oldLength);
    }

    public void CacheEventPoints(float oldLength)
    {
        if (oldLength <= 0)
            oldLength = 0.0001f;
        foreach( var evt in eventPoints)
        {
            evt.positionOnPath = Mathf.Clamp01(evt.positionOnPath / oldLength) * CachedPathLength();
            evt.cachedPosition = Sample(evt.positionOnPath).pos;
        }
    }

    public float CachedPathLength()
    {
        if (cachedSections.Count == 0)
            return 0;

        return cachedSections[cachedSections.Count - 1].points[cachedSections[cachedSections.Count - 1].points.Count - 1].length;
    }

    CachedSection CachedSectionByLength( float t)
    {
        t = Mathf.Clamp(t, 0, CachedPathLength());
        foreach( var sec in cachedSections)
        {
            if (t>=sec.StartingLength() && t <= sec.EndingLength() )
            {
                return sec;
            }
        }
        return null;
    }

    public void AddEvent(float t, GameObject gameObject, System.Type type)
    {
        var sec = CachedSectionByLength(t);
        if (sec == null)
            return;
        var pos = Sample(t).pos;
        var el = new List<EventBase>
        {
           gameObject.AddComponent(type) as EventBase
        };
        eventPoints.Add(new EventPoint { positionOnPath = t, events = el, cachedPosition = pos });

    }

    public struct SampleResult
    {
        public Vector3 pos;
        public Vector3 tangent;
    }

    public void Simplify()
    {

        int pointsBeforeSimplfied = 0;
        int pointsAfterSimplfied = 0;
        foreach (var sec in cachedSections)
        {
            pointsBeforeSimplfied += sec.points.Count;
            if (sec.points.Count <= 3)
                continue;
            int i = 0;
            Vector2 initialDir = (sec.points[1].pos - sec.points[0].pos).normalized;
            while (i < sec.points.Count - 2)
            {
                var dir0 = (sec.points[i + 1].pos - sec.points[i].pos).normalized;
                var l0 = (sec.points[i + 1].pos - sec.points[i].pos).magnitude;
                var dir1 = (sec.points[i + 2].pos - sec.points[i + 1].pos).normalized;
                var l1 = (sec.points[i + 2].pos - sec.points[i + 1].pos).magnitude;
                var dirSkipped = (sec.points[i + 2].pos - sec.points[i].pos).normalized;
                if (Vector2.Dot(dir0, dir1) > simplifyTorrence)// && Vector2.Dot(dirSkipped ,dir0)<0.02f)
                {
                    sec.points.RemoveAt(i + 1);
                }
                else
                {
                    i++;
                }
            }
            pointsAfterSimplfied += sec.points.Count;

        }
        Debug.Log("points before simplify:" + pointsBeforeSimplfied);
        Debug.Log("points after simplify:" + pointsAfterSimplfied);
    }
    public SampleResult Sample(float t, SampleSpace space = SampleSpace.local)
    {
        SampleResult ret = new SampleResult { pos = Vector3.zero, tangent = Vector3.zero};
       t = Mathf.Clamp(t, 0, CachedPathLength());
        for (int s = 0; s < cachedSections.Count; s++)
        {

            var sec = cachedSections[s];
            if (t >= sec.StartingLength() && t <= sec.EndingLength())
            {
                for (int i = 0; i < sec.points.Count; i++)
                {
                    var p = sec.points[i];
                    if (p.length < t)
                        continue;
                    if (p.length == t)
                    {
                        if (i == sec.points.Count - 1)
                            ret.tangent = (p.pos - sec.points[i - 1].pos).normalized;
                        else
                            ret.tangent = (sec.points[i + 1].pos - p.pos).normalized;
                        ret.pos = p.pos;
                        break;
                    }
                    if (p.length > t)
                    {
                        var p0 = sec.points[i - 1];
                        ret.tangent = (p.pos - p0.pos).normalized;
                        ret.pos =  p0.pos + (p.pos - p0.pos).normalized * (t - p0.length);
                        break;
                    }
                }

            }
        }
        if ( !worldSpace && space == SampleSpace.world)
            ret.pos = transform.TransformPoint(ret.pos);
        return ret;
    }
    ProjectToSegmentResult ProjectPointToSection(Vector3 p, CachedSection sec)
    {
        p = new Vector3(p.x, p.y, 0);
        ProjectToSegmentResult result = new ProjectToSegmentResult
        {
            segmentIndex = -1,
            length = 0,
            projectedPos = Vector3.zero,
        };
        float closetDistance = 100000.0f;
        float currentLength = 0;        
        for (int j = 0; j < sec.points.Count - 1; j++)
        {

            var a = sec.points[j].pos;
            var b = sec.points[j + 1].pos;
            var ab = b - a;
            var ap = (Vector2)p - a;          
            var t = Vector2.Dot(ab, ap)/ab.sqrMagnitude;
            t = Mathf.Clamp01(t);   
            if ((a + t * ab - (Vector2)p).magnitude < closetDistance)
            {
                result.projectedPos = a + t * ab;
                closetDistance = (result.projectedPos - p).magnitude;
                result.segmentIndex = j;
                result.length = t * (ab).magnitude + currentLength+sec.StartingLength();
            }
            currentLength += ab.magnitude;
        }
        return result;
    }


    public void DeleteControlPoint(int index)
    {
        controlPoints.RemoveAt(index);
        CacheSections();
    }

    public ControlPoint FirstControlPoint
    {
        get
        {
            return controlPoints[0];
        }
    }
    public ControlPoint LastControlPoint
    {
        get
        {
            return controlPoints[controlPoints.Count - 1];
        }
    }

    public void AddControlPoint(Vector3 inPos, int sectionIndex, SampleSpace space )
    {
        var cp = new MasterPath.ControlPoint
        {

            pos = (space==SampleSpace.world && !worldSpace)? WorldToLocal(inPos):inPos,
            //todo calc tangent based on tangent in this point
            inTangent = (space == SampleSpace.world && !worldSpace) ? WorldToLocal(inPos) : inPos,
            outTangent = (space == SampleSpace.world && !worldSpace) ? WorldToLocal(inPos) : inPos
        };
        if (controlPoints.Count > sectionIndex + 1)
            controlPoints.Insert(sectionIndex + 1, cp);
        else
            controlPoints.Add(cp);
        CacheSections();
    }

    public struct PathProjectionResult
    {
        public Vector3 projectedPos;
        public float length;
        public int segmentIndex;
        public int secIndex;
    }
    public PathProjectionResult ProjectPointToPath(Vector3 pos)
    {
        PathProjectionResult ret = new PathProjectionResult
        {
            projectedPos = Vector3.zero,
            length = 0,
            segmentIndex = -1,
            secIndex = -1
        };
        float closestDistance = 100000.0f;
        if (cachedSections.Count == 0)
            return ret;
        for ( int i = 0; i < cachedSections.Count; i++)
        {
            var sec = cachedSections[i];
            var projectedResult = ProjectPointToSection(pos, sec);
            if ((projectedResult.projectedPos - pos).magnitude < closestDistance)
            {
                closestDistance = (projectedResult.projectedPos - pos).magnitude;
                ret.segmentIndex = projectedResult.segmentIndex;
                ret.secIndex = i;
                ret.projectedPos = projectedResult.projectedPos;
                ret.length = projectedResult.length;
            }

        }
        return ret;
    }
}
