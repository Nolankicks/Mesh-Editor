using Sandbox;
using System.Linq;

namespace Editor.MeshEditor;

public class MeshComponentTool : EditorTool<EditorMeshComponent>
{
	private EditorMeshComponent _component;
	private BBox _startBox;
	private BBox _newBox;
	private bool _dragging;

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
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !_component.IsValid() )
			return;

		if ( Manager.CurrentSubTool is not null )
			return;

		var box = BBox.FromPoints( _component.Vertices
			.Select( x => _component.GetVertexPosition( x ) ) );

		using ( Gizmo.Scope( "Tool", _component.Transform.World ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( !Gizmo.HasPressed )
			{
				Reset();
			}

			if ( Gizmo.Control.BoundingBox( "Resize", box, out var outBox ) )
			{
				if ( !_dragging )
				{
					_startBox = box;
					_dragging = true;
				}

				_newBox.Maxs += outBox.Maxs - box.Maxs;
				_newBox.Mins += outBox.Mins - box.Mins;

				outBox.Maxs = _startBox.Maxs + Gizmo.Snap( _newBox.Maxs, _newBox.Maxs );
				outBox.Mins = _startBox.Mins + Gizmo.Snap( _newBox.Mins, _newBox.Mins );
			}
		}
	}
}
