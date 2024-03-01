using System;
using Sandbox;

namespace Editor.MeshEditor;

/// <summary>
/// References a edge handle and the mesh component it belongs to.
/// </summary>
public readonly struct MeshEdge : IMeshElement
{
	[Hide] public EditorMeshComponent Component { get; private init; }
	[Hide] public int Index { get; private init; }

	[Hide] public readonly bool IsValid => Component.IsValid() && Index >= 0;
	[Hide] public readonly Transform Transform => IsValid ? Component.Transform.World : Transform.Zero;

	public MeshEdge( EditorMeshComponent component, int index )
	{
		Component = component;
		Index = index;
	}

	public readonly override int GetHashCode() => HashCode.Combine( Component, nameof( MeshEdge ), Index );
	public override readonly string ToString() => IsValid ? $"{Component.GameObject.Name} Edge {Index}" : "Invalid Edge";
}
