﻿// based on https://github.com/dotnet/Silk.NET/blob/main/examples/CSharp/OpenGL%20Tutorials/Tutorial%201.4%20-%20Abstractions/VertexArrayObject.cs
using Silk.NET.OpenGL;

namespace GLControlSilk.WinForms.TestForm;

//The vertex array object abstraction.
internal class VertexArrayObject<TVertexType, TIndexType> : IDisposable
    where TVertexType : unmanaged
    where TIndexType : unmanaged
{
    //Our handle and the GL instance this class will use, these are private because they have no reason to be public.
    //Most of the time you would want to abstract items to make things like this invisible.
    private uint _handle;
    private GL _gl;

    public VertexArrayObject(GL gl, BufferObject<TVertexType> vbo, BufferObject<TIndexType> ebo)
    {
        //Saving the GL instance.
        _gl = gl;

        //Setting out handle and binding the VBO and EBO to this VAO.
        _handle = _gl.GenVertexArray();
        Bind();
        vbo.Bind();
        ebo.Bind();
    }

    public unsafe void VertexAttributePointer(
        uint index,
        int count,
        VertexAttribPointerType type,
        uint vertexSize,
        int offSet
    )
    {
        //Setting up a vertex attribute pointer
        _gl.VertexAttribPointer(
            index,
            count,
            type,
            false,
            vertexSize * (uint)sizeof(TVertexType),
            (void*)(offSet * sizeof(TVertexType))
        );
        _gl.EnableVertexAttribArray(index);
    }

    public void Bind()
    {
        //Binding the vertex array.
        _gl.BindVertexArray(_handle);
    }

    public void Dispose()
    {
        //Remember to dispose this object so the data GPU side is cleared.
        //We dont delete the VBO and EBO here, as you can have one VBO stored under multiple VAO's.
        _gl.DeleteVertexArray(_handle);
    }
}
