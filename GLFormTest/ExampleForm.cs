using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace GLFormTest
{
  public class ExampleForm : GLForm
  {

    private int m_frames_per_second = 0;
    private float m_rotation = 0.0f;
    private DateTime m_last_time = DateTime.Now;
    private int m_frame_count = 0;
    private List<PointF> m_stars = new List<PointF>();
    private List<float> m_star_depths = new List<float>();
    private Random m_random = new Random();

    private const int WM_NCHITTEST = 0x84;
    private const int HTCAPTION = 0x2;
    private const int HTLEFT = 0xA;
    private const int HTRIGHT = 0xB;
    private const int HTTOP = 0xC;
    private const int HTTOPLEFT = 0xD;
    private const int HTTOPRIGHT = 0xE;
    private const int HTBOTTOM = 0xF;
    private const int HTBOTTOMLEFT = 0x10;
    private const int HTBOTTOMRIGHT = 0x11;
    private const int BORDER_WIDTH = 10;

    public ExampleForm() : base() {
      this.FormBorderStyle = FormBorderStyle.None;
      CenterToScreen();  
    }

    protected override void WndProc(ref Message m) {
      if (m.Msg == WM_NCHITTEST) {
        var cursor = PointToClient(Cursor.Position);
        if (cursor.Y < BORDER_WIDTH) {
          if (cursor.X < BORDER_WIDTH)
            m.Result = (IntPtr)HTTOPLEFT;
          else if (cursor.X > ClientSize.Width - BORDER_WIDTH)
            m.Result = (IntPtr)HTTOPRIGHT;
          else
            m.Result = (IntPtr)HTTOP;
        }
        else if (cursor.Y > ClientSize.Height - BORDER_WIDTH) {
          if (cursor.X < BORDER_WIDTH)
            m.Result = (IntPtr)HTBOTTOMLEFT;
          else if (cursor.X > ClientSize.Width - BORDER_WIDTH)
            m.Result = (IntPtr)HTBOTTOMRIGHT;
          else
            m.Result = (IntPtr)HTBOTTOM;
        }
        else if (cursor.X < BORDER_WIDTH) {
          m.Result = (IntPtr)HTLEFT;
        }
        else if (cursor.X > ClientSize.Width - BORDER_WIDTH) {
          m.Result = (IntPtr)HTRIGHT;
        }
        else {
          m.Result = (IntPtr)HTCAPTION;
        }
        return;
      }
      base.WndProc(ref m);
    }

    public override void OnPaintGL(PaintGLEventArgs e) {
      base.OnPaintGL(e);
      m_rotation += 1.0f;
      if (m_rotation > 360.0f) {
        m_rotation -= 360.0f;
      }
      var state = e.Graphics.Save();
      e.Graphics.TranslateTransform(200, 200);
      e.Graphics.RotateTransform(m_rotation);
      PointF[] pts_triangle = new PointF[] {
        new PointF(-100, 100),
        new PointF(100, 100),
        new PointF(0, -100)
      };
      using (PathGradientBrush brush = new PathGradientBrush(pts_triangle)) {
        brush.CenterPoint = new PointF(0, 33.33f);
        brush.CenterColor = Color.Transparent;
        brush.SurroundColors = new Color[] {
          Color.FromArgb(128, 255, 0, 0),
          Color.FromArgb(128, 0, 255, 0),
          Color.FromArgb(128, 0, 0, 255)
        };
        e.Graphics.FillPolygon(brush, pts_triangle);
      }
      e.Graphics.Restore(state);
      state = e.Graphics.Save();
      e.Graphics.TranslateTransform(420, 200);
      e.Graphics.RotateTransform(-m_rotation);
      e.Graphics.FillRectangle(Brushes.Black, -100, -100, 200, 200);
      e.Graphics.Restore(state);
      e.Graphics.DrawRectangle(Pens.Red, 10, 10, 300, 100);
      e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
      e.Graphics.DrawRectangle(Pens.Black, 0, 0, Width - 1, Height - 1);
      e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
      e.Graphics.FillEllipse(Brushes.White, 500, 500, 100, 100);
      e.Graphics.DrawEllipse(Pens.Black, 500, 500, 100, 100);
      e.Graphics.SmoothingMode = SmoothingMode.Default;
      DrawStarfield(e);
      CalculateFPS();
      e.Graphics.DrawString(m_frames_per_second.ToString() + "FPS", Font, Brushes.Blue, 20, 20);
      Invalidate();
    }

    private void DrawStarfield(PaintGLEventArgs e) {
      int star_counts = 200;
      float max_depth = 1000.0f;
      float star_speed = 10.0f;
      if (m_stars.Count == 0) {
        for (int i = 0; i < star_counts; i++) {
          m_stars.Add(new PointF(
              m_random.Next(-Width, Width),
              m_random.Next(-Height, Height)
          ));
          m_star_depths.Add(m_random.Next(1, (int)max_depth));
        }
      }
      int center_x = Width / 2;
      int center_y = Height / 2;
      using (Brush star_brush = new SolidBrush(Color.FromArgb(m_random.Next(0, 256), m_random.Next(0, 256), m_random.Next(0, 256)))) {
        for (int i = 0; i < m_stars.Count; i++) {
          float factor = 200 / (m_star_depths[i] + 200);
          float x = m_stars[i].X * factor + center_x;
          float y = m_stars[i].Y * factor + center_y;
          float size = (1 - m_star_depths[i] / max_depth) * 3 + 1;
          e.Graphics.FillEllipse(star_brush, x, y, size, size);
          m_star_depths[i] -= star_speed;
          if (m_star_depths[i] <= 0) {
            m_stars[i] = new PointF(
                m_random.Next(-Width, Width),
                m_random.Next(-Height, Height)
            );
            m_star_depths[i] = max_depth;
          }
        }
      }
    }

    private void CalculateFPS() {
      m_frame_count++;
      DateTime current_time = DateTime.Now;
      if ((current_time - m_last_time).TotalSeconds >= 1.0) {
        m_frames_per_second = m_frame_count / (int)(current_time - m_last_time).TotalSeconds;
        m_frame_count = 0;
        m_last_time = current_time;
      }
    }
  }
}
