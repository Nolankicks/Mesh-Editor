using Sandbox;
using System.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Move, rotate and scale mesh vertices
/// </summary>
[EditorTool]
[Title( "Vertex" )]
[Icon( "workspaces" )]
[Alias( "Vertex" )]
[Group( "1" )]
[Shortcut( "mesh.vertex", "1" )]
public class VertexTool : BaseMeshTool
{
	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Gizmo.HasHovered )
			SelectVertex();

		using ( Gizmo.Scope( "Vertex Selection" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Yellow;

			foreach ( var vertex in MeshSelection.OfType<MeshVertex>() )
				Gizmo.Draw.Sprite( vertex.PositionWorld, 12, null, false );
		}
	}

	private void SelectVertex()
	{
		var vertex = GetClosestVertex( 8 );
		if ( vertex.IsValid() )
		{
			using ( Gizmo.ObjectScope( vertex.Component.GameObject, vertex.Transform ) )
			{
				using ( Gizmo.Scope( "Vertex Hover" ) )
				{
					Gizmo.Draw.IgnoreDepth = true;
					Gizmo.Draw.Color = Color.Green;
					Gizmo.Draw.Sprite( vertex.PositionLocal, 12, null, false );
				}

				if ( Gizmo.HasClicked )
					Select( vertex );
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked && !IsMultiSelecting )
		{
			MeshSelection.Clear();
		}
	}
}
