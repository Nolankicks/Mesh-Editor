using System;
using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// References a edge handle and the mesh component it belongs to.
/// </summary>
public readonly struct MeshEdge : IValid
{
	public EditorMeshComponent Component { get; private init; }
	public int Index { get; private init; }

	public readonly bool IsValid => Component.IsValid() && Index >= 0;

	public readonly Transform Transform => Component.IsValid() ? Component.Transform.World : Transform.Zero;

	public MeshEdge( EditorMeshComponent component, int index )
	{
		Component = component;
		Index = index;
	}

	public readonly override int GetHashCode() => HashCode.Combine( Component, nameof( MeshEdge ), Index );
	public override readonly string ToString() => $"{Component.GameObject.Name} Edge {Index}";
}
