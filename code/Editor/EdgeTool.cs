using Sandbox;
using System.Linq;
using System.Collections.Generic;

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
	public override IEnumerable<EditorTool> GetSubtools()
	{
		yield return new MoveTool( this );
		yield return new RotateTool( this );
		yield return new ScaleTool( this );
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Gizmo.HasHovered )
		{
			SelectEdge();
		}

		using ( Gizmo.Scope( "Edge Selection" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.LineThickness = 4;

			foreach ( var element in MeshSelection.OfType<MeshElement>()
				.Where( x => x.ElementType == MeshElementType.Edge ) )
			{
				var line = element.Component.GetEdge( element.Index );
				var a = element.Transform.PointToWorld( line.Start );
				var b = element.Transform.PointToWorld( line.End );
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
				{
					Select( edge );
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
	}
}
