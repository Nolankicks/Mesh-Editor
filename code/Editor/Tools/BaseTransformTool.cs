using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Editor.MeshEditor;

/// <summary>
/// Base class for transforming mesh elements (move, rotate, scale)
/// </summary>
public abstract class BaseTransformTool : EditorTool
{
	protected BaseMeshTool MeshTool { get; private init; }
	protected Dictionary<MeshVertex, Vector3> StartVertices { get; private init; } = new();

	public BaseTransformTool( BaseMeshTool meshTool )
	{
		MeshTool = meshTool;
	}

	protected void StartDrag()
	{
		if ( StartVertices.Any() )
			return;

		if ( Gizmo.IsShiftPressed )
		{
			foreach ( var face in MeshTool.MeshSelection.OfType<MeshFace>() )
			{
				face.Component.ExtrudeFace( face.Index );
			}

			var edge = MeshTool.MeshSelection.OfType<MeshEdge>().FirstOrDefault();
			if ( edge.IsValid() )
			{
				edge = new MeshEdge( edge.Component, edge.Component.ExtrudeEdge( edge.Index ) );
				if ( edge.IsValid() )
					MeshTool.MeshSelection.Set( edge );
			}

			MeshTool.CalculateSelectionVertices();
		}

		foreach ( var vertex in MeshTool.VertexSelection )
		{
			StartVertices[vertex] = vertex.PositionWorld;
		}
	}
}
