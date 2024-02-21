using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Vertices
/// </summary>
[EditorTool]
[Title( "Vertex" )]
[Icon( "workspaces" )]
[Alias( "Vertex" )]
[Group( "1" )]
public class VertexEditorTool : EditorTool
{
	public override void OnEnabled()
	{
		base.OnEnabled();

		AllowGameObjectSelection = false;
	}

	public override IEnumerable<EditorTool> GetSubtools()
	{
		yield return new PositionEditorTool();
		yield return new RotationEditorTool();
		yield return new ScaleEditorTool();
	}
}

/// <summary>
/// Edges
/// </summary>
[EditorTool]
[Title( "Edge" )]
[Icon( "polyline" )]
[Alias( "Edge" )]
[Group( "2" )]
public class EdgeEditorTool : EditorTool
{
	public override void OnEnabled()
	{
		base.OnEnabled();

		AllowGameObjectSelection = false;
	}

	public override IEnumerable<EditorTool> GetSubtools()
	{
		yield return new PositionEditorTool();
		yield return new RotationEditorTool();
		yield return new ScaleEditorTool();
	}
}

/// <summary>
/// Faces
/// </summary>
[EditorTool]
[Title( "Face" )]
[Icon( "change_history" )]
[Alias( "Face" )]
[Group( "3" )]
public class FaceEditorTool : EditorTool
{
	public override void OnEnabled()
	{
		base.OnEnabled();

		AllowGameObjectSelection = false;
	}

	public override IEnumerable<EditorTool> GetSubtools()
	{
		yield return new PositionEditorTool();
		yield return new RotationEditorTool();
		yield return new ScaleEditorTool();
	}
}
