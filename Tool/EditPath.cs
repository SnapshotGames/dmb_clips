#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DMB
{

public class EditPath
{
    public static bool IsTangentHandle( int index ) {
        return index % 3 != 0;
    }

    public static bool DrawPositionHandle( Vector3 [] path, out Vector3 drag )
    {
        if ( path.Length == 0 ) {
            drag = Vector3.zero;
            return false;
        }
        Bounds b = new Bounds( path[0] + new Vector3( 0.1f, 0.1f, 0.1f ), Vector3.zero );
        if ( Tools.pivotMode == PivotMode.Center ) {
            for ( int i = 0; i < path.Length; i++ ) {
                var p = path[i];
                b.Encapsulate( p );
            }
        }
        Vector3 newCenter = Handles.PositionHandle( b.center, Quaternion.identity );
        drag = newCenter - b.center;
        return drag.sqrMagnitude > 0.001f;
    }

    public static bool DrawControlPoints( Vector3 [] path, int selIndex, bool selectedSpline,
                                            out int newSelIndex, out int dragIndex, out Vector3 drag, 
                                            bool detectTangents = false )
    {
        drag = Vector3.zero;
        dragIndex = -1;
        newSelIndex = -1;
        for ( int i = 0; i < path.Length; i++ ) {
            Vector3 selPt = path[i];
            EditorGUI.BeginChangeCheck();
            Vector3 newPt;
            if ( SWUI.LinePoint( selPt, selectedSpline, false, out newPt, 
                        selectedIndex: i == selIndex, isSecondary: detectTangents && IsTangentHandle( i ) ) ) {
                newSelIndex = i;
            }
            if ( EditorGUI.EndChangeCheck() ) {
                dragIndex = i;
                drag = newPt - selPt;
            }
        }
        return newSelIndex >= 0 || dragIndex >= 0;
    }

    protected int _selectedPath;
    protected int _selectedIndex;

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
}

}

#endif
