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
			if ( vertices is not null )
			{
				foreach ( var vertex in vertices )
					FaceObject.AddVertex( vertex );
			}

			HoverFace = default;
		}

		var selectionColor = Color.Yellow.WithAlpha( 0.2f );
		foreach ( var face in MeshSelection.OfType<MeshFace>() )
		{
			var mesh = face.Component.PolygonMesh;
			var vertices = mesh.CreateFace( face.Index, face.Transform, selectionColor );
			if ( vertices is not null )
			{
				foreach ( var vertex in vertices )
					FaceObject.AddVertex( vertex );
			}
		}

		DrawBounds();
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
		VertexSelection.Clear();
	}

	public override Rotation CalculateSelectionBasis()
	{
		if ( Gizmo.Settings.GlobalSpace )
			return Rotation.Identity;

		var edge = MeshSelection.OfType<MeshFace>().FirstOrDefault();
		if ( edge.Component.IsValid() )
		{
			var normal = edge.Component.GetAverageFaceNormal( edge.Index );
			var vAxis = PolygonMesh.ComputeTextureVAxis( normal );
			var basis = Rotation.LookAt( normal, vAxis * -1.0f );
			return edge.Transform.RotationToWorld( basis );
		}

		return Rotation.Identity;
	}

	private void DrawBounds()
	{
		using ( Gizmo.Scope( "Face Size" ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.LineThickness = 4;

			//TODO: Draw Text for edges closest to the camera
			var _box = CalculateSelectionBounds();
			var textSize = 22 * Gizmo.Settings.GizmoScale * Application.DpiScale;

			Gizmo.Draw.Color = Gizmo.Colors.Active.WithAlpha( 0.5f );
			Gizmo.Draw.LineThickness = 1;
			Gizmo.Draw.LineBBox( _box );

			Gizmo.Draw.LineThickness = 2;
			Gizmo.Draw.Color = Gizmo.Colors.Left;
			if ( _box.Size.y > 0.01f )
				Gizmo.Draw.ScreenText( $"L: {_box.Size.y:0.#}", Gizmo.Camera.ToScreen( _box.Maxs.WithY( _box.Center.y ) ) + Vector2.Down * 32, size: textSize );
			Gizmo.Draw.Line( _box.Maxs.WithY( _box.Mins.y ), _box.Maxs.WithY( _box.Maxs.y ) );
			Gizmo.Draw.Color = Gizmo.Colors.Forward;
			if ( _box.Size.x > 0.01f )
				Gizmo.Draw.ScreenText( $"W: {_box.Size.x:0.#}", Gizmo.Camera.ToScreen( _box.Maxs.WithX( _box.Center.x ) ) + Vector2.Down * 32, size: textSize );
			Gizmo.Draw.Line( _box.Maxs.WithX( _box.Mins.x ), _box.Maxs.WithX( _box.Maxs.x ) );
			Gizmo.Draw.Color = Gizmo.Colors.Up;
			if ( _box.Size.z > 0.01f )
				Gizmo.Draw.ScreenText( $"H: {_box.Size.z:0.#}", Gizmo.Camera.ToScreen( _box.Maxs.WithZ( _box.Center.z ) ) + Vector2.Down * 32, size: textSize );
			Gizmo.Draw.Line( _box.Maxs.WithZ( _box.Mins.z ), _box.Maxs.WithZ( _box.Maxs.z ) );
		}
	}
}
