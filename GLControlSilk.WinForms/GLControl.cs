using Silk.NET.Core.Contexts;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GLControlSilk.WinForms;

/// <summary>
/// OpenGL-capable WinForms control that is a specialized wrapper around
/// OpenTK's NativeWindow.
/// </summary>
public class GLControl : Control
{
    #region Private/internal fields

    /// <summary>
    /// The OpenGL configuration of this control.
    /// </summary>
    private readonly GLControlSettings _glControlSettings;

    /// <summary>
    /// The underlying native window.  This will be reparented to be a child of
    /// this control.
    /// </summary>
    private IWindow? _nativeWindow = null;

    // Indicates that OnResize was called before OnHandleCreated.
    // To avoid issues with missing OpenGL contexts, we suppress
    // the premature Resize event and raise it as soon as the handle
    // is ready.
    private bool _resizeEventSuppressed;

    /// <summary>
    /// This is used to render the control at design-time, since we cannot
    /// use a real GLFW instance in the WinForms Designer.
    /// </summary>
    private GLControlDesignTimeRenderer? _designTimeRenderer;

    private bool _nativeWindowFocused = false;
    private Glfw? _glfw = null;
    private GlfwNativeWindow? _glfwNativeWindow = null;

    #endregion

    #region Public configuration

    /// <summary>
    /// Get or set a value representing the current graphics API.
    /// If you change this, the OpenGL context will be recreated, and any
    /// data previously allocated with it will be lost.
    /// </summary>
    public ContextAPI API
    {
        get => _nativeWindow?.API.API ?? _glControlSettings.API;
        set
        {
            if (value != API)
            {
                _glControlSettings.API = value;
                RecreateControl();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value representing the current graphics API profile.
    /// If you change this, the OpenGL context will be recreated, and any
    /// data previously allocated with it will be lost.
    /// </summary>
    public ContextProfile Profile
    {
        get => _nativeWindow?.API.Profile ?? _glControlSettings.Profile;
        set
        {
            if (value != Profile)
            {
                _glControlSettings.Profile = value;
                RecreateControl();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value representing the current graphics profile flags.
    /// If you change this, the OpenGL context will be recreated, and any
    /// data previously allocated with it will be lost.
    /// </summary>
    public ContextFlags Flags
    {
        get => _nativeWindow?.API.Flags ?? _glControlSettings.Flags;
        set
        {
            if (value != Flags)
            {
                _glControlSettings.Flags = value;
                RecreateControl();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value representing the current version of the graphics API.
    /// If you change this, the OpenGL context will be recreated, and any
    /// data previously allocated with it will be lost.
    /// </summary>
    public APIVersion APIVersion
    {
        get => _nativeWindow?.API.Version ?? _glControlSettings.APIVersion;
        set
        {
            if (
                value.MajorVersion != APIVersion.MajorVersion
                || value.MinorVersion != APIVersion.MinorVersion
            )
            {
                _glControlSettings.APIVersion = value;
                RecreateControl();
            }
        }
    }

    /// <summary>
    /// Gets the <see cref="IGLContext"/> instance that is associated with the <see cref="GLControl"/>.
    /// </summary>
    public IGLContext? Context => _nativeWindow?.GLContext;

    /// <summary>
    /// Gets or sets a value indicating whether or not this window is event-driven.
    /// An event-driven window will wait for events before updating/rendering. It is useful for non-game applications,
    /// where the program only needs to do any processing after the user inputs something.
    /// </summary>
    public bool IsEventDriven
    {
        get => _nativeWindow?.IsEventDriven ?? _glControlSettings.IsEventDriven;
        set
        {
            if (value != IsEventDriven)
            {
                _glControlSettings.IsEventDriven = value;
                if (IsHandleCreated && _nativeWindow != null)
                {
                    _nativeWindow.IsEventDriven = value;
                }
            }
        }
    }

    /// <summary>
    /// The standard DesignMode property is horribly broken; it doesn't work correctly
    /// inside the constructor, and it doesn't work correctly under inheritance or when
    /// a control is contained by another control.  For compatibility reasons, Microsoft
    /// is also unlikely to fix it.  So this properly has *more* correct design-time
    /// behavior, everywhere except the constructor.  It tries several techniques to
    /// figure out if this is design-time or not, and then it caches the result.
    /// </summary>
    public bool IsDesignMode => _isDesignMode ??= DetermineIfThisIsInDesignMode();
    private bool? _isDesignMode;

    #endregion

    #region Read-only status properties

    /// <summary>
    /// Gets a value indicating whether the underlying native window was
    /// successfully created.
    /// </summary>
    [Browsable(false)]
    public bool HasValidContext => _nativeWindow != null;

    /// <summary>
    /// Gets the aspect ratio of this GLControl.
    /// </summary>
    [Description("The aspect ratio of the client area of this GLControl.")]
    public float AspectRatio => Width / (float)Height;

    /// <summary>
    /// Access to native-input properties and methods, for more direct control
    /// of the keyboard/mouse/joystick than WinForms natively provides.
    /// We don't instantiate this unless someone asks for it.  In general, if you
    /// *can* do input using WinForms, you *should* do input using WinForms.  But
    /// if you need more direct input control, you can use this property instead.
    ///
    /// This property is null by default.  If you need NativeInput, you
    /// *must* use EnableNativeInput to access it.
    /// </summary>
    private IInputContext? _nativeInput;

    #endregion

    #region Construction/creation

    /// <summary>
    /// Constructs a new instance with default GLControlSettings.  Various things
    /// that like to use reflection want to have an empty constructor available,
    /// so we offer this constructor rather than just adding `= null` to the
    /// constructor that does the actual construction work.
    /// </summary>
    public GLControl() : this(null) { }

    /// <summary>
    /// Constructs a new instance with the specified GLControlSettings.
    /// </summary>
    /// <param name="glControlSettings">The preferred configuration for the OpenGL
    /// renderer.  If null, 'GLControlSettings.Default' will be used instead.</param>
    public GLControl(GLControlSettings? glControlSettings)
    {
        SetStyle(ControlStyles.Opaque, true);
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = false;

        _glControlSettings =
            glControlSettings != null ? glControlSettings.Clone() : new GLControlSettings();
    }

    /// <summary>
    /// This event handler will be invoked by WinForms when the HWND of this
    /// control itself has been created and assigned in the Handle property.
    /// We capture the event to construct the NativeWindow that will be responsible
    /// for all of the actual OpenGL rendering and native device input.
    /// </summary>
    /// <param name="e">An EventArgs instance (ignored).</param>
    protected override void OnHandleCreated(EventArgs e)
    {
        // We don't convert the GLControlSettings to NativeWindowSettings here as that would call GLFW.
        // And this function will be created in design mode.
        CreateNativeWindow(_glControlSettings);

        base.OnHandleCreated(e);

        if (_resizeEventSuppressed)
        {
            OnResize(EventArgs.Empty);
            _resizeEventSuppressed = false;
        }

        if (IsDesignMode)
        {
            _designTimeRenderer = new GLControlDesignTimeRenderer(this);
        }

        if (Focused || _nativeWindowFocused)
        {
            ForceFocusToCorrectWindow();
        }
    }

    /// <summary>
    /// Construct the child NativeWindow that will wrap the underlying GLFW instance.
    /// </summary>
    /// <param name="glControlSettings">The settings to use for
    /// the new GLFW window.</param>
    private unsafe void CreateNativeWindow(GLControlSettings glControlSettings)
    {
        if (IsDesignMode)
            return;

        WindowOptions nativeWindowSettings = glControlSettings.ToNativeWindowSettings();
        nativeWindowSettings.Position = new(ClientRectangle.X, ClientRectangle.Y);
        nativeWindowSettings.Size = new(ClientRectangle.Width, ClientRectangle.Height);
        nativeWindowSettings.Title = "Silk.NET Native Window";
        nativeWindowSettings.WindowBorder = WindowBorder.Hidden;
        nativeWindowSettings.WindowState = WindowState.Normal;

        _nativeWindow = Window.Create(nativeWindowSettings);
        _glfw = GlfwWindowing.GetExistingApi(_nativeWindow);

        _nativeWindow.FocusChanged += OnNativeWindowFocused;
        _nativeWindow.Load += () => OnLoad(EventArgs.Empty);
        _nativeWindow.Initialize();

        _glfwNativeWindow = new GlfwNativeWindow(_glfw, (WindowHandle*)_nativeWindow.Handle);

        NonportableReparent(_nativeWindow);

        // Force the newly child-ified GLFW window to be resized to fit this control.
        ResizeNativeWindow();

        // And now show the child window, since it hasn't been made visible yet.
        _nativeWindow.IsVisible = true;
    }

    /// <summary>
    /// Gets the CreateParams instance for this GLControl.
    /// This is overridden to force correct child behavior.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_VREDRAW = 0x1;
            const int CS_HREDRAW = 0x2;
            const int CS_OWNDC = 0x20;

            CreateParams cp = base.CreateParams;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cp.ClassStyle |= CS_VREDRAW | CS_HREDRAW | CS_OWNDC;
            }
            return cp;
        }
    }

    /// <summary>
    /// When major OpenGL-configuration properties are changed, this method is
    /// invoked to recreate the underlying NativeWindow accordingly.
    /// </summary>
    private void RecreateControl()
    {
        if (_nativeWindow != null && !IsDesignMode)
        {
            DestroyNativeWindow();
            CreateNativeWindow(_glControlSettings);
        }
    }

    /// <summary>
    /// Ensure that the required underlying GLFW window has been created.
    /// </summary>
    private void EnsureCreated()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);

        if (!IsHandleCreated)
        {
            CreateControl();

            if (_nativeWindow == null)
                throw new InvalidOperationException(
                    "Failed to create GLControl."
                        + " This is usually caused by trying to perform operations on the GLControl"
                        + " before its containing form has been fully created.  Make sure you are not"
                        + " invoking methods on it before the Form's constructor has completed."
                );
        }

        if (_nativeWindow == null && !IsDesignMode)
        {
            RecreateHandle();

            if (_nativeWindow == null)
                throw new InvalidOperationException("Failed to recreate GLControl :-(");
        }
    }

    /// <summary>
    /// Because we're really two windows in one, keyboard-focus is a complex
    /// topic.  To ensure correct behavior, we have to capture the various attempts
    /// to assign focus to one or the other window, and if focus is sent to the
    /// wrong window, we have to redirect it to the correct one.  So every attempt
    /// to set focus to *either* window will trigger this method, which will force
    /// the focus to whichever of the two windows it's supposed to be on.
    /// </summary>
    private unsafe void ForceFocusToCorrectWindow()
    {
        if (IsDesignMode || _nativeWindow == null)
            return;

        if (IsNativeInputEnabled(_nativeWindow))
        {
            // Focus should be on the NativeWindow inside the GLControl.
            _glfw?.FocusWindow((WindowHandle*)_nativeWindow.Handle);
        }
        else
        {
            // Focus should be on the GLControl itself.
            Focus();
        }
    }

    /// <summary>
    /// Reparent the given NativeWindow to be a child of this GLControl.  This is a
    /// non-portable operation, as its name implies:  It works wildly differently
    /// between OSes.  The current implementation only supports Microsoft Windows.
    /// </summary>
    /// <param name="nativeWindow">The NativeWindow that must become a child of
    /// this control.</param>
    private unsafe void NonportableReparent(IWindow nativeWindow)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            IntPtr hWnd = _glfwNativeWindow?.Win32?.Hwnd ?? IntPtr.Zero;

            // Reparent the real HWND under this control.
            var h = Win32.SetParent(hWnd, Handle);

            if (h == IntPtr.Zero)
            {
                Console.WriteLine($"Win32.SetParent error code: {Win32.GetLastError()}");
            }

            // Change the real HWND's window styles to be "WS_CHILD | WS_DISABLED" (i.e.,
            // a child of some container, with no input support), and turn off *all* the
            // other style bits (most of the rest of them could cause trouble).  In
            // particular, this turns off stuff like WS_BORDER and WS_CAPTION and WS_POPUP
            // and so on, any of which GLFW might have turned on for us.
            IntPtr style = (IntPtr)
                (long)(Win32.WindowStyles.WS_CHILD | Win32.WindowStyles.WS_DISABLED);
            Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE, style);

            // Change the real HWND's extended window styles to be "WS_EX_NOACTIVATE", and
            // turn off *all* the other extended style bits (most of the rest of them
            // could cause trouble).  We want WS_EX_NOACTIVATE because we don't want
            // Windows mistakenly giving the GLFW window the focus as soon as it's created,
            // regardless of whether it's a hidden window.
            style = (IntPtr)(long)Win32.WindowStylesEx.WS_EX_NOACTIVATE;
            Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_EXSTYLE, style);
        }
        else
            throw new NotSupportedException(
                "The current operating system is not supported by this control."
            );
    }

    /// <summary>
    /// Enable/disable NativeInput for the given NativeWindow.
    /// </summary>
    /// <param name="isEnabled">Whether NativeInput support should be enabled or disabled.</param>
    private unsafe void EnableNativeInput(IWindow nativeWindow, bool isEnabled)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            IntPtr hWnd = _glfwNativeWindow?.Win32?.Hwnd ?? IntPtr.Zero;

            // Tweak the WS_DISABLED style bit for the native window.  When enabled,
            // it will eat all input events directed to it.  When disabled, events will
            // "pass through" to the parent window (i.e., our WinForms control).
            IntPtr style = Win32.GetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE);
            if (isEnabled)
            {
                style = (IntPtr)((Win32.WindowStyles)(long)style & ~Win32.WindowStyles.WS_DISABLED);
            }
            else
            {
                style = (IntPtr)((Win32.WindowStyles)(long)style | Win32.WindowStyles.WS_DISABLED);
            }
            Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE, style);
        }
        else
            throw new NotSupportedException(
                "The current operating system is not supported by this control."
            );
    }

    /// <summary>
    /// Determine if native input is enabled for the given NativeWindow.
    /// </summary>
    /// <param name="nativeWindow">The NativeWindow to query.</param>
    /// <returns>True if native input is enabled; false if it is not.</returns>
    private unsafe bool IsNativeInputEnabled(IWindow nativeWindow)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            IntPtr hWnd = _glfwNativeWindow?.Win32?.Hwnd ?? IntPtr.Zero;
            IntPtr style = Win32.GetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE);
            return ((Win32.WindowStyles)(long)style & Win32.WindowStyles.WS_DISABLED) == 0;
        }
        else
            throw new NotSupportedException(
                "The current operating system is not supported by this control."
            );
    }

    /// <summary>
    /// A fix for the badly-broken DesignMode property, this answers (somewhat more
    /// reliably) whether this is DesignMode or not.  This does *not* work when invoked
    /// from the GLControl's constructor.
    /// </summary>
    /// <returns>True if this is in design mode, false if it is not.</returns>
    private bool DetermineIfThisIsInDesignMode()
    {
        // The obvious test.
        if (DesignMode)
            return true;

        // This works on .NET Framework but no longer seems to work reliably on .NET Core.
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            return true;

        // Try walking the control tree to see if any ancestors are in DesignMode.
        for (Control control = this; control != null; control = control.Parent)
        {
            if (control.Site != null && control.Site.DesignMode)
                return true;
        }

        // Try checking for `IDesignerHost` in the service collection.
        if (GetService(typeof(System.ComponentModel.Design.IDesignerHost)) != null)
            return true;

        // Last-ditch attempt:  Is the process named `devenv` or `VisualStudio`?
        // These are bad, hacky tests, but they *can* work sometimes.
        if (
            System.Reflection.Assembly
                .GetExecutingAssembly()
                .Location.Contains("VisualStudio", StringComparison.OrdinalIgnoreCase)
        )
            return true;
        if (
            string.Equals(
                System.Diagnostics.Process.GetCurrentProcess().ProcessName,
                "devenv",
                StringComparison.OrdinalIgnoreCase
            )
        )
            return true;

        // Nope.  Not design mode.  Probably.  Maybe.
        return false;
    }

    #endregion

    #region Destruction/cleanup

    /// <summary>
    /// This is triggered when the underlying Handle/HWND instance is *about to be*
    /// destroyed (this is called *before* the Handle/HWND is destroyed).  We use it
    /// to cleanly destroy the NativeWindow before its parent disappears.
    /// </summary>
    /// <param name="e">An EventArgs instance (ignored).</param>
    protected override void OnHandleDestroyed(EventArgs e)
    {
        base.OnHandleDestroyed(e);

        DestroyNativeWindow();

        if (_designTimeRenderer != null)
        {
            _designTimeRenderer.Dispose();
            _designTimeRenderer = null;
        }
    }

    /// <summary>
    /// Destroy the child NativeWindow that wraps the underlying GLFW instance.
    /// </summary>
    private void DestroyNativeWindow()
    {
        if (_nativeWindow != null)
        {
            _nativeWindow.Dispose();
            _nativeWindow = null!;
            _glfwNativeWindow = null;
            _glfw?.Dispose();
            _glfw = null;
        }
    }

    #endregion

    #region WinForms event handlers

    /// <summary>
    /// This private object is used as the reference for the 'Load' handler in
    /// the Events collection, and is only needed if you use the 'Load' event.
    /// </summary>
    private static readonly object EVENT_LOAD = new();

    /// <summary>
    /// An event hook, triggered when the control is created for the first time.
    /// </summary>
    [Category("Behavior")]
    [Description("Occurs when the GLControl is first created.")]
    public event EventHandler Load
    {
        add => Events.AddHandler(EVENT_LOAD, value);
        remove => Events.RemoveHandler(EVENT_LOAD, value);
    }

    /// <summary>
    /// Raises the CreateControl event.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected override void OnCreateControl()
    {
        base.OnCreateControl();
    }

    /// <summary>
    /// The Load event is fired before the control becomes visible for the first time.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected virtual void OnLoad(EventArgs e)
    {
        // There is no good way to explain this event except to say
        // that it's just another name for OnControlCreated.
        ((EventHandler?)Events[EVENT_LOAD])?.Invoke(this, e);
    }

    /// <summary>
    /// This is raised by WinForms to paint this instance.
    /// </summary>
    /// <param name="e">A PaintEventArgs object that describes which areas
    /// of the control need to be painted.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureCreated();

        if (IsDesignMode)
        {
            _designTimeRenderer?.Paint(e.Graphics);
        }

        base.OnPaint(e);
    }

    /// <summary>
    /// This is invoked when the Resize event is triggered, and is used to position
    /// the internal GLFW window accordingly.
    ///
    /// Note: This method may be called before the OpenGL context is ready or the
    /// NativeWindow even exists, so everything inside it requires safety checks.
    /// </summary>
    /// <param name="e">An EventArgs instance (ignored).</param>
    protected override void OnResize(EventArgs e)
    {
        // Do not raise OnResize event before the handle and context are created.
        if (!IsHandleCreated)
        {
            _resizeEventSuppressed = true;
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            BeginInvoke(new Action(ResizeNativeWindow)); // Need the native window to resize first otherwise our control will be in the wrong place.
        }
        else
        {
            ResizeNativeWindow();
        }

        base.OnResize(e);
    }

    /// <summary>
    /// Resize the native window to fit this control.
    /// </summary>
    private unsafe void ResizeNativeWindow()
    {
        if (IsDesignMode)
            return;

        if (_nativeWindow is null)
            return;

        _nativeWindow.Size = new(Width, Height);
    }

    /// <summary>
    /// This event is raised when this control's parent control is changed,
    /// which may result in this control becoming a different size or shape, so
    /// we capture it to ensure that the underlying GLFW window gets correctly
    /// resized and repositioned as well.
    /// </summary>
    /// <param name="e">An EventArgs instance (ignored).</param>
    protected override void OnParentChanged(EventArgs e)
    {
        ResizeNativeWindow();

        base.OnParentChanged(e);
    }

    /// <summary>
    /// This event is raised when something sets the focus to the GLControl.
    /// It is overridden to potentially force the focus to the NativeWindow, if
    /// necessary.
    /// </summary>
    /// <param name="e">An EventArgs instance (ignored).</param>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);

        if (!ReferenceEquals(e, _noRecursionSafetyArgs))
        {
            ForceFocusToCorrectWindow();
        }
    }

    /// <summary>
    /// These EventArgs are used as a safety check to prevent unexpected recursion
    /// in OnGotFocus.
    /// </summary>
    private static readonly EventArgs _noRecursionSafetyArgs = new();

    /// <summary>
    /// This event is raised when something sets the focus to the NativeWindow.
    /// It is overridden to potentially force the focus to the GLControl, if
    /// necessary.
    /// </summary>
    /// <param name="isFocused">Used to detect if the
    /// NativeWindow is gaining the focus.</param>
    private void OnNativeWindowFocused(bool isFocused)
    {
        _nativeWindowFocused = isFocused;

        if (isFocused)
        {
            ForceFocusToCorrectWindow();
            OnGotFocus(_noRecursionSafetyArgs);
        }
        else
        {
            OnLostFocus(EventArgs.Empty);
        }
    }

    #endregion

    #region Public OpenGL-related proxy methods

    /// <summary>
    /// Create opengl context
    /// </summary>
    public GL CreateOpenGL()
    {
        if (_nativeWindow is null)
            throw new Exception("Native window has not been created yet!");

        return _nativeWindow.CreateOpenGL();
    }

    /// <summary>
    /// Swaps the front and back buffers, presenting the rendered scene to the user.
    /// </summary>
    public void SwapBuffers()
    {
        if (IsDesignMode)
            return;

        EnsureCreated();
        _nativeWindow?.SwapBuffers();
    }

    /// <summary>
    /// Makes this control's OpenGL context current in the calling thread.
    /// All OpenGL commands issued are hereafter interpreted by this context.
    /// When using multiple GLControls, calling MakeCurrent on one control
    /// will make all other controls non-current in the calling thread.
    /// A GLControl can only be current in one thread at a time.
    /// </summary>
    public void MakeCurrent()
    {
        if (IsDesignMode)
            return;

        EnsureCreated();
        _nativeWindow?.MakeCurrent();
    }

    /// <summary>
    /// Access to native-input properties and methods, for more direct control
    /// of the keyboard/mouse/joystick than WinForms natively provides.
    /// We don't enable this unless someone asks for it.  In general, if you
    /// *can* do input using WinForms, you *should* do input using WinForms.  But
    /// if you need more direct input control, you can use this property instead.
    ///
    /// Note that enabling native input causes *normal* WinForms input methods to
    /// stop working for this GLControl -- all input for will be sent through the
    /// NativeInput interface instead.
    /// </summary>
    public IInputContext EnableNativeInput()
    {
        EnsureCreated();

        if (_nativeWindow is null)
            throw new Exception("Native window has not been created yet!");

        _nativeInput ??= _nativeWindow.CreateInput();

        if (!IsNativeInputEnabled(_nativeWindow))
        {
            EnableNativeInput(_nativeWindow, true);
        }

        if (Focused || _nativeWindowFocused)
        {
            ForceFocusToCorrectWindow();
        }

        return _nativeInput;
    }

    /// <summary>
    /// Disable native input support, and return to using WinForms for all
    /// keyboard/mouse input.  Any INativeInput interface you may have access
    /// to will no longer work propertly until you call EnableNativeInput() again.
    /// </summary>
    public void DisableNativeInput()
    {
        EnsureCreated();

        if (_nativeWindow is null)
            throw new Exception("Native window has not been created yet!");

        if (IsNativeInputEnabled(_nativeWindow))
        {
            EnableNativeInput(_nativeWindow, false);
        }

        if (Focused || _nativeWindowFocused)
        {
            ForceFocusToCorrectWindow();
        }
    }

    #endregion
}
