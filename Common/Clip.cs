using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DMB
{

[Serializable]
public class Moment
{
    public string Name = "__NOT_SET__";
    public float Time;
}

[Serializable]
public abstract class Sample
{
    public const string NOT_SET_TOKEN = "__NOT_SET__";
    public abstract Color Color { get; }

    public int Track;
    public float StartTime;
    public float Duration;
    public float EndTime { get { return StartTime + Duration; } }
    public string Tag = NOT_SET_TOKEN;
    public string ActorPrefab = "UNKNOWN";

    protected void CopyBase( Sample dest )
    {
        dest.Track = Track;
        dest.StartTime = StartTime;
        dest.Duration = Duration;
        dest.Tag = Tag;
        dest.ActorPrefab = ActorPrefab;
    }

    public abstract Sample VClone();

    public virtual void Clear()
    {
    }

    // these have shorter named wrappers in their respective class
    public T CloneResize<T>( float newDuration ) where T : Sample
    {
        var clone = VClone();
        clone.Duration = newDuration;
        return ( T )clone;
    }

    public T CloneExtend<T>( float extension ) where T : Sample
    {
        var clone = VClone();
        clone.Duration += extension;
        return ( T )clone;
    }

    public Sample CloneRebase( int baselineTrack )
    {
        var clone = VClone();
        clone.Track += baselineTrack;
        return clone;
    }

    public T CloneSetStart<T>( float startTime ) where T : Sample
    {
        var clone = VClone();
        clone.StartTime = startTime;
        return ( T )clone;
    }

    public T CloneShift<T>( float startTimeOffset ) where T : Sample
    {
        var clone = VClone();
        clone.StartTime += startTimeOffset;
        return ( T )clone;
    }
}

public abstract class KeyedSample : Sample
{
    public abstract void SetKeyTime( int key, float time );
    public abstract float[] GetKeyTimes();
    public abstract void RemoveKey( int key );
}

[Serializable]
public class CameraPathKey 
{
    public Vector3 [] Node = new Vector3[3];
    public float TimeNorm;

    // FIXME: implement it 
    public CameraPathKey Clone()
    {
        var clone = new CameraPathKey();
        clone.Node[0] = Node[0];
        clone.Node[1] = Node[1];
        clone.Node[2] = Node[2];
        clone.TimeNorm = TimeNorm;
        return clone;
    }
}

[Serializable]
public class CameraPathSample : KeyedSample
{
    public override Color Color { get { return IsLookatPath ? new Color( 1, 0.75f, 0 ) : Color.white; } }

    public bool IsLookatPath;
    // FIXME: obsolete
    public List<Vector3> Points = new List<Vector3>();
    public List<CameraPathKey> Keys = new List<CameraPathKey>();

    public override void Clear()
    {
        Points.Clear();
        Keys.Clear();
    }

    public CameraPathSample Clone()
    {
        var clone = new CameraPathSample();
        CopyBase( clone );
        clone.IsLookatPath = IsLookatPath;
        // ports old points to keys
        if ( Points.Count > 0 ) {
            Debug.Log( "Importing old camera points, moving to keys..." );
            clone.SetPoints( Points.ToArray() );
        } else {
            foreach ( var k in Keys ) {
                clone.Keys.Add( k.Clone() );
            }
        }
        return clone;
    }
    
    public override Sample VClone()
    {
        return Clone();
    }

    public CameraPathSample CloneResize( float newDuration )
    {
        return CloneResize<CameraPathSample>( newDuration );
    }

    public CameraPathSample CloneExtend( float extension )
    {
        return CloneExtend<CameraPathSample>( extension );
    }

    public CameraPathSample CloneShift( float startTimeOffset )
    {
        return CloneShift<CameraPathSample>( startTimeOffset );
    }

    private static int NumKeysFromPoints( int numPoints )
    {
        return ( 2 + numPoints ) / 3;
    }

    // points for spline drawing
    public Vector3[] GetPoints() 
    {
        var n = Keys.Count;
        if ( n < 2 ) {
            return new Vector3[0];
        }
        var points = new Vector3[n * 3 - 2];
        points[0] = Keys[0].Node[1];
        points[1] = Keys[0].Node[2];
        int p = 2;
        for ( int i = 1; i < n - 1; i++ ) {
            for ( int j = 0; j < 3; j++ ) {
                points[p++] = Keys[i].Node[j];
            }
        }
        points[p++] = Keys[n - 1].Node[0];
        points[p++] = Keys[n - 1].Node[1];
        return points;
    }

    public void SetPoints( Vector3 [] points ) 
    {
        int n = points.Length;
        int nkfp = NumKeysFromPoints( n );
        if ( Keys.Count != nkfp ) {
            Keys.Clear();
            float t = 0;
            float step = 1.0f / ( nkfp - 1 );
            for ( int i = 0; i < nkfp; i++ ) {
                var k = new CameraPathKey();
                k.TimeNorm = t;
                Keys.Add( k );
                t += step;
            }
            Debug.Log( "Importing points: " + n );
        }
        for( int i = 0; i < n; i++ ) {
            UpdatePoint( i, points[i] );
        }
    }

    public int KeyFromPoint( int pointIndex )
    {
        return ( pointIndex + 1 ) / 3;
    }

    public void UpdatePoint( int pointIndex, Vector3 newValue )
    {
        int key = KeyFromPoint( pointIndex );
        int i = ( pointIndex + 1 ) % 3;
        Keys[key].Node[i] = newValue;
    }
    
    public override void SetKeyTime( int key, float time )
    {
        Keys[key].TimeNorm = time;
    }

    public override void RemoveKey( int key )
    {
        Keys.RemoveAt( key );
    }

    public override float[] GetKeyTimes() 
    {
        var n = Keys.Count;
        var times = new float[n];
        for ( int i = 0; i < n; i++ ) {
            times[i] = Keys[i].TimeNorm;
        }
        return times;
    }
}

[Serializable]
public struct MecanimInternals
{
    public float Duration;
    public Vector3 Offset;
    public float Speed;
    public bool IsLooping;
}

[Serializable]
public class MecanimSample : Sample
{
    public override Color Color { get { return Color.green; } }

    public string State = "UNKNOWN";
    public bool Looped;
    public float OneShotDuration { get { return TakeDurationNorm * Internals.Duration; } }

    // the part of the clip actually used for sampling
    public float TakeStartNorm = 0;
    public float TakeDurationNorm = 1;

    // All clip import settings stuff
    public MecanimInternals Internals;

    public MecanimSample Clone()
    {
        var clone = new MecanimSample();
        CopyBase( clone );
        clone.State = State;
        clone.Looped = Looped;
        clone.TakeStartNorm = TakeStartNorm;
        clone.TakeDurationNorm = TakeDurationNorm;
        clone.Internals = Internals;
        return clone;
    }

    public override Sample VClone()
    {
        return Clone();
    }

    public MecanimSample CloneShift( float startTimeOffset )
    {
        return CloneShift<MecanimSample>( startTimeOffset );
    }

    public MecanimSample CloneResize( float newDuration )
    {
        return CloneResize<MecanimSample>( newDuration );
    }
}

[Serializable]
public class FollowPathKey
{
    public Vector3 Position;
    public Vector3 Facing;
    public float TimeNorm;
    public float ChasePointOffsetOverride;

    public FollowPathKey Clone()
    {
        var clone = new FollowPathKey();
        clone.Position = Position;
        clone.Facing = Facing;
        clone.TimeNorm = TimeNorm;
        clone.ChasePointOffsetOverride = ChasePointOffsetOverride;
        return clone;
    }
}

[Serializable]
public class FollowPathSample : KeyedSample
{
    public override Color Color { get { return Color.magenta; } }

    public float ChasePointOffset = 0.1f;
    // FIXME: obsolete someday
    public List<Vector3> Points = new List<Vector3>();
    public List<FollowPathKey> Keys = new List<FollowPathKey>();

    public void UpdatePositions( Vector3[] positions )
    {
        var n = Mathf.Min( Keys.Count, positions.Length );
        for ( int i = 0; i < n; i++ ) {
            Keys[i].Position = positions[i];
        }
    }

    public Vector3[] GetPositions() 
    {
        var n = Keys.Count;
        var positions = new Vector3[n];
        for ( int i = 0; i < n; i++ ) {
            positions[i] = Keys[i].Position;
        }
        return positions;
    }

    public Vector3 GetSegmentVector( int index )
    {
        return Keys[index + 1].Position - Keys[index].Position;
    }

    public float GetSegmentSqLength( int index )
    {
        if ( index >= Keys.Count - 1 ) {
            return 0;
        }
        return GetSegmentVector( index ).sqrMagnitude;
    }

    public float GetSegmentLength( int index )
    {
        return Mathf.Sqrt( GetSegmentSqLength( index ) );
    }

    public void UpdateTimes( float [] times )
    {
        var n = Mathf.Min( Keys.Count, times.Length );
        for ( int i = 0; i < n; i++ ) {
            Keys[i].TimeNorm = times[i];
        }
    }

    public FollowPathSample Clone()
    {
        var clone = new FollowPathSample();
        CopyBase( clone );
        clone.ChasePointOffset = ChasePointOffset;
        clone.Points.AddRange( Points );
        foreach ( var k in Keys ) {
            clone.Keys.Add( k.Clone() );
        }
        return clone;
    }

    public override void SetKeyTime( int key, float time )
    {
        Keys[key].TimeNorm = time;
    }

    public override float[] GetKeyTimes() 
    {
        var n = Keys.Count;
        var times = new float[n];
        for ( int i = 0; i < n; i++ ) {
            times[i] = Keys[i].TimeNorm;
        }
        return times;
    }

    public override void RemoveKey( int key )
    {
        Keys.RemoveAt( key );
    }


    public override void Clear()
    {
        Points.Clear();
        Keys.Clear();
    }

    public override Sample VClone()
    {
        return Clone();
    }
}

[Serializable]
public class ParticleSample : Sample
{
    public override Color Color { get { return Color.red; } }

    public ParticleSample Clone()
    {
        var clone = new ParticleSample();
        CopyBase( clone );
        return clone;
    }

    public override Sample VClone()
    {
        return Clone();
    }

    public ParticleSample CloneShift( float startTimeOffset )
    {
        return CloneShift<ParticleSample>( startTimeOffset );
    }

    public ParticleSample CloneSetStart( float startTime )
    {
        return CloneSetStart<ParticleSample>( startTime );
    }

}

[Serializable]
public class MovementKey
{
    public Vector3 Position;
    public Vector3 Facing;

    public MovementKey Clone()
    {
        var clone = new MovementKey();
        clone.Position = Position;
        clone.Facing = Facing;
        return clone;
    }

    public void Mul4x4( Matrix4x4 m )
    {
        Position = m.MultiplyPoint3x4( Position );
        Facing = m.MultiplyVector( Facing );
    }
}

[Serializable]
public class MovementSample : Sample
{
    public override Color Color { get { return Color.cyan; } }

    public MovementKey Start, End;

    public MovementSample Clone()
    {
        var clone = new MovementSample();
        CopyBase( clone );
        clone.Start = Start.Clone();
        clone.End = End.Clone();
        return clone;
    }

    public override Sample VClone()
    {
        return Clone();
    }

    public MovementSample CloneOffsetFace( float startTimeOffset, Vector3 startFacing, Vector3 endFacing )
    {
        var clone = CloneShift( startTimeOffset );
        clone.Start.Facing = startFacing;
        clone.End.Facing = endFacing;
        return clone;
    }

    public void Mul4x4( Matrix4x4 m )
    {
        Start.Mul4x4( m );
        End.Mul4x4( m );
    }

    public MovementSample CloneOffsetMul4x4( float startTimeOffset, Matrix4x4 m )
    {
        var clone = CloneShift( startTimeOffset );
        clone.Mul4x4( m );
        return clone;
    }

    public MovementSample CloneMul4x4( Matrix4x4 m )
    {
        var clone = Clone();
        clone.Mul4x4( m );
        return clone;
    }

    public MovementSample CloneFace( Vector3 startFacing, Vector3 endFacing )
    {
        var clone = Clone();
        clone.Start.Facing = startFacing;
        clone.End.Facing = endFacing;
        return clone;
    }

    public MovementSample CloneShift( float shift )
    {
        return CloneShift<MovementSample>( shift );
    }

    public MovementSample CloneResize( float newDuration )
    {
        return CloneResize<MovementSample>( newDuration );
    }
}

[Serializable]
public partial class Clip
{
    // these are setup in separate lists just to show nice in the inspector
    public List<CameraPathSample> CamPathSamples = new List<CameraPathSample>();
    public List<MecanimSample> MecanimSamples = new List<MecanimSample>();
    public List<FollowPathSample> FollowPathSamples = new List<FollowPathSample>();
    public List<ParticleSample> ParticleSamples = new List<ParticleSample>();
    public List<MovementSample> MovementSamples = new List<MovementSample>();
    public List<Moment> Moments = new List<Moment>();
    // FIXME: make this part of the Sequencer/ClipData and remove it from the clip
    // FIXME: clips are infinite -- just samples and moments
    public float Duration;

    private struct ListInfo
    {
        public string PropName;
        public IList List;
    }

    private Dictionary<Type,ListInfo> _allLists = new Dictionary<Type,ListInfo>();

    public Clip()
    {
        FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach ( var f in fields ) {
            object val = f.GetValue( this );
            if ( f.FieldType.IsGenericType && val is IList  ) {
                Type[] typeArguments = f.FieldType.GetGenericArguments();
                if ( typeArguments.Length == 1 
                        && typeArguments[0].IsSubclassOf( typeof( Sample ) ) ) {
                    _allLists[typeArguments[0]] = new ListInfo {
                        List = val as IList,
                        PropName = f.Name,
                    };
                    //Debug.Log( "Found sample list '" + f.Name + "'." );
                }
            }
        }
    }

    private IList GetList( Type sampleType ) 
    {
        return _allLists[sampleType].List;
    }

    private IList GetList( Sample sample ) 
    {
        return GetList( sample.GetType() );
    }

    private IList GetList<T>() where T : Sample
    {
        return GetList( typeof( T ) );
    }

    public void Import( Clip other, int baselineTrack )
    {
        other.ForEachSample( s => { 
            GetList( s ).Add( s.CloneRebase( baselineTrack ) );
        } );
    }

    public Sample CloneSample( Sample sample ) 
    {
        Sample clone = sample.VClone();
        GetList( sample ).Add( clone );
        return clone;
    }

    public void ForEachSample( Action<Sample> a ) {
        foreach ( var li in _allLists.Values ) {
            foreach ( Sample s in li.List ) {
                a( s );
            }
        }
    }

    public void ClearAllLists( bool clearDuration = false )
    {
        ForEachSample( s => {
            s.Clear();
        } );
        foreach ( var li in _allLists.Values ) {
            li.List.Clear();
        }
        Moments.Clear();
        if ( clearDuration ) {
            Duration = 0;
        }
    }

    public void SortSamplesByTime()
    {
        foreach ( var li in _allLists.Values ) {
            bool stillGoing = true;
            while (stillGoing) {
                stillGoing = false;
                for (int i = 0; i < li.List.Count-1; i++) {
                    Sample x = li.List[i] as Sample;
                    Sample y = li.List[i + 1] as Sample;
                    if (x.StartTime > y.StartTime) {
                        li.List[i] = y;
                        li.List[i + 1] = x;
                        stillGoing = true;
                    }
                }
            }
        }
    }

    public void RemoveSample( Sample sample )
    {
        if ( sample != null ) {
            foreach ( var li in _allLists.Values ) {
                int idx = li.List.IndexOf( sample );
                if ( idx >= 0 ) {
                    li.List.RemoveAt( idx );
                    break;
                }
            }
        }
    }
    
    public void RemoveSamples( Func<Sample,bool> condition )
    {
        foreach ( var li in _allLists.Values ) {
            for ( int i = li.List.Count - 1; i >= 0; i-- ) {
                Sample s = li.List[i] as Sample;
                if ( condition( s ) ) {
                    li.List.RemoveAt( i );
                }
            }
        }
    }

    public bool FindFirstSampleParams( Func<Sample,bool> condition, 
                                        out Sample result, 
                                        out int indexInList,
                                        out int listCount,
                                        out string propName )
    {
        foreach ( var li in _allLists.Values ) {
            for ( int i = 0; i < li.List.Count; i++ ) {
                Sample s = li.List[i] as Sample;
                if ( condition( s ) ) {
                    indexInList = i;
                    propName = li.PropName;
                    listCount = li.List.Count;
                    result = s;
                    return true;
                }
            }
        }
        indexInList = -1;
        propName = null;
        listCount = -1;
        result = null;
        return false;
    }

    public void ImportAfterLastTrack( Clip other )
    {
        int numTracks = GetNumberOfTracks();
        Import( other, numTracks );
    }

    public bool GetBindMaster( Sample slave, out MecanimSample master )
    {
        if ( GetTopTrackSampleByTag<MecanimSample>( slave.Tag, out master ) ) {
            return master != slave;
        }
        return false;
    }

    public bool GetBinds( MecanimSample master, out Sample [] slaves )
    {
        List<Sample> slavesList = new List<Sample>();
        ForEachSample ( s => {
            if ( ! ( s is MecanimSample ) && s.Tag == master.Tag ) {
                slavesList.Add( s );
            }
        } );
        if ( slavesList.Count > 0 ) {
            slaves = slavesList.ToArray();
            return true;
        }
        slaves = null;
        return false;
    }

    public bool CloneSample<T>( Sample sample, out Sample clone ) where T : Sample
    {
        clone = null;
        if ( sample is T ) {
            clone = sample.VClone();
            GetList<T>().Add( ( T )clone );
            return true;
        }
        return false;
    }

    private static bool IsMergeMatch( Sample template, Sample toMerge )
    {
        // we compare track too so we have a way to have 'masters' to animations
        // i.e. the rocket launcher animations are slave (have lower track) to the heavy animations, 
        // we don't check sample actors because we want to merge i.e. sniper into assault 
        if ( template.Track == toMerge.Track && template.Tag != Sample.NOT_SET_TOKEN ) {
            return template.Tag == toMerge.Tag;
        }
        return false;
    }

    // the sample to merge may come from another clip
    public bool TryMergeInSample<T>( Clip overrides, T toMerge ) where T : Sample
    {
        var list = ( List<T> )GetList<T>();
        //string overwriteActor = null;
        T mergedSample = null;
        for ( int i = 0; i < list.Count; i++ ) {
            T template = list[i];
            if ( IsMergeMatch( template, toMerge ) ) {
                float templateFI, templateFO;
                T prev, next;
                GetSamplesAround<T>( i, out prev, out next );
                GetSampleOverlaps<T>( template, prev, next, out templateFI, out templateFO );
                mergedSample = ( T )toMerge.VClone();
                float overrideFI, overrideFO;
                overrides.GetSampleOverlaps<T>( toMerge, out overrideFI, out overrideFO );
                float fadeIn = overrideFI > 0.001f ? overrideFI : templateFI;
                float fadeOut = overrideFO > 0.001f ? overrideFO : templateFO;
                if ( fadeIn > 0.001f ) {
                    if ( prev != null ) {
                        mergedSample.StartTime = prev.EndTime - fadeIn;
                    } else {
                        mergedSample.StartTime = template.StartTime;
                    }
                }
                if ( fadeOut > 0.001f ) {
                    if ( next != null ) {
                        float oldStart = next.StartTime;
                        float newStart = mergedSample.EndTime - fadeOut;
                        float shift = newStart - oldStart;
                        ShiftSamplesRightOfCursor( next.StartTime, template.Track, shift );
                    }
                }
                mergedSample.Track = list[i].Track;
                mergedSample.ActorPrefab = list[i].ActorPrefab;
                //overwriteActor = list[i].ActorPrefab;
                list[i] = mergedSample;
                break;
            }
        }
        return mergedSample != null;
    }

    // assumes animations are already merged in
    public void AlignBindsAfterMerge<T>( Clip fallback, Clip overrides ) where T : Sample
    {
        // animations on highest track are considered 'masters'
        // all other samples with the same tag -- 'slaves'
        List<T> list = ( List<T> )GetList<T>();
        foreach ( var s in list ) {
            MecanimSample templateMaster = null;
            T template = null;
            // look up master/slave pair in overrides
            if ( overrides.GetBindMaster( s, out templateMaster ) ) {
                if ( overrides.GetTopTrackSampleByTag<T>( s.Tag, out template ) ) {
                    //Debug.Log( s + " '" + s.Tag + "' template is in overrides " + templateMaster.ActorPrefab );
                }
            } 
            // look up master/slave pair in fallback
            if ( templateMaster == null || template == null ) {
                if ( fallback.GetBindMaster( s, out templateMaster ) ) {
                    if ( fallback.GetTopTrackSampleByTag<T>( s.Tag, out template ) ) {
                        //Debug.Log( s + " '" + s.Tag + "' template is in fallback " + templateMaster.ActorPrefab );
                    }
                }
            }
            if ( templateMaster != null && template != null ) {
                MecanimSample master;
                if ( GetBindMaster( s, out master ) ) {
                    float offset = template.StartTime - templateMaster.StartTime;
                    s.StartTime = master.StartTime;
                    ShiftSamplesRightOfCursor( s.StartTime, s.Track, offset );
                    // if master was resized, we need to resize too
                    s.Duration += master.Duration - templateMaster.Duration;
                }
            } else {
                //Debug.Log( s + " '" + s.Tag + "' has no master sample." );
            }
        }
    }

    public void RemoveUntaggedSamples<T>() where T : Sample
    {
        RemoveSamples<T>( s => s.Tag == Sample.NOT_SET_TOKEN );
    }

    public void RemoveUntaggedSamples()
    {
        RemoveSamples( s => s.Tag == Sample.NOT_SET_TOKEN );
    }

    public void MergeList<T>( Clip overrides, bool silent = true ) where T : Sample
    {
        List<T> list = ( List<T> )overrides.GetList<T>();
        // TODO: collect to the left of and to the right of merged
        // TODO: insert properly
        foreach ( var s in list ) {
            if ( TryMergeInSample<T>( overrides, s ) ) {
                if ( ! silent ) {
                    Debug.Log( "Merge_f: Merged " + typeof( T ) + " '" + s.Tag + "'." );
                }
            }
        }
    }

    public int GetNumberOfTracks()
    {
        int maxTrack = -1;
        ForEachSample( s => {
            maxTrack = Mathf.Max( maxTrack, s.Track );
        } );
        return maxTrack + 1;
    }

    public void ShiftSamplesRightOfCursor( float cursorTime, int track, float shift, 
                                            bool stretchPrev = false )
    {
        if ( shift * shift < 0.00001f ) {
            return;
        }
        ForEachSample( s => {
            if ( s.Track == track ) {
                if ( s.StartTime >= cursorTime ) {
                    s.StartTime += shift;
                } 
                else if ( stretchPrev && s.EndTime >= cursorTime ) {
                    s.Duration += shift;
                }
            }
        } );
    }

    public void Shift( float time )
    {
        ForEachSample( s => {
            s.StartTime += time;
        } );
    }

    public bool FindFirstMoment( string name, out Moment moment )
    {
        moment = null;
        foreach ( var m in Moments ) {
            if ( m.Name == name ) {
                moment = m;
                return true;
            }
        }
        return false;
    }

    public bool FindFirstSample( Func<Sample,bool> condition, out Sample result )
    {
        int idx;
        int listCount;
        string prop;
        return FindFirstSampleParams( condition, out result, out idx, out listCount, out prop );
    }

    private bool FindFirst<T>( string nameOfList,
                                Func<Sample,bool> condition, 
                                out Sample result, 
                                out int indexInList,
                                out int numInList,
                                out string propName ) where T : Sample
    {
        List<T> list = ( List<T> )GetList<T>();
        for ( int i = 0; i < list.Count; i++ ) {
            Sample s = list[i];
            if ( condition( s ) ) {
                indexInList = i;
                propName = nameOfList;
                numInList = list.Count;
                result = s;
                return true;
            }
        }
        indexInList = -1;
        propName = null;
        numInList = -1;
        result = null;
        return false;
    }

    public bool FindFirst<T>( Func<T,bool> condition, out T match ) where T : Sample
    {
        List<T> list = ( List<T> )GetList<T>();
        foreach ( T s in  list ) {
            if ( condition( s ) ) {
                match = s; 
                return true;
            }
        }
        match = null;
        return false;
    }

    public List<T> FindInSamples<T>( Func<T,bool> condition ) where T : Sample
    {
        List<T> result = new List<T>();
        List<T> list = ( List<T> )GetList<T>();
        foreach ( T s in  list ) {
            if ( condition( s ) ) {
                result.Add( s );
            }
        }
        return result;
    }

    public List<T> FindSamplesStartingInTimeRange<T>( float startTime, float duration ) where T : Sample
    {
        return FindInSamples<T>( s => {
            return s.StartTime >= startTime && s.StartTime <= startTime + duration;
        } );
    }

    // FIXME: this is shit, should be removed
    public void BreakableForEach<T>( Func<T,bool> a ) where T : Sample
    {
        List<T> list = ( List<T> )GetList<T>();
        foreach ( T s in list ) {
            if ( ! a( s ) ) {
                return;
            }
        }
    }

    public bool GetSampleListInfo( Sample sample, out int index, out int listCount, out string propName )
    {
        Sample result;
        return FindFirstSampleParams( s => s == sample, out result, out index, out listCount, out propName );
    }

    public bool GetFirstSampleByTag<T>( string tag, out int index ) where T : Sample
    {
        index = -1;
        List<T> list = ( List<T> )GetList<T>();
        for ( int i = 0; i < list.Count; i++ ) {
            T s = list[i];
            if ( s.Tag == tag ) {
                index = i;
                return true;
            }
        }
        return false;
    }

    public bool GetFirstSampleByTrack<T>( int track, out T sample ) where T : Sample
    {
        return FindFirst<T>( s => s.Track == track, out sample );
    }

    public bool GetFirstSampleByTag<T>( string tag, out T sample ) where T : Sample
    {
        return FindFirst<T>( s => s.Tag == tag, out sample );
    }

    public bool GetTopTrackSampleByTag<T>( string tag, out int index ) where T : Sample
    {
        int minTrack = 999999;
        index = -1;
        List<T> list = ( List<T> )GetList<T>();
        for ( int i = 0; i < list.Count; i++ ) {
            T s = list[i];
            if ( s.Tag == tag && s.Track < minTrack ) {
                minTrack = s.Track;
                index = i;
            }
        }
        return index >= 0;
    }

    public bool GetTopTrackSampleByTag<T>( string tag, out T sample ) where T : Sample
    {
        int index;
        sample = null;
        if ( GetTopTrackSampleByTag<T>( tag, out index ) ) {
            List<T> list = ( List<T> )GetList<T>();
            sample = list[index];
            return true;
        }
        return false;
    }

    public void RemoveSample<T>( T sample ) where T : Sample
    {
        if ( sample != null ) {
            List<T> list = ( List<T> )GetList<T>();
            list.Remove( sample );
        }
    }

    public void RemoveSamples<T>( Func<T,bool> condition ) where T : Sample
    {
        List<T> list = ( List<T> )GetList<T>();
        for ( int i = list.Count - 1; i >= 0; i-- ) {
            if ( condition( list[i] ) ) {
                list.RemoveAt( i );
            }
        }
    }

    public void ApplyTransform( Transform transform )
    {
        foreach ( var cps in CamPathSamples ) {
            for ( int i = 0; i < cps.Points.Count; i++ ) {
                cps.Points[i] = transform.TransformPoint( cps.Points[i] );
            }
        }
        foreach ( var ms in MovementSamples ) {
            ms.Start.Position = transform.TransformPoint( ms.Start.Position );
            ms.Start.Facing = transform.TransformVector( ms.Start.Facing );
            ms.End.Position = transform.TransformPoint( ms.End.Position );
            ms.End.Facing = transform.TransformVector( ms.End.Facing );
        }
    }

    public void SortSamplesByTime<T>() where T : Sample
    {
        var list = ( List<T> )GetList<T>();
        list.Sort((a,b)=>a.StartTime.CompareTo(b.StartTime));
    }

    public T GetNextOnTrack<T>( int idx ) where T : Sample
    { 
        var list = ( List<T> )GetList<T>();
        T srcSample = list[idx];
        for ( int i = idx + 1; i < list.Count; i++ ) {
            T s = list[i];
            if ( s.Track == srcSample.Track ) {
                return s;
            }
        }
        return null;
    }

    public T GetPrevOnTrack<T>( int idx ) where T : Sample
    {
        var list = ( List<T> )GetList<T>();
        T srcSample = list[idx];
        for ( int i = idx - 1; i >= 0; i-- ) {
            T s = list[i];
            if ( s.Track == srcSample.Track ) {
                return s;
            }
        }
        return null;
    }

    public void GetSamplesOverlaps( Sample srcSample, 
                                    out Sample prevSample, out Sample nextSample, 
                                    out float fadeIn, out float fadeOut )
    {
        fadeIn = fadeOut = 0;
        GetSamplesAroundSample( srcSample, out prevSample, out nextSample );
        if ( prevSample != null && prevSample.EndTime < srcSample.EndTime ) {
            fadeIn = Mathf.Max( 0, prevSample.EndTime - srcSample.StartTime );
        }
        if ( nextSample != null && nextSample.EndTime > srcSample.EndTime ) {
            fadeOut = Mathf.Max( 0, srcSample.EndTime - nextSample.StartTime );
        }
    }

    public void GetSamplesOverlaps( Sample srcSample, out float prev, out float next )
    {
        Sample ps, ns;
        GetSamplesOverlaps( srcSample, out ps, out ns, out prev, out next );
    }

    // This one is slow, used in the editor. The per-type ones are faster and work on sorted
    public void GetSamplesAroundSample( Sample srcSample, out Sample prevSample, out Sample nextSample )
    {
        float pt = -9999999, nt = 9999999;
        Sample cPrev = null, cNext = null;
        ForEachSample( s => {
            if ( s.Track == srcSample.Track ) {
                if ( s.StartTime < srcSample.StartTime && s.StartTime > pt ) {
                    pt = s.StartTime;
                    cPrev = s;
                }
                if ( s.StartTime > srcSample.StartTime && s.StartTime < nt ) {
                    nt = s.StartTime;
                    cNext = s;
                }
            }
        } );
        prevSample = cPrev;
        nextSample = cNext;
    }

    // FIXME: same track or same actor?
    // FIXME: options maybe?
    public void GetSamplesAround<T>( T srcSample, out T prevSample, out T nextSample ) where T : Sample
    {
        prevSample = nextSample = null;
        var list = ( List<T> )GetList<T>();
        int idx = list.IndexOf( srcSample );
        if ( idx >= 0 ) {
            GetSamplesAround<T>( idx, out prevSample, out nextSample );
        }
    }

    public void GetSamplesAround<T>( int sampleIdx, out T prevSample, out T nextSample ) where T : Sample
    {
        prevSample = GetPrevOnTrack<T>( sampleIdx );
        nextSample = GetNextOnTrack<T>( sampleIdx );
    }

    public void GetSampleOverlaps<T>( int sampleIndex, out float prev, out float next ) where T : Sample
    {
        T prevSample = GetPrevOnTrack<T>( sampleIndex );
        T nextSample = GetNextOnTrack<T>( sampleIndex );
        var list = ( List<T> )GetList<T>();
        GetSampleOverlaps<T>( list[sampleIndex], prevSample, nextSample, out prev, out next );
    }

    public void GetSampleOverlaps<T>( T sample, T prevSample, T nextSample, 
                                        out float prev, out float next ) where T : Sample
    {
        prev = next = 0;
        if ( prevSample != null ) {
            prev = Mathf.Max( prevSample.EndTime - sample.StartTime, 0 );
        }
        if ( nextSample != null ) {
            next = Mathf.Max( sample.EndTime - nextSample.StartTime, 0 );
        }
    }

    public void GetSampleOverlaps<T>( T sample, out float prev, out float next ) where T : Sample
    {
        T prevSample, nextSample;
        GetSamplesAround<T>( sample, out prevSample, out nextSample );
        GetSampleOverlaps<T>( sample, prevSample, nextSample, out prev, out next );
    }

    public int GetSamplesPairAtTime<T>( float t, int track, out T [] result, out float overlap ) where T : Sample
    {
        return GetSamplesPairAtTimeDelta<T>( t, t, track, out result, out overlap );
    }

    public int GetSamplesPairAtTimeDelta<T>( float t0, float t1, int track, out T [] result, out float overlap ) where T : Sample
    {
        // FIXME: the lists are generally sorted, implement this for sorted lists by time
        // FIXME: and just use prev/next samples for overlaps
        var pair = new T[2];
        overlap = 0;
        int numSamples = 0;
        float dt = t0 - t1 > 0 ? 1 : -1;
        float minDist = 9999999;
        T fallback = null;
        BreakableForEach<T>( s => {
            if ( s.Track == track ) {
                float start = s.StartTime;
                float end = s.StartTime + s.Duration;
                if ( start <= t1 && t1 <= end ) {
                    pair[numSamples++] = s;
                } 
                float toStart = ( start - t1 ) * dt;
                float toEnd = ( end - t1 ) * dt;
                float dist = Mathf.Min( toStart < 0 ? minDist : toStart, toEnd < 0 ? minDist : toEnd );
                if ( dist < minDist ) {
                    fallback = s;
                    minDist = dist;
                }
            }
            return numSamples < 2;
        } );
        // if no sample is hit, sample the end/start of the last potentially sampled
        if ( numSamples == 0 && fallback != null ) {
            // signify fallback sampling
            overlap = -minDist;
            pair[numSamples++] = fallback;
        } else if ( numSamples == 2 ) {
            float start0 = pair[0].StartTime;
            float start1 = pair[1].StartTime;
            float end0 = start0 + pair[0].Duration;
            float end1 = start1 + pair[1].Duration;
            if ( start0 < start1 && end0 < end1 ) {
                overlap = Mathf.Max( 0, end0 - start1 );
            }
        }
        result = pair;
        return numSamples;
    }

    public float CalculateEnd()
    {
        float clipEnd = 0;
        ForEachSample( s => {
            float end = s.StartTime + s.Duration;
            if ( end > clipEnd ) {
                clipEnd = end;
            }
        } );
        return clipEnd;
    }
}

}
