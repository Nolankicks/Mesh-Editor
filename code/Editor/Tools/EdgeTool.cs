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

					var textSize = 22 * Gizmo.Settings.GizmoScale * Application.DpiScale;
					var distance = line.Start.Distance( line.End );
					Gizmo.Draw.Color = Color.White;
					Gizmo.Draw.ScreenText( $"{distance:0.##}", Gizmo.Camera.ToScreen( edge.Transform.PointToWorld( line.Center ) ), size: textSize );
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

	public override Rotation CalculateSelectionBasis()
	{
		if ( Gizmo.Settings.GlobalSpace )
			return Rotation.Identity;

		var edge = MeshSelection.OfType<MeshEdge>().FirstOrDefault();
		if ( edge.Component.IsValid() )
		{
			var line = edge.Component.GetEdge( edge.Index );
			var normal = (line.End - line.Start).Normal;
			var vAxis = PolygonMesh.ComputeTextureVAxis( normal );
			var basis = Rotation.LookAt( normal, vAxis * -1.0f );
			return edge.Transform.RotationToWorld( basis );
		}

		return Rotation.Identity;
	}
}
