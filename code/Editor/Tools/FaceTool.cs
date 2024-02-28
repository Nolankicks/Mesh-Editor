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
	private MeshFace HoverFace;
	private SceneDynamicObject FaceObject;

	public override void OnEnabled()
	{
		base.OnEnabled();

		CreateOverlay();

		FaceObject = new SceneDynamicObject( Scene.SceneWorld );
		FaceObject.Material = Material.Load( "materials/tools/vertex_color_translucent.vmat" );
		FaceObject.Attributes.SetCombo( "D_DEPTH_BIAS", 1 );
		FaceObject.Flags.CastShadows = false;
	}

	public override void OnDisabled()
	{
		base.OnDisabled();

		FaceObject?.Delete();
		FaceObject = null;

		HoverFace = default;
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( Application.IsKeyDown( KeyCode.Delete ) && Application.FocusWidget is not null )
		{
			DeleteSelection();

			return;
		}

		if ( !Gizmo.HasHovered )
			SelectFace();

		FaceObject.Init( Graphics.PrimitiveType.Triangles );

		if ( HoverFace.IsValid() )
		{
			var hoverColor = Color.Green.WithAlpha( 0.2f );
			var mesh = HoverFace.Component.PolygonMesh;
			var vertices = mesh.CreateFace( HoverFace.Index, HoverFace.Transform, hoverColor );
			foreach ( var vertex in vertices )
				FaceObject.AddVertex( vertex );

			HoverFace = default;
		}

		var selectionColor = Color.Yellow.WithAlpha( 0.2f );
		foreach ( var face in MeshSelection.OfType<MeshFace>() )
		{
			var mesh = face.Component.PolygonMesh;
			var vertices = mesh.CreateFace( face.Index, face.Transform, selectionColor );
			foreach ( var vertex in vertices )
				FaceObject.AddVertex( vertex );
		}
	}

	private void SelectFace()
	{
		HoverFace = TraceFace();

		if ( HoverFace.IsValid() && Gizmo.HasClicked )
		{
			Select( HoverFace );
		}
		else if ( !Gizmo.HasPressed && Gizmo.HasClicked && !IsMultiSelecting )
		{
			MeshSelection.Clear();
		}
	}

	private void DeleteSelection()
	{
		var groups = MeshSelection.OfType<MeshFace>()
			.GroupBy( face => face.Component );

		foreach ( var group in groups )
		{
			group.Key.RemoveFaces( group.Select( x => x.Index ).ToArray() );
		}

		MeshSelection.Clear();
	}
}
