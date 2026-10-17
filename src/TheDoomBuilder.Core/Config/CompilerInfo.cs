
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Compilers;

#endregion

namespace CodeImp.DoomBuilder.Config
{
	public sealed class CompilerInfo
	{
		#region ================== Constants
		
		#endregion
		
		#region ================== Variables
		
		private readonly string filename;
		private readonly string name;
		private readonly string programfile;
		private readonly string programinterface;
		private readonly string path;
		private readonly HashSet<string> files;
		
		#endregion
		
		#region ================== Properties

		public string FileName { get { return filename; } }
		public string Name { get { return name; } }
		public string Path { get { return path; } }
		public string ProgramFile { get { return programfile; } }
		public string ProgramInterface { get { return programinterface; } }
		public HashSet<string> Files { get { return files; } }

		/// <summary>
		/// The program to run: the one in the compiler's folder, or else one of that name on the PATH (the binaries of the compilers are
		/// not distributed with the editor on Linux and macOS, where they are usually installed system-wide). When none exists, the path
		/// in the compiler's folder, so that the error message says where the editor looked.
		/// </summary>
		public string ProgramPath
		{
			get
			{
				string local = System.IO.Path.Combine(path, programfile);
				if(File.Exists(local)) return local;
				if(OperatingSystem.IsWindows() && File.Exists(local + ".exe")) return local + ".exe";

				string name = programfile;
				if(!OperatingSystem.IsWindows() && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
				string onpath = FindOnPath(name);
				return onpath ?? local;
			}
		}

		/// <summary>The full path of an executable of this name in one of the PATH folders, or null.</summary>
		internal static string FindOnPath(string name)
		{
			if(string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0) return null;
			string pathvar = Environment.GetEnvironmentVariable("PATH");
			if(string.IsNullOrEmpty(pathvar)) return null;

			foreach(string folder in pathvar.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
			{
				try
				{
					string candidate = System.IO.Path.Combine(folder.Trim('"'), name);
					if(File.Exists(candidate)) return candidate;
					if(OperatingSystem.IsWindows() && File.Exists(candidate + ".exe")) return candidate + ".exe";
				}
				catch(ArgumentException) { }   // a folder with characters that are not valid in a path
			}
			return null;
		}

		/// <summary>
		/// Files unpacked from an archive lose their execute permission on Linux and macOS: gives it back (to the owner) before running.
		/// </summary>
		internal static void EnsureExecutable(string program)
		{
			if(OperatingSystem.IsWindows() || !File.Exists(program)) return;
			try
			{
				UnixFileMode mode = File.GetUnixFileMode(program);
				if((mode & UnixFileMode.UserExecute) == 0) File.SetUnixFileMode(program, mode | UnixFileMode.UserExecute);
			}
			catch(Exception) { }   // not ours to change: starting it reports the problem
		}
		
		#endregion
		
		#region ================== Constructor / Disposer
		
		// Constructor
		internal CompilerInfo(string filename, string name, string path, Configuration cfg)
		{
			General.WriteLogLine("Registered compiler configuration \"" + name + "\" from \"" + filename + "\"");
			
			// Initialize
			this.filename = filename;
			this.path = path;
			this.name = name;
			this.files = new HashSet<string>(StringComparer.OrdinalIgnoreCase); //mxd. List -> HashSet
			
			// Read program file and interface
			this.programfile = cfg.ReadSetting("compilers." + name + ".program", "");
			this.programinterface = cfg.ReadSetting("compilers." + name + ".interface", "");
			
			// Make list of files required
			IDictionary cfgfiles = cfg.ReadSetting("compilers." + name, new Hashtable());
			foreach(DictionaryEntry de in cfgfiles)
			{
				if(de.Key.ToString() != "interface" && de.Key.ToString() != "program")
				{
                    //mxd
                    string include = de.Value.ToString().Replace('\\', '/');
                    if (files.Contains(include))
						General.ErrorLogger.Add(ErrorType.Warning, "Include file \"" + de.Value + "\" is double defined in \"" + name + "\" compiler configuration");
					else
						files.Add(include);
				}
			}
		}
		
		#endregion
		
		#region ================== Methods
		
		// This creates the actual compiler interface
		internal Compiler Create()
		{
			return Compiler.Create(this);
		}
		
		#endregion
	}
}
