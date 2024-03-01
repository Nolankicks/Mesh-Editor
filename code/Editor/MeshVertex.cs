using System;
using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// References a vertex handle and the mesh component it belongs to.
/// </summary>
public readonly struct MeshVertex : IMeshElement
{
	[Hide] public EditorMeshComponent Component { get; private init; }
	[Hide] public int Index { get; private init; }

	[Hide] public readonly bool IsValid => Component.IsValid() && Index >= 0;
	[Hide] public readonly Transform Transform => IsValid ? Component.Transform.World : Transform.Zero;

	[Hide] public readonly Vector3 PositionLocal => IsValid ? Component.GetVertexPosition( Index ) : Vector3.Zero;
	[Hide] public readonly Vector3 PositionWorld => IsValid ? Transform.PointToWorld( PositionLocal ) : Vector3.Zero;

	public MeshVertex( EditorMeshComponent component, int index )
	{
		Component = component;
		Index = index;
	}

	public readonly override int GetHashCode() => HashCode.Combine( Component, nameof( MeshVertex ), Index );
	public override readonly string ToString() => IsValid ? $"{Component.GameObject.Name} Vertex {Index}" : "Invalid Vertex";
}
