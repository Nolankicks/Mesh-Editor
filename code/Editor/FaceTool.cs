using Sandbox;
using System.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Move, rotate and scale mesh faces
/// </summary>
[EditorTool]
[Title( "Face" )]
[Icon( "change_history" )]
[Alias( "Face" )]
[Group( "3" )]
[Shortcut( "mesh.face", "3" )]
public partial class FaceTool : BaseMeshTool
{
	public override void OnEnabled()
	{
		base.OnEnabled();

		CreateOverlay();
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		var tr = MeshTrace.Run();
		if ( tr.Hit && tr.Component is EditorMeshComponent component )
		{
			using ( Gizmo.ObjectScope( tr.GameObject, tr.GameObject.Transform.World ) )
			{
				Gizmo.Hitbox.DepthBias = 1;
				Gizmo.Hitbox.TrySetHovered( tr.Distance );

				var face = component.TriangleToFace( tr.Triangle );

				using ( Gizmo.Scope( "Face Hover" ) )
				{
					Gizmo.Draw.IgnoreDepth = true;
					Gizmo.Draw.Color = Color.Green;

					var position = component.GetFaceCenter( face );
					Gizmo.Draw.SolidSphere( position, 4 );
				}

				if ( Gizmo.WasClicked )
					Select( new MeshFace( component, face ) );
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked )
		{
			var multiSelect = Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) ||
				Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Shift );

			if ( !multiSelect )
				MeshSelection.Set( null );
		}

		using ( Gizmo.Scope( "Face Selection" ) )
		{
			foreach ( var face in MeshSelection.OfType<MeshFace>() )
			{
				Gizmo.Draw.Color = Color.Yellow;
				var position = face.Transform.PointToWorld( face.Component.GetFaceCenter( face.Index ) );
				Gizmo.Draw.SolidSphere( position, 4 );
			}
		}
	}
}
