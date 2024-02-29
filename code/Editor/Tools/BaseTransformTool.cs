
namespace Editor.MeshEditor;

/// <summary>
/// Base class for transforming mesh elements (move, rotate, scale)
/// </summary>
public abstract class BaseTransformTool : EditorTool
{
	protected BaseMeshTool MeshTool { get; private init; }

	public BaseTransformTool( BaseMeshTool meshTool )
	{
		MeshTool = meshTool;
	}
}
