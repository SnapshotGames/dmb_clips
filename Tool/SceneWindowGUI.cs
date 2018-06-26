#if UNITY_EDITOR

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UEGUI = UnityEngine.GUI;

namespace DMB
{

[Serializable]
public class SWUI
{
    public Color ColorButtonInactive = new Color( 1.5f, 1.5f, 1.5f );
    public Color ColorButtonActive = new Color( 0.3f, 0.3f, 0.3f );
    public Color ColorButtonTextInactive = new Color( 0.75f, 0.75f, 0.75f );
    public Color ColorButtonTextActive = Color.white;

    public Texture2D GUIFrameTexture;
    public Font GUIFont;

    private const float LinePointSize = 0.04f;

    private GUIStyle _style;
    private GUIStyle _framedStyle;
    private bool _guiConsumedInput;
    private string _singleEditText = "";
    private List<int> _hoverControls = new List<int>();
    private bool _altDown;
    private bool _ctlDown;
    private bool _isLeftButton;
    private bool _buttonDown;
    private int _hoverControl;
    private int _dragControl;
    private Vector2 _prevMousePos;
    private UnityEngine.Object _lastDnD;
    public Rect Window { get ; private set; }

    public void Init()
    {
        _style = new GUIStyle {
            font = GUIFont,
             fontSize = 8,
             normal = new GUIStyleState {
                 background = Texture2D.whiteTexture,
                 textColor = Color.white,
             },
             alignment = TextAnchor.MiddleCenter,
             clipping = TextClipping.Clip,
        };
        _framedStyle = new GUIStyle( _style ) {
            normal = new GUIStyleState {
                background = GUIFrameTexture,
                textColor = Color.white,
            },
            border = new RectOffset( 2, 2, 2, 2 ),
        };
    }

    public static Color CCol( Color c ) 
    {
        return PlayerSettings.colorSpace == ColorSpace.Linear ? c.linear : c;
    }

    public void DrawText( string text, float x, float y, float width, float height, bool shadow = false, Color? color = null )
    {
        if ( shadow ) {
            Shadow( x, y, width, height, text: text );
        }
        UEGUI.backgroundColor = new Color( 0, 0, 0, 0 );
        UEGUI.contentColor = CCol( color == null ? Color.white : color.Value );
        UEGUI.Label( new Rect( x, y, width, height ), text, _style );
    }

    public void FramedBox( float x, float y, float width, float height, string text = "", 
                                Color? contentColor = null, Color? bgrColor = null ) 
    {
        UEGUI.backgroundColor = bgrColor ?? Color.white;
        UEGUI.contentColor = contentColor ?? Color.white;
        UEGUI.Label( new Rect( x, y, width, height ), text, _framedStyle );
    }

    public string EditBox( string str, float x, float y, float width, float height, Color? bgrColor = null ) 
    {
        UEGUI.backgroundColor = CCol( bgrColor ?? new Color( 0.3f, 0.3f, 0.3f ) );
        UEGUI.contentColor = CCol( new Color( 0.75f, 0.75f, 0.75f ) );
        return UEGUI.TextField( new Rect( x, y, width, height ), str, 32, _style );
    }

    public enum Result {
        None,
        HoverEnter,
        HoverExit,
        LBDown,
        LBUp,
        RBDown,
        RBUp,
        Drag,
        DragAndDropExit,
    }

    public void DrawTexture( float x, float y, float w, float h, Texture2D texture, Color? color = null, bool shadow = false )
    {
        bool flipX = w < 0;
        bool flipY = h < 0;
        w = Mathf.Abs( w );
        h = Mathf.Abs( h );
        UEGUI.color = CCol( color != null ? color.Value : Color.white );
        UEGUI.DrawTextureWithTexCoords( new Rect( x, y, w, h ), texture, 
                                        new Rect( 0, 0, flipX ? -1 : 1, flipY ? -1 : 1 ) );
    }

    public void DrawFillBox( float x, float y, float w, float h, Color color )
    {
        UEGUI.color = Color.white;
        UEGUI.backgroundColor = CCol( color );
        UEGUI.Label( new Rect( x, y, w, h ), null as Texture2D, _style );
    }

    public void Shadow( float x, float y, float w, float h, string text = null, Texture2D texture = null ) 
    {
        Vector2 [] off = {
            new Vector2( -1, -1 ),
            new Vector2( -1,  0 ),
            new Vector2( -1,  1 ),
            new Vector2(  1, -1 ),
            new Vector2(  1,  0 ),
            new Vector2(  1,  1 ),
            new Vector2(  0, -1 ),
            new Vector2(  0,  1 ),
        };
        Rect rect = new Rect( x, y, w, h );
        if ( text != null ) {
            UEGUI.contentColor = Color.black;
            UEGUI.backgroundColor = new Color( 0, 0, 0, 0 );
            foreach ( var v in off ) {
                UEGUI.Label( new Rect( x + v.x, y + v.y, w, h ), text, _style ); 
            }   
        } else if ( texture != null ) {
            UEGUI.contentColor = Color.black;
            UEGUI.backgroundColor = new Color( 0, 0, 0, 0 );
            foreach ( var v in off ) {
                UEGUI.Label( new Rect( x + v.x, y + v.y, w, h ), texture, _style ); 
            }
        } else {
            UEGUI.Label( rect, "", _style); 
        }
    }

    public struct PopupMenuItem
    {
        public string Text;
        public Action Action;
        public PopupMenuItem( string t, Action a )
        {
            Text = t;
            Action = a;
        }
    }

    public void CreatePopupMenu( List<PopupMenuItem> items )
    {
        List<string> cmitems = new List<string>();
        foreach ( var i in items ) {
            cmitems.Add( i.Text );
        }
        Vector2 mousePos = Event.current.mousePosition;
        Pop( () => {
            int selection;
            if ( ! ContextMenu( mousePos, cmitems, out selection ) ) {
                if ( selection >= 0 ) {
                    items[selection].Action();
                }
                return false;
            }
            return true;
        } );
    }

    public void CreatePopupMenu( Dictionary<string,Action> items, string delimiter )
    {
        var keys = items.Keys.ToList();
        var values = items.Values.ToList();
        Vector2 mousePos = Event.current.mousePosition;
        Pop( () => {
            int selection;
            if ( ! ContextMenu( mousePos, keys, out selection ) ) {
                if ( selection >= 0 ) {
                    //if ( keys[selection] == delimiter ) {
                    //  return true;
                    //}
                    values[selection]();
                }
                return false;
            }
            return true;
        } );
    }

    public static bool LinePoint( Vector4 selectedPoint, bool selectedSpline, bool showPosHandle, out Vector3 newPoint, bool selectedIndex = false, bool isSecondary = false ) 
    {
        selectedIndex = selectedSpline && selectedIndex;
        float alpha = selectedSpline ? 1 : 0.3f;
        Handles.color = new Color( 1, 1, 1, alpha );
        Vector3 snap = Vector3.one * 0.1f;
        float scale;
        float selScale = selectedSpline ? 1 : 0.3f;
        if ( isSecondary && ! selectedIndex ) {
            scale = 0.7f;
        } else {
            scale = selectedIndex ? 1.5f : 0.7f;
        }
        float size = HandleUtility.GetHandleSize( selectedPoint ) * scale * selScale;
        int hot = GUIUtility.hotControl;
        newPoint = Handles.FreeMoveHandle( selectedPoint, Quaternion.identity, LinePointSize * size,
                                                    snap, Handles.RectangleHandleCap );
        if ( selectedSpline && ( showPosHandle || Event.current.shift ) ) {
            newPoint = Handles.PositionHandle(selectedPoint, Quaternion.identity);
        }
        bool result = hot != GUIUtility.hotControl;
        // if the hot control is not reset, some random point gets dragged instead
        // when selecting new spline, (old one hides its tangent handles)
        if ( ! selectedSpline && result ) {
            GUIUtility.hotControl = 0;
        }
        return result;
    }

    public Result InteractBoxSimple( float x, float y, float w, float h, out bool hover,
                                    string text = null,
                                    Texture2D texture = null, bool skipBackground = false, 
                                    bool shadow = false,
                                    Color? colorInactiveBgr = null, Color? colorActiveBgr = null, 
                                    Color? colorInactive = null, Color? colorActive = null )
    {
        Vector2 clampedDrag;
        UnityEngine.Object dragAndDrop;
        return InteractBox( x, y, w, h, out hover, 
                                out clampedDrag, out dragAndDrop, text,
                                texture, skipBackground, 
                                shadow,
                                colorInactiveBgr, colorActiveBgr, 
                                colorInactive, colorActive );
    }

    public Result InteractBoxSimple( float x, float y, float w, float h, 
                                    string text = null, 
                                    Texture2D texture = null, bool skipBackground = false, 
                                    bool shadow = false,
                                    Color? colorInactiveBgr = null, Color? colorActiveBgr = null, 
                                    Color? colorInactive = null, Color? colorActive = null )
    {
        bool hover;
        return InteractBoxSimple( x, y, w, h, out hover, 
                                        text, texture, skipBackground, shadow, colorInactiveBgr, 
                                        colorActiveBgr, colorInactive, colorActive );
    }

    public Result InteractBox( float x, float y, float w, float h, out bool hover, 
                                    out Vector2 clampedDrag, out UnityEngine.Object dragAndDrop, string text = null, 
                                    Texture2D texture = null, bool skipBackground = false, 
                                    bool shadow = false,
                                    Color? colorInactiveBgr = null, Color? colorActiveBgr = null, 
                                    Color? colorInactive = null, Color? colorActive = null )
    {
        bool flipX = w < 0;
        bool flipY = h < 0;
        w = Mathf.Abs( w );
        h = Mathf.Abs( h );
        int controlID = GUIUtility.GetControlID( FocusType.Passive );
        Result result = _dragControl == controlID ? Result.Drag : Result.None;
        Rect rect = new Rect( x, y, w, h );
        bool boxContainsMouse = rect.Contains( Event.current.mousePosition );
        if ( boxContainsMouse ) {
            _hoverControls.Add( controlID );
            if ( _dragControl == 0 && _hoverControl == controlID ) {
                result = Result.HoverEnter;
            }
        } else {
            if ( _dragControl == 0 && _hoverControl == controlID ) {
                result = Result.HoverExit;
            }
        }
        hover = _hoverControl == controlID;
        dragAndDrop = null;
        if ( hover && _lastDnD ) {
            dragAndDrop = _lastDnD;
            _lastDnD = null;
            result = Result.DragAndDropExit;
        }
        bool hilight = hover || _dragControl == controlID;
        Color cinactBgr = colorInactiveBgr != null ? colorInactiveBgr.Value : ColorButtonInactive;
        Color cactBgr = colorActiveBgr != null ? colorActiveBgr.Value : ColorButtonActive;
        Color cinact = colorInactive != null ? colorInactive.Value : ColorButtonTextInactive;
        Color cact = colorActive != null ? colorActive.Value : ColorButtonTextActive;
        Color bgrColor = hilight ? cactBgr : cinactBgr;
        DrawFillBox( x, y, w, h, skipBackground ? new Color( 0, 0, 0, 0 ) : bgrColor );
        switch( Event.current.GetTypeForControl( controlID ) ) {
            case EventType.Repaint:
                UEGUI.color = Color.white;
                UEGUI.backgroundColor = new Color( 0, 0, 0, 0 );
                if ( shadow ) {
                    Shadow( x, y, w, h, text, texture );
                }
                if ( text != null ) {
                    UEGUI.contentColor = CCol( hilight ? cact : cinact );
                    UEGUI.Label( rect, text, _style); 
                } else if ( texture != null ) {
                    UEGUI.color = CCol( hilight ? cact : cinact );
                    float tx = rect.x + ( rect.width - texture.width ) / 2;
                    float ty = rect.y + ( rect.height - texture.height ) / 2;
                    UEGUI.DrawTextureWithTexCoords( new Rect( tx, ty, texture.width, texture.height ), texture, 
                                                new Rect( 0, 0, flipX ? -1 : 1, flipY ? -1 : 1 ) );
                } else {
                    UEGUI.Label( rect, "", _style); 
                }
                break;
            case EventType.MouseDown:
                if( hover ) {
                    if ( Event.current.button == 0 ) {
                        _dragControl = controlID;
                        result = Result.LBDown;
                    } else if ( Event.current.button == 1 ) {
                        result = Result.RBDown;
                    }
                    GUIUtility.hotControl = controlID;
                }
                break;
            case EventType.MouseUp:
                if ( _dragControl == controlID ) {
                    // zero drag control only if it has been released
                    if ( Event.current.button == 0 ) {
                        result = Result.LBUp;
                    }
                } else if ( _dragControl == 0 && _hoverControl == controlID ) {
                    if ( Event.current.button == 0 ) {
                        result = Result.LBUp;
                    } else if ( Event.current.button == 1 ) {
                        result = Result.RBUp;
                    }
                }
                if ( GUIUtility.hotControl == controlID ) {
                    GUIUtility.hotControl = 0;
                }
                if ( _dragControl != 0 ) {
                    _dragControl = 0;
                }
                break;
            case EventType.DragUpdated:
                if ( ! dragAndDrop && boxContainsMouse ) {
                    // ZAMMI: 
                    var or = DragAndDrop.objectReferences;
                    if ( or != null && or.Length > 0 ) {
                        _lastDnD = or[0];
                        _hoverControl = 0;
                        Event.current.Use();
                    }
                }
                break;
            case EventType.DragExited:
                // ZAMMI: there is a hack to give drag and drop only to hovered
                Event.current.Use();
                break;
        }
        Vector2 drag = Event.current.mousePosition - rect.position;
        clampedDrag = new Vector2( Mathf.Clamp01( drag.x / rect.width ), 
                                    Mathf.Clamp01( drag.y / rect.height ) );
        if ( result != Result.None ) {
            UEGUI.changed = true;
            // a warning is triggered when Use is called for i.e. repaint
            if ( Event.current.isMouse ) {
                Event.current.Use();
            }
        }
        return result;
    }

    public bool Button( Texture2D texture, float x, float y, float width, float height )
    {
        bool hover;
        Vector2 drag;
        UnityEngine.Object dragAndDrop;
        return InteractBox( x, y, width, height, out hover, out drag, out dragAndDrop, texture: texture ) == Result.LBUp;
    }

    private Result ButtonRes( string text, float x, float y, float width, float height ) 
    {
        bool hover;
        return ButtonRes( text, x, y, width, height, out hover );
    }

    private Result ButtonRes( string text, float x, float y, float width, float height, out bool hover ) 
    {
        Vector2 drag;
        UnityEngine.Object dragAndDrop;
        return InteractBox( x, y, width, height, out hover, out drag, out dragAndDrop, text: text );
    }

    public bool Button( string text, float x, float y, float width, float height )
    {
        bool hover;
        return ButtonRes( text, x, y, width, height, out hover ) == Result.LBUp;
    }

    private enum ModalState
    {
        Processing,
        Confirm,
        Deny,
    }

    // -1: cancel
    //  1: ok
    //  0: still processing
    private int NameDialog( out string name )
    {
        name = "";
        const float w = 200;
        const float h = 26;
        float x = ( Window.width - w ) / 2;
        float y = ( Window.height - h ) / 2;
        FramedBox( x - 10, y - 30, w + 20, h + 100 );
        FramedBox( x - 10, y - 30, w + 20, h, text: "Enter clip name.", 
                        bgrColor: new Color( 0, 0, 0, 0 ) );
        UEGUI.SetNextControlName( "singleEdit" );
        float gray = 0.1f;
        _singleEditText = EditBox( _singleEditText, x, y, w, h, 
                                        bgrColor: new Color( gray, gray, gray ) );
        float gaptop = 16;
        float gapside = 4;
        if ( Button( "Ok", x, y + h + gaptop, w / 2 - gapside, h ) ) {
            name = _singleEditText;
            return 1;
        } 
        if ( Button( "Cancel", x + w - ( w / 2 - gapside ), y + h + gaptop, w / 2 - gapside, h ) ) {
            return -1;
        } 
        return 0;
    }

    private bool ContextMenu( Vector2 origin, IList<string> items, out int selection )
    {
        float itemW = 0;
        float itemH = 18;
        foreach ( var i in items ) {
            Vector2 sz = _style.CalcSize( new GUIContent( i ) );
            if ( sz.x > itemW ) {
                itemW = sz.x;
            }
        }
        itemW += 8;
        float totalH = itemH * items.Count;
        float x = origin.x, y = origin.y;
        if ( origin.x + itemW > Window.width ) {
            x = Window.width - itemW;
        }
        if ( origin.y + totalH > Window.height ) {
            y = Window.height - totalH;
        }
        bool hover = false;
        selection = -1;
        for ( int i = 0 ; i < items.Count; i++ ) {
            bool itemHover;
            Result result = ButtonRes( items[i], x, y, itemW, itemH, out itemHover );
            if ( result == Result.LBUp ) {
                selection = i;
            }
            if ( itemHover ) {
                hover = itemHover;
            }
            y += itemH;
        }
        return selection == -1 && ( ! _buttonDown || hover );
    }

    // can't use Event.current.delta because
    // it is same for two consecutive calls
    public Vector2 MouseDelta()
    {
        return Event.current.mousePosition - _prevMousePos;
    }

    public bool IsEnterPressed()
    {
        return Event.current.isKey
            && Event.current.type == EventType.KeyDown
            && Event.current.keyCode == KeyCode.Return;
    }

    public bool IsDeletePressed()
    {
        return Event.current.isKey
            && Event.current.type == EventType.KeyDown
            && Event.current.keyCode == KeyCode.Delete;
    }

    public bool IsAltHoldDown()
    {
        return _altDown;
    }

    public bool IsCtlHoldDown()
    {
        return _ctlDown;
    }

    public void ConsumeEvent()
    {
        Event.current.Use();
    }

    public bool ClickedOutsideOfUI()
    {
        return ! _altDown && ! _ctlDown && _hoverControl == 0 
                && _buttonDown && _isLeftButton;
    }

    public void Begin()
    {
        Window = Camera.current.pixelRect;
        Handles.BeginGUI();
        if ( Event.current.isMouse ) {
            _hoverControl = ( _dragControl == 0 && _hoverControls.Count > 0 ) ? _hoverControls[_hoverControls.Count - 1] : 0;
            _hoverControls.Clear();
            _buttonDown = Event.current.type == EventType.MouseDown;
            _isLeftButton = Event.current.button == 0;
            //_isRightButton = Event.current.button == 1;
        } else if ( Event.current.isKey ) {
            //Debug.Log( Event.current.keyCode );
            bool down = Event.current.type == EventType.KeyDown;
            if ( Event.current.keyCode == KeyCode.LeftAlt ) {
                _altDown = down;
            } else if ( Event.current.keyCode == KeyCode.LeftControl ) {
                _ctlDown = down;
            }
        }
    }

    public void End( bool forceRepaint )
    {
        UpdatePopups();
        _prevMousePos = Event.current.mousePosition;
        Handles.EndGUI();
        if ( _hoverControl != 0 || forceRepaint ) {
            SceneView.RepaintAll();
        }
    }

    //public bool Update( bool forceRepaint = false )
    //{
    //  Window = Camera.current.pixelRect;
    //  Handles.BeginGUI();
    //  if ( Event.current.isMouse ) {
    //      _hoverControl = ( _dragControl == 0 && _hoverControls.Count > 0 ) ? _hoverControls[_hoverControls.Count - 1] : 0;
    //      _hoverControls.Clear();
    //      _buttonDown = Event.current.type == EventType.MouseDown;
    //      _isLeftButton = Event.current.button == 0;
    //      //_isRightButton = Event.current.button == 1;
    //  } else if ( Event.current.isKey ) {
    //      //Debug.Log( Event.current.keyCode );
    //      if ( Event.current.keyCode == KeyCode.LeftAlt ) {
    //          _altDown = Event.current.type == EventType.KeyDown;
    //      } else if ( Event.current.keyCode == KeyCode.Delete ) {
    //          //if ( CAM_CanDeleteNode() && Event.current.type == EventType.KeyDown ) {
    //          //  CAM_DeleteNode();
    //          //}
    //          // always eat delete keys while gui is on
    //          var go = Selection.activeGameObject;
    //          if ( go && ! go.GetComponent<Sequencer>() ) {
    //              Undo.DestroyObjectImmediate( go );
    //          }
    //          Event.current.Use();
    //      }
    //  }
    //  _guiState();
    //  if ( ! _altDown && _hoverControl == 0 && _buttonDown ) {
    //      ////if ( _isRightButton ) {
    //      ////    CAM_ContextMenu();
    //      ////} else 
    //      //if ( _isLeftButton ) {
    //      //  CAM_Deselect();
    //      //  FP_Deselect();
    //      //}
    //  }
    //  _prevMousePos = Event.current.mousePosition;
    //  Handles.EndGUI();
    //  // FIXME: handle plyaback
    //  if ( _hoverControl != 0 ) { //|| _playback ) {
    //      forceRepaint = true;
    //  }
    //  return forceRepaint;
    //}

    private List<Func<bool>> _popupUI = new List<Func<bool>>();
    public void Pop( Func<bool> ui )
    {
        _popupUI.Add( ui );
    }

    private void UpdatePopups()
    {
        for ( int i = _popupUI.Count - 1; i >= 0; i-- ) {
            if ( ! _popupUI[i]() ) {
                _popupUI.RemoveAt( i );
            }
        }
    }
}

}

#endif
