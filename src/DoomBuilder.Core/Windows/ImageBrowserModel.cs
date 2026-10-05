#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.IO;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>A texture set or a folder inside a resource, as shown in the left tree of the texture/flat browser.</summary>
	internal sealed class ImageBrowserNode
	{
		public string Name;          // Unique key (used to remember the last set)
		public string Text;          // Caption, with the image count
		public string FolderName;    // Caption of the folder tile
		public IFilledTextureSet Set;
		public ImageBrowserNode Parent;
		public readonly List<ImageBrowserNode> Children = new List<ImageBrowserNode>();
		public override string ToString() { return Text; }
	}

	/// <summary>What the list of the browser shows: folder tiles and images, in this order.</summary>
	internal sealed class ImageBrowserListing
	{
		/// <summary>"Browse up" target (null at root level, where there is nothing above), as a caption.</summary>
		public string UpCaption;
		public readonly List<ImageBrowserNode> Folders = new List<ImageBrowserNode>();
		/// <summary>The images used in the map, when asked to list them first (they are repeated in <see cref="Images"/>).</summary>
		public readonly List<ImageData> Used = new List<ImageData>();
		public readonly List<ImageData> Images = new List<ImageData>();
	}

	/// <summary>
	/// The logic of UDB's texture/flat browser (TextureBrowserForm + ImageBrowserControl) without any UI: the tree of texture
	/// sets and resource folders, the list for the selected node, filtering by name and size, and the initial selection.
	/// </summary>
	internal sealed class ImageBrowserModel
	{
		public const string AllImages = "[All]";

		private readonly bool browseflats;
		private readonly List<ImageBrowserNode> roots = new List<ImageBrowserNode>();

		public ImageBrowserModel(string selecttexture, bool browseflats)
		{
			this.browseflats = browseflats;
			long longname = Lump.MakeLongName(selecttexture ?? "");
			// Not a valid flat: nothing to select
			if(browseflats && General.Map.Data.GetFlatImage(longname) == General.Map.Data.UnknownImage)
				longname = Lump.MakeLongName("");
			SelectLongName = browseflats ? General.Map.Data.GetFullLongFlatName(longname) : General.Map.Data.GetFullLongTextureName(longname);

			General.Map.Data.UpdateUsedTextures();

			// Normal texture sets
			foreach(IFilledTextureSet ts in General.Map.Data.TextureSets)
			{
				int count = ImagesOf(ts).Count;
				if((count == 0 && !General.Map.Config.MixTexturesFlats) || (ts.Flats.Count == 0 && ts.Textures.Count == 0)) continue;
				roots.Add(new ImageBrowserNode { Name = ts.Name, Text = ts.Name + " [" + count + "]", FolderName = ts.Name, Set = ts });
			}

			// Container-specific texture sets
			foreach(ResourceTextureSet ts in General.Map.Data.ResourceTextureSets)
			{
				if((ImagesOf(ts).Count == 0 && !General.Map.Config.MixTexturesFlats) || (ts.Flats.Count == 0 && ts.Textures.Count == 0)) continue;
				var node = new ImageBrowserNode { Name = ts.Name, Text = ts.Name, FolderName = ts.Name, Set = ts };
				CreateNodes(node);
				roots.Add(node);
			}

			// "All" texture set, always last
			AllTextureSet all = browseflats ? General.Map.Data.FlatTextureSet : General.Map.Data.WallTextureSet;
			roots.Add(new ImageBrowserNode { Name = all.Name, Text = all.Name + " [" + ImagesOf(all).Count + "]", FolderName = all.Name, Set = all });

			Selected = FindInitialNode(selecttexture);
		}

		#region ================== Properties

		public IList<ImageBrowserNode> Roots { get { return roots; } }

		/// <summary>The set whose images are listed; null at root level (then the list shows the sets themselves).</summary>
		public ImageBrowserNode Selected { get; set; }

		/// <summary>Full long name of the image to select when the browser opens.</summary>
		public long SelectLongName { get; private set; }

		public bool BrowseFlats { get { return browseflats; } }

		#endregion

		#region ================== Tree

		private ICollection<ImageData> ImagesOf(IFilledTextureSet set) { return browseflats ? set.Flats : set.Textures; }

		private static int SortImageData(ImageData a, ImageData b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); }

		private static int SortNodes(ImageBrowserNode a, ImageBrowserNode b) { return string.Compare(a.Text, b.Text, StringComparison.InvariantCultureIgnoreCase); }

		// Builds the folder nodes of a resource out of the virtual names (folders inside the archive) of its images
		private void CreateNodes(ImageBrowserNode root)
		{
			ResourceTextureSet set = root.Set as ResourceTextureSet;
			if(set == null) { General.ErrorLogger.Add(ErrorType.Error, "Resource " + root.Name + " doesn't have TextureSet!"); return; }

			ImageData[] images = ImagesOf(set).ToArray();
			Array.Sort(images, SortImageData);

			var rootimages = new List<ImageData>();
			char[] separator = { Path.AltDirectorySeparatorChar };
			foreach(ImageData image in images)
			{
				string[] parts = image.VirtualName.Split(separator, StringSplitOptions.RemoveEmptyEntries);
				if(parts.Length == 1) { rootimages.Add(image); continue; }

				ImageBrowserNode cur = root;
				string category = set.Name;
				for(int i = 0; i < parts.Length - 1; i++)
				{
					category += Path.DirectorySeparatorChar + parts[i];
					ImageBrowserNode next = cur.Children.FirstOrDefault(n => n.Name == category);
					if(next == null)
					{
						next = new ImageBrowserNode { Name = category, Text = parts[i], FolderName = parts[i], Parent = cur, Set = new ResourceTextureSet(category, set.Location) };
						cur.Children.Add(next);
					}
					cur = next;

					if(i == parts.Length - 2)
					{
						var folderset = (ResourceTextureSet)cur.Set;
						if(image.TextureNamespace == TextureNamespace.FLAT) folderset.AddFlat(image); else folderset.AddTexture(image);
					}
				}
			}

			// Shift the tree up when only a single child folder was added
			if(root.Children.Count == 1 && root.Children[0].Children.Count > 0)
			{
				List<ImageBrowserNode> grandchildren = root.Children[0].Children;
				root.Children.Clear();
				root.Children.AddRange(grandchildren);
				foreach(ImageBrowserNode n in grandchildren) n.Parent = root;
			}

			// "All set images" entry
			if(General.Map.Config.MixTexturesFlats && root.Children.Count > 1)
				root.Children.Add(new ImageBrowserNode { Name = AllImages, Text = AllImages, FolderName = AllImages, Set = set, Parent = root });

			root.Children.Sort(SortNodes);

			// The images directly in the resource
			var rootset = new ResourceTextureSet(set.Name, set.Location);
			foreach(ImageData data in rootimages) { if(browseflats) rootset.AddFlat(data); else rootset.AddTexture(data); }
			if(General.Map.Config.MixTexturesFlats) rootset.MixTexturesAndFlats();
			root.Set = rootset;

			int rootcount = ImagesOf(rootset).Count;
			if(rootcount > 0) root.Text += " [" + rootcount + "]";
			foreach(ImageBrowserNode n in root.Children) SetItemsCount(n);
		}

		private void SetItemsCount(ImageBrowserNode node)
		{
			var ts = (ResourceTextureSet)node.Set;
			if(node.Parent != null && General.Map.Config.MixTexturesFlats)
			{
				ts.MixTexturesAndFlats();
				if(ts.Textures.Count > 0) node.Text += " [" + ts.Textures.Count + "]";
			}
			else
			{
				int count = ImagesOf(ts).Count;
				if(count > 0) node.Text += " [" + count + "]";
			}
			foreach(ImageBrowserNode child in node.Children) SetItemsCount(child);
		}

		private static ImageBrowserNode FindByName(IEnumerable<ImageBrowserNode> nodes, string name)
		{
			foreach(ImageBrowserNode n in nodes)
			{
				if(n.Name == name) return n;
				ImageBrowserNode match = FindByName(n.Children, name);
				if(match != null) return match;
			}
			return null;
		}

		private ImageBrowserNode FindByImage(ImageBrowserNode node, long longname)
		{
			if(node.Name == AllImages) return null;
			foreach(ImageBrowserNode n in node.Children)
			{
				ImageBrowserNode match = FindByImage(n, longname);
				if(match != null) return match;
			}
			return ImagesOf(node.Set).Any(img => img.LongName == longname) ? node : null;
		}

		/// <summary>The node to open on: the last used set when it holds the image, else any set that does, else the last used set or "All".</summary>
		private ImageBrowserNode FindInitialNode(string selecttexture)
		{
			ImageBrowserNode last = roots[roots.Count - 1];
			if(!General.Settings.LocateTextureGroup) return last;

			string previous = General.Settings.ReadSetting(SettingPath + ".textureset", "");
			ImageBrowserNode match = string.IsNullOrEmpty(previous) ? last : FindByName(roots, previous);
			ImageBrowserNode found = null;
			if(match != null && ImagesOf(match.Set).Any(img => img.LongName == SelectLongName)) found = match;

			if(found == null && selecttexture != "-")
				foreach(ImageBrowserNode n in roots)
				{
					found = FindByImage(n, SelectLongName);
					if(found != null) break;
				}

			return found ?? match ?? last;
		}

		/// <summary>Where the browser keeps its settings (the same keys as UDB).</summary>
		public string SettingPath { get { return "windows.texturebrowserform"; } }

		/// <summary>Remembers the chosen set for the next time (only when the user accepted).</summary>
		public void RememberSelection()
		{
			if(Selected != null) General.Settings.WriteSetting(SettingPath + ".textureset", Selected.Name);
		}

		#endregion

		#region ================== Listing

		/// <summary>Moves one level up the tree ("browse up" tile); returns false when already at root.</summary>
		public bool Up()
		{
			if(Selected == null) return false;
			Selected = Selected.Parent;
			return true;
		}

		/// <summary>Opens a folder tile of the current level, by its caption.</summary>
		public bool Open(string foldername)
		{
			IEnumerable<ImageBrowserNode> level = Selected == null ? roots : (IEnumerable<ImageBrowserNode>)Selected.Children;
			ImageBrowserNode child = level.FirstOrDefault(n => n.FolderName == foldername);
			if(child == null) return false;
			Selected = child;
			return true;
		}

		/// <summary>
		/// The list for the selected node. <paramref name="name"/> filters folders and images by name; <paramref name="width"/> and
		/// <paramref name="height"/> (-1 for any) filter images by size, as UDB does (exactly that size, once the image is loaded).
		/// </summary>
		public ImageBrowserListing List(string name, int width, int height, bool usedfirst)
		{
			var result = new ImageBrowserListing();
			string filter = (name ?? "").ToUpperInvariant();

			if(Selected == null)
			{
				result.Folders.AddRange(roots.Where(n => n.FolderName.ToUpperInvariant().Contains(filter)));
				return result;
			}

			result.UpCaption = Selected.Parent != null ? Selected.Parent.FolderName : "All Texture Sets";
			result.Folders.AddRange(Selected.Children.Where(n => n.FolderName.ToUpperInvariant().Contains(filter)));

			// Names are unique: the first occurrence wins (the images matching the wall/floor come first)
			var seen = new HashSet<string>();
			var images = new List<ImageData>();
			foreach(ImageData img in ImagesOf(Selected.Set))
				if(seen.Add(DisplayName(img)) && Matches(img, filter, width, height)) images.Add(img);
			images.Sort((a, b) => string.Compare(DisplayName(a), DisplayName(b), StringComparison.OrdinalIgnoreCase));

			result.Images.AddRange(images);
			if(usedfirst) result.Used.AddRange(images.Where(i => i.UsedInMap));
			return result;
		}

		/// <summary>The name shown under an image: the short name unless the configuration uses long texture names.</summary>
		public static string DisplayName(ImageData image)
		{
			return General.Map.Config.UseLongTextureNames ? image.Name : image.ShortName;
		}

		private static bool Matches(ImageData img, string filter, int width, int height)
		{
			if(filter.Length > 0 && !DisplayName(img).ToUpperInvariant().Contains(filter)) return false;
			if(!img.IsPreviewLoaded) return true; // size is unknown until loaded
			if(width > 0 && img.Width != width) return false;
			if(height > 0 && img.Height != height) return false;
			return true;
		}

		#endregion
	}
}
