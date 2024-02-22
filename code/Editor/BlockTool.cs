using Sandbox;

namespace Editor;

/// <summary>
/// Create new shapes by dragging out a block
/// </summary>
[EditorTool]
[Title( "Block Tool" )]
[Icon( "hardware" )]
[Shortcut( "editortool.block", "b" )]
public class BlockTool : EditorTool
{
	private bool _dragging = false;
	private Vector3 _dragStartPos;

	public override void OnEnabled()
	{
		AllowGameObjectSelection = false;

		Selection.Clear();
	}

	public override void OnUpdate()
	{
		if ( Gizmo.HasHovered )
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

		if ( tr.Hit )
		{
			var r = Rotation.LookAt( tr.Normal );
			var localPosition = tr.EndPosition * r.Inverse;

			if ( Gizmo.Settings.SnapToGrid )
				localPosition = localPosition.SnapToGrid( Gizmo.Settings.GridSpacing, false, true, true );

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
				_dragging = true;
				_dragStartPos = tr.EndPosition;

				Selection.Clear();
			}
			else if ( Gizmo.WasLeftMouseReleased && _dragging )
			{
				var spacing = Gizmo.Settings.SnapToGrid ? Gizmo.Settings.GridSpacing : 1.0f;
				var box = new BBox( _dragStartPos, tr.EndPosition );

				if ( box.Size.x >= spacing || box.Size.y >= spacing )
				{
					var go = new GameObject( true, "Box" );
					var mc = go.Components.Create<MeshComponent>();
					mc.Type = MeshComponent.PrimitiveType.Box;

					if ( Gizmo.Settings.SnapToGrid )
					{
						if ( box.Size.x < spacing ) box.Maxs.x += spacing;
						if ( box.Size.y < spacing ) box.Maxs.y += spacing;
					}

					mc.BoxSize = box.Size.WithZ( 128 );
					mc.Transform.Position = box.Center.WithZ( box.Center.z + 64 );
					mc.TextureOrigin = mc.Transform.Position;

					Selection.Set( go );
				}

				_dragging = false;
				_dragStartPos = default;
			}

			using ( Gizmo.Scope( "box", 0 ) )
			{
				if ( _dragging )
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
}
