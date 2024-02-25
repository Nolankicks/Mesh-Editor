using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// Create new shapes by dragging out a block
/// </summary>
[EditorTool]
[Title( "Block Tool" )]
[Icon( "hardware" )]
[Shortcut( "editortool.block", "b" )]
public class BlockTool : EditorTool
{
	private BBox _box;
	private BBox _startBox;
	private BBox _deltaBox;
	private bool _resizing;
	private bool _boxCreated;
	private bool _dragging;
	private bool _finished;
	private Vector3 _dragStartPos;

	public override void OnEnabled()
	{
		AllowGameObjectSelection = false;

		Selection.Clear();
	}

	private GameObject CreateFromBox( BBox box )
	{
		var go = new GameObject( true, "Box" );
		var mc = go.Components.Create<EditorMeshComponent>( false );

		mc.FromBox( box );
		mc.Enabled = true;

		EditLog( "Create Block", null );

		return go;
	}

	public override void OnUpdate()
	{
		if ( _finished )
			return;

		if ( _boxCreated && Application.IsKeyDown( KeyCode.Escape ) )
		{
			_resizing = false;
			_dragging = false;
			_boxCreated = false;
		}

		if ( _boxCreated )
		{
			using ( Gizmo.Scope( "box", 0 ) )
			{
				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.LineThickness = 2;
				Gizmo.Draw.Color = Gizmo.Colors.Active;
				Gizmo.Draw.LineBBox( _box );
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.ScreenText( $"L: {_box.Size.y:0.#}", Gizmo.Camera.ToScreen( _box.Mins.WithY( _box.Center.y ) ) + Vector2.Down * 32, size: 14 );
				Gizmo.Draw.ScreenText( $"W: {_box.Size.x:0.#}", Gizmo.Camera.ToScreen( _box.Mins.WithX( _box.Center.x ) ) + Vector2.Down * 32, size: 14 );
			}

			using ( Gizmo.Scope( "Tool" ) )
			{
				Gizmo.Hitbox.DepthBias = 0.01f;

				if ( !Gizmo.HasPressed )
				{
					_resizing = false;
					_deltaBox = default;
					_startBox = default;
				}

				if ( Gizmo.Control.BoundingBox( "Resize", _box, out var outBox ) )
				{
					if ( !_resizing )
					{
						_startBox = _box;
						_resizing = true;
					}

					_deltaBox.Maxs += outBox.Maxs - _box.Maxs;
					_deltaBox.Mins += outBox.Mins - _box.Mins;

					_box.Maxs = _startBox.Maxs + Gizmo.Snap( _deltaBox.Maxs, _deltaBox.Maxs );
					_box.Mins = _startBox.Mins + Gizmo.Snap( _deltaBox.Mins, _deltaBox.Mins );
				}
			}

			if ( Application.IsKeyDown( KeyCode.Enter ) )
			{
				var go = CreateFromBox( _box );
				Selection.Set( go );

				EditorToolManager.CurrentModeName = "object";
				_finished = true;
			}
		}

		if ( Gizmo.HasPressed || Gizmo.HasHovered )
			return;

		var tr = MeshTrace.Run();
		if ( !tr.Hit || _dragging )
		{
			var plane = _dragging ? new Plane( _dragStartPos, Vector3.Up ) : new Plane( Vector3.Up, 0.0f );
			if ( plane.TryTrace( new Ray( tr.StartPosition, tr.Direction ), out tr.EndPosition, true ) )
			{
				tr.Hit = true;
				tr.Normal = plane.Normal;
			}
		}

		if ( !tr.Hit )
			return;

		var r = Rotation.LookAt( tr.Normal );
		var localPosition = tr.EndPosition * r.Inverse;
		localPosition = Gizmo.Snap( localPosition, new Vector3( 0, 1, 1 ) );
		tr.EndPosition = localPosition * r;

		if ( !_dragging )
		{
			using ( Gizmo.Scope( "Aim Handle", new Transform( tr.EndPosition, Rotation.LookAt( tr.Normal ) ) ) )
			{
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.LineCircle( 0, 2 );
				Gizmo.Draw.Color = Color.White.WithAlpha( 0.5f );
				Gizmo.Draw.LineCircle( 0, 3 );
				Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
				Gizmo.Draw.LineCircle( 0, 6 );
				Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
				Gizmo.Draw.LineCircle( 0, 12 );
			}
		}

		if ( Gizmo.WasLeftMousePressed )
		{
			if ( _boxCreated )
			{
				CreateFromBox( _box );
			}

			_dragging = true;
			_dragStartPos = tr.EndPosition;
			_boxCreated = false;
		}
		else if ( Gizmo.WasLeftMouseReleased && _dragging )
		{
			var spacing = Gizmo.Settings.SnapToGrid ? Gizmo.Settings.GridSpacing : 1.0f;
			var box = new BBox( _dragStartPos, tr.EndPosition );

			if ( box.Size.x >= spacing || box.Size.y >= spacing )
			{
				if ( Gizmo.Settings.SnapToGrid )
				{
					if ( box.Size.x < spacing ) box.Maxs.x += spacing;
					if ( box.Size.y < spacing ) box.Maxs.y += spacing;
				}

				const float height = 128;
				var size = box.Size.WithZ( height );
				var position = box.Center.WithZ( box.Center.z + (height * 0.5f) );
				_box = BBox.FromPositionAndSize( position, size );
				_boxCreated = true;
			}

			_dragging = false;
			_dragStartPos = default;
		}

		if ( _dragging )
		{
			using ( Gizmo.Scope( "Rect", 0 ) )
			{
				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.LineThickness = 2;
				Gizmo.Draw.Color = Gizmo.Colors.Active;
				var box = new BBox( _dragStartPos, tr.EndPosition );
				Gizmo.Draw.LineBBox( box );
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.ScreenText( $"L: {box.Size.y:0.#}", Gizmo.Camera.ToScreen( box.Mins.WithY( box.Center.y ) ) + Vector2.Down * 32, size: 14 );
				Gizmo.Draw.ScreenText( $"W: {box.Size.x:0.#}", Gizmo.Camera.ToScreen( box.Mins.WithX( box.Center.x ) ) + Vector2.Down * 32, size: 14 );
			}
		}
	}
}
