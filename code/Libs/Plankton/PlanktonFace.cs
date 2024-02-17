
namespace Plankton;

/// <summary>
/// Represents a face in Plankton's halfedge mesh data structure.
/// </summary>
public partial class PlanktonMesh<TFaceTraits, THalfedgeTraits, TVertexTraits>
{
	public class PlanktonFace
	{
		public TFaceTraits Traits { get; set; }

		public int FirstHalfedge;

		public PlanktonFace()
		{
			FirstHalfedge = -1;
		}

		internal PlanktonFace( int halfedgeIndex )
		{
			FirstHalfedge = halfedgeIndex;
		}

		/// <summary>
		/// Gets an unset PlanktonFace. Unset faces have -1 for their first halfedge index.
		/// </summary>
		public static PlanktonFace Unset
		{
			get { return new PlanktonFace() { FirstHalfedge = -1 }; }
		}

		/// <summary>
		/// Whether or not the face is currently being referenced in the mesh.
		/// </summary>
		public bool IsUnused { get { return (FirstHalfedge < 0); } }

		public static implicit operator TFaceTraits( PlanktonFace face )
		{
			return face.Traits;
		}
	}
}
