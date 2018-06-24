#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DMB
{

public class CameraPathEdit
{
    private int _selectedPath;
    private int _selectedIndex;
    private Vector3 _camLookat;
    private Vector3 _camPos;
    public static Camera FallbackCamera { get {
        if ( Camera.main == null ) {
            GameObject go = new GameObject();
            go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.name = "Main Camera";
        }
        return Camera.main;
    } }

    private static void DrawCurve( Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color, float alpha, 
                                    bool drawHandles, float thickness = 2, Texture2D tex = null )
    {
        if ( drawHandles ) {
            Handles.color = new Color( Color.gray.r, Color.gray.g, Color.gray.b, alpha );
            //Handles.color = new Color( 1, 1, 0, alpha );
            Handles.DrawAAPolyLine( tex, thickness, new Vector3 [] { p0, p1 } ); 
            Handles.DrawAAPolyLine( tex, thickness, new Vector3 [] { p2, p3 } ); 
        }
        Color c = SWUI.CCol( new Color( color.r, color.g, color.b, alpha ) );
        Handles.DrawBezier( p0, p3, p1, p2, c, tex, thickness);
    }   

    private void DrawDeselectedPath( Vector3 [] path, Color color, int pathId, float thickness ) 
    {
        for ( int i = 0; i < path.Length - 3; i += 3 ) {
            DrawCurve( path[i + 0], path[i + 1], path[i + 2], path[i + 3], 
                    color, 0.3f, false, thickness );
        }
        for ( int i = 0; i < path.Length; i += 3 ) {
            Vector3 selPt = path[i];
            Vector3 newPt;
            if ( SWUI.LinePoint( selPt, false, false, out newPt ) ) {
                SelectPath( i, pathId );
            }
        }
    }

    private static bool IsTangentHandle( int index ) {
        return index % 3 != 0;
    }

    private static bool DrawPositionHandle( Vector3 [] path, int selectedIndex, out Vector3 drag )
    {
        if ( path.Length == 0 ) {
            drag = Vector3.zero;
            return false;
        }
        Bounds b = new Bounds( path[0] + new Vector3( 0.1f, 0.1f, 0.1f ), Vector3.zero );
        if ( Tools.pivotMode == PivotMode.Center ) {
            for ( int i = 0; i < path.Length; i++ ) {
                //if ( i != selectedIndex ) {
                    var p = path[i];
                    b.Encapsulate( p );
                //}
            }
        }
        Vector3 newCenter = Handles.PositionHandle( b.center, Quaternion.identity );
        drag = newCenter - b.center;
        return drag.sqrMagnitude > 0.001f;
    }

    private static void DrawSpline( Vector3 [] path, Color color, float thickness, Texture2D tex ) 
    {
        for ( int i = 0; i < path.Length - 3; i += 3 ) {
            DrawCurve( path[i + 0], path[i + 1], path[i + 2], path[i + 3], color, 1, true, thickness, tex );
        }
    }

    private static bool DrawControlPoints( Vector3 [] path, int selIndex, out int newSelIndex, out int dragIndex, out Vector3 drag )
    {
        drag = Vector3.zero;
        dragIndex = -1;
        newSelIndex = -1;
        for ( int i = 0; i < path.Length; i++ ) {
            Vector3 selPt = path[i];
            EditorGUI.BeginChangeCheck();
            Vector3 newPt;
            if ( SWUI.LinePoint( selPt, true, false, out newPt, 
                        selectedIndex: i == selIndex, isTangent: IsTangentHandle( i ) ) ) {
                newSelIndex = i;
            }
            if ( EditorGUI.EndChangeCheck() ) {
                dragIndex = i;
                drag = newPt - selPt;
            }
        }
        return newSelIndex >= 0 || dragIndex >= 0;
    }

    private bool DrawSelectedPath( Vector3 [] path, Color color, float thickness = 2, Texture2D tex = null ) 
    {
        DrawSpline( path, color, thickness, tex );
        Vector3 pointDrag;
        int newSelIndex, dragIndex;
        bool somePointChanged = DrawControlPoints( path, _selectedIndex, out newSelIndex, out dragIndex, 
                                                out pointDrag );
        Vector3 posHandleDrag;
        bool wholeSplineMoved = DrawPositionHandle( path, _selectedIndex, out posHandleDrag );
        if ( wholeSplineMoved ) {
            for ( int i = 0; i < path.Length; i++ ) {
                path[i] += posHandleDrag;
            }
        } 
        // ignore points dragging while dragging the entire spline
        else if ( somePointChanged ) {
            if (newSelIndex >= 0) {
                _selectedIndex = newSelIndex;
            }
            if (dragIndex >= 0) {
                Vector3 oldPos = path[dragIndex];
                Vector3 newPos = oldPos + pointDrag;
                if ( dragIndex % 3 == 0 ) {
                    Vector3 dPrev = dragIndex == 0 ? Vector3.zero : path[dragIndex - 1] - oldPos;
                    Vector3 dNext = dragIndex == path.Length - 1 ? Vector3.zero : path[dragIndex + 1] - oldPos;
                    path[Mathf.Max( dragIndex - 1, 0 )] = newPos + dPrev;
                    path[Mathf.Min( dragIndex + 1, path.Length - 1 )] = newPos + dNext;
                } else {
                    int controlPoint;
                    int mirrorPoint;
                    if ( dragIndex % 3 == 1 ) {
                        controlPoint = dragIndex - 1;
                        mirrorPoint = dragIndex - 2;
                    } else {
                        controlPoint = dragIndex + 1;
                        mirrorPoint = dragIndex + 2;
                    }
                    if ( mirrorPoint >= 0 && mirrorPoint < path.Length ) {
                        Vector3 cpv = path[controlPoint];
                        path[mirrorPoint] = cpv + cpv - newPos;
                    }
                }
                path[dragIndex] = newPos;
            }
        }
        return wholeSplineMoved || somePointChanged;
    }

    public bool DrawPath( Vector3 [] path, Color color, int pathId, 
                        float selectedWidth = 2, 
                        float unselectedWidth = 5, 
                        Texture2D tex = null ) 

    {
        if ( _selectedPath == pathId ) {
            return DrawSelectedPath( path, color, selectedWidth, tex );
        }
        DrawDeselectedPath( path, color, pathId, unselectedWidth );
        return false;
    }

    public void SelectPath( int pointIndex, int pathId )
    {
        _selectedIndex = pointIndex;
        _selectedPath = pathId;
    }

    public void Deselect()
    {
        _selectedPath = -1;
        _selectedIndex = -1;
    }

    public int GetSelectedPath()
    {
        return _selectedPath;
    }

    public int GetSelectedPoint()
    {
        return _selectedIndex;
    }

    public bool IsSelected()
    {
        return _selectedPath >= 0;
    }

    public bool IsSelectedNode()
    {
        return _selectedIndex >= 0;
    }

    public void SetLookatSegment( Vector3 pos, Vector3 lookat )
    {
        _camPos = pos;
        _camLookat = lookat;
    }

    public void DrawLookatLine()
    {
        Handles.color = Color.gray;
        Handles.DrawDottedLine( _camPos, _camLookat, 2 );
    }
}

}

#endif
