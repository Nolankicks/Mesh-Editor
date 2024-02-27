using System;
using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// References a vertex handle and the mesh component it belongs to.
/// </summary>
public readonly struct MeshVertex : IValid
{
	public EditorMeshComponent Component { get; private init; }
	public int Index { get; private init; }

	public readonly bool IsValid => Component.IsValid() && Index >= 0;

	public readonly GameObject GameObject => Component.IsValid() ? Component.GameObject : null;
	public readonly Transform Transform => Component.IsValid() ? Component.Transform.World : Transform.Zero;

	public readonly Vector3 PositionLocal => Component.IsValid() ? Component.GetVertexPosition( Index ) : Vector3.Zero;
	public readonly Vector3 PositionWorld => Component.IsValid() ? Transform.PointToWorld( PositionLocal ) : Vector3.Zero;

	public MeshVertex( EditorMeshComponent component, int index )
	{
		Component = component;
		Index = index;
	}

	public readonly override int GetHashCode() => HashCode.Combine( Component, nameof( MeshVertex ), Index );
	public override readonly string ToString() => $"Vertex {Index}";
}
