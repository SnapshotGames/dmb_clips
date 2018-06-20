#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DMB
{

public partial class Sequencer : MonoBehaviour
{
	// SCENE VIEW EDITING

	//private List<Vector3> _movSelectedPath;
	//private int _movSelectedIndex = -1;

	//private bool MOV_DrawPath( List<Vector3> path, Color color ) 
	//{
	//	bool selected = _movSelectedPath == path;
	//	float alpha = selected ? 1 : 0.3f;
	//	Handles.color = new Color( color.r, color.g, color.b, alpha );
	//	Handles.DrawPolyLine( path.ToArray() );
	//	bool result = false;
	//	for ( int i = 0; i < path.Count; i++ ) {
	//		Vector3 selPt = path[i];
	//		EditorGUI.BeginChangeCheck();
	//		Vector3 newPt;
	//		if ( GUI_LinePoint( selPt, selected, out newPt ) ) {
	//			//_movSelectedIndex = i;
	//			_movSelectedPath = path;
	//		}
	//		if ( EditorGUI.EndChangeCheck() ) {
	//			UNDORecordChange( "Modified Follow Path" );
	//			path[i] = newPt;
	//			result = true;
	//		}
	//	}
	//	return result;
	//}

	private void MOV_Reset()
	{
		//_movSelectedPath = null;
		//_movSelectedIndex = -1;
	}

	// EXECUTION / SAMPLING

	private static Vector3 MOV_GetPointOnPath( List<Vector3> path, float localTime )
	{
		if ( path.Count == 0 ) {
			return Vector3.zero;
		}
		if ( localTime <= 0 ) {
			return path[0];
		}
		if ( localTime >= 1 ) {
			return path[path.Count - 1];
		}
		float len = 0;
		for ( int i = 1; i < path.Count; i++ ) {
			var p0 = path[i - 1];
			var p1 = path[i - 0];
			len += ( p1 - p0 ).magnitude;
		}
		float timeOnPath = localTime * len;
		len = 0;
		for ( int i = 1; i < path.Count; i++ ) {
			var p0 = path[i - 1];
			var p1 = path[i - 0];
			var dp = p1 - p0;
			var magn = dp.magnitude;
			if ( len + magn >= timeOnPath ) {
				var timeOnSeg = timeOnPath - len;
				return Vector3.Lerp( p0, p1, timeOnSeg / magn );
			}
			len += magn;
		}
		return Vector3.zero;
	}

	private static void MOV_SampleAtTime( string actorName, List<Vector3> points, float localTime, Vector3? facing = null ) 
	{
		GameObject go = GameObject.Find( actorName );
		if ( go != null ) {
			float t0 = localTime;
			Vector3 p0 = MOV_GetPointOnPath( points, t0 );
			go.transform.position = p0;
			if ( facing == null || facing.Value.sqrMagnitude < 0.0001f ) {
				float t1 = localTime + 0.05f;
				if ( t1 <= 1 ) {
					Vector3 p1 = MOV_GetPointOnPath( points, t1 );
					Vector3 v = p1 - p0;
					v = new Vector3( v.x, 0, v.z );
					if ( v.sqrMagnitude > 0.0001f ) {
						go.transform.forward = v.normalized;
					}
				}
			} else {
				go.transform.forward = facing.Value.normalized;
			}
		} else {
			Debug.Log( "Can't find object in scene called '" + actorName + "'" );
		}
	}
}

}

#endif
