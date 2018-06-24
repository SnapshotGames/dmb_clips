#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DMB
{

public class FollowPathEdit
{
    // EDITING

    // FIXME: static?
    private static Vector3 _chasePoint;
    private int _selectedPath;
    //private int _fpSelectedIndex = -1;

    public bool DrawPath( Vector3 [] path, Color color, int pathId, 
            bool moveWholeSpline,
            float selectedThickness,
            float unselectedThickness,
            Texture2D texture ) 
    {
        Vector3 dummy;
        SWUI.LinePoint( _chasePoint, true, false, out dummy );
        bool selected = _selectedPath == pathId;
        float alpha = selected ? 1 : 0.3f;
        float thickness = selected ? selectedThickness : unselectedThickness;
        Handles.color = SWUI.CCol( new Color( color.r, color.g, color.b, alpha ) );
        Handles.DrawAAPolyLine( texture, thickness, path ); 
        bool result = false;
        for ( int i = 0; i < path.Length; i++ ) {
            Vector3 selPt = path[i];
            EditorGUI.BeginChangeCheck();
            Vector3 newPt;
            if ( SWUI.LinePoint( selPt, selected, moveWholeSpline, out newPt ) ) {
                //_fpSelectedIndex = i;
                _selectedPath = pathId;
            }
            if ( EditorGUI.EndChangeCheck() ) {
                if ( moveWholeSpline ) {
                    Vector3 d = newPt - selPt;
                    for ( int j = 0; j < path.Length; j++ ) {
                        path[j] += d;
                    }
                } else {
                    path[i] = newPt;
                }
                result = true;
            }
            if ( moveWholeSpline ) {
                break;
            }
        }
        return result;
    }

    public void SelectPath( int pathId )
    {
        _selectedPath = pathId;
    }

    public void Deselect()
    {
        _selectedPath = -1;
        //_fpSelectedIndex = -1;
    }

    // EXECUTION

    public static Vector3 GetPointOnPath( List<FollowPathKey> path, float normTime )
    {
        Vector3 pos;
        int segmentIndex;
        GetPointOnPath( path, normTime, out pos, out segmentIndex );
        return pos;
    }

    public static void GetPointOnPath( List<FollowPathKey> path, float normTime, 
                                        out Vector3 pos, out int segmentIndex )
    {
        if ( path.Count == 0 ) {
            segmentIndex = 0;
            pos = Vector3.zero;
            return;
        }
        if ( path.Count == 1 || normTime <= 0 ) {
            segmentIndex = 0;
            pos = path[0].Position;
            return;
        }
        if ( normTime >= 1 ) {
            segmentIndex = path.Count - 2;
            pos = path[path.Count - 1].Position;
            return;
        }
        for ( int i = 0; i < path.Count - 1; i++ ) {
            FollowPathKey fpk0 = path[i + 0];
            FollowPathKey fpk1 = path[i + 1];
            float t0 = fpk0.TimeNorm;
            float t1 = fpk1.TimeNorm;
            if ( normTime >= t0 && normTime < t1 ) {
                Vector3 p0 = fpk0.Position;
                Vector3 p1 = fpk1.Position;
                float t = ( normTime - t0 ) / ( t1 - t0 );
                segmentIndex = i;
                pos = Vector3.Lerp( p0, p1, t );
                return;
            }
        }
        segmentIndex = 0;
        pos = Vector3.zero;
    }

    public static void SampleAtTime( string actorName, FollowPathSample fps, float absoluteTime ) 
    {
        List<FollowPathKey> points = fps.Keys;
        float localTime = ClipUtil.LocalTime( absoluteTime, fps.StartTime, fps.Duration );
        if ( localTime >= 0 && localTime <= 1 ) {
            GameObject go = GameObject.Find( actorName );
            if ( go != null ) {
                float t0 = localTime;
                int segIndex;
                Vector3 p0;
                GetPointOnPath( points, t0, out p0, out segIndex );
                FollowPathKey key = points[segIndex];
                go.transform.position = p0;
                if ( key.Facing.sqrMagnitude < 0.0001f ) {
                    float chaseOff = key.ChasePointOffset > 0.00001f ? key.ChasePointOffset : 0.03f;
                    float t1 = localTime + chaseOff / fps.Duration;
                    if ( t1 <= 1 ) {
                        Vector3 p1 = GetPointOnPath( points, t1 );
                        _chasePoint = p1;
                        Vector3 v = p1 - p0;
                        v = new Vector3( v.x, 0, v.z );
                        if ( v.sqrMagnitude > 0.0001f ) {
                            go.transform.forward = v.normalized;
                        }
                    }
                } else {
                    go.transform.forward = key.Facing.normalized;
                }
            } else {
                Debug.Log( "Can't find object in scene called '" + actorName + "'" );
            }
        }
    }
}

}

#endif
