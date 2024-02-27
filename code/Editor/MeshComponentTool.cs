using Sandbox;
using System.Linq;
using System.Collections.Generic;

namespace Editor.MeshEditor;

public class MeshComponentTool : EditorTool<EditorMeshComponent>
{
	private EditorMeshComponent _component;
	private BBox _startBox;
	private BBox _deltaBox;
	private BBox _box;
	private bool _dragging;
	private Transform _startTransform;
	private Vector3 _startTextureOrigin;
	private Dictionary<int, Vector3> _vertices = new();

	public override void OnSelectionChanged()
	{
		_component = GetSelectedComponent<EditorMeshComponent>();

		Reset();
	}

	private void Reset()
	{
		_dragging = false;
		_deltaBox = default;
		_vertices.Clear();
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !_component.IsValid() )
			return;

		if ( Manager.CurrentSubTool is not null )
			return;

		if ( !_dragging )
		{
			_box = BBox.FromPoints( _component.Vertices
				.Select( x => _component.GetVertexPosition( x ) ) );

			_startTransform = _component.Transform.World;
			_startTextureOrigin = _component.TextureOrigin;
		}

		using ( Gizmo.Scope( "Tool", _startTransform ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( !Gizmo.HasPressed )
			{
				Reset();
			}

			using ( Gizmo.Scope( "Box" ) )
			{
				var textSize = 22 * Gizmo.Settings.GizmoScale * Application.DpiScale;

				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.LineThickness = 2;
				Gizmo.Draw.Color = Gizmo.Colors.Active;
				Gizmo.Draw.LineBBox( _box );

				var l = _startTransform.PointToWorld( _box.Maxs.WithY( _box.Center.y ) );
				var w = _startTransform.PointToWorld( _box.Maxs.WithX( _box.Center.x ) );
				var h = _startTransform.PointToWorld( _box.Maxs.WithZ( _box.Center.z ) );

				Gizmo.Draw.Color = Gizmo.Colors.Left;
				Gizmo.Draw.ScreenText( $"L: {_box.Size.y:0.#}", Gizmo.Camera.ToScreen( l ) + Vector2.Down * 32, size: textSize );
				Gizmo.Draw.Color = Gizmo.Colors.Forward;
				Gizmo.Draw.ScreenText( $"W: {_box.Size.x:0.#}", Gizmo.Camera.ToScreen( w ) + Vector2.Down * 32, size: textSize );
				Gizmo.Draw.Color = Gizmo.Colors.Up;
				Gizmo.Draw.ScreenText( $"H: {_box.Size.z:0.#}", Gizmo.Camera.ToScreen( h ) + Vector2.Down * 32, size: textSize );
			}

			if ( Gizmo.Control.BoundingBox( "Resize", _box, out var outBox ) )
			{
				if ( !_dragging )
				{
					_startBox = _box;
					_dragging = true;

					foreach ( var i in _component.Vertices )
					{
						_vertices[i] = _component.GetVertexPosition( i ) - _startBox.Center;
					}
				}

				_deltaBox.Maxs += outBox.Maxs - _box.Maxs;
				_deltaBox.Mins += outBox.Mins - _box.Mins;

				_box.Maxs = _startBox.Maxs + Gizmo.Snap( _deltaBox.Maxs, _deltaBox.Maxs );
				_box.Mins = _startBox.Mins + Gizmo.Snap( _deltaBox.Mins, _deltaBox.Mins );

				var scale = _box.Size / _startBox.Size;
				foreach ( var i in _vertices )
				{
					_component.SetVertexPosition( i.Key, (_startBox.Center + i.Value) * scale );
				}

				var offset = _box.Center - (_startBox.Center * scale);
				_component.Transform.Position = _startTransform.Position + (_startTransform.Rotation * offset);
				_component.TextureOrigin = _startTextureOrigin + offset;

				_component.RebuildMesh();
			}
		}
	}
}
