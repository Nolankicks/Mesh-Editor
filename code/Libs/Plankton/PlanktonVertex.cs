
namespace Plankton;

/// <summary>
/// Represents a vertex in Plankton's halfedge mesh data structure.
/// </summary>
public partial class PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits>
{
	public class PlanktonVertex
	{
		public TVertexTraits Traits { get; set; }

		public int OutgoingHalfedge;

		internal PlanktonVertex()
		{
			OutgoingHalfedge = -1;
		}

		internal PlanktonVertex( float x, float y, float z )
		{
			OutgoingHalfedge = -1;
			X = x;
			Y = y;
			Z = z;
		}

		public float X { get; set; }

		public float Y { get; set; }

		public float Z { get; set; }

		public Vector3 Position
		{
			get { return new Vector3( X, Y, Z ); }
			set { X = value.x; Y = value.y; Z = value.z; }
		}

		/// <summary>
		/// Gets an unset PlanktonVertex. Unset vertices have an outgoing halfedge index of -1.
		/// </summary>
		public static PlanktonVertex Unset
		{
			get { return new PlanktonVertex() { OutgoingHalfedge = -1 }; }
		}

		/// <summary>
		/// Whether or not the vertex is currently being referenced in the mesh.
		/// </summary>
		public bool IsUnused { get { return OutgoingHalfedge < 0; } }

		public static implicit operator TVertexTraits( PlanktonVertex vertex )
		{
			return vertex.Traits;
		}
	}
}
