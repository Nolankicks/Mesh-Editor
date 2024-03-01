using Sandbox;
using System;
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
public sealed partial class EdgeTool : BaseMeshTool
{
	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Gizmo.HasHovered )
			SelectEdge();

		var edges = MeshSelection.OfType<MeshEdge>().ToList();

		using ( Gizmo.Scope( "Edge Selection" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.LineThickness = 4;

			foreach ( var edge in edges )
			{
				var line = edge.Component.GetEdge( edge.Index );
				var a = edge.Transform.PointToWorld( line.Start );
				var b = edge.Transform.PointToWorld( line.End );
				Gizmo.Draw.Line( a, b );
			}
		}

		if ( edges.Count == 2 )
			AngleFromEdges( edges[0], edges[1] );
	}

	private static void AngleFromEdges( MeshEdge edge1, MeshEdge edge2 )
	{
		if ( !edge1.IsValid() || !edge2.IsValid() )
			return;

		var line1 = edge1.Component.GetEdge( edge1.Index );
		var line2 = edge2.Component.GetEdge( edge2.Index );

		// Convert start and end points to world space
		var a1 = edge1.Transform.PointToWorld( line1.Start );
		var b1 = edge1.Transform.PointToWorld( line1.End );
		var a2 = edge2.Transform.PointToWorld( line2.Start );
		var b2 = edge2.Transform.PointToWorld( line2.End );

		// Check for a shared vertex
		Vector3? sharedVertex = null;
		if ( a1 == a2 || a1 == b2 ) sharedVertex = a1;
		else if ( b1 == a2 || b1 == b2 ) sharedVertex = b1;

		if ( sharedVertex.HasValue )
		{
			var vec1 = (a1 == sharedVertex.Value) ? b1 - a1 : a1 - b1;
			var vec2 = (a2 == sharedVertex.Value) ? b2 - a2 : a2 - b2;

			vec1 = vec1.Normal;
			vec2 = vec2.Normal;

			// Calculate angle
			float dotProduct = Vector3.Dot( vec1, vec2 );
			float angle = MathF.Acos( Math.Clamp( dotProduct, -1.0f, 1.0f ) ) * (180f / MathF.PI);

			// Check if Alt key is pressed and flip the angle
			var newStart = -90f;
			if ( Gizmo.KeyboardModifiers.HasFlag( KeyboardModifiers.Alt ) && Application.FocusWidget is not null )
			{
				angle = 360f - angle;
				newStart = 270f + angle;
			}

			Vector3 midPoint = sharedVertex.Value;

			// Draw angle text at midpoint of the arc
			Gizmo.Draw.Color = Color.White;
			var textSize = 16 * Gizmo.Settings.GizmoScale * Application.DpiScale;
			var cameraDistance = Gizmo.Camera.Position.Distance( midPoint );
			Gizmo.Draw.ScreenText( $"{angle:0.##}°", Gizmo.Camera.ToScreen( midPoint + ((vec1 + vec2) / 2).Normal * 7 * (cameraDistance / 100).Clamp( 1, 2f ) ), size: textSize, flags: TextFlag.Center );

			//Draw line from the center of the arc
			Gizmo.Draw.Line( midPoint + ((vec1 + vec2) / 2).Normal * 4 * (cameraDistance / 100).Clamp( 1, 2f ), midPoint + ((vec1 + vec2) / 2).Normal * 6 * (cameraDistance / 100).Clamp( 1, 2f ) );

			using ( Gizmo.Scope( "Angle Arc" ) )
			{
				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.LineThickness = 2;

				Gizmo.Transform = new Transform( midPoint, Rotation.LookAt( vec1, -vec2 ) * Rotation.FromYaw( 270f ) * Rotation.FromRoll( newStart ) );
				Gizmo.Draw.LineCircle( 0, (Gizmo.Camera.Position.Distance( midPoint ) / 20).Clamp( 5, 10 ), 0, angle );
				Gizmo.Draw.Color = Color.White.WithAlpha( 0.2f );
				Gizmo.Draw.SolidCircle( 0, (Gizmo.Camera.Position.Distance( midPoint ) / 20).Clamp( 5, 10 ), 0, -angle );
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
