using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class GLForm : Form
{
  public event EventHandler<PaintGLEventArgs> PaintGL = delegate { };

  const uint GL_TEXTURE_2D = 0x0DE1;
  const uint GL_TEXTURE_MIN_FILTER = 0x2801;
  const uint GL_TEXTURE_MAG_FILTER = 0x2800;
  const uint GL_LINEAR = 0x2601;
  const uint GL_RGBA = 0x1908;
  const uint GL_BGRA = 0x80E1;
  const uint GL_UNSIGNED_BYTE = 0x1401;
  const uint GL_QUADS = 0x0007;
  const uint GL_VERSION = 0x1F02;
  const uint GL_COLOR_BUFFER_BIT = 0x00004000;

  const int GL_PROJECTION = 0x1701;
  const int GL_MODELVIEW = 0x1700;
  const int GL_BLEND = 0x0BE2;
  const int GL_SRC_ALPHA = 0x0302;
  const int GL_ONE_MINUS_SRC_ALPHA = 0x0303;

  const int DWM_BB_ENABLE = 0x00000001;
  const int CS_HREDRAW = 0x0002;
  const int CS_VREDRAW = 0x0001;

  const int PFD_TYPE_RGBA = 0;
  const int PFD_MAIN_PLANE = 0;
  const int PFD_DRAW_TO_WINDOW = 0x00000004;
  const int PFD_SUPPORT_OPENGL = 0x00000020;
  const int PFD_DOUBLEBUFFER = 0x00000001;
  const int PFD_FLAGS = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER;

  private IntPtr m_hdc;
  private IntPtr m_hrc;

  private int m_title_bar_height;
  private int m_texture_id;
  private Bitmap m_surface;
  private Graphics m_graphics;

  public GLForm() {
    SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    InitializeOpenGL();
    InitializeTexture();
    ImplementTransparency();
    this.SizeChanged += (s, e) => {
      wglMakeCurrent(m_hdc, m_hrc);
      UpdateGLViewPortSize();
      wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
    };
  }

  protected override void OnStyleChanged(EventArgs e) {
    base.OnStyleChanged(e);
    m_title_bar_height = Height - ClientSize.Height;
  }

  public int TitleBarHeight {
    get { return m_title_bar_height; }
    set { m_title_bar_height = value; }
  }

  private void InitializeOpenGL() {
    m_hdc = GetDC(Handle);
    PIXELFORMATDESCRIPTOR pfd = new PIXELFORMATDESCRIPTOR {
      nSize = (ushort)Marshal.SizeOf(typeof(PIXELFORMATDESCRIPTOR)),
      nVersion = 1,
      dwFlags = PFD_FLAGS,
      iPixelType = PFD_TYPE_RGBA,
      cColorBits = 32,
      cAlphaBits = 8,
      iLayerType = PFD_MAIN_PLANE
    };

    int pixel_format = ChoosePixelFormat(m_hdc, ref pfd);
    SetPixelFormat(m_hdc, pixel_format, ref pfd);
    m_hrc = wglCreateContext(m_hdc);
    wglMakeCurrent(m_hdc, m_hrc);

    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    UpdateGLViewPortSize();
  }

  private void InitializeTexture() {
    m_surface = new Bitmap(Width, Height);
    m_graphics = Graphics.FromImage(m_surface);
    glGenTextures(1, out m_texture_id);
    glBindTexture(GL_TEXTURE_2D, m_texture_id);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
  }

  private void ImplementTransparency() {
    MARGINS margins = new MARGINS { cxLeftWidth = -1 };
    DwmExtendFrameIntoClientArea(Handle, ref margins);
    DWM_BLURBEHIND bb = new DWM_BLURBEHIND {
      dwFlags = DWM_BB_ENABLE,
      fEnable = true,
      hRgnBlur = IntPtr.Zero
    };
    DwmEnableBlurBehindWindow(Handle, ref bb);
  }

  protected override CreateParams CreateParams {
    get {
      CreateParams cp = base.CreateParams;
      cp.ClassStyle |= CS_HREDRAW;
      cp.ClassStyle |= CS_VREDRAW;
      return cp;
    }
  }

  private void UpdateGLViewPortSize() {
    glViewport(0, 0, this.Width, this.Height);
    glMatrixMode(GL_PROJECTION);
    glLoadIdentity();
    glOrtho(0, this.Width, this.Height, 0, -1, 1);
    glMatrixMode(GL_MODELVIEW);
    glLoadIdentity();
  }

  public static string GetOpenGLVersion() {
    IntPtr ptr_version = glGetString(GL_VERSION);
    if (ptr_version != IntPtr.Zero) {
      string version = Marshal.PtrToStringAnsi(ptr_version);
      return version;
    }
    return string.Empty;
  }

  protected override void OnPaint(PaintEventArgs e) {
    wglMakeCurrent(m_hdc, m_hrc);
    glClear(GL_COLOR_BUFFER_BIT);
    HandlePaint();
    glEnable(GL_TEXTURE_2D);
    glBindTexture(GL_TEXTURE_2D, m_texture_id);
    glBegin(GL_QUADS);
    glTexCoord2f(0, 0); glVertex2f(0, 0);
    glTexCoord2f(1, 0); glVertex2f(Width, 0);
    glTexCoord2f(1, 1); glVertex2f(Width, Height);
    glTexCoord2f(0, 1); glVertex2f(0, Height);
    glEnd();
    glDisable(GL_TEXTURE_2D);
    SwapBuffers(m_hdc);
  }

  private void HandlePaint() {
    if (m_surface.Width != Width || m_surface.Height != Height) {
      m_surface.Dispose();
      m_surface = new Bitmap(Width, Height);
      m_graphics.Dispose();
      m_graphics = Graphics.FromImage(m_surface);
    }
    m_graphics.Clear(Color.Transparent);
    m_graphics.TranslateTransform(0, TitleBarHeight);
    OnPaintGL(new PaintGLEventArgs(m_graphics));
    UpdateSurfaceTexture();
  }

  public virtual void OnPaintGL(PaintGLEventArgs e) {
    PaintGL(this, e);
  }

  private void UpdateSurfaceTexture() {
    BitmapData bmp_data = m_surface.LockBits(
         new Rectangle(0, 0, m_surface.Width, m_surface.Height),
         ImageLockMode.ReadOnly,
         PixelFormat.Format32bppArgb);

    glBindTexture(GL_TEXTURE_2D, m_texture_id);
    glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA, m_surface.Width, m_surface.Height, 0,
                 GL_BGRA, GL_UNSIGNED_BYTE, bmp_data.Scan0);

    m_surface.UnlockBits(bmp_data);
  }

  protected override void OnFormClosing(FormClosingEventArgs e) {
    CleanUp();
    base.OnFormClosing(e);
  }

  private void CleanUp() {
    if (m_texture_id != 0) {
      glDeleteTextures(1, ref m_texture_id);
      m_texture_id = 0;
    }
    if (m_surface != null) {
      m_surface.Dispose();
      m_surface = null;
    }
    if (m_graphics != null) {
      m_graphics.Dispose();
      m_graphics = null;
    }
    if (m_hrc != IntPtr.Zero) {
      wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
      wglDeleteContext(m_hrc);
      m_hrc = IntPtr.Zero;
    }
    if (m_hdc != IntPtr.Zero) {
      ReleaseDC(Handle, m_hdc);
      m_hdc = IntPtr.Zero;
    }
  }

  /* :::::::::::::::::::: Unmanaged Code :::::::::::::::::::: */

  public class PaintGLEventArgs : EventArgs
  {
    public Graphics Graphics { get; }

    public PaintGLEventArgs(Graphics graphics) {
      Graphics = graphics;
    }
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct MARGINS
  {
    public int cxLeftWidth;
    public int cxRightWidth;
    public int cyTopHeight;
    public int cyBottomHeight;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct DWM_BLURBEHIND
  {
    public uint dwFlags;
    public bool fEnable;
    public IntPtr hRgnBlur;
    public bool fTransitionOnMaximized;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct PIXELFORMATDESCRIPTOR
  {
    public ushort nSize;
    public ushort nVersion;
    public uint dwFlags;
    public byte iPixelType;
    public byte cColorBits;
    public byte cRedBits;
    public byte cRedShift;
    public byte cGreenBits;
    public byte cGreenShift;
    public byte cBlueBits;
    public byte cBlueShift;
    public byte cAlphaBits;
    public byte cAlphaShift;
    public byte cAccumBits;
    public byte cAccumRedBits;
    public byte cAccumGreenBits;
    public byte cAccumBlueBits;
    public byte cAccumAlphaBits;
    public byte cDepthBits;
    public byte cStencilBits;
    public byte cAuxBuffers;
    public byte iLayerType;
    public byte bReserved;
    public uint dwLayerMask;
    public uint dwVisibleMask;
    public uint dwDamageMask;
  }

  [DllImport("user32.dll")]
  static extern IntPtr GetDC(IntPtr hwnd);

  [DllImport("user32.dll")]
  static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

  [DllImport("opengl32.dll")]
  static extern IntPtr wglCreateContext(IntPtr hDC);

  [DllImport("opengl32.dll")]
  static extern bool wglMakeCurrent(IntPtr hDC, IntPtr hRC);

  [DllImport("opengl32.dll")]
  static extern bool wglDeleteContext(IntPtr hRC);

  [DllImport("gdi32.dll")]
  static extern int ChoosePixelFormat(IntPtr hDC, [In] ref PIXELFORMATDESCRIPTOR ppfd);

  [DllImport("gdi32.dll")]
  static extern bool SetPixelFormat(IntPtr hDC, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);

  [DllImport("opengl32.dll")]
  static extern void glViewport(int x, int y, int width, int height);

  [DllImport("opengl32.dll")]
  static extern void glMatrixMode(uint mode);

  [DllImport("opengl32.dll")]
  static extern void glLoadIdentity();

  [DllImport("opengl32.dll")]
  static extern void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar);

  [DllImport("opengl32.dll")]
  static extern void glClear(uint mask);

  [DllImport("opengl32.dll")]
  static extern void glBegin(uint mode);

  [DllImport("opengl32.dll")]
  static extern void glEnd();

  [DllImport("opengl32.dll")]
  static extern void glColor4f(float red, float green, float blue, float alpha);

  [DllImport("opengl32.dll")]
  static extern void glVertex2f(float x, float y);

  [DllImport("opengl32.dll")]
  static extern void glEnable(uint cap);

  [DllImport("opengl32.dll")]
  static extern void glBlendFunc(uint sfactor, uint dfactor);

  [DllImport("opengl32.dll", SetLastError = true)]
  private static extern IntPtr glGetString(uint name);

  [DllImport("opengl32.dll")]
  static extern void glGenTextures(int n, out int textures);

  [DllImport("opengl32.dll")]
  static extern void glBindTexture(uint target, int texture);

  [DllImport("opengl32.dll")]
  private static extern void glDeleteTextures(int n, ref int textures);

  [DllImport("opengl32.dll")]
  static extern void glTexParameteri(uint target, uint pname, int param);

  [DllImport("opengl32.dll")]
  static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);

  [DllImport("opengl32.dll")]
  static extern void glTexCoord2f(float s, float t);

  [DllImport("opengl32.dll")]
  static extern void glDisable(uint cap);

  [DllImport("gdi32.dll")]
  static extern bool SwapBuffers(IntPtr hdc);

  [DllImport("dwmapi.dll")]
  static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

  [DllImport("dwmapi.dll")]
  static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND pBlurBehind);

}
