using Sandbox;

namespace Editor.MeshEditor;

public abstract class PrimitiveBuilder
{
	/// <summary>
	/// Create the primitive in the mesh.
	/// </summary>
	public abstract void Build( PolygonMesh mesh );

	/// <summary>
	/// Setup properties from box.
	/// </summary>
	public abstract void SetFromBox( BBox box );

	/// <summary>
	/// If this primitive is 2D the bounds box will be limited to have no depth.
	/// </summary>
	[Hide]
	public virtual bool Is2D { get => false; }
}
