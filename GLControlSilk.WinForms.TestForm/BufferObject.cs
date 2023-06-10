﻿// based on https://github.com/dotnet/Silk.NET/blob/main/examples/CSharp/OpenGL%20Tutorials/Tutorial%201.4%20-%20Abstractions/BufferObject.cs
using Silk.NET.OpenGL;

namespace GLControlSilk.WinForms.TestForm;

internal class BufferObject<TDataType> : IDisposable where TDataType : unmanaged
{
    //Our handle, buffertype and the GL instance this class will use, these are private because they have no reason to be public.
    //Most of the time you would want to abstract items to make things like this invisible.
    private uint _handle;
    private BufferTargetARB _bufferType;
    private GL _gl;

    public unsafe BufferObject(GL gl, Span<TDataType> data, BufferTargetARB bufferType)
    {
        //Setting the gl instance and storing our buffer type.
        _gl = gl;
        _bufferType = bufferType;

        //Getting the handle, and then uploading the data to said handle.
        _handle = _gl.GenBuffer();
        Bind();
        fixed (void* d = data)
        {
            _gl.BufferData(
                bufferType,
                (nuint)(data.Length * sizeof(TDataType)),
                d,
                BufferUsageARB.StaticDraw
            );
        }
    }

    public void Bind()
    {
        //Binding the buffer object, with the correct buffer type.
        _gl.BindBuffer(_bufferType, _handle);
    }

    public void Dispose()
    {
        //Remember to delete our buffer.
        _gl.DeleteBuffer(_handle);
    }
}
