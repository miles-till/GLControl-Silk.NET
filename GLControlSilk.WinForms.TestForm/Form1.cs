using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace GLControlSilk.WinForms.TestForm
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer _timer = null!;
        private float _angle = 0.0f;
        private float _aspectRatio = 1.0f;

        private GL? _gl;
        private Shader? _shader;
        private readonly List<Face> _faces = new();

        public Matrix4X4<float> Model { get; private set; } = Matrix4X4<float>.Identity;
        public Matrix4X4<float> View { get; private set; } = Matrix4X4<float>.Identity;
        public Matrix4X4<float> Projection { get; private set; } = Matrix4X4<float>.Identity;

        public Form1()
        {
            InitializeComponent();
        }

        private void glControl_Load(object sender, EventArgs e)
        {
            _gl = glControl.CreateOpenGL();

            // Make sure that when the GLControl is resized or needs to be painted,
            // we update our projection matrix or re-render its contents, respectively.
            glControl.Resize += glControl_Resize;
            glControl.Paint += glControl_Paint;

            // Redraw the screen every 1/20 of a second.
            _timer = new System.Windows.Forms.Timer();
            _timer.Tick += (sender, e) =>
            {
                _angle += 0.5f;
                Render();
            };
            _timer.Interval = 50; // 1000 ms per sec / 50 ms per frame = 20 FPS
            _timer.Start();

            // Ensure that the viewport and projection matrix are set correctly initially.
            glControl_Resize(glControl, EventArgs.Empty);

            // Set up View matrix
            View = Matrix4X4.CreateLookAt(
                new Vector3D<float>(0, 5, 5),
                new Vector3D<float>(0, 0, 0),
                new Vector3D<float>(0, 1, 0)
            );

            // Set up Shader
            _shader = new Shader(_gl, "shader.vert", "shader.frag");
            // Create cube
            // csharpier-ignore-start
            _faces.Add(
                new Face(
                    _gl,
                    Color.Silver,
                    new float[]
                    {
                        -1.0f, -1.0f, -1.0f,
                        -1.0f,  1.0f, -1.0f,
                         1.0f,  1.0f, -1.0f,
                         1.0f, -1.0f, -1.0f
                    },
                    new uint[] {
                        0, 1, 3,
                        1, 2, 3
                    }
                )
            );
            _faces.Add(
                new Face(
                    _gl,
                    Color.Honeydew,
                    new float[]
                    {
                        -1.0f, -1.0f, -1.0f,
                         1.0f, -1.0f, -1.0f,
                         1.0f, -1.0f,  1.0f,
                        -1.0f, -1.0f,  1.0f
                    },
                    new uint[] {
                        0, 1, 3,
                        1, 2, 3
                    }
                )
            );
            _faces.Add(
                new Face(
                    _gl,
                    Color.Moccasin,
                    new float[]
                    {
                        -1.0f, -1.0f, -1.0f,
                        -1.0f, -1.0f,  1.0f,
                        -1.0f,  1.0f,  1.0f,
                        -1.0f,  1.0f, -1.0f
                    },
                    new uint[] {
                        0, 1, 3,
                        1, 2, 3
                    }
                )
            );
            _faces.Add(
                new Face(
                    _gl,
                    Color.IndianRed,
                    new float[]
                    {
                        -1.0f, -1.0f, 1.0f,
                         1.0f, -1.0f, 1.0f,
                         1.0f,  1.0f, 1.0f,
                        -1.0f,  1.0f, 1.0f
                    },
                    new uint[] {
                        0, 1, 3,
                        1, 2, 3
                    }
                )
            );
            _faces.Add(
                new Face(
                    _gl,
                    Color.PaleVioletRed,
                    new float[]
                    {
                        -1.0f, 1.0f, -1.0f,
                        -1.0f, 1.0f,  1.0f,
                         1.0f, 1.0f,  1.0f,
                         1.0f, 1.0f, -1.0f
                    },
                    new uint[] {
                        0, 1, 3,
                        1, 2, 3
                    }
                )
            );
            _faces.Add(
                new Face(
                    _gl,
                    Color.ForestGreen,
                    new float[]
                    {
                        1.0f, -1.0f, -1.0f,
                        1.0f,  1.0f, -1.0f,
                        1.0f,  1.0f,  1.0f,
                        1.0f, -1.0f,  1.0f
                    },
                    new uint[] {
                        0, 1, 3,
                        1, 2, 3
                    }
                )
            );
            // csharpier-ignore-end
        }

        private void glControl_Resize(object? sender, EventArgs e)
        {
            glControl.MakeCurrent();

            if (glControl.ClientSize.Height == 0)
                glControl.ClientSize = new System.Drawing.Size(glControl.ClientSize.Width, 1);

            _gl?.Viewport(
                0,
                0,
                (uint)glControl.ClientSize.Width,
                (uint)glControl.ClientSize.Height
            );

            _aspectRatio =
                Math.Max(glControl.ClientSize.Width, 1)
                / (float)Math.Max(glControl.ClientSize.Height, 1);

            Projection = Matrix4X4.CreatePerspectiveFieldOfView(
                MathHelper.PiOver4,
                _aspectRatio,
                1,
                64
            );
        }

        private void glControl_Paint(object? sender, PaintEventArgs e)
        {
            Render();
        }

        private void Render()
        {
            _gl?.ClearColor(Color.MidnightBlue);
            _gl?.Enable(EnableCap.DepthTest);

            Model = Matrix4X4.CreateRotationY(_angle);

            _gl?.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _shader?.Use();
            _shader?.SetUniform("uModel", Model);
            _shader?.SetUniform("uView", View);
            _shader?.SetUniform("uProjection", Projection);

            foreach (var face in _faces)
            {
                _shader?.SetUniform("uColor", face.Color);
                face.Render();
            }

            glControl.SwapBuffers();
        }
    }
}
