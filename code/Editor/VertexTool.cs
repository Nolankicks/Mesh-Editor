using Sandbox;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Move, rotate and scale mesh vertices
/// </summary>
[EditorTool]
[Title( "Vertex" )]
[Icon( "workspaces" )]
[Alias( "Vertex" )]
[Group( "1" )]
public class VertexTool : BaseMeshTool
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

				var vertex = component.GetClosestVertex( tr.EndPosition );
				if ( vertex >= 0 )
				{
					Gizmo.Draw.Color = Color.Yellow;
					var p = component.GetVertexPosition( vertex );
					Gizmo.Draw.SolidSphere( p, 4 );

					if ( Gizmo.WasClicked )
					{
						Select( MeshElement.Vertex( component, vertex ) );
					}
				}
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked )
		{
			MeshSelection.Clear();
		}

		using ( Gizmo.Scope( "Vertex Selection" ) )
		{
			foreach ( var element in MeshSelection.OfType<MeshElement>()
			.Where( x => x.ElementType == MeshElementType.Vertex ) )
			{
				Gizmo.Draw.Color = Color.Green;
				var p = element.Transform.PointToWorld( element.Component.GetVertexPosition( element.Index ) );
				Gizmo.Draw.SolidSphere( p, 4 );
			}
		}
	}
}
