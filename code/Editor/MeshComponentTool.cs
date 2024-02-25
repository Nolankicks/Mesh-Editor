using Sandbox;
using System.Linq;
using System.Collections.Generic;

namespace Editor.MeshEditor;

public class MeshComponentTool : EditorTool<EditorMeshComponent>
{
	private EditorMeshComponent _component;
	private BBox _startBox;
	private BBox _newBox;
	private BBox _box;
	private bool _dragging;
	private Dictionary<int, Vector3> _vertices = new();

	public override void OnSelectionChanged()
	{
		_component = GetSelectedComponent<EditorMeshComponent>();

		Reset();
	}

	private void Reset()
	{
		_dragging = false;
		_newBox = default;
		_startBox = default;
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
		}

		using ( Gizmo.Scope( "Tool", _component.Transform.World ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( !Gizmo.HasPressed )
			{
				Reset();
			}

			if ( Gizmo.Control.BoundingBox( "Resize", _box, out var outBox ) )
			{
				if ( !_dragging )
				{
					_startBox = _box;
					_dragging = true;

					foreach ( var i in _component.Vertices )
					{
						_vertices[i] = _component.GetVertexPosition( i );
					}
				}

				_newBox.Maxs += outBox.Maxs - _box.Maxs;
				_newBox.Mins += outBox.Mins - _box.Mins;

				_box.Maxs = _startBox.Maxs + Gizmo.Snap( _newBox.Maxs, _newBox.Maxs );
				_box.Mins = _startBox.Mins + Gizmo.Snap( _newBox.Mins, _newBox.Mins );

				var scale = _box.Size / _startBox.Size;
				foreach ( var i in _vertices )
				{
					_component.SetVertexPosition( i.Key, _box.Center + ((i.Value - _startBox.Center) * scale) );
				}

				_component.CreateSceneObject();
			}

			using ( Gizmo.Scope( "Box" ) )
			{
				var textSize = 22 * Gizmo.Settings.GizmoScale * Application.DpiScale;

				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.LineThickness = 2;
				Gizmo.Draw.Color = Gizmo.Colors.Active;
				Gizmo.Draw.LineBBox( _box );
			}
		}
	}
}
