using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace DoomBuilder.Rendering
{
	/// <summary>One GLSL program (vertex+fragment), built on first use. Every shader exists twice: plain and with ALPHA_TEST defined.</summary>
	internal sealed class GlShader
	{
		// Vertex attribute locations (shared with the vertex array setup)
		public const uint AttrPosition = 0, AttrColor = 1, AttrUV = 2, AttrNormal = 3;

		private string identifier, vertexText, fragmentText;
		private bool alphatest, built;
		private string errors = string.Empty;
		private uint vertexShader, fragmentShader;

		public uint Program { get; private set; }
		public int[] UniformLocations = Array.Empty<int>();
		public int[] UniformLastUpdates = Array.Empty<int>();

		public void Setup(string identifier, string vertexText, string fragmentText, bool alphatest)
		{
			this.identifier = identifier;
			this.vertexText = vertexText;
			this.fragmentText = fragmentText;
			this.alphatest = alphatest;
		}

		public bool HasErrors { get { return errors.Length > 0; } }

		public string GetCompileError()
		{
			string what = vertexShader == 0 ? "vertex " : (fragmentShader == 0 ? "fragment " : "");
			return "Error compiling " + what + "shader \"" + identifier + "\":\r\n" + errors.Replace("\r", "").Replace("\n", "\r\n");
		}

		/// <summary>Builds the program the first time; returns false when it failed to compile or link.</summary>
		public bool CheckCompile(GL gl, bool gles, IReadOnlyList<string> uniformNames)
		{
			if(!built)
			{
				built = true;
				CreateProgram(gl, gles, uniformNames);
				if(Program != 0)
				{
					gl.UseProgram(Program);
					gl.Uniform1(gl.GetUniformLocation(Program, "texture1"), 0);
					gl.Uniform1(gl.GetUniformLocation(Program, "texture2"), 1);
					gl.Uniform1(gl.GetUniformLocation(Program, "texture3"), 2);
					gl.UseProgram(0);
				}
			}
			return !HasErrors;
		}

		public void Bind(GL gl)
		{
			if(Program == 0 || !built || HasErrors) return;
			gl.UseProgram(Program);
		}

		private void CreateProgram(GL gl, bool gles, IReadOnlyList<string> uniformNames)
		{
			string version = gles ? "#version 300 es\nprecision highp float;\nprecision highp int;\n" : "#version 330\n";
			string prefix = version + (alphatest ? "#define ALPHA_TEST\n" : "") + "#line 1\n";

			vertexShader = Compile(gl, prefix + vertexText, ShaderType.VertexShader);
			if(vertexShader == 0) return;
			fragmentShader = Compile(gl, prefix + fragmentText, ShaderType.FragmentShader);
			if(fragmentShader == 0) return;

			Program = gl.CreateProgram();
			gl.AttachShader(Program, vertexShader);
			gl.AttachShader(Program, fragmentShader);
			gl.BindAttribLocation(Program, AttrPosition, "AttrPosition");
			gl.BindAttribLocation(Program, AttrColor, "AttrColor");
			gl.BindAttribLocation(Program, AttrUV, "AttrUV");
			gl.BindAttribLocation(Program, AttrNormal, "AttrNormal");
			gl.LinkProgram(Program);

			gl.GetProgram(Program, ProgramPropertyARB.LinkStatus, out int status);
			if(status != (int)GLEnum.True)
			{
				errors = gl.GetProgramInfoLog(Program);
				gl.DeleteProgram(Program);
				gl.DeleteShader(vertexShader);
				gl.DeleteShader(fragmentShader);
				Program = 0; vertexShader = 0; fragmentShader = 0;
				return;
			}

			UniformLastUpdates = new int[uniformNames.Count];
			UniformLocations = new int[uniformNames.Count];
			for(int i = 0; i < uniformNames.Count; i++)
				UniformLocations[i] = string.IsNullOrEmpty(uniformNames[i]) ? -1 : gl.GetUniformLocation(Program, uniformNames[i]);
		}

		private uint Compile(GL gl, string code, ShaderType type)
		{
			uint shader = gl.CreateShader(type);
			gl.ShaderSource(shader, code);
			gl.CompileShader(shader);
			gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
			if(status != (int)GLEnum.True)
			{
				errors = gl.GetShaderInfoLog(shader);
				gl.DeleteShader(shader);
				return 0;
			}
			return shader;
		}

		/// <summary>The GL context is gone and with it the program: forget it without deleting anything, to be built again on first use.</summary>
		public void Invalidate()
		{
			Program = 0; vertexShader = 0; fragmentShader = 0;
			built = false;
			errors = string.Empty;
			UniformLocations = Array.Empty<int>();
			UniformLastUpdates = Array.Empty<int>();
		}

		public void ReleaseResources(GL gl)
		{
			if(Program != 0) gl.DeleteProgram(Program);
			if(vertexShader != 0) gl.DeleteShader(vertexShader);
			if(fragmentShader != 0) gl.DeleteShader(fragmentShader);
			Program = 0; vertexShader = 0; fragmentShader = 0;
		}
	}
}
