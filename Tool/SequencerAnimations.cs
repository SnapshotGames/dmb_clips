#if UNITY_EDITOR

using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DMB
{

public partial class Sequencer : MonoBehaviour
{
	private class AnimationClipOverrides : List<KeyValuePair<AnimationClip, AnimationClip>>
	{
		public AnimationClipOverrides(int capacity) : base(capacity) {}

		public AnimationClip this[string name]
		{
			get { return this.Find(x => x.Key.name.Equals(name)).Value; }
			set
			{
				int index = this.FindIndex(x => x.Key.name.Equals(name));
				if (index != -1)
					this[index] = new KeyValuePair<AnimationClip, AnimationClip>(this[index].Key, value);
			}
		}
	}

	private AnimationClipOverrides _clipOverrides;

	private bool AnimGetClip(Animator animator, string clipName, out AnimationClip clip)
	{
		bool result = false;
		var rac = animator.runtimeAnimatorController;
		clip = rac.animationClips.FirstOrDefault(ac => ac.name == clipName);
		if (! clip && rac is AnimatorOverrideController) {
			if (_clipOverrides == null) {
				var aoc = rac as AnimatorOverrideController;
				_clipOverrides = new AnimationClipOverrides(aoc.overridesCount);
				aoc.GetOverrides(_clipOverrides);
			}
			clip = _clipOverrides[clipName];
		}
		if (clip != null) {
			result = true;
		} else {
			Debug.Log("Can't find animation called '" + clipName + "'");
		}
		return result;
	}

	private static Transform FindTransformInChildren(Transform t, 
														Func<Transform, bool> condition) { 
		 Transform[] children = t.GetComponentsInChildren<Transform> ();
		 foreach ( var child in children ) {
			 if ( condition( child ) ) {
				 return child;
			 }
		 }
		 return null;
	}

	private static Transform AnimGetMotionPoint(Animator animator) {
		Transform animatorTransform = animator.transform;
		Transform motionPoint = FindTransformInChildren(animatorTransform, t => 
			t.name.ToUpper().EndsWith("REFERENCE")
		);
		if ( ! motionPoint ) {
			motionPoint = FindTransformInChildren(animatorTransform, t => 
				t.name.ToUpper().EndsWith("ROOT")
			);
		}
		if (! motionPoint) {
			Debug.Log("No motion point (with suffix 'Reference' or 'Root') supplied for '" 
								+ animator.name + "'.");
			return null;
		}
		return motionPoint;
	}

	public bool AnimGetInternalsFromAnimator(string actor, string state, out MecanimInternals internals)
	{
		Animator animator;
		if ( ClipUtil.GetAnimator( actor, out animator ) ) {
			AnimationClip clip;
			if (AnimGetClip(animator, state, out clip)) {
				//var rot = animator.transform.rotation;
				//animator.transform.rotation = Quaternion.identity;
				Vector3 offset = Vector3.zero;
				Transform motionPoint = AnimGetMotionPoint(animator);
				if ( motionPoint != null ) {
					clip.SampleAnimation(animator.gameObject, 0f);
					Vector3 start = motionPoint.localPosition;
					clip.SampleAnimation(animator.gameObject, clip.length);
					Vector3 end = motionPoint.localPosition;
					offset = end - start;
				}
				internals = new MecanimInternals {
					Duration = clip.length,
					Speed = offset.magnitude / clip.length,
					Offset = offset,
					IsLooping = clip.isLooping,
				};
				animator.Update(0);	// Restore target object back to original state.
				return true;
			}
		}
		internals = new MecanimInternals();
		return false;
	}
}

}

#endif
