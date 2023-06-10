using Silk.NET.OpenGL;

namespace GLControlSilk.WinForms.TestForm;

internal class Face : IDisposable
{
    private readonly GL _gl;

    private readonly BufferObject<float> _vbo;
    private readonly BufferObject<uint> _ebo;
    private readonly VertexArrayObject<float, uint> _vao;

    public readonly Color Color;
    public readonly float[] Vertices;
    public readonly uint[] Indices;

    public Face(GL gl, Color color, float[] vertices, uint[] indices)
    {
        _gl = gl;
        Color = color;
        Vertices = vertices;
        Indices = indices;

        _vbo = new BufferObject<float>(_gl, Vertices, BufferTargetARB.ArrayBuffer);
        _ebo = new BufferObject<uint>(_gl, Indices, BufferTargetARB.ElementArrayBuffer);
        _vao = new VertexArrayObject<float, uint>(_gl, _vbo, _ebo);

        _vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 3, 0);
    }

    public unsafe void Render()
    {
        _vao.Bind();
        _gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)Indices.Length,
            DrawElementsType.UnsignedInt,
            null
        );
    }

    public void Dispose()
    {
        _vbo.Dispose();
        _ebo.Dispose();
        _vao.Dispose();
    }
}
