using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// A mesh element can be a vertex, edge or face belonging to a mesh
/// </summary>
public interface IMeshElement : IValid
{
	public EditorMeshComponent Component { get; }
	public GameObject GameObject => Component.IsValid() ? Component.GameObject : null;
}
