using Sandbox;
using System.Linq;
using System.Collections.Generic;

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
public class FaceTool : BaseMeshTool
{
	public override IEnumerable<EditorTool> GetSubtools()
	{
		yield return new MoveTool( this );
		yield return new RotateTool( this );
		yield return new ScaleTool( this );
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

				if ( Gizmo.WasClicked )
				{
					Select( MeshElement.Face( component, face ) );
				}
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked )
		{
			var multiSelect = Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl ) ||
				Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Shift );

			if ( !multiSelect )
				MeshSelection.Clear();
		}

		using ( Gizmo.Scope( "Face Selection" ) )
		{
			foreach ( var element in MeshSelection.OfType<MeshElement>()
			.Where( x => x.ElementType == MeshElementType.Face ) )
			{
				Gizmo.Draw.Color = Color.Green;
				var p = element.Transform.PointToWorld( element.Component.GetFaceCenter( element.Index ) );
				Gizmo.Draw.SolidSphere( p, 4 );
			}
		}
	}
}
