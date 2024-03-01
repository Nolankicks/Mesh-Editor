using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Base class for moving mesh elements (move, rotate, scale)
/// </summary>
public abstract class BaseMoveTool : EditorTool
{
	protected BaseMeshTool MeshTool { get; private init; }
	protected Dictionary<MeshVertex, Vector3> StartVertices { get; private init; } = new();

	public BaseMoveTool( BaseMeshTool meshTool )
	{
		MeshTool = meshTool;
	}

	protected void StartDrag()
	{
		if ( StartVertices.Any() )
			return;

		if ( Gizmo.IsShiftPressed )
		{
			MeshTool.ExtrudeSelection();
		}

		foreach ( var vertex in MeshTool.VertexSelection )
		{
			StartVertices[vertex] = vertex.PositionWorld;
		}
	}
}
