using Sandbox;
using System.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Move, rotate and scale mesh edges
/// </summary>
[EditorTool]
[Title( "Edge" )]
[Icon( "show_chart" )]
[Alias( "Edge" )]
[Group( "2" )]
[Shortcut( "mesh.edge", "2" )]
public class EdgeTool : BaseMeshTool
{
	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Gizmo.HasHovered )
			SelectEdge();

		using ( Gizmo.Scope( "Edge Selection" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.LineThickness = 4;

			foreach ( var edge in MeshSelection.OfType<MeshEdge>() )
			{
				var line = edge.Component.GetEdge( edge.Index );
				var a = edge.Transform.PointToWorld( line.Start );
				var b = edge.Transform.PointToWorld( line.End );
				Gizmo.Draw.Line( a, b );
			}
		}
	}

	private void SelectEdge()
	{
		var edge = GetClosestEdge( 8 );
		if ( edge.IsValid() )
		{
			using ( Gizmo.ObjectScope( edge.Component.GameObject, edge.Transform ) )
			{
				using ( Gizmo.Scope( "Edge Hover" ) )
				{
					Gizmo.Draw.IgnoreDepth = true;
					Gizmo.Draw.Color = Color.Green;
					Gizmo.Draw.LineThickness = 4;
					var line = edge.Component.GetEdge( edge.Index );
					Gizmo.Draw.Line( line );
				}

				if ( Gizmo.HasClicked )
					Select( edge );
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked && !IsMultiSelecting )
		{
			MeshSelection.Clear();
		}
	}
}
