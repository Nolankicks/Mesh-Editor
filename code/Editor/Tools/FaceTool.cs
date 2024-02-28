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

		if ( !Gizmo.HasHovered )
			SelectFace();

		using ( Gizmo.Scope( "Face Selection" ) )
		{
			foreach ( var face in MeshSelection.OfType<MeshFace>() )
			{
				Gizmo.Draw.Color = Color.Yellow;
				var position = face.Transform.PointToWorld( face.Center );
				Gizmo.Draw.SolidSphere( position, 4 );
			}
		}
	}

	private void SelectFace()
	{
		var face = TraceFace();
		if ( face.IsValid() )
		{
			using ( Gizmo.ObjectScope( face.Component.GameObject, face.Transform ) )
			{
				using ( Gizmo.Scope( "Face Hover" ) )
				{
					Gizmo.Draw.IgnoreDepth = true;
					Gizmo.Draw.Color = Color.Green;
					Gizmo.Draw.SolidSphere( face.Center, 4 );
				}

				if ( Gizmo.HasClicked )
					Select( face );
			}
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked && !IsMultiSelecting )
		{
			MeshSelection.Clear();
		}
	}
}
