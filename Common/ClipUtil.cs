using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DMB
{

public static partial class ClipUtil
{
	public static float LocalTime( float t, float start, float duration )
	{
		return ( t - start ) / duration;
	}

	public static float ClampedLocalTime( float currentTime, float startTime, float duration, bool looped = false, float oneShotDuration = 1 )
	{
		float localTime;
		if ( looped ) {
			float d = currentTime - startTime;
			localTime = Mathf.Repeat( Mathf.Clamp( d, 0, duration ), oneShotDuration ) / oneShotDuration;
		} else {
			localTime = Mathf.Clamp( LocalTime( currentTime, startTime, duration ), 0, 1 );
		}
		return localTime;
	}

	public static Vector3 GetPointCurve( Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t ) 
	{
		t = Mathf.Clamp01(t);
		float oneMinusT = 1f - t;
		return
			oneMinusT * oneMinusT * oneMinusT * p0 +
			3f * oneMinusT * oneMinusT * t * p1 +
			3f * oneMinusT * t * t * p2 +
			t * t * t * p3;
	}

	public static Vector3 GetPointSpline( Vector3 [] points, float t )
	{
		int i;
		if (t >= 1f) {
			t = 1f;
			i = points.Length - 4;
		}
		else {
			t = Mathf.Clamp01(t) * ( ( points.Length - 1 ) / 3 );
			i = (int)t;
			t -= i;
			i *= 3;
		}
		return GetPointCurve( points[i], points[i + 1], points[i + 2], points[i + 3], t );
	}	

	public static Vector3 CameraNoiseVector( float t, float duration, float noiseAmplitude, float noiseFreq )
	{	
		Vector3 noise;
		if ( noiseFreq > 0.0001f && noiseAmplitude > 0.0001f ) {
			float absTime = t * duration;
			float noiseX = Mathf.Repeat( absTime * noiseFreq, 1 );
			float noiseY = Mathf.Repeat( ( absTime + 43.017f ) * noiseFreq, 1 );
			float px = ( Mathf.PerlinNoise( noiseX, 0 ) - 0.5f ) * noiseAmplitude;
			float py = ( Mathf.PerlinNoise( 0, noiseY ) - 0.5f ) * noiseAmplitude;
			noise = new Vector3( px, py, 0 );
		} else {
			noise = Vector3.zero;
		}
		return noise;
	}

	public struct ActorHint 
	{
		public string NameInClip;
		public GameObject ObjectInScene;
	}

	public enum BuiltinHints {
		AnyAnimatedActor, 
	}

	public class ScrubEvent
	{
		public string Tag;

		public bool TagEquals(ScrubEvent other)
		{
			return other.Tag == Tag;
		}

		public virtual bool Equals(ScrubEvent other)
		{
			return false;
		}
	}

	public class OnEnterAnimation : ScrubEvent
	{
		public override bool Equals(ScrubEvent other)
		{
			OnEnterAnimation o = other as OnEnterAnimation;
			return o != null && TagEquals(other);
		}
	}

	public class OnEnterParticle : ScrubEvent
	{
		public override bool Equals(ScrubEvent other)
		{
			OnEnterParticle o = other as OnEnterParticle;
			return o != null && TagEquals(other);
		}
	}

	private class AnimCrossfade {
		public string [] Actors;
		public string [] States;
		public float [] Time;
		public float [] Weight;
	}

	private static AnimCrossfade GetAnimCrossFade( MecanimSample [] anims, int numInPair, float pairOverlap,
										float currentTime )
	{
		string [] actors = { null, null };
		string [] states = { null, null };
		float [] time = { 0, 0 };
		float [] weight = { 0, 0 };
		if ( numInPair == 1 || anims[0].ActorPrefab != anims[1].ActorPrefab  ) {
			MecanimSample ms = anims[0];
			states[1] = states[0] = ms.State;
			time[1] = time[0] = ms.TakeStartNorm + ms.TakeDurationNorm * ClampedLocalTime( currentTime, ms.StartTime, ms.Duration, ms.Looped, ms.OneShotDuration );
			actors[1] = actors[0] = ms.ActorPrefab;
			weight = new float [] { 1, 0 };
		} else {
			if ( pairOverlap <= 0 ) {
				weight[0] = 1;
			} else {
				weight[0] = ( anims[0].StartTime + anims[0].Duration - currentTime ) / pairOverlap;
			}
			weight[1] = 1 - weight[0];
			for ( int i = 0; i < 2; i++ ) {
				MecanimSample ms = anims[i];
				time[i] = ms.TakeStartNorm + ms.TakeDurationNorm * ClampedLocalTime( currentTime, ms.StartTime, ms.Duration, ms.Looped, ms.OneShotDuration );
				actors[i] = ms.ActorPrefab;
				states[i] = ms.State;
			}
		}
		return new AnimCrossfade {
			Actors = actors,
			States = states,
			Time = time,
			Weight = weight,
		};
	}

	public static bool GetAnimator( string actorName, out Animator animator )
	{
		animator = null;
		GameObject go = GameObject.Find( actorName );
		if ( go != null ) {
			animator = go.GetComponent<Animator>();
			if ( ! animator ) {
				Debug.Log( actorName + " has no AnimatorController. Assign one first." );
				return false;
			}
			if ( ! animator.isInitialized ) {
				animator.Rebind();
			}
			if ( animator.layerCount == 0 ) {
				// still not initialized
				// spits a warning when in editor. this fixes it obviously
				animator.enabled = false;
				animator.enabled = true;
				return false;
			}
			return true;
		}
		Debug.Log( "Can't find object in scene called '" + actorName + "'" );
		return false;
	}

	public static bool SetAnimStateInCrossFadeLayer( Animator animator, string state, 
														float speed = 1, float normalizedTime = 0, 
														int animsLayer = 0, float weight = 1 ) 
	{
		//Debug.Log( animator.name + " set layer: " + animsLayer + " weight: " + weight );
		animator.SetLayerWeight( animsLayer, weight );
		bool result = true;
		if ( weight > 0 ) {
			int hash = Animator.StringToHash( state );
			result = animator.HasState( animsLayer, hash );
			if ( result ) {
				// FIXME: either crossfade or play/update depending if in game or editor mode?
				//animator.CrossFade( hash, 0, animsLayer, normalizedTime );
				animator.Play( hash, animsLayer, normalizedTime );
			} else {
				Debug.Log( "SetStateInCrossFadeLayer: Failed to get state " + state );
			}
		}
		return result;
	}

	private static void SampleAnimations( Animator animator, AnimCrossfade crossfade, 
											bool tryCollectingFBX = false ) 
	{
		animator.applyRootMotion = false;
		animator.speed = 0;
		for ( int i = 0; i < 2; i++ ) {
			SetAnimStateInCrossFadeLayer( animator, crossfade.States[i], 
										speed: 1, normalizedTime: crossfade.Time[i], 
										animsLayer: i, weight: crossfade.Weight[i] );
		}
		animator.Update( 0 );
	}

	public static Vector3 ScrubCameraSample( CameraPathSample cps, float time ) 
	{
		Vector3 pos;
		int index;
		ScrubCameraSample( cps, time, out pos, out index );
		return pos;
	}

	public static void ScrubCameraSample( CameraPathSample cps, float time, 
										out Vector3 pos, out int segIndex ) 
	{
		float normTime = ClampedLocalTime( time, cps.StartTime, cps.Duration );
		if ( normTime == 0 ) {
			segIndex = 0;
			pos = cps.Keys[0].Node[1];
			return;
		}
		if ( normTime == 1 ) {
			segIndex = cps.Keys.Count - 1;
			pos = cps.Keys[segIndex].Node[1];
			return;
		}
		for ( int i = 0; i < cps.Keys.Count - 1; i++ ) {
			CameraPathKey cpk0 = cps.Keys[i + 0];
			CameraPathKey cpk1 = cps.Keys[i + 1];
			float t0 = cpk0.TimeNorm;
			float t1 = cpk1.TimeNorm;
			if ( normTime >= t0 && normTime < t1 ) {
				Vector3 p0 = cpk0.Node[1];
				Vector3 p1 = cpk0.Node[2];
				Vector3 p2 = cpk1.Node[0];
				Vector3 p3 = cpk1.Node[1];
				float t = ( normTime - t0 ) / ( t1 - t0 );
				segIndex = i;
				pos = GetPointCurve( p0, p1, p2, p3, t );
				return;
			}
		}
		segIndex = 0;
		pos = Vector3.zero;
	}

	public static CameraPathSample GetCamSampleAtTime( Clip clip, int numTracks, float prevTime, float currentTime, bool lookat )
	{
		CameraPathSample result = null;
		float minOffset = 9999999;
		for ( int track = 0; track < numTracks; track++ ) {
			CameraPathSample [] pair;
			float overlap;
			int numSamples = clip.GetSamplesPairAtTimeDelta<CameraPathSample>( prevTime, currentTime, track, out pair, out overlap );
			if ( numSamples > 0 && pair[0].IsLookatPath == lookat ) {
				if ( overlap >= 0 ) {
					// time hits inside the sample
					result = pair[0];
					break;
				}
				// fallback to nearest
				float offset = -overlap;
				if ( result == null || offset < minOffset ) {
					// TODO: crossfade of camera paths?
					result = pair[0];
					minOffset = offset;
				}
			}
		}
		return result;
	}

	private static bool GetCamScrubGO( string actor, bool fallbackToMainCam, out GameObject go ) 
	{
		// TODO: should get cam actor from hints
		go = GameObject.Find( actor );
		if ( go == null && fallbackToMainCam && Camera.main ) {
			go = Camera.main.gameObject;
		}
		if ( ! go ) {
			Debug.Log( "Can't find MainCamera or camera called '" + actor + "' in scene." );
		}
		return go != null;
	}

	public static Vector3 ScrubCamLookats( Clip clip, int numTracks, float prevTime, float currentTime, bool fallbackToMainCam = false )
	{
		Vector3 lookat = Vector3.zero;
		CameraPathSample cps = GetCamSampleAtTime( clip, numTracks, prevTime, currentTime, true );
		if ( cps != null ) {
			lookat = ScrubCameraSample( cps, currentTime );
			GameObject go;
			if ( GetCamScrubGO( cps.ActorPrefab, fallbackToMainCam, out go ) ) {
				go.transform.LookAt( lookat );
			}
		}
		return lookat;
	}

	public static Vector3 ScrubCamFollowPath( Clip clip, int numTracks, float prevTime, float currentTime, Vector3 lastLookat, bool fallbackToMainCam = false )
	{
		Vector3 result = Vector3.zero;
		CameraPathSample cps = GetCamSampleAtTime( clip, numTracks, prevTime, currentTime, false );
		if ( cps != null ) { 
			GameObject go;
			if ( GetCamScrubGO( cps.ActorPrefab, fallbackToMainCam, out go ) ) {
				go.transform.position = ScrubCameraSample( cps, currentTime );
				go.transform.LookAt( lastLookat );
				result = go.transform.position;
			}
		}
		return result;
	}

	private static GameObject GetActorFromHints( string nameInClip, ActorHint[] hints )
	{
		if ( hints != null ) {
			foreach ( var h in hints ) {
				if ( h.NameInClip == nameInClip ) {
					return h.ObjectInScene;
				}
			}
		}
		return null;
	}

	private static void ScrubAnimations( Clip clip, int numTracks, float prevTime, float currentTime, ActorHint [] hints, List<ScrubEvent> events )
	{
		Animator fallbackAnimator = null; 
		if ( hints != null ) {
			foreach ( var h in hints ) {
				if ( h.NameInClip == "AnyAnimatedActor" ) {
					// use this animator for all mecanim samples
					fallbackAnimator = h.ObjectInScene.GetComponent<Animator>();
				}
			}
		}
		for ( int track = 0; track < numTracks; track++ ) {
			MecanimSample [] pair;
			float overlap;
			int numSamples = clip.GetSamplesPairAtTimeDelta<MecanimSample>( prevTime, currentTime, track, out pair, out overlap );
			if ( numSamples > 0 ) {
				AnimCrossfade animCrossfade = GetAnimCrossFade( pair, numSamples, overlap, currentTime );
				GameObject go = GetActorFromHints( pair[0].ActorPrefab, hints );
				Animator animator = go != null ? go.GetComponent<Animator>() : null;
				if ( animator == null ) {
					if ( fallbackAnimator == null ) {
						GetAnimator( animCrossfade.Actors[0], out animator );
					} else {
						animator = fallbackAnimator;
					}
				}
				if ( animator != null ) {
					SampleAnimations( animator, animCrossfade, tryCollectingFBX: false );
				} else {
					Debug.Log( "Clip scrub needs animated actor, and none is supplied." );
				}
				for ( int i = 0; i < numSamples; i++ ) {
					var s = pair[i];
					if ( prevTime < s.StartTime && s.StartTime <= currentTime ) {
						//Debug.Log( "enter anim " + s.Tag );
						events.Add( new OnEnterAnimation { Tag = s.Tag, } );
					}
				}
			}
		}
	}

	static partial void ScrubExtensions(Clip clip, int numTracks, float prevTime, float currentTime, ActorHint [] hints, List<ScrubEvent> events);

	private static void ScrubParticles( Clip clip, int numTracks, float prevTime, float currentTime, List<ScrubEvent> events )
	{
		for ( int track = 0; track < numTracks; track++ ) {
			ParticleSample [] pair;
			float overlap;
			int numSamples = clip.GetSamplesPairAtTimeDelta<ParticleSample>( prevTime, currentTime, track, out pair, out overlap );
			if ( numSamples > 0 ) {
				var ps = pair[0];
				if ( prevTime < ps.StartTime && ps.StartTime <= currentTime ) {
					events.Add( new OnEnterParticle {
						Tag = ps.Tag,
					} );
				}
			}
		}
	}

	public class ScrubResult
	{
		// can go over 1
		public float NormalizedTime;
		public Dictionary<GameObject,Vector3> Facing;
		public Dictionary<GameObject,Vector3> Position;
		public ScrubEvent [] Events;
	}

	public static float SimpleScrub( Clip clip, int numTracks, float prevTime, float currentTime, bool skipCamera = false ) 
	{
		ScrubEvent [] scrubEvents;
		return ScrubAndApplyActorTransforms( clip, numTracks, prevTime, currentTime, out scrubEvents, skipCamera: skipCamera );
	}
	
	public static float ScrubAndApplyActorTransforms( Clip clip, int numTracks, float prevTime, float currentTime, out ScrubEvent [] scrubEvents, 
														ActorHint [] hints = null, bool skipCamera = false )
	{
		ScrubResult result = Scrub( clip, numTracks, prevTime, currentTime, hints: hints, skipCamera: skipCamera );
		foreach ( var kv in result.Position ) {
			Transform t = kv.Key.transform;
			t.position = kv.Value;
		}
		foreach ( var kv in result.Facing ) {
			Transform t = kv.Key.transform;
			t.forward = kv.Value;
		}
		scrubEvents = result.Events;
		return result.NormalizedTime;
	}
	
	// times are in seconds from clip start
	public static ScrubResult Scrub( Clip clip, int numTracks, float prevTime, float currentTime, 
										ActorHint [] hints = null, bool skipCamera = false )
	{
		List<ScrubEvent> eventsList = new List<ScrubEvent>();
		if ( ! skipCamera ) {
			// sample the camera lookat point first
			// because setting camera position needs to update its lookat too
			Vector3 camLookat = ScrubCamLookats( clip, numTracks, prevTime, currentTime );
			ScrubCamFollowPath( clip, numTracks, prevTime, currentTime, camLookat );
		}
		ScrubAnimations( clip, numTracks, prevTime, currentTime, hints, eventsList );
		ScrubParticles( clip, numTracks, prevTime, currentTime, eventsList );
		Dictionary<GameObject,Vector3> facingDict = new Dictionary<GameObject,Vector3>();
		Dictionary<GameObject,Vector3> posDict = new Dictionary<GameObject,Vector3>();
		for ( int track = 0; track < numTracks; track++ ) {
			MovementSample [] pair;
			float overlap;
			int numSamples = clip.GetSamplesPairAtTimeDelta<MovementSample>( prevTime, currentTime, track, out pair, out overlap );
			if ( numSamples > 0 ) {
				MovementSample leadSample = pair[0];
				// FIXME: use different hints to get actor for movement
				GameObject go = GetActorFromHints( leadSample.ActorPrefab, hints );
				if ( go == null ) {
					Animator fallbackAnimator = null; 
					if ( hints != null ) {
						foreach ( var h in hints ) {
							if ( h.NameInClip == "AnyAnimatedActor" ) {
								// use this animator for all mecanim samples
								fallbackAnimator = h.ObjectInScene.GetComponent<Animator>();
							}
						}
					}
					if ( fallbackAnimator == null ) {
						go = GameObject.Find( leadSample.ActorPrefab );
					} else {
						go = fallbackAnimator.gameObject;
					}
				}
				if ( go != null ) {
					//Transform t = go.transform;
					float time = ClampedLocalTime( currentTime, leadSample.StartTime, leadSample.Duration );
					//t.position = Vector3.Lerp( leadSample.Start.Position, leadSample.End.Position, time );
					posDict[go] = Vector3.Lerp( leadSample.Start.Position, leadSample.End.Position, time );
					if ( leadSample.Start.Facing.sqrMagnitude > 0.0001f 
						&& leadSample.End.Facing.sqrMagnitude > 0.0001f ) {
						//t.forward = Vector3.Slerp( leadSample.Start.Facing, leadSample.End.Facing, time );
						facingDict[go] = Vector3.Slerp( leadSample.Start.Facing, leadSample.End.Facing, time ); 
					}
				}
			}
		}
		ScrubExtensions( clip, numTracks, prevTime, currentTime, hints, eventsList );
		ScrubResult sr = new ScrubResult {
			NormalizedTime = currentTime / clip.Duration,
			Facing = facingDict,
			Position = posDict,
			Events = eventsList.ToArray(),
		};
		return sr;
	}

	//public class ScrubCrtParams
	//{
	//	public Clip Clip; 
	//	public float StartTime; 
	//	public ScrubEvent StopEvent;
	//	public Timing Timing;
	//	public ActorHint [] hints;

	//	public float OutScrubTime;
	//}

	//public static IEnumerator<NextUpdate> ScrubCrt( ScrubCrtParams prms )
	//{
	//	int numTracks = prms.Clip.GetNumberOfTracks();
	//	prms.OutScrubTime = prms.StartTime;
	//	float prevTime = prms.OutScrubTime;
	//	float normalizedTime = 0;
	//	while (normalizedTime < 1) {
	//		prms.OutScrubTime += prms.Timing.Delta;
	//		ScrubEvent [] events;
	//		normalizedTime = Scrub(prms.Clip, numTracks, prevTime, prms.OutScrubTime, 
	//									out events, NavigationSettings.ScrubActors);
	//		prevTime = prms.OutScrubTime;
	//		foreach (var se in events) {
	//			if (se.Equals(prms.StopEvent)) {
	//				yield break;
	//			}
	//		}
	//		yield return NextUpdate.NextFrame;
	//	}
	//	if (NavigationSettings.ScrubStop != null) {
	//		Debug.LogError("Navigation didn't hit the expected timeline stop event.");
	//	}
	//}
}

}
