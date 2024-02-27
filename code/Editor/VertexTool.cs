﻿using Sandbox;
using System.Linq;
using System.Collections.Generic;

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
			SelectVertex();
		}

		using ( Gizmo.Scope( "Vertex Selection" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Yellow;

			foreach ( var element in MeshSelection.OfType<MeshElement>()
				.Where( x => x.ElementType == MeshElementType.Vertex ) )
			{
				var p = element.Transform.PointToWorld( element.Component.GetVertexPosition( element.Index ) );
				Gizmo.Draw.Sprite( p, 12, null, false );
			}
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

					var position = vertex.Component.GetVertexPosition( vertex.Index );
					Gizmo.Draw.Sprite( position, 12, null, false );
				}

				if ( Gizmo.HasClicked )
				{
					Select( vertex );
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
