using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Create new shapes by dragging out a block
/// </summary>
[EditorTool]
[Title( "Block Tool" )]
[Icon( "hardware" )]
[Shortcut( "editortool.block", "Shift+B" )]
public partial class BlockTool : EditorTool
{
	private BBox _box;
	private BBox _startBox;
	private BBox _deltaBox;
	private bool _resizing;
	private bool _inProgress;
	private bool _dragging;
	private bool _finished;
	private Vector3 _dragStartPos;

	private readonly HashSet<PrimitiveBuilder> _primitives = new();
	private PrimitiveBuilder _primitive;
	private SceneObject _sceneObject;

	private PrimitiveBuilder Current
	{
		get => _primitive;
		set
		{
			if ( _primitive == value )
				return;

			_primitive = value;

			BuildControlSheet();
			RebuildMesh();
		}
	}

	private bool InProgress
	{
		get => _inProgress;
		set
		{
			if ( _inProgress == value )
				return;

			_inProgress = value;

			UpdateStatus();
		}
	}

	private static float LastHeight = 128;

	public override void OnEnabled()
	{
		base.OnEnabled();

		AllowGameObjectSelection = false;
		Selection.Clear();

		CreatePrimitiveBuilders();

		CreateOverlay();
	}

	public override void OnDisabled()
	{
		base.OnDisabled();

		if ( InProgress )
		{
			var go = CreateFromBox( _box );
			Selection.Set( go );

			_finished = true;
			InProgress = false;
		}
	}

	private void RebuildMesh()
	{
		if ( !InProgress )
			return;

		if ( Current.Is2D )
		{
			_box.Maxs.z = _box.Mins.z;
		}
		else
		{
			_box.Maxs.z = _box.Mins.z + LastHeight;
		}

		var box = _box;
		var position = box.Center;
		box = BBox.FromPositionAndSize( 0, box.Size );

		var polygonMesh = new PolygonMesh();
		_primitive.SetFromBox( box );
		_primitive.Build( polygonMesh );

		polygonMesh.TextureOrigin = position;
		polygonMesh.ApplyPlanarMapping();
		polygonMesh.Rebuild();

		var model = polygonMesh.Model;
		var transform = new Transform( position );

		if ( !_sceneObject.IsValid() )
		{
			_sceneObject = new SceneObject( Scene.SceneWorld, model, transform );
		}
		else
		{
			_sceneObject.Model = model;
			_sceneObject.Transform = transform;
		}
	}

	private void CreatePrimitiveBuilders()
	{
		_primitives.Clear();

		foreach ( var type in GetBuilderTypes() )
		{
			_primitives.Add( type.Create<PrimitiveBuilder>() );
		}

		_primitive = _primitives.FirstOrDefault();
	}

	private static IEnumerable<TypeDescription> GetBuilderTypes()
	{
		return EditorTypeLibrary.GetTypes<PrimitiveBuilder>()
			.Where( x => !x.IsAbstract ).OrderBy( x => x.Name );
	}

	private GameObject CreateFromBox( BBox box )
	{
		if ( _primitive is null )
			return null;

		if ( _sceneObject.IsValid() )
		{
			_sceneObject.RenderingEnabled = false;
			_sceneObject.Delete();
		}

		var go = new GameObject( true, "Box" );
		var mc = go.Components.Create<EditorMeshComponent>( false );

		var position = box.Center;
		box = BBox.FromPositionAndSize( 0, box.Size );

		var polygonMesh = new PolygonMesh();
		_primitive.SetFromBox( box );
		_primitive.Build( polygonMesh );

		mc.Transform.Position = position;
		mc.ConstructPolygonMesh( polygonMesh );

		mc.Enabled = true;

		EditLog( "Create Block", null );

		return go;
	}

	public override void OnSelectionChanged()
	{
		base.OnSelectionChanged();

		if ( !Selection.OfType<GameObject>().Any() )
			return;

		EditorToolManager.CurrentModeName = "object";
		_finished = true;
	}

	public override void OnUpdate()
	{
		if ( _finished )
			return;

		if ( Selection.OfType<GameObject>().Any() )
			return;

		if ( InProgress && Application.FocusWidget is not null && Application.IsKeyDown( KeyCode.Escape ) )
		{
			_resizing = false;
			_dragging = false;
			InProgress = false;
		}

		if ( Current is null )
			return;

		var textSize = 22 * Gizmo.Settings.GizmoScale * Application.DpiScale;

		if ( InProgress )
		{
			using ( Gizmo.Scope( "Tool" ) )
			{
				Gizmo.Hitbox.DepthBias = 0.01f;

				if ( !Gizmo.HasPressed )
				{
					_resizing = false;
					_deltaBox = default;
					_startBox = default;

					if ( Current.Is2D )
					{
						_box.Maxs.z = _box.Mins.z;
					}
					else
					{
						_box.Maxs.z = _box.Mins.z + LastHeight;
					}
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

					var spacing = (Gizmo.Settings.SnapToGrid != Gizmo.IsCtrlPressed) ? Gizmo.Settings.GridSpacing : 1.0f;
					_box.Maxs.x = System.Math.Max( _box.Maxs.x, _startBox.Mins.x + spacing );
					_box.Mins.x = System.Math.Min( _box.Mins.x, _startBox.Maxs.x - spacing );
					_box.Maxs.y = System.Math.Max( _box.Maxs.y, _startBox.Mins.y + spacing );
					_box.Mins.y = System.Math.Min( _box.Mins.y, _startBox.Maxs.y - spacing );
					_box.Maxs.z = System.Math.Max( _box.Maxs.z, _startBox.Mins.z + spacing );
					_box.Mins.z = System.Math.Min( _box.Mins.z, _startBox.Maxs.z - spacing );

					if ( Current.Is2D )
					{
						_box.Mins.z = _startBox.Mins.z;
						_box.Maxs.z = _startBox.Mins.z;
					}
					else
					{
						LastHeight = System.MathF.Abs( _box.Size.z );
					}

					RebuildMesh();
				}

				Gizmo.Draw.Color = Color.Red.WithAlpha( 0.5f );
				Gizmo.Draw.LineBBox( _startBox );
			}

			using ( Gizmo.Scope( "box" ) )
			{
				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.LineThickness = 2;
				Gizmo.Draw.Color = Gizmo.Colors.Active.WithAlpha( 0.5f );
				Gizmo.Draw.LineBBox( _box );
				Gizmo.Draw.Color = Gizmo.Colors.Left;
				Gizmo.Draw.ScreenText( $"L: {_box.Size.y:0.#}", Gizmo.Camera.ToScreen( _box.Maxs.WithY( _box.Center.y ) ) + Vector2.Down * 32, size: textSize );
				Gizmo.Draw.Color = Gizmo.Colors.Forward;
				Gizmo.Draw.ScreenText( $"W: {_box.Size.x:0.#}", Gizmo.Camera.ToScreen( _box.Maxs.WithX( _box.Center.x ) ) + Vector2.Down * 32, size: textSize );
				Gizmo.Draw.Color = Gizmo.Colors.Up;
				Gizmo.Draw.ScreenText( $"H: {_box.Size.z:0.#}", Gizmo.Camera.ToScreen( _box.Maxs.WithZ( _box.Center.z ) ) + Vector2.Down * 32, size: textSize );
			}

			if ( Application.FocusWidget is not null && Application.IsKeyDown( KeyCode.Enter ) )
			{
				var go = CreateFromBox( _box );
				Selection.Set( go );

				_finished = true;
				InProgress = false;

				EditorToolManager.CurrentModeName = "object";
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
			if ( _inProgress )
			{
				CreateFromBox( _box );
			}

			_dragging = true;
			_dragStartPos = tr.EndPosition;
			InProgress = false;
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

				float height = Current.Is2D ? 0 : LastHeight;
				var size = box.Size.WithZ( height );
				var position = box.Center.WithZ( box.Center.z + (height * 0.5f) );
				_box = BBox.FromPositionAndSize( position, size );
				InProgress = true;

				RebuildMesh();
			}

			_dragging = false;
			_dragStartPos = default;
		}

		if ( _dragging )
		{
			using ( Gizmo.Scope( "Rect", 0 ) )
			{
				var box = new BBox( _dragStartPos, tr.EndPosition );

				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.LineThickness = 2;
				Gizmo.Draw.Color = Gizmo.Colors.Active.WithAlpha( 0.5f );
				Gizmo.Draw.LineBBox( box );
				Gizmo.Draw.Color = Gizmo.Colors.Left;
				Gizmo.Draw.ScreenText( $"L: {box.Size.y:0.#}", Gizmo.Camera.ToScreen( box.Mins.WithY( box.Center.y ) ) + Vector2.Down * 32, size: textSize );
				Gizmo.Draw.Color = Gizmo.Colors.Forward;
				Gizmo.Draw.ScreenText( $"W: {box.Size.x:0.#}", Gizmo.Camera.ToScreen( box.Mins.WithX( box.Center.x ) ) + Vector2.Down * 32, size: textSize );
			}
		}
	}
}
