using System;

namespace CodeImp.DoomBuilder.Rendering
{
    /// <summary>Vertex buffer handle. The GPU storage is owned by the <see cref="IRenderBackend"/> that fills it.</summary>
    public class VertexBuffer : IDisposable
    {
        ~VertexBuffer()
        {
            Dispose();
        }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            if (!Disposed)
            {
                Disposed = true;
                Backend?.ReleaseResource(BackendData);
                BackendData = null;
            }
        }

        /// <summary>Set by the backend that owns the GPU object (backend implementations live in another assembly).</summary>
        public IRenderBackend Backend;
        public object BackendData;
    }
}
