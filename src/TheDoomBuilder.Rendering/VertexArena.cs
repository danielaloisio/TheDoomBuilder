using System;
using System.Collections.Generic;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.Rendering
{
	/// <summary>
	/// CPU-side bookkeeping for the big shared vertex buffer of one vertex format. Every <see cref="VertexBuffer"/> the
	/// editor creates is a range inside it (this keeps the number of GL buffers tiny and lets one VAO serve them all).
	/// Ranges are only ever appended; when the buffer is full the live ranges are compacted into a new, possibly larger,
	/// buffer. This class holds no GL objects so the allocation logic can be tested without a graphics context.
	/// </summary>
	internal sealed class VertexArena
	{
		public sealed class Range
		{
			public int Offset;       // bytes from the start of the shared buffer
			public int Size;         // bytes
			public int StartIndex;   // first vertex index (Offset / stride)
			public LinkedListNode<Range> Node;
			public object Tag;       // owner (the buffer object)
		}

		public readonly struct CopyRun
		{
			public readonly int ReadOffset, WriteOffset, Size;
			public CopyRun(int read, int write, int size) { ReadOffset = read; WriteOffset = write; Size = size; }
		}

		public readonly VertexFormat Format;
		public readonly int Stride;
		public int Size { get; private set; }
		public int NextPos { get; private set; }
		public readonly LinkedList<Range> Ranges = new LinkedList<Range>();

		public VertexArena(VertexFormat format, int size)
		{
			Format = format;
			Stride = format == VertexFormat.Flat ? FlatVertex.Stride : WorldVertex.Stride;
			Size = size;
		}

		public bool HasRoom(int size) { return NextPos + size <= Size; }

		/// <summary>Appends a range. The caller must have made room first (see <see cref="Compact"/>).</summary>
		public Range Allocate(int size, object tag)
		{
			if(!HasRoom(size)) throw new InvalidOperationException("Vertex arena is full");
			Range r = new Range { Offset = NextPos, Size = size, StartIndex = NextPos / Stride, Tag = tag };
			r.Node = Ranges.AddLast(r);
			NextPos += size;
			return r;
		}

		public void Free(Range range)
		{
			if(range.Node != null && range.Node.List == Ranges)
				Ranges.Remove(range.Node);
			range.Node = null;
		}

		/// <summary>
		/// Builds the arena that replaces this one when it is full and an allocation of <paramref name="requested"/> bytes is
		/// pending: live ranges are packed to the start, and the size grows when the buffer was more than half used.
		/// The ranges are moved (not copied) into the returned arena and their offsets updated.
		/// </summary>
		/// <param name="copies">The GPU copies that move the live data, in order.</param>
		public VertexArena Compact(int requested, out List<CopyRun> copies)
		{
			int total = requested;
			foreach(Range r in Ranges) total += r.Size;

			// If the buffer is only half full we only need to GC. Otherwise we also need to expand it.
			int newsize = Math.Max(total, Size);
			if(newsize < total * 2) newsize *= 2;

			VertexArena next = new VertexArena(Format, newsize);
			copies = new List<CopyRun>();

			int readpos = 0, writepos = 0, copysize = 0;
			foreach(Range r in Ranges)
			{
				if(r.Offset != readpos + copysize)
				{
					if(copysize != 0) copies.Add(new CopyRun(readpos, writepos, copysize));
					readpos = r.Offset;
					writepos += copysize;
					copysize = 0;
				}

				r.Offset = next.NextPos;
				r.StartIndex = r.Offset / Stride;
				next.NextPos += r.Size;
				copysize += r.Size;
			}
			if(copysize != 0) copies.Add(new CopyRun(readpos, writepos, copysize));

			// Hand the ranges over
			while(Ranges.First != null)
			{
				LinkedListNode<Range> node = Ranges.First;
				Ranges.RemoveFirst();
				node.Value.Node = next.Ranges.AddLast(node.Value);
			}

			return next;
		}
	}
}
