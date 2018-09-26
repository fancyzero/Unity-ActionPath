using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MasterPath))]
public class PathEditor : Editor
{
    static float VectorCompressPower = 4.0f;
    static bool editingCurve = false;
    static bool editingEvent = false;
    static bool showEventLabel = false;
    static float handleSize = 0.05f;
    static float mouseCursorCaptureRange = 0.1f;

    static protected Vector2 CompressVector(Vector2 v)
    {
        return v.normalized * v.magnitude * 1.0f / VectorCompressPower;
    }

    static protected Vector2 DecompressVector(Vector2 v)
    {
        return v.normalized * v.magnitude * VectorCompressPower;
    }

    class HandleRecord
    {
        public int controlPointIndex;
    }

    protected virtual void OnSceneGUI()
    {
        var src = target as MasterPath;
        Vector3 mousePosition = Event.current.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var mouseWorldPosition = ray.origin;
        var mouseLocalPosition = mouseWorldPosition;
        if (!src.worldSpace)
            mouseLocalPosition = src.WorldToLocal(mouseWorldPosition);

        if (editingEvent)
        {
            if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
            {
                GenericMenu menu = new GenericMenu();

                var ret = src.ProjectPointToPath(src.WorldToLocal(mouseWorldPosition));
                if (((Vector2)ret.projectedPos - (Vector2)mouseLocalPosition).magnitude < HandleUtility.GetHandleSize(mouseWorldPosition) * mouseCursorCaptureRange)
                {
                    Event.current.Use();
                    menu.AddItem(new GUIContent("add Event "), false, t => src.AddEvent((float)t, src.gameObject, typeof(FiringEvent)), ret.length);

                    menu.ShowAsContext();
                }
            }

            foreach (var evt in src.eventPoints)
            {
                EditorGUI.BeginChangeCheck();
                int ctrlID = GUIUtility.GetControlID(FocusType.Passive);
                var pos = Handles.FreeMoveHandle(ctrlID, src.LocalToWorld(evt.cachedPosition), Quaternion.identity,
                    HandleUtility.GetHandleSize(evt.cachedPosition) * handleSize * 1.5f, Vector3.zero, Handles.SphereHandleCap);
                pos = src.WorldToLocal(pos);
                MasterPath.PathProjectionResult projRet = new MasterPath.PathProjectionResult();
                if ((pos - evt.cachedPosition).magnitude > 0.0001f)
                {
                    projRet = src.ProjectPointToPath(pos);
                }
                var distanceToMouse = ((Vector2)mouseWorldPosition - (Vector2)evt.cachedPosition).magnitude;
                if (distanceToMouse < 0.02f)
                    HandleUtility.AddControl(ctrlID, distanceToMouse);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "set event point");
                    evt.cachedPosition = projRet.projectedPos;
                    evt.positionOnPath = projRet.length;
                    //src.CacheSections();
                }
            }
        }
        if (editingCurve)
        {
            Dictionary<int, HandleRecord> handleRecords = new Dictionary<int, HandleRecord>();

            for (int i = 0; i < src.GetControlPointsCount(); i++)
            {
                var wcp = src.GetControlPoint(i, MasterPath.SampleSpace.world);
                //var lcp = src.GetControlPoint(i, MasterPath.SampleSpace.local);
                EditorGUI.BeginChangeCheck();
                int ctrlID = GUIUtility.GetControlID(FocusType.Passive);
                var newPos = Handles.FreeMoveHandle(ctrlID, wcp.pos, Quaternion.identity,
                    HandleUtility.GetHandleSize(wcp.pos) * handleSize * 1.5f, Vector3.zero, Handles.RectangleHandleCap);
                handleRecords[ctrlID] = new HandleRecord { controlPointIndex = i };
                var distanceToMouse = ((Vector2)mouseWorldPosition - (Vector2)newPos).magnitude;
                if (distanceToMouse < 0.02f)
                    HandleUtility.AddControl(ctrlID, distanceToMouse);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "set control point");
                    src.SetControlPointPosition(i, newPos, MasterPath.SampleSpace.world);
                    src.CacheSections();
                }
                EditorGUI.BeginChangeCheck();
                if (i > 0 || src.looped)
                {
                    var intan = Handles.FreeMoveHandle(GUIUtility.GetControlID(FocusType.Passive),
                        wcp.pos + CompressVector((wcp.inTangent - wcp.pos) * -1), Quaternion.identity,
                        HandleUtility.GetHandleSize(wcp.pos + CompressVector((wcp.inTangent - wcp.pos) * -1)) * handleSize,
                        Vector3.zero, Handles.RectangleHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "set intangent");
                        src.SetControlPointInTangent(i, DecompressVector(intan - (Vector3)(wcp.pos)) * -1 + wcp.pos, MasterPath.SampleSpace.world);
                        src.CacheSections();
                    }
                }

                if (i < src.GetControlPointsCount() - 1 || src.looped)
                {
                    EditorGUI.BeginChangeCheck();
                    var outtan = Handles.FreeMoveHandle(GUIUtility.GetControlID(FocusType.Passive),
                        wcp.pos + CompressVector(wcp.outTangent - wcp.pos), Quaternion.identity,
                        HandleUtility.GetHandleSize(wcp.pos + CompressVector(wcp.outTangent - wcp.pos)) * handleSize,
                        Vector3.zero, Handles.RectangleHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "set out tangent");
                        src.SetControlPointOutTangent(i, DecompressVector(outtan - (Vector3)(wcp.pos)) + wcp.pos, MasterPath.SampleSpace.world);
                        src.CacheSections();
                    }
                }
            }



            if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
            {
                int targetControlPointIndex = -1;
                if (handleRecords.ContainsKey(HandleUtility.nearestControl))
                {
                    targetControlPointIndex = handleRecords[HandleUtility.nearestControl].controlPointIndex;
                }
                if (targetControlPointIndex >= 0)
                {
                    GenericMenu menu = new GenericMenu();
                    Event.current.Use();
                    menu.AddItem(new GUIContent("delete point " + targetControlPointIndex.ToString()), false, x => src.DeleteControlPoint((int)x), targetControlPointIndex);
                    menu.ShowAsContext();
                }
            }
            if (Event.current.type == EventType.MouseDown)
            {
                if (Event.current.control && Event.current.button == 0)
                {
                    Event.current.Use();
                    Undo.RecordObject(target, "add control point");
                    bool pointAdded = false;
                    if (src.GetControlPointsCount() >= 2)
                    {
                        var projRet = src.ProjectPointToPath(mouseLocalPosition);
                        //must compare in world space
                        if (((Vector2)(src.LocalToWorld(projRet.projectedPos) - mouseWorldPosition)).magnitude < HandleUtility.GetHandleSize(mouseWorldPosition) * mouseCursorCaptureRange)
                        {
                            src.AddControlPoint(projRet.projectedPos, projRet.secIndex, MasterPath.SampleSpace.local);
                            pointAdded = true;
                        }
                    }
                    if (!pointAdded)
                    {
                        src.AddControlPoint(mouseLocalPosition, src.cachedSections.Count, MasterPath.SampleSpace.local);
                    }
                }
            }
        }
    }

    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.InSelectionHierarchy | GizmoType.Pickable)]
    static void DrawGizmos(MasterPath src, GizmoType gizmoType)
    {
        bool selected = (GizmoType.InSelectionHierarchy & gizmoType) != 0;
        if (selected)
        {

            Vector3 mousePosition = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            var mouseWorldPosition = ray.origin;
            int secIndex = 0;
            foreach (var sec in src.cachedSections)
            {
                Handles.color = Color.white;
                List<Vector3> points = new List<Vector3>();
                foreach (var p in sec.points)
                {
                    points.Add(src.LocalToWorld(p.pos));
                    Handles.DrawAAPolyLine(points.ToArray());
                }
                if (editingCurve)
                {
                    Handles.color = Color.red;
                    var wcp = src.GetControlPoint(secIndex, MasterPath.SampleSpace.world);
                    if (secIndex > 0 || src.looped)
                        Handles.DrawAAPolyLine(wcp.pos,
                            wcp.pos + CompressVector((wcp.inTangent - wcp.pos) * -1));
                    Handles.color = Color.green;
                    if (secIndex < src.GetControlPointsCount() - 1 || src.looped)
                        Handles.DrawAAPolyLine(wcp.pos,
                            wcp.pos + CompressVector(wcp.outTangent - wcp.pos));
                }
                secIndex++;

            }
        }
    }

    static void SetEventType(ChangeEventType param)
    {
        DestroyImmediate(param.path.eventPoints[param.pointIndex].events[param.eventIndex]);
        param.path.eventPoints[param.pointIndex].events[param.eventIndex] = param.gameObject.AddComponent(param.type) as EventBase;

    }


    struct ChangeEventType
    {
        public GameObject gameObject;
        public MasterPath path;
        public int pointIndex;
        public int eventIndex;
        public System.Type type;
    }

    public override void OnInspectorGUI()
    {
        bool needRecache = false;
        MasterPath owner = serializedObject.targetObject as MasterPath;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("worldSpace"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("looped"));
        if (EditorGUI.EndChangeCheck())
        {
            needRecache = true;
        }


        var propControlPoints = serializedObject.FindProperty("controlPoints");

        if (propControlPoints != null)
        {
            EditorGUI.indentLevel += 1;
            EditorGUILayout.BeginHorizontal();
            propControlPoints.isExpanded = EditorGUILayout.Foldout(propControlPoints.isExpanded, "control points");
            //if (propControlPoints.arraySize == 0)
            //    if (GUILayout.Button("+", GUILayout.MaxWidth(30)))
            //    {
            //        propControlPoints.InsertArrayElementAtIndex(0);
            //    }
            if (GUILayout.Button("[]", GUILayout.MaxWidth(30)))
            {
                Undo.RecordObject(serializedObject.targetObject, "remove all control points");
                owner.ClearControlPoints();
            }
            EditorGUILayout.EndHorizontal();
            if (propControlPoints.isExpanded)
            {

                for (int i = 0; i < propControlPoints.arraySize; i++)
                {
                    var p = propControlPoints.GetArrayElementAtIndex(i);
                    EditorGUI.indentLevel += 1;
                    p.isExpanded = EditorGUILayout.Foldout(p.isExpanded, string.Format("point {0}", i));
                    EditorGUI.indentLevel += 1;
                    if (p.isExpanded)
                    {
                        EditorGUILayout.PropertyField(p.FindPropertyRelative("pos"), GUILayout.MinHeight(10));
                        EditorGUILayout.PropertyField(p.FindPropertyRelative("inTangent"), GUILayout.MinHeight(10));
                        EditorGUILayout.PropertyField(p.FindPropertyRelative("outTangent"), GUILayout.MinHeight(10));
                    }
                    //EditorGUI.PropertyField(rc1, p);
                    EditorGUI.indentLevel -= 2;
                }
            }
            EditorGUI.indentLevel -= 1;
        }

        var proEventPoints = serializedObject.FindProperty("eventPoints");

        if (proEventPoints != null)
        {
            EditorGUI.indentLevel += 1;
            EditorGUILayout.BeginHorizontal();
            proEventPoints.isExpanded = EditorGUILayout.Foldout(proEventPoints.isExpanded, "event points");
            //if (proEventPoints.arraySize == 0)
            //    if (GUILayout.Button("+", GUILayout.MaxWidth(30)))
            //    {
            //        proEventPoints.InsertArrayElementAtIndex(0);
            //    }
            if (GUILayout.Button("[]", GUILayout.MaxWidth(30)))
            {
                Undo.RecordObject(serializedObject.targetObject, "remove all events");
                owner.ClearEventPoints();
            }
            EditorGUILayout.EndHorizontal();
            if (proEventPoints.isExpanded)
            {
                for (int i = 0; i < proEventPoints.arraySize; i++)
                {
                    var p = proEventPoints.GetArrayElementAtIndex(i);
                    EditorGUI.indentLevel += 1;
                    p.isExpanded = EditorGUILayout.Foldout(p.isExpanded, string.Format("event point {0}", i));
                    EditorGUI.indentLevel += 1;

                    if (p.isExpanded)
                    {

                        //if (p.FindPropertyRelative("events").arraySize == 0)
                        //{
                        //    if (GUILayout.Button("+", GUILayout.MaxWidth(30)))
                        //    {
                        //        p.FindPropertyRelative("events").InsertArrayElementAtIndex(0);
                        //    }
                        //}
                        EditorGUILayout.LabelField("events");
                        EditorGUILayout.PropertyField(p.FindPropertyRelative("positionOnPath"));
                        EditorGUI.indentLevel += 1;
                        int eventIndex = 0;
                        foreach (var evt in owner.eventPoints[i].events)
                        {
                            if (evt == null)
                            {
                                EditorGUILayout.LabelField("error");
                                continue;
                            }
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PrefixLabel("Type");
                            if (EditorGUILayout.DropdownButton(new GUIContent(evt.GetType().ToString()), FocusType.Passive, GUILayout.MinHeight(10), GUILayout.MaxWidth(100)))
                            {
                                GenericMenu menu = new GenericMenu();
                                var pp = p.serializedObject.targetObject;
                                menu.AddItem(new GUIContent("FireEvent"), false, param => SetEventType((ChangeEventType)param),
                                    new ChangeEventType { gameObject = (serializedObject.targetObject as MonoBehaviour).gameObject, path = owner, pointIndex = i, eventIndex = eventIndex, type = typeof(FiringEvent) });
                                menu.ShowAsContext();
                            }
                            EditorGUILayout.EndHorizontal();

                            if (evt.GetType() == typeof(FiringEvent))
                            {
                                var fireEvent = evt as FiringEvent;
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.PrefixLabel("fireDuration");
                                fireEvent.firingDuration = EditorGUILayout.FloatField(fireEvent.firingDuration, GUILayout.MinHeight(10));
                                EditorGUILayout.EndHorizontal();
                            }

                            eventIndex++;
                        }
                        EditorGUI.indentLevel -= 1;
                    }
                    //EditorGUI.PropertyField(rc1, p);
                    EditorGUI.indentLevel -= 2;
                }
            }
            EditorGUI.indentLevel -= 1;
        }
        //EditorGUI.EndProperty();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Cache Path", GUILayout.MaxWidth(100)))
        {
            var t = target as MasterPath;
            t.CacheSections();
        }
        bool b = editingCurve;
        editingCurve = GUILayout.Toggle(editingCurve, "Edit Curve", "Button", GUILayout.MaxWidth(100));
        if (b != editingCurve)
            SceneView.RepaintAll();

        b = editingEvent;
        editingEvent = GUILayout.Toggle(editingEvent, "Edit Event", "Button", GUILayout.MaxWidth(100));
        if (b != editingCurve)
            SceneView.RepaintAll();
        GUILayout.EndHorizontal();
        b = showEventLabel;
        showEventLabel = GUILayout.Toggle(showEventLabel, "Event Label", "Button", GUILayout.MaxWidth(100));
        if (b != showEventLabel)
            SceneView.RepaintAll();
        EditorGUILayout.Popup(0, new[] { "aa", "bb", "cc" });
        serializedObject.ApplyModifiedProperties();
        if (needRecache)
        {
            owner.CacheSections();
            SceneView.RepaintAll();

        }
    }

}
