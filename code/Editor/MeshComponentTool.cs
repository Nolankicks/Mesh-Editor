using Sandbox;

namespace Editor;

public class MeshComponentTool : EditorTool<MeshComponent>
{
	private MeshComponent _component;
	private BBox _startBox;
	private BBox _newBox;
	private bool _dragging;
	private Transform _startTransform;
	private Vector3 _startTextureOrigin;

	public override void OnSelectionChanged()
	{
		_component = GetSelectedComponent<MeshComponent>();

		Reset();
	}

	private void Reset()
	{
		_dragging = false;
		_newBox = default;
		_startBox = default;
		_startTransform = default;
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !_component.IsValid() )
			return;

		if ( Manager.CurrentSubTool is not null )
			return;

		using ( Gizmo.Scope( "Tool", _component.Transform.World ) )
		{
			Gizmo.Hitbox.DepthBias = 0.01f;

			if ( !Gizmo.HasPressed )
			{
				Reset();
			}

			var box = BBox.FromPositionAndSize( _component.Center, _component.Size );

			if ( Gizmo.Control.BoundingBox( "Resize", box, out var outBox ) )
			{
				if ( !_dragging )
				{
					_startBox = box;
					_dragging = true;
					_startTransform = _component.Transform.World;
					_startTextureOrigin = _component.TextureOrigin;
				}

				_newBox.Maxs += outBox.Maxs - box.Maxs;
				_newBox.Mins += outBox.Mins - box.Mins;

				outBox.Maxs = _startBox.Maxs + Gizmo.Snap( _newBox.Maxs, _newBox.Maxs );
				outBox.Mins = _startBox.Mins + Gizmo.Snap( _newBox.Mins, _newBox.Mins );

				if ( _component.Type == MeshComponent.PrimitiveType.Plane )
				{
					_component.PlaneSize = outBox.Size;
				}
				else if ( _component.Type == MeshComponent.PrimitiveType.Box )
				{
					_component.BoxSize = outBox.Size;
				}

				var origin = outBox.Center - _component.Center;
				_component.TextureOrigin = _startTextureOrigin + origin;
				_component.Transform.World = _startTransform.ToWorld( new Transform( origin ) );
			}
		}
	}
}
