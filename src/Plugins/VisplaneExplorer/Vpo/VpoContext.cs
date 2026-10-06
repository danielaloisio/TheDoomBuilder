// The Visplane Overflow library (VPO): Doom's own renderer, run without drawing anything, to count what a view needs: visplanes, drawsegs,
// openings and solid segs. UDB ships it as native code (BuilderNative); this is a C# port of that library (itself from Doom 1.9 by id Software,
// Simon Howard's Chocolate Doom and Andrew Apted's Visplane Explorer, GPL), so there is no native code to build for each platform.
// The code follows the C++ line by line (fixed point arithmetic, unsigned angles that wrap) so that it counts what the original counts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeImp.DoomBuilder.Plugins.VisplaneExplorer.Vpo
{
	/// <summary>Raised when one of the (enlarged) limits of the renderer is passed.</summary>
	internal class OverflowException : Exception { }

	internal class InvalidMapDataException : Exception
	{
		public InvalidMapDataException(string message) : base(message) { }
	}

	internal sealed class VpoContext
	{
		#region ================== Constants

		public const int RESULT_OK = 0;
		public const int RESULT_BAD_Z = -1;
		public const int RESULT_IN_VOID = -2;
		public const int RESULT_OVERFLOW = -3;

		private const int FRACBITS = 16;
		private const int FRACUNIT = 1 << FRACBITS;
		private const int FINEANGLES = 8192;
		private const int ANGLETOFINESHIFT = 19;
		private const uint ANG90 = 0x40000000;
		private const uint ANG180 = 0x80000000;
		private const uint ANG270 = 0xc0000000;
		private const int SLOPERANGE = 2048;
		private const int SLOPEBITS = 11;
		private const int DBITS = FRACBITS - SLOPEBITS;
		private const int FIELDOFVIEW = 2048;
		private const int SCREENWIDTH = 320;
		private const int SCREENHEIGHT = 200;
		private const int NF_SUBSECTOR = 0x8000;
		private const int HEIGHTBITS = 12;
		private const int HEIGHTUNIT = 1 << HEIGHTBITS;
		private const int skyflatnum = 2;

		private const int MAXDRAWSEGS = 1024;          // (the lump, 1.9 value)
		private const int MAXVISPLANES = 512;          // increased for Visplane Explorer
		private const int MAXOPENINGS = SCREENWIDTH * 256;
		private const int MAXSOLIDSEGS = 128;

		private const int SIL_BOTTOM = 1;
		private const int SIL_TOP = 2;
		private const int SIL_BOTH = 3;

		private const int ML_TWOSIDED = 4;
		private const int ML_DONTPEGTOP = 8;
		private const int ML_DONTPEGBOTTOM = 16;
		private const int ML_MAPPED = 256;

		private const int BOXTOP = 0, BOXBOTTOM = 1, BOXLEFT = 2, BOXRIGHT = 3;

		// The lumps of a map after its header
		private const int ML_THINGS = 1, ML_LINEDEFS = 2, ML_SIDEDEFS = 3, ML_VERTEXES = 4, ML_SEGS = 5, ML_SSECTORS = 6, ML_NODES = 7, ML_SECTORS = 8;

		private static readonly string[] LevelLumps = { "THINGS", "LINEDEFS", "SIDEDEFS", "VERTEXES", "SEGS", "SSECTORS", "NODES", "SECTORS", "REJECT", "BLOCKMAP" };

		private static readonly int[][] checkcoord =
		{
			new[] { 3, 0, 2, 1 },
			new[] { 3, 0, 2, 0 },
			new[] { 3, 1, 2, 0 },
			new[] { 0, 0, 0, 0 },
			new[] { 2, 0, 2, 1 },
			new[] { 0, 0, 0, 0 },
			new[] { 3, 1, 3, 0 },
			new[] { 0, 0, 0, 0 },
			new[] { 2, 0, 3, 1 },
			new[] { 2, 1, 3, 1 },
			new[] { 2, 1, 3, 0 },
			new[] { 0, 0, 0, 0 },
		};

		#endregion

		#region ================== Map structures

		private sealed class Vertex { public int x, y; }

		private sealed class Sector
		{
			public int floorheight, ceilingheight;
			public short floorpic, ceilingpic, lightlevel, special, tag;
			public List<Line> lines = new List<Line>();
			public int is_door;
			public int alt_height;
		}

		private sealed class Side
		{
			public int textureoffset, rowoffset;
			public short toptexture, bottomtexture, midtexture;
			public Sector sector;
		}

		private sealed class Line
		{
			public Vertex v1, v2;
			public int dx, dy;
			public short flags, special, tag;
			public short[] sidenum = new short[2];
			public int[] bbox = new int[4];
			public Sector frontsector, backsector;
			public byte[] args = new byte[5];
		}

		private sealed class Subsector
		{
			public Sector sector;
			public short numlines, firstline;
		}

		private sealed class Seg
		{
			public Vertex v1, v2;
			public int offset;
			public uint angle;
			public Side sidedef;
			public Line linedef;
			public Sector frontsector, backsector;
		}

		private sealed class Node
		{
			public int x, y, dx, dy;
			public int[][] bbox = { new int[4], new int[4] };
			public ushort[] children = new ushort[2];
		}

		private sealed class Visplane
		{
			public int height, picnum, lightlevel, minx, maxx;
			public readonly byte[] top = new byte[SCREENWIDTH + 2];
			public readonly byte[] bottom = new byte[SCREENWIDTH + 2];
		}

		private struct ClipRange { public int first, last; }

		private struct LumpInfo
		{
			public int position, size;
			public string name;
			public bool is_map_header, is_hexen;
		}

		#endregion

		#region ================== Variables

		// Wad
		private byte[] wad;
		private LumpInfo[] lumpinfo = new LumpInfo[0];
		private string error = "(No Error)";
		private bool level_is_hexen;

		// Level
		private Vertex[] vertexes = new Vertex[0];
		private Seg[] segs = new Seg[0];
		private Sector[] sectors = new Sector[0];
		private Subsector[] subsectors = new Subsector[0];
		private Node[] nodes = new Node[0];
		private Line[] lines = new Line[0];
		private Side[] sides = new Side[0];
		private readonly int[] Map_bbox = new int[4];

		// Renderer
		private Seg curline;
		private Side sidedef;
		private Line linedef;
		private Sector frontsector, backsector;
		private int total_drawsegs;
		private readonly ClipRange[] solidsegs = new ClipRange[MAXSOLIDSEGS + 8];
		private int newend;                   // index in solidsegs: one past the last valid seg
		private int max_solidsegs;
		private int centerx, centery, centerxfrac, centeryfrac, projection;
		private int sscount;
		private int viewx, viewy, viewz;
		private uint viewangle;
		private uint clipangle;
		private readonly int[] viewangletox = new int[FINEANGLES / 2];
		private readonly uint[] xtoviewangle = new uint[SCREENWIDTH + 1];
		private int viewwidth, scaledviewwidth, viewheight;
		private readonly Visplane[] visplanes = new Visplane[MAXVISPLANES + 10];
		private int lastvisplane;
		private Visplane floorplane, ceilingplane;
		private int total_visplanes;
		private readonly short[] openings = new short[MAXOPENINGS + 400];
		private int lastopening;
		private int total_openings;
		private readonly short[] floorclip = new short[SCREENWIDTH];
		private readonly short[] ceilingclip = new short[SCREENWIDTH];
		private readonly int[] yslope = new int[SCREENHEIGHT];
		private readonly int[] distscale = new int[SCREENWIDTH];

		private bool segtextured, markfloor, markceiling, maskedtexture;
		private int toptexture, bottomtexture, midtexture;
		private uint rw_normalangle;
		private int rw_angle1;
		private int rw_x, rw_stopx;
		private uint rw_centerangle;
		private int rw_offset, rw_distance, rw_scale, rw_scalestep, rw_midtexturemid, rw_toptexturemid, rw_bottomtexturemid;
		private int worldtop, worldbottom, worldhigh, worldlow;
		private int pixhigh, pixlow, pixhighstep, pixlowstep;
		private int topfrac, topstep, bottomfrac, bottomstep;
		private int maskedtexturecol;         // index in openings of "column 0" of the masked texture columns

		// cache for the sector lookup
		private int last_x, last_y;
		private Sector last_sector;

		#endregion

		#region ================== Fixed point math

		private static int Abs(int x) { return x < 0 ? -x : x; }   // (wraps for int.MinValue, like in C)

		private static int FixedMul(int a, int b)
		{
			return (int)(((long)a * (long)b) >> FRACBITS);
		}

		private static int FixedDiv(int a, int b)
		{
			if((Abs(a) >> 14) >= Abs(b))
			{
				return (a ^ b) < 0 ? int.MinValue : int.MaxValue;
			}
			else
			{
				long result = ((long)a << 16) / b;
				return (int)result;
			}
		}

		private static int SlopeDiv(uint num, uint den)
		{
			if(den < 512) return SLOPERANGE;
			uint ans = (num << 3) / (den >> 8);
			return (int)(ans <= SLOPERANGE ? ans : SLOPERANGE);
		}

		private static int finecosine(uint index) { return VpoTables.finesine[FINEANGLES / 4 + (int)index]; }

		private static void M_ClearBox(int[] box)
		{
			box[BOXTOP] = box[BOXRIGHT] = int.MinValue;
			box[BOXBOTTOM] = box[BOXLEFT] = int.MaxValue;
		}

		private static void M_AddToBox(int[] box, int x, int y)
		{
			if(x < box[BOXLEFT]) box[BOXLEFT] = x;
			else if(x > box[BOXRIGHT]) box[BOXRIGHT] = x;

			if(y < box[BOXBOTTOM]) box[BOXBOTTOM] = y;
			else if(y > box[BOXTOP]) box[BOXTOP] = y;
		}

		#endregion

		#region ================== Constructor

		public VpoContext()
		{
			for(int i = 0; i < visplanes.Length; i++) visplanes[i] = new Visplane();
			last_x = last_y = -77777;
		}

		/// <summary>The message of the last thing that failed.</summary>
		public string Error { get { return error; } }

		#endregion

		#region ================== Wad

		private static bool NameIs(byte[] data, int offset, string name)
		{
			for(int i = 0; i < 8; i++)
			{
				int c = data[offset + i];
				int n = i < name.Length ? name[i] : 0;
				if(c >= 'a' && c <= 'z') c -= 32;
				if(n >= 'a' && n <= 'z') n -= 32;
				if(c != n) return false;
				if(c == 0) return true;
			}
			return true;
		}

		private static string NameOf(byte[] data, int offset)
		{
			int length = 0;
			while(length < 8 && data[offset + length] != 0) length++;
			return Encoding.ASCII.GetString(data, offset, length);
		}

		// Returns 0 when this is not a map header, 1 for a Doom map, 2 for a Hexen map
		private static int CheckMapHeader(byte[] data, int dirofs, int index, int count)
		{
			int num_after = count - index - 1;
			if(num_after < 10) return 0;

			for(int i = 0; i < 10; i++)
			{
				string name = LevelLumps[i];
				if(NameIs(data, dirofs + index * 16 + 8, name)) return 0;
				if(!NameIs(data, dirofs + (index + 1 + i) * 16 + 8, name)) return 0;
			}

			if(num_after >= 11 && NameIs(data, dirofs + (index + 11) * 16 + 8, "BEHAVIOR")) return 2;
			return 1;
		}

		/// <summary>Reads the directory of a wad file. False when it is not a wad (see Error).</summary>
		public bool LoadWad(string filename)
		{
			error = "(No Error)";
			FreeWad();

			Init();

			try
			{
				byte[] data = File.ReadAllBytes(filename);
				if(data.Length < 12) throw new InvalidDataException();
				string id = Encoding.ASCII.GetString(data, 0, 4);
				if(id != "IWAD" && id != "PWAD") throw new InvalidDataException();
				int numlumps = BitConverter.ToInt32(data, 4);
				int infotableofs = BitConverter.ToInt32(data, 8);
				if(numlumps < 0 || infotableofs < 0 || (long)infotableofs + (long)numlumps * 16 > data.Length) throw new InvalidDataException();

				LumpInfo[] info = new LumpInfo[numlumps];
				for(int i = 0; i < numlumps; i++)
				{
					int entry = infotableofs + i * 16;
					info[i].position = BitConverter.ToInt32(data, entry);
					info[i].size = BitConverter.ToInt32(data, entry + 4);
					info[i].name = NameOf(data, entry + 8);
					int header = CheckMapHeader(data, infotableofs, i, numlumps);
					info[i].is_map_header = (header >= 1);
					info[i].is_hexen = (header == 2);
				}

				wad = data;
				lumpinfo = info;
				return true;
			}
			catch(Exception)
			{
				error = "Missing or invalid wad file: " + filename;
				return false;
			}
		}

		/// <summary>Frees the wad and the map.</summary>
		public void FreeWad()
		{
			CloseMap();
			wad = null;
			lumpinfo = new LumpInfo[0];
		}

		private int CheckNumForName(string name)
		{
			for(int i = lumpinfo.Length - 1; i >= 0; --i)
			{
				if(string.Equals(lumpinfo[i].name, name, StringComparison.OrdinalIgnoreCase)) return i;
			}
			return -1;
		}

		private byte[] LoadLump(int lumpnum)
		{
			if(lumpnum < 0 || lumpnum >= lumpinfo.Length) throw new InvalidMapDataException("Bad lump " + lumpnum);
			LumpInfo l = lumpinfo[lumpnum];
			if(l.size < 0 || l.position < 0 || (long)l.position + l.size > wad.Length) throw new InvalidMapDataException("Bad map data : lump " + l.name + " is outside of the file");
			byte[] result = new byte[l.size];
			Buffer.BlockCopy(wad, l.position, result, 0, l.size);
			return result;
		}

		#endregion

		#region ================== Map

		private static short S16(byte[] data, int offset) { return BitConverter.ToInt16(data, offset); }
		private static ushort U16(byte[] data, int offset) { return BitConverter.ToUInt16(data, offset); }

		private static int R_TextureNumForName(byte[] data, int offset)
		{
			return data[offset] == '-' ? 0 : 1;
		}

		private static int R_FlatNumForName(byte[] data, int offset)
		{
			if(data[offset] == 'F' && data[offset + 1] == '_' && data[offset + 2] == 'S' && data[offset + 3] == 'K') return skyflatnum;
			return 1;
		}

		private void P_LoadVertexes(int lump)
		{
			byte[] data = LoadLump(lump);
			vertexes = new Vertex[data.Length / 4];
			for(int i = 0; i < vertexes.Length; i++)
				vertexes[i] = new Vertex { x = S16(data, i * 4) << FRACBITS, y = S16(data, i * 4 + 2) << FRACBITS };
		}

		private void P_LoadSegs(int lump)
		{
			byte[] data = LoadLump(lump);
			segs = new Seg[data.Length / 12];
			for(int i = 0; i < segs.Length; i++)
			{
				int o = i * 12;
				int v1_idx = S16(data, o), v2_idx = S16(data, o + 2);
				if(v1_idx < 0 || v1_idx >= vertexes.Length || v2_idx < 0 || v2_idx >= vertexes.Length)
					throw new InvalidMapDataException("Bad map data : vertex out of range (seg #" + i + ")");

				Seg li = new Seg { v1 = vertexes[v1_idx], v2 = vertexes[v2_idx] };
				int line_idx = S16(data, o + 6);
				if(line_idx < 0 || line_idx >= lines.Length)
					throw new InvalidMapDataException("Bad map data : linedef out of range (seg #" + i + ")");

				li.angle = (uint)(S16(data, o + 4) << 16);
				li.offset = S16(data, o + 10) << 16;
				Line ldef = lines[line_idx];
				li.linedef = ldef;
				int side = S16(data, o + 8);
				if(side < 0 || side > 1 || ldef.sidenum[side] < 0 || ldef.sidenum[side] >= sides.Length)
					throw new InvalidMapDataException("Bad map data : no sidedef for the seg #" + i);

				li.sidedef = sides[ldef.sidenum[side]];
				li.frontsector = sides[ldef.sidenum[side]].sector;

				if((ldef.flags & ML_TWOSIDED) != 0)
				{
					int sidenum = ldef.sidenum[side ^ 1];
					if(sidenum < 0 || sidenum >= sides.Length)
						li.backsector = sectors[0];     // (GetSectorAtNullAddress)
					else
						li.backsector = sides[sidenum].sector;
				}
				else
				{
					li.backsector = null;
				}
				segs[i] = li;
			}
		}

		private void P_LoadSubsectors(int lump)
		{
			byte[] data = LoadLump(lump);
			subsectors = new Subsector[data.Length / 4];
			for(int i = 0; i < subsectors.Length; i++)
				subsectors[i] = new Subsector { numlines = S16(data, i * 4), firstline = S16(data, i * 4 + 2) };
		}

		private void ValidateSubsectors()
		{
			for(int i = 0; i < subsectors.Length; i++)
			{
				Subsector ss = subsectors[i];
				if(ss.firstline < 0 || ss.numlines < 0 || ss.firstline + ss.numlines > segs.Length)
					throw new InvalidMapDataException("Bad map data : invalid seg range in subsector #" + i);
			}
		}

		private void P_LoadSectors(int lump)
		{
			byte[] data = LoadLump(lump);
			sectors = new Sector[data.Length / 26];
			for(int i = 0; i < sectors.Length; i++)
			{
				int o = i * 26;
				sectors[i] = new Sector
				{
					floorheight = S16(data, o) << FRACBITS,
					ceilingheight = S16(data, o + 2) << FRACBITS,
					floorpic = (short)R_FlatNumForName(data, o + 4),
					ceilingpic = (short)R_FlatNumForName(data, o + 12),
					lightlevel = S16(data, o + 20),
					special = S16(data, o + 22),
					tag = S16(data, o + 24),
				};
			}
		}

		private bool IsChildValid(ushort child)
		{
			if((child & NF_SUBSECTOR) != 0) return ((child & ~NF_SUBSECTOR) < subsectors.Length);
			return child < nodes.Length;
		}

		private void P_LoadNodes(int lump)
		{
			byte[] data = LoadLump(lump);
			nodes = new Node[data.Length / 28];
			for(int i = 0; i < nodes.Length; i++) nodes[i] = new Node();

			for(int i = 0; i < nodes.Length; i++)
			{
				int o = i * 28;
				Node no = nodes[i];
				no.x = S16(data, o) << FRACBITS;
				no.y = S16(data, o + 2) << FRACBITS;
				no.dx = S16(data, o + 4) << FRACBITS;
				no.dy = S16(data, o + 6) << FRACBITS;

				for(int j = 0; j < 2; j++)
				{
					ushort child = U16(data, o + 24 + j * 2);
					if(!IsChildValid(child)) throw new InvalidMapDataException("Bad map data : invalid child in node #" + i);
					no.children[j] = child;
					for(int k = 0; k < 4; k++) no.bbox[j][k] = S16(data, o + 8 + j * 8 + k * 2) << FRACBITS;
				}
			}
		}

		private void LineDef_CommonSetup(Line ld)
		{
			Vertex v1 = ld.v1, v2 = ld.v2;
			ld.dx = v2.x - v1.x;
			ld.dy = v2.y - v1.y;

			if(v1.x < v2.x) { ld.bbox[BOXLEFT] = v1.x; ld.bbox[BOXRIGHT] = v2.x; }
			else { ld.bbox[BOXLEFT] = v2.x; ld.bbox[BOXRIGHT] = v1.x; }

			if(v1.y < v2.y) { ld.bbox[BOXBOTTOM] = v1.y; ld.bbox[BOXTOP] = v2.y; }
			else { ld.bbox[BOXBOTTOM] = v2.y; ld.bbox[BOXTOP] = v1.y; }

			// Sanity checks on the sidedef numbers (as in the original, which cannot be trusted with the data of a map being edited)
			if(ld.sidenum[0] < 0 || ld.sidenum[0] >= sides.Length)
			{
				if(sides.Length == 0) throw new InvalidMapDataException("Bad map data : no sidedefs!");
				ld.sidenum[0] = 0;
			}
			if(ld.sidenum[1] < -1 || ld.sidenum[1] >= sides.Length) ld.sidenum[1] = -1;

			ld.frontsector = ld.sidenum[0] != -1 ? sides[ld.sidenum[0]].sector : null;
			ld.backsector = ld.sidenum[1] != -1 ? sides[ld.sidenum[1]].sector : null;
		}

		private void P_LoadLineDefs(int lump, bool hexen)
		{
			byte[] data = LoadLump(lump);
			int size = hexen ? 16 : 14;
			lines = new Line[data.Length / size];
			for(int i = 0; i < lines.Length; i++)
			{
				int o = i * size;
				int v1_idx = S16(data, o), v2_idx = S16(data, o + 2);
				if(v1_idx < 0 || v1_idx >= vertexes.Length || v2_idx < 0 || v2_idx >= vertexes.Length)
					throw new InvalidMapDataException("Bad map data : vertex out of range (line #" + i + ")");

				Line ld = new Line { v1 = vertexes[v1_idx], v2 = vertexes[v2_idx], flags = S16(data, o + 4) };
				if(hexen)
				{
					ld.special = data[o + 6];
					ld.tag = 0;
					for(int k = 0; k < 5; k++) ld.args[k] = data[o + 7 + k];
					ld.sidenum[0] = S16(data, o + 12);
					ld.sidenum[1] = S16(data, o + 14);
				}
				else
				{
					ld.special = S16(data, o + 6);
					ld.tag = S16(data, o + 8);
					ld.sidenum[0] = S16(data, o + 10);
					ld.sidenum[1] = S16(data, o + 12);
				}
				lines[i] = ld;
				LineDef_CommonSetup(ld);
			}
		}

		private void P_LoadSideDefs(int lump)
		{
			byte[] data = LoadLump(lump);
			sides = new Side[data.Length / 30];
			for(int i = 0; i < sides.Length; i++)
			{
				int o = i * 30;
				int sec_idx = S16(data, o + 28);
				if(sec_idx < 0 || sec_idx >= sectors.Length)
				{
					if(sectors.Length == 0) throw new InvalidMapDataException("Bad map data : no sectors!");
					sec_idx = 0;
				}

				sides[i] = new Side
				{
					textureoffset = S16(data, o) << FRACBITS,
					rowoffset = S16(data, o + 2) << FRACBITS,
					toptexture = (short)R_TextureNumForName(data, o + 4),
					bottomtexture = (short)R_TextureNumForName(data, o + 12),
					midtexture = (short)R_TextureNumForName(data, o + 20),
					sector = sectors[sec_idx],
				};
			}
		}

		private void P_GroupLines()
		{
			// Look up the sector of each subsector
			foreach(Subsector ss in subsectors) ss.sector = segs[ss.firstline].sidedef.sector;

			// Build the list of lines of each sector
			foreach(Line li in lines)
			{
				if(li.frontsector != null) li.frontsector.lines.Add(li);
				if(li.backsector != null && li.frontsector != li.backsector) li.backsector.lines.Add(li);
			}

			// The bounding box of the map
			M_ClearBox(Map_bbox);
			foreach(Line li in lines)
			{
				M_AddToBox(Map_bbox, li.v1.x, li.v1.y);
				M_AddToBox(Map_bbox, li.v2.x, li.v2.y);
			}
		}

		private bool HasManualDoor(Sector sec)
		{
			foreach(Line L in sec.lines)
			{
				if(level_is_hexen)
				{
					switch(L.special)
					{
						case 10: case 11: case 12: case 13:
						case 202: /* zdoom's Generic_Door */
							if(L.args[0] == 0) return true;
							break;
					}
				}
				else
				{
					switch(L.special)
					{
						case 1: case 26: case 27: case 28:
						case 31: case 32: case 33: case 34:
						case 117: case 118:
							return true;
					}
				}
			}
			return false;
		}

		// Finds the height a door sector opens to (or closes to)
		private void CalcDoorAltHeight(Sector sec)
		{
			int door_h = sec.floorheight;
			int low_ceil = 32767 << FRACBITS;
			int high_floor = -32767 << FRACBITS;

			foreach(Line L in sec.lines)
			{
				for(int pass = 0; pass < 2; pass++)
				{
					Sector nb = pass != 0 ? L.backsector : L.frontsector;
					if(nb != null && nb != sec)
					{
						if(low_ceil > nb.ceilingheight) low_ceil = nb.ceilingheight;
						if(high_floor < nb.floorheight) high_floor = nb.floorheight;
					}
				}
			}

			int mid_h = (low_ceil >> 1) + (high_floor >> 1);
			if(door_h > mid_h)
			{
				// a door that is open at the start: it closes to the ceiling
				sec.is_door = -1;
				sec.alt_height = high_floor;
			}
			else
			{
				sec.alt_height = low_ceil - (4 * FRACUNIT);
			}
		}

		private void P_DetectDoorSectors()
		{
			foreach(Sector sec in sectors)
			{
				if(sec.floorheight != sec.ceilingheight) continue;
				if(sec.tag != 0 || HasManualDoor(sec))
				{
					sec.is_door = +1;
					CalcDoorAltHeight(sec);
				}
			}
		}

		/// <summary>Opens a map of the loaded wad. Returns false with the message in Error when that is not possible.</summary>
		public bool OpenMap(string mapname, ref bool isHexen)
		{
			if(wad == null)
			{
				error = "VPO_OpenMap called without any loaded wad";
				return false;
			}

			error = "(No Error)";
			CloseMap();

			int basenum = CheckNumForName(mapname);
			if(basenum < 0 || !lumpinfo[basenum].is_map_header)
			{
				error = "No such map in wad: " + mapname;
				return false;
			}

			level_is_hexen = lumpinfo[basenum].is_hexen;
			isHexen = level_is_hexen;

			if(lumpinfo[basenum + ML_SEGS].size == 0)
			{
				error = "Missing nodes for: " + mapname;
				return false;
			}

			try
			{
				P_LoadVertexes(basenum + ML_VERTEXES);
				P_LoadSectors(basenum + ML_SECTORS);
				P_LoadSideDefs(basenum + ML_SIDEDEFS);
				P_LoadLineDefs(basenum + ML_LINEDEFS, level_is_hexen);
				P_LoadSubsectors(basenum + ML_SSECTORS);
				P_LoadNodes(basenum + ML_NODES);
				P_LoadSegs(basenum + ML_SEGS);
				ValidateSubsectors();
			}
			catch(InvalidMapDataException e)
			{
				error = e.Message;
				CloseMap();
				return false;
			}

			P_GroupLines();
			P_DetectDoorSectors();
			return true;
		}

		/// <summary>Frees the map.</summary>
		public void CloseMap()
		{
			error = "(No Error)";
			last_x = -77777;
			last_y = -77777;
			last_sector = null;

			vertexes = new Vertex[0];
			sectors = new Sector[0];
			sides = new Side[0];
			lines = new Line[0];
			segs = new Seg[0];
			subsectors = new Subsector[0];
			nodes = new Node[0];
		}

		/// <summary>Opens (dir &gt; 0) or closes (dir &lt; 0) all the sectors that look like doors.</summary>
		public void OpenDoorSectors(int dir)
		{
			foreach(Sector sec in sectors)
			{
				if(sec.is_door == 0) continue;

				if(dir > 0)
				{
					if(sec.is_door > 0) sec.ceilingheight = sec.alt_height;
					else sec.floorheight = sec.alt_height;
				}
				else if(dir < 0)
				{
					if(sec.is_door > 0) sec.ceilingheight = sec.floorheight;
					else sec.floorheight = sec.ceilingheight;
				}
			}
		}

		#endregion

		#region ================== Finding the sector of a point

		private int ClosestLine_CastingHoriz(int x, int y, out int side)
		{
			int best_match = -1;
			int best_dist = 32000 << FRACBITS;
			side = 0;

			for(int n = 0; n < lines.Length; n++)
			{
				int ly1 = lines[n].v1.y;
				int ly2 = lines[n].v2.y;

				// ignore horizontal lines
				if(ly1 == ly2) continue;

				// ignore lines that are completely above or below the point
				if((y < ly1) && (y < ly2)) continue;
				if((y > ly1) && (y > ly2)) continue;

				int lx1 = lines[n].v1.x;
				int lx2 = lines[n].v2.x;

				int quot = FixedDiv(y - ly1, ly2 - ly1);
				int dist = lx1 - x + FixedMul(lx2 - lx1, quot);

				if(Abs(dist) < best_dist)
				{
					best_match = n;
					best_dist = Abs(dist);

					if(best_dist < FRACUNIT / 8) side = 0;
					else if((ly1 > ly2) == (dist > 0)) side = 1;
					else side = -1;
				}
			}

			return best_match;
		}

		private Sector X_SectorForPoint(int x, int y)
		{
			int sd;
			int ld = ClosestLine_CastingHoriz(x, y, out sd);
			if(ld < 0) return null;

			// the closest line must have a sidedef on the side of the point
			if(lines[ld].sidenum[sd <= 0 ? 1 : 0] < 0) return null;

			Subsector sub = R_PointInSubsector(x, y);
			return sub.sector;
		}

		#endregion

		#region ================== Renderer: main

		// Sets up the tables for the view size (the full screen)
		private void Init()
		{
			scaledviewwidth = SCREENWIDTH;
			viewheight = SCREENHEIGHT;
			viewwidth = scaledviewwidth;
			centery = viewheight / 2;
			centerx = viewwidth / 2;
			centerxfrac = centerx << FRACBITS;
			centeryfrac = centery << FRACBITS;
			projection = centerxfrac;

			R_InitTextureMapping();

			for(int i = 0; i < viewheight; i++)
			{
				int dy = ((i - viewheight / 2) << FRACBITS) + FRACUNIT / 2;
				dy = Abs(dy);
				yslope[i] = FixedDiv((viewwidth << 0) / 2 * FRACUNIT, dy);
			}

			for(int i = 0; i < viewwidth; i++)
			{
				int cosadj = Abs(finecosine(xtoviewangle[i] >> ANGLETOFINESHIFT));
				distscale[i] = FixedDiv(FRACUNIT, cosadj);
			}
		}

		private void R_InitTextureMapping()
		{
			int[] finetangent = VpoTables.finetangent;

			// Use tangent table to generate viewangletox: viewangletox will give the next greatest x after the view angle.
			// Calc focallength so FIELDOFVIEW angles covers SCREENWIDTH.
			int focallength = FixedDiv(centerxfrac, finetangent[FINEANGLES / 4 + FIELDOFVIEW / 2]);

			for(int i = 0; i < FINEANGLES / 2; i++)
			{
				int t;
				if(finetangent[i] > FRACUNIT * 2) t = -1;
				else if(finetangent[i] < -FRACUNIT * 2) t = viewwidth + 1;
				else
				{
					t = FixedMul(finetangent[i], focallength);
					t = (centerxfrac - t + FRACUNIT - 1) >> FRACBITS;

					if(t < -1) t = -1;
					else if(t > viewwidth + 1) t = viewwidth + 1;
				}
				viewangletox[i] = t;
			}

			// Scan viewangletox[] to generate xtoviewangle[]: xtoviewangle will give the smallest view angle that maps to x.
			for(int x = 0; x <= viewwidth; x++)
			{
				int i = 0;
				while(viewangletox[i] > x) i++;
				xtoviewangle[x] = unchecked((uint)(i << ANGLETOFINESHIFT) - ANG90);
			}

			// Take out the fencepost cases from viewangletox.
			for(int i = 0; i < FINEANGLES / 2; i++)
			{
				if(viewangletox[i] == -1) viewangletox[i] = 0;
				else if(viewangletox[i] == viewwidth + 1) viewangletox[i] = viewwidth;
			}

			clipangle = xtoviewangle[0];
		}

		private int R_PointOnSide(int x, int y, Node node)
		{
			if(node.dx == 0)
			{
				if(x <= node.x) return node.dy > 0 ? 1 : 0;
				return node.dy < 0 ? 1 : 0;
			}
			if(node.dy == 0)
			{
				if(y <= node.y) return node.dx < 0 ? 1 : 0;
				return node.dx > 0 ? 1 : 0;
			}

			int dx = (x - node.x);
			int dy = (y - node.y);

			// Try to quickly decide by looking at sign bits.
			if(((node.dy ^ node.dx ^ dx ^ dy) & int.MinValue) != 0)
			{
				if(((node.dy ^ dx) & int.MinValue) != 0)
				{
					// (left is negative)
					return 1;
				}
				return 0;
			}

			int left = FixedMul(node.dy >> FRACBITS, dx);
			int right = FixedMul(dy, node.dx >> FRACBITS);

			if(right < left) return 0;      // front side
			return 1;                       // back side
		}

		private uint R_PointToAngle(int x, int y)
		{
			uint[] tantoangle = VpoTables.tantoangle;
			x -= viewx;
			y -= viewy;

			if((x == 0) && (y == 0)) return 0;

			unchecked
			{
				if(x >= 0)
				{
					// x >=0
					if(y >= 0)
					{
						// y>= 0
						if(x > y) return tantoangle[SlopeDiv((uint)y, (uint)x)];                       // octant 0
						else return ANG90 - 1 - tantoangle[SlopeDiv((uint)x, (uint)y)];                 // octant 1
					}
					else
					{
						// y<0
						y = -y;
						if(x > y) return 0u - tantoangle[SlopeDiv((uint)y, (uint)x)];                   // octant 8
						else return ANG270 + tantoangle[SlopeDiv((uint)x, (uint)y)];                    // octant 7
					}
				}
				else
				{
					// x<0
					x = -x;
					if(y >= 0)
					{
						// y>= 0
						if(x > y) return ANG180 - 1 - tantoangle[SlopeDiv((uint)y, (uint)x)];          // octant 3
						else return ANG90 + tantoangle[SlopeDiv((uint)x, (uint)y)];                     // octant 2
					}
					else
					{
						// y<0
						y = -y;
						if(x > y) return ANG180 + tantoangle[SlopeDiv((uint)y, (uint)x)];               // octant 4
						else return ANG270 - 1 - tantoangle[SlopeDiv((uint)x, (uint)y)];                // octant 5
					}
				}
			}
		}

		private int R_PointToDist(int x, int y)
		{
			int dx = Abs(x - viewx);
			int dy = Abs(y - viewy);

			if(dy > dx)
			{
				int temp = dx;
				dx = dy;
				dy = temp;
			}

			int frac;
			if(dx != 0) frac = FixedDiv(dy, dx);
			else frac = 0;

			int angle = (int)((VpoTables.tantoangle[frac >> DBITS] + ANG90) >> ANGLETOFINESHIFT);

			// use as cosine
			return FixedDiv(dx, VpoTables.finesine[angle]);
		}

		// Calculates the scale of a wall at an angle: the projection of the screen on the wall
		private int R_ScaleFromGlobalAngle(uint visangle)
		{
			unchecked
			{
				uint anglea = ANG90 + (visangle - viewangle);
				uint angleb = ANG90 + (visangle - rw_normalangle);

				// both sines are always positive
				int sinea = VpoTables.finesine[anglea >> ANGLETOFINESHIFT];
				int sineb = VpoTables.finesine[angleb >> ANGLETOFINESHIFT];
				int num = FixedMul(projection, sineb) << 0;
				int den = FixedMul(rw_distance, sinea);

				int scale;
				if(den > num >> 16)
				{
					scale = FixedDiv(num, den);

					if(scale > 64 * FRACUNIT) scale = 64 * FRACUNIT;
					else if(scale < 256) scale = 256;
				}
				else
				{
					scale = 64 * FRACUNIT;
				}
				return scale;
			}
		}

		private Subsector R_PointInSubsector(int x, int y)
		{
			// single subsector is a special case
			if(nodes.Length == 0) return subsectors[0];

			int nodenum = nodes.Length - 1;
			while((nodenum & NF_SUBSECTOR) == 0)
			{
				Node node = nodes[nodenum];
				int side = R_PointOnSide(x, y, node);
				nodenum = node.children[side];
			}
			return subsectors[nodenum & ~NF_SUBSECTOR];
		}

		private void R_RenderView(int x, int y, int z, uint angle)
		{
			// R_SetupFrame
			viewx = x;
			viewy = y;
			viewz = z;
			viewangle = angle;
			sscount = 0;

			R_ClearClipSegs();
			total_drawsegs = 0;      // R_ClearDrawSegs
			R_ClearPlanes();

			R_RenderBSPNode(nodes.Length - 1);
		}

		#endregion

		#region ================== Renderer: planes

		private void R_ClearPlanes()
		{
			// opening / clipping determination
			for(int i = 0; i < viewwidth; i++)
			{
				floorclip[i] = (short)viewheight;
				ceilingclip[i] = -1;
			}

			total_visplanes = 0;
			total_openings = 0;
			lastvisplane = 0;
			lastopening = 0;
		}

		private Visplane R_FindPlane(int height, int picnum, int lightlevel)
		{
			if(picnum == skyflatnum)
			{
				height = 0;          // all skys map together
				lightlevel = 0;
			}

			int check;
			for(check = 0; check < lastvisplane; check++)
			{
				if(height == visplanes[check].height && picnum == visplanes[check].picnum && lightlevel == visplanes[check].lightlevel) break;
			}

			if(check < lastvisplane) return visplanes[check];

			if(total_visplanes >= MAXVISPLANES) throw new OverflowException();
			total_visplanes++;

			Visplane plane = visplanes[lastvisplane++];
			plane.height = height;
			plane.picnum = picnum;
			plane.lightlevel = lightlevel;
			plane.minx = SCREENWIDTH;
			plane.maxx = -1;

			for(int i = 0; i < plane.top.Length; i++) plane.top[i] = 0xff;
			return plane;
		}

		private Visplane R_CheckPlane(Visplane pl, int start, int stop)
		{
			int intrl, intrh, unionl, unionh;

			if(start < pl.minx) { intrl = pl.minx; unionl = start; }
			else { unionl = pl.minx; intrl = start; }

			if(stop > pl.maxx) { intrh = pl.maxx; unionh = stop; }
			else { unionh = pl.maxx; intrh = stop; }

			int x;
			for(x = intrl; x <= intrh; x++)
				if(pl.top[x] != 0xff) break;

			if(x > intrh)
			{
				pl.minx = unionl;
				pl.maxx = unionh;

				// use the same one
				return pl;
			}

			// make a new visplane
			Visplane plane = visplanes[lastvisplane];
			plane.height = pl.height;
			plane.picnum = pl.picnum;
			plane.lightlevel = pl.lightlevel;

			if(total_visplanes >= MAXVISPLANES) throw new OverflowException();
			total_visplanes++;

			pl = visplanes[lastvisplane++];
			pl.minx = start;
			pl.maxx = stop;

			for(int i = 0; i < pl.top.Length; i++) pl.top[i] = 0xff;
			return pl;
		}

		#endregion

		#region ================== Renderer: BSP

		private void R_ClipSolidWallSegment(int first, int last)
		{
			int next;
			int start;

			// Find the first range that touches the range (adjacent pixels are touching).
			start = 0;
			while(solidsegs[start].last < first - 1) start++;

			if(first < solidsegs[start].first)
			{
				if(last < solidsegs[start].first - 1)
				{
					// Post is entirely visible (above start), so insert a new clippost.
					R_StoreWallRange(first, last);
					next = newend;
					newend++;

					max_solidsegs = Math.Max(max_solidsegs, newend);
					if(max_solidsegs >= MAXSOLIDSEGS) throw new OverflowException();

					while(next != start)
					{
						solidsegs[next] = solidsegs[next - 1];
						next--;
					}
					solidsegs[next].first = first;
					solidsegs[next].last = last;
					return;
				}

				// There is a fragment above *start.
				R_StoreWallRange(first, solidsegs[start].first - 1);
				// Now adjust the clip size.
				solidsegs[start].first = first;
			}

			// Bottom contained in start?
			if(last <= solidsegs[start].last) return;

			next = start;
			while(last >= solidsegs[next + 1].first - 1)
			{
				// There is a fragment between two posts.
				R_StoreWallRange(solidsegs[next].last + 1, solidsegs[next + 1].first - 1);
				next++;

				if(last <= solidsegs[next].last)
				{
					// Bottom is contained in next. Adjust the clip size.
					solidsegs[start].last = solidsegs[next].last;
					goto crunch;
				}
			}

			// There is a fragment after *next.
			R_StoreWallRange(solidsegs[next].last + 1, last);
			// Adjust the clip size.
			solidsegs[start].last = last;

			// Remove start+1 to next from the clip list, because start now covers their area.
			crunch:
			if(next == start)
			{
				// Post just extended past the bottom of one post.
				return;
			}

			while(next++ != newend)
			{
				// Remove a post.
				solidsegs[++start] = solidsegs[next];
			}

			newend = start + 1;

			max_solidsegs = Math.Max(max_solidsegs, newend);
			if(max_solidsegs >= MAXSOLIDSEGS) throw new OverflowException();
		}

		// Clips the given range of columns, but does not include it in the clip list. Does handle windows, e.g. LineDefs with upper and lower texture.
		private void R_ClipPassWallSegment(int first, int last)
		{
			// Find the first range that touches the range (adjacent pixels are touching).
			int start = 0;
			while(solidsegs[start].last < first - 1) start++;

			if(first < solidsegs[start].first)
			{
				if(last < solidsegs[start].first - 1)
				{
					// Post is entirely visible (above start).
					R_StoreWallRange(first, last);
					return;
				}

				// There is a fragment above *start.
				R_StoreWallRange(first, solidsegs[start].first - 1);
			}

			// Bottom contained in start?
			if(last <= solidsegs[start].last) return;

			while(last >= solidsegs[start + 1].first - 1)
			{
				// There is a fragment between two posts.
				R_StoreWallRange(solidsegs[start].last + 1, solidsegs[start + 1].first - 1);
				start++;

				if(last <= solidsegs[start].last) return;
			}

			// There is a fragment after *next.
			R_StoreWallRange(solidsegs[start].last + 1, last);
		}

		private void R_ClearClipSegs()
		{
			solidsegs[0].first = -0x7fffffff;
			solidsegs[0].last = -1;
			solidsegs[1].first = viewwidth;
			solidsegs[1].last = 0x7fffffff;
			newend = 2;
			max_solidsegs = 2;
		}

		// Clips the given segment and adds any visible pieces to the line list.
		private void R_AddLine(Seg line)
		{
			curline = line;

			// OPTIMIZE: quickly reject orthogonal back sides.
			uint angle1 = R_PointToAngle(line.v1.x, line.v1.y);
			uint angle2 = R_PointToAngle(line.v2.x, line.v2.y);

			unchecked
			{
				// Clip to view edges.
				uint span = angle1 - angle2;

				// Back side? I.e. backface culling?
				if(span >= ANG180) return;

				// Global angle needed by segcalc.
				rw_angle1 = (int)angle1;
				angle1 -= viewangle;
				angle2 -= viewangle;

				uint tspan = angle1 + clipangle;
				if(tspan > 2 * clipangle)
				{
					tspan -= 2 * clipangle;

					// Totally off the left edge?
					if(tspan >= span) return;

					angle1 = clipangle;
				}
				tspan = clipangle - angle2;
				if(tspan > 2 * clipangle)
				{
					tspan -= 2 * clipangle;

					// Totally off the left edge?
					if(tspan >= span) return;

					angle2 = 0u - clipangle;
				}

				// The seg is in the view range, but not necessarily visible.
				angle1 = (angle1 + ANG90) >> ANGLETOFINESHIFT;
				angle2 = (angle2 + ANG90) >> ANGLETOFINESHIFT;
				int x1 = viewangletox[angle1];
				int x2 = viewangletox[angle2];

				// Does not cross a pixel?
				if(x1 == x2) return;

				backsector = line.backsector;

				// Single sided line?
				if(backsector == null) goto clipsolid;

				// Closed door.
				if(backsector.ceilingheight <= frontsector.floorheight || backsector.floorheight >= frontsector.ceilingheight) goto clipsolid;

				// Window.
				if(backsector.ceilingheight != frontsector.ceilingheight || backsector.floorheight != frontsector.floorheight) goto clippass;

				// Reject empty lines used for triggers and special events. Identical floor and ceiling on both sides, identical light levels
				// on both sides, and no middle texture.
				if(backsector.ceilingpic == frontsector.ceilingpic && backsector.floorpic == frontsector.floorpic
					&& backsector.lightlevel == frontsector.lightlevel && curline.sidedef.midtexture == 0)
				{
					return;
				}

				clippass:
				R_ClipPassWallSegment(x1, x2 - 1);
				return;

				clipsolid:
				R_ClipSolidWallSegment(x1, x2 - 1);
			}
		}

		// Checks BSP node/subtree bounding box. Returns true if some part of the bbox might be visible.
		private bool R_CheckBBox(int[] bspcoord)
		{
			int boxx, boxy;

			// Find the corners of the box that define the edges from current viewpoint.
			if(viewx <= bspcoord[BOXLEFT]) boxx = 0;
			else if(viewx < bspcoord[BOXRIGHT]) boxx = 1;
			else boxx = 2;

			if(viewy >= bspcoord[BOXTOP]) boxy = 0;
			else if(viewy > bspcoord[BOXBOTTOM]) boxy = 1;
			else boxy = 2;

			int boxpos = (boxy << 2) + boxx;
			if(boxpos == 5) return true;

			int x1 = bspcoord[checkcoord[boxpos][0]];
			int y1 = bspcoord[checkcoord[boxpos][1]];
			int x2 = bspcoord[checkcoord[boxpos][2]];
			int y2 = bspcoord[checkcoord[boxpos][3]];

			unchecked
			{
				// check clip list for an open space
				uint angle1 = R_PointToAngle(x1, y1) - viewangle;
				uint angle2 = R_PointToAngle(x2, y2) - viewangle;

				uint span = angle1 - angle2;

				// Sitting on a line?
				if(span >= ANG180) return true;

				uint tspan = angle1 + clipangle;
				if(tspan > 2 * clipangle)
				{
					tspan -= 2 * clipangle;

					// Totally off the left edge?
					if(tspan >= span) return false;

					angle1 = clipangle;
				}
				tspan = clipangle - angle2;
				if(tspan > 2 * clipangle)
				{
					tspan -= 2 * clipangle;

					// Totally off the left edge?
					if(tspan >= span) return false;

					angle2 = 0u - clipangle;
				}

				// Find the first clippost that touches the source post (adjacent pixels are touching).
				angle1 = (angle1 + ANG90) >> ANGLETOFINESHIFT;
				angle2 = (angle2 + ANG90) >> ANGLETOFINESHIFT;
				int sx1 = viewangletox[angle1];
				int sx2 = viewangletox[angle2];

				// Does not cross a pixel.
				if(sx1 == sx2) return false;
				sx2--;

				int start = 0;
				while(solidsegs[start].last < sx2) start++;

				if(sx1 >= solidsegs[start].first && sx2 <= solidsegs[start].last)
				{
					// The clippost contains the new span.
					return false;
				}
			}

			return true;
		}

		// Determine floor/ceiling planes. Add sprites of things in sector. Draw one or more line segments.
		private void R_Subsector(int num)
		{
			if(num >= subsectors.Length) throw new InvalidMapDataException("R_Subsector: ss " + num + " with numss = " + subsectors.Length);

			sscount++;
			Subsector sub = subsectors[num];
			frontsector = sub.sector;
			int count = sub.numlines;
			int line = sub.firstline;

			if(frontsector.floorheight < viewz)
				floorplane = R_FindPlane(frontsector.floorheight, frontsector.floorpic, frontsector.lightlevel);
			else
				floorplane = null;

			if(frontsector.ceilingheight > viewz || frontsector.ceilingpic == skyflatnum)
				ceilingplane = R_FindPlane(frontsector.ceilingheight, frontsector.ceilingpic, frontsector.lightlevel);
			else
				ceilingplane = null;

			while(count-- > 0)
			{
				R_AddLine(segs[line]);
				line++;
			}
		}

		// Renders all subsectors below a given node, traversing subtree recursively. Just call with BSP root.
		private void R_RenderBSPNode(int bspnum)
		{
			// Found a subsector?
			if((bspnum & NF_SUBSECTOR) != 0)
			{
				if(bspnum == -1) R_Subsector(0);
				else R_Subsector(bspnum & (~NF_SUBSECTOR));
				return;
			}

			Node bsp = nodes[bspnum];

			// Decide which side the view point is on.
			int side = R_PointOnSide(viewx, viewy, bsp);

			// Recursively divide front space.
			R_RenderBSPNode(bsp.children[side]);

			// Possibly divide back space.
			if(R_CheckBBox(bsp.bbox[side ^ 1])) R_RenderBSPNode(bsp.children[side ^ 1]);
		}

		#endregion

		#region ================== Renderer: segs

		// Draws zero, one, or two textures (and possibly a masked texture) for walls. Can draw or mark the starting pixel of floor and ceiling textures.
		private void R_RenderSegLoop()
		{
			for(; rw_x < rw_stopx; rw_x++)
			{
				// mark floor / ceiling areas
				int yl = (topfrac + HEIGHTUNIT - 1) >> HEIGHTBITS;

				// no space above wall?
				if(yl < ceilingclip[rw_x] + 1) yl = ceilingclip[rw_x] + 1;

				if(markceiling)
				{
					int top = ceilingclip[rw_x] + 1;
					int bottom = yl - 1;

					if(bottom >= floorclip[rw_x]) bottom = floorclip[rw_x] - 1;

					if(top <= bottom)
					{
						ceilingplane.top[rw_x] = (byte)top;
						ceilingplane.bottom[rw_x] = (byte)bottom;
					}
				}

				int yh = bottomfrac >> HEIGHTBITS;

				if(yh >= floorclip[rw_x]) yh = floorclip[rw_x] - 1;

				if(markfloor)
				{
					int top = yh + 1;
					int bottom = floorclip[rw_x] - 1;
					if(top <= ceilingclip[rw_x]) top = ceilingclip[rw_x] + 1;
					if(top <= bottom)
					{
						floorplane.top[rw_x] = (byte)top;
						floorplane.bottom[rw_x] = (byte)bottom;
					}
				}

				// texturecolumn and lighting are independent of wall tiers
				int texturecolumn;
				if(segtextured)
				{
					// calculate texture offset
					uint angle = unchecked(rw_centerangle + xtoviewangle[rw_x]) >> ANGLETOFINESHIFT;
					texturecolumn = rw_offset - FixedMul(VpoTables.finetangent[angle & (FINEANGLES / 2 - 1)], rw_distance);
					texturecolumn >>= FRACBITS;
				}
				else
				{
					texturecolumn = 0;
				}

				// draw the wall tiers
				if(midtexture != 0)
				{
					// single sided line
					ceilingclip[rw_x] = (short)viewheight;
					floorclip[rw_x] = -1;
				}
				else
				{
					// two sided line
					if(toptexture != 0)
					{
						// top wall
						int mid = pixhigh >> HEIGHTBITS;
						pixhigh += pixhighstep;

						if(mid >= floorclip[rw_x]) mid = floorclip[rw_x] - 1;

						if(mid >= yl) ceilingclip[rw_x] = (short)mid;
						else ceilingclip[rw_x] = (short)(yl - 1);
					}
					else
					{
						// no top wall
						if(markceiling) ceilingclip[rw_x] = (short)(yl - 1);
					}

					if(bottomtexture != 0)
					{
						// bottom wall
						int mid = (pixlow + HEIGHTUNIT - 1) >> HEIGHTBITS;
						pixlow += pixlowstep;

						// no space above wall?
						if(mid <= ceilingclip[rw_x]) mid = ceilingclip[rw_x] + 1;

						if(mid <= yh) floorclip[rw_x] = (short)mid;
						else floorclip[rw_x] = (short)(yh + 1);
					}
					else
					{
						// no bottom wall
						if(markfloor) floorclip[rw_x] = (short)(yh + 1);
					}

					if(maskedtexture)
					{
						// save texturecol for backdrawing of masked mid texture
						openings[maskedtexturecol + rw_x] = (short)texturecolumn;
					}
				}

				rw_scale += rw_scalestep;
				topfrac += topstep;
				bottomfrac += bottomstep;
			}
		}

		// Called from R_AddLine when a (part of a) seg is visible: stores what it clips and marks the planes it touches.
		private void R_StoreWallRange(int start, int stop)
		{
			// don't overflow and crash
			total_drawsegs++;
			if(total_drawsegs >= MAXDRAWSEGS) throw new OverflowException();

			if(start >= viewwidth || start > stop) throw new InvalidMapDataException("Bad R_RenderWallRange: " + start + " to " + stop);

			sidedef = curline.sidedef;
			linedef = curline.linedef;

			// mark the segment as visible for auto map
			linedef.flags |= ML_MAPPED;

			// calculate rw_distance for scale calculation
			unchecked
			{
				rw_normalangle = curline.angle + ANG90;
				uint offsetangle = (uint)Abs((int)rw_normalangle - rw_angle1);

				if(offsetangle > ANG90) offsetangle = ANG90;

				uint distangle = ANG90 - offsetangle;
				int hyp = R_PointToDist(curline.v1.x, curline.v1.y);
				int sineval = VpoTables.finesine[distangle >> ANGLETOFINESHIFT];
				rw_distance = FixedMul(hyp, sineval);

				rw_x = start;
				rw_stopx = stop + 1;

				// calculate scale at both ends and step
				rw_scale = R_ScaleFromGlobalAngle(viewangle + xtoviewangle[start]);

				if(stop > start)
				{
					int scale2 = R_ScaleFromGlobalAngle(viewangle + xtoviewangle[stop]);
					rw_scalestep = (scale2 - rw_scale) / (stop - start);
				}

				// calculate texture boundaries and decide if floor / ceiling marks are needed
				worldtop = frontsector.ceilingheight - viewz;
				worldbottom = frontsector.floorheight - viewz;

				midtexture = toptexture = bottomtexture = 0;
				maskedtexture = false;

				// what the seg tells about the things behind it (only whether it has them matters here)
				int silhouette;
				bool sprtopclip, sprbottomclip;

				if(backsector == null)
				{
					// single sided line
					midtexture = sidedef.midtexture;

					// a single sided line is terminal, so it must mark ends
					markfloor = markceiling = true;
					if((linedef.flags & ML_DONTPEGBOTTOM) != 0)
					{
						int vtop = frontsector.floorheight + 128;    // (the height of a texture is not known here)
						rw_midtexturemid = vtop - viewz;
					}
					else
					{
						// top of texture at top
						rw_midtexturemid = worldtop;
					}
					rw_midtexturemid += sidedef.rowoffset;

					silhouette = SIL_BOTH;
					sprtopclip = sprbottomclip = true;
				}
				else
				{
					// two sided line
					sprtopclip = sprbottomclip = false;
					silhouette = 0;

					if(frontsector.floorheight > backsector.floorheight) silhouette = SIL_BOTTOM;
					else if(backsector.floorheight > viewz) silhouette = SIL_BOTTOM;

					if(frontsector.ceilingheight < backsector.ceilingheight) silhouette |= SIL_TOP;
					else if(backsector.ceilingheight < viewz) silhouette |= SIL_TOP;

					if(backsector.ceilingheight <= frontsector.floorheight)
					{
						sprbottomclip = true;
						silhouette |= SIL_BOTTOM;
					}

					if(backsector.floorheight >= frontsector.ceilingheight)
					{
						sprtopclip = true;
						silhouette |= SIL_TOP;
					}

					worldhigh = backsector.ceilingheight - viewz;
					worldlow = backsector.floorheight - viewz;

					// hack to allow height changes in outdoor areas
					if(frontsector.ceilingpic == skyflatnum && backsector.ceilingpic == skyflatnum) worldtop = worldhigh;

					if(worldlow != worldbottom || backsector.floorpic != frontsector.floorpic || backsector.lightlevel != frontsector.lightlevel)
						markfloor = true;
					else
						markfloor = false;      // same plane on both sides

					if(worldhigh != worldtop || backsector.ceilingpic != frontsector.ceilingpic || backsector.lightlevel != frontsector.lightlevel)
						markceiling = true;
					else
						markceiling = false;    // same plane on both sides

					if(backsector.ceilingheight <= frontsector.floorheight || backsector.floorheight >= frontsector.ceilingheight)
					{
						// closed door
						markceiling = markfloor = true;
					}

					if(worldhigh < worldtop)
					{
						// top texture
						toptexture = sidedef.toptexture;
						if((linedef.flags & ML_DONTPEGTOP) != 0)
						{
							// top of texture at top
							rw_toptexturemid = worldtop;
						}
						else
						{
							int vtop = backsector.ceilingheight + 128;
							rw_toptexturemid = vtop - viewz;
						}
					}

					if(worldlow > worldbottom)
					{
						// bottom texture
						bottomtexture = sidedef.bottomtexture;

						if((linedef.flags & ML_DONTPEGBOTTOM) != 0)
						{
							// bottom of texture at bottom, top of texture at top
							rw_bottomtexturemid = worldtop;
						}
						else
						{
							// top of texture at top
							rw_bottomtexturemid = worldlow;
						}
					}
					rw_toptexturemid += sidedef.rowoffset;
					rw_bottomtexturemid += sidedef.rowoffset;

					// allocate space for masked texture tables
					if(sidedef.midtexture != 0)
					{
						// masked midtexture
						maskedtexture = true;
						maskedtexturecol = lastopening - rw_x;
						lastopening += rw_stopx - rw_x;

						total_openings += (rw_stopx - rw_x);
						if(total_openings >= MAXOPENINGS) throw new OverflowException();
					}
				}

				// calculate rw_offset (only needed for textured lines)
				segtextured = (midtexture | toptexture | bottomtexture) != 0 || maskedtexture;

				if(segtextured)
				{
					offsetangle = rw_normalangle - (uint)rw_angle1;

					if(offsetangle > ANG180) offsetangle = 0u - offsetangle;

					if(offsetangle > ANG90) offsetangle = ANG90;

					sineval = VpoTables.finesine[offsetangle >> ANGLETOFINESHIFT];
					rw_offset = FixedMul(hyp, sineval);

					if(rw_normalangle - (uint)rw_angle1 < ANG180) rw_offset = -rw_offset;

					rw_offset += sidedef.textureoffset + curline.offset;
					rw_centerangle = ANG90 + viewangle - rw_normalangle;
				}

				// if a floor / ceiling plane is on the wrong side of the view plane, it is definitely invisible and doesn't need to be marked.
				if(frontsector.floorheight >= viewz)
				{
					// above view plane
					markfloor = false;
				}

				if(frontsector.ceilingheight <= viewz && frontsector.ceilingpic != skyflatnum)
				{
					// below view plane
					markceiling = false;
				}

				// calculate incremental stepping values for texture edges
				worldtop >>= 4;
				worldbottom >>= 4;

				topstep = -FixedMul(rw_scalestep, worldtop);
				topfrac = (centeryfrac >> 4) - FixedMul(worldtop, rw_scale);

				bottomstep = -FixedMul(rw_scalestep, worldbottom);
				bottomfrac = (centeryfrac >> 4) - FixedMul(worldbottom, rw_scale);

				if(backsector != null)
				{
					worldhigh >>= 4;
					worldlow >>= 4;

					if(worldhigh < worldtop)
					{
						pixhigh = (centeryfrac >> 4) - FixedMul(worldhigh, rw_scale);
						pixhighstep = -FixedMul(rw_scalestep, worldhigh);
					}

					if(worldlow > worldbottom)
					{
						pixlow = (centeryfrac >> 4) - FixedMul(worldlow, rw_scale);
						pixlowstep = -FixedMul(rw_scalestep, worldlow);
					}
				}

				// render it
				if(floorplane == null) markfloor = false;
				if(ceilingplane == null) markceiling = false;

				if(markceiling) ceilingplane = R_CheckPlane(ceilingplane, rw_x, rw_stopx - 1);
				if(markfloor) floorplane = R_CheckPlane(floorplane, rw_x, rw_stopx - 1);

				R_RenderSegLoop();

				// save sprite clipping info
				if(((silhouette & SIL_TOP) != 0 || maskedtexture) && !sprtopclip)
				{
					Array.Copy(ceilingclip, start, openings, lastopening, rw_stopx - start);
					lastopening += rw_stopx - start;

					total_openings += (rw_stopx - start);
					if(total_openings >= MAXOPENINGS) throw new OverflowException();
				}

				if(((silhouette & SIL_BOTTOM) != 0 || maskedtexture) && !sprbottomclip)
				{
					Array.Copy(floorclip, start, openings, lastopening, rw_stopx - start);
					lastopening += rw_stopx - start;

					total_openings += (rw_stopx - start);
					if(total_openings >= MAXOPENINGS) throw new OverflowException();
				}
			}
		}

		#endregion

		#region ================== Testing a spot

		/// <summary>
		/// Renders the view from a spot (as Doom would) and updates the counters, which then hold the maximum of what they held and what the view needed.
		/// dz is the height above the floor (or the offset from the ceiling when negative); the angle is in degrees (0 is east, 90 is north).
		/// Returns RESULT_OK, or RESULT_BAD_Z, RESULT_IN_VOID or RESULT_OVERFLOW.
		/// </summary>
		public int TestSpot(int x, int y, int dz, int angle, ref int num_visplanes, ref int num_drawsegs, ref int num_openings, ref int num_solidsegs)
		{
			// the center of the map unit
			int rx = (x << FRACBITS) + (FRACUNIT / 2);
			int ry = (y << FRACBITS) + (FRACUNIT / 2);

			// outside the map is in the void
			if(rx < Map_bbox[BOXLEFT] || rx > Map_bbox[BOXRIGHT] || ry < Map_bbox[BOXBOTTOM] || ry > Map_bbox[BOXTOP]) return RESULT_IN_VOID;

			Sector sec;
			if(x == last_x && y == last_y)
			{
				sec = last_sector;
			}
			else
			{
				sec = X_SectorForPoint(rx, ry);
				last_x = x;
				last_y = y;
				last_sector = sec;
			}

			if(sec == null) return RESULT_IN_VOID;

			int rz;
			if(dz < 0) rz = sec.ceilingheight + (dz << FRACBITS);
			else rz = sec.floorheight + (dz << FRACBITS);

			// the view must be inside the sector
			if(rz <= sec.floorheight || rz >= sec.ceilingheight) return RESULT_BAD_Z;

			if(angle == 360) angle = 0;
			int ang2 = FixedDiv(angle << FRACBITS, 360 << FRACBITS);
			uint r_ang = unchecked((uint)(ang2 << 16));

			int result = RESULT_OK;
			try
			{
				R_RenderView(rx, ry, rz, r_ang);
			}
			catch(OverflowException)
			{
				result = RESULT_OVERFLOW;
			}

			num_visplanes = Math.Max(num_visplanes, total_visplanes);
			num_drawsegs = Math.Max(num_drawsegs, total_drawsegs);
			num_openings = Math.Max(num_openings, total_openings);
			num_solidsegs = Math.Max(num_solidsegs, max_solidsegs);
			return result;
		}

		#endregion
	}
}
