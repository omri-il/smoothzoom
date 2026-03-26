using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SmoothAnnotate.Services;

public class ConfettiService
{
    private readonly Canvas _canvas;
    private readonly DispatcherTimer _timer;
    private readonly List<(UIElement Element, double VX, double VY, double Rotation, double RotSpeed)> _particles = new();
    private readonly Random _rng = new();
    private double _canvasWidth;
    private double _canvasHeight;

    private static readonly Color[] ConfettiColors = {
        Color.FromRgb(255, 60, 80),    // Red
        Color.FromRgb(50, 180, 255),   // Blue
        Color.FromRgb(255, 220, 0),    // Yellow
        Color.FromRgb(0, 220, 100),    // Green
        Color.FromRgb(255, 100, 200),  // Pink
        Color.FromRgb(255, 140, 30),   // Orange
        Color.FromRgb(180, 80, 255),   // Purple
    };

    public ConfettiService(Canvas canvas)
    {
        _canvas = canvas;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(25) // ~40fps
        };
        _timer.Tick += OnTick;
    }

    public void Burst(double centerX, double centerY, double width, double height)
    {
        _canvasWidth = width;
        _canvasHeight = height;

        // Spawn 60 confetti particles
        for (int i = 0; i < 60; i++)
        {
            var color = ConfettiColors[_rng.Next(ConfettiColors.Length)];
            var shape = _rng.Next(3);

            UIElement particle;
            if (shape == 0)
            {
                // Small rectangle
                particle = new System.Windows.Shapes.Rectangle
                {
                    Width = 6 + _rng.NextDouble() * 8,
                    Height = 4 + _rng.NextDouble() * 4,
                    Fill = new SolidColorBrush(color),
                    IsHitTestVisible = false
                };
            }
            else if (shape == 1)
            {
                // Small circle
                particle = new Ellipse
                {
                    Width = 5 + _rng.NextDouble() * 6,
                    Height = 5 + _rng.NextDouble() * 6,
                    Fill = new SolidColorBrush(color),
                    IsHitTestVisible = false
                };
            }
            else
            {
                // Triangle
                var size = 5 + _rng.NextDouble() * 7;
                particle = new Polygon
                {
                    Points = new PointCollection {
                        new(0, size), new(size / 2, 0), new(size, size)
                    },
                    Fill = new SolidColorBrush(color),
                    IsHitTestVisible = false
                };
            }

            double x = centerX + (_rng.NextDouble() - 0.5) * 200;
            double y = centerY - _rng.NextDouble() * 100;
            Canvas.SetLeft(particle, x);
            Canvas.SetTop(particle, y);

            double vx = (_rng.NextDouble() - 0.5) * 12;
            double vy = -3 - _rng.NextDouble() * 8; // upward burst
            double rotSpeed = (_rng.NextDouble() - 0.5) * 15;

            _canvas.Children.Add(particle);
            _particles.Add((particle, vx, vy, 0, rotSpeed));
        }

        if (!_timer.IsEnabled)
            _timer.Start();
    }

    public void FullScreenBurst(double width, double height)
    {
        // Burst from 3 points across the screen
        Burst(width * 0.25, height * 0.3, width, height);
        Burst(width * 0.5, height * 0.2, width, height);
        Burst(width * 0.75, height * 0.3, width, height);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var (el, vx, vy, rot, rotSpeed) = _particles[i];

            double x = Canvas.GetLeft(el) + vx;
            double y = Canvas.GetTop(el) + vy;
            double newVY = vy + 0.35; // gravity
            double newVX = vx * 0.98; // air drag
            double newRot = rot + rotSpeed;

            Canvas.SetLeft(el, x);
            Canvas.SetTop(el, y);

            // Rotate via RenderTransform
            el.RenderTransform = new RotateTransform(newRot);

            // Fade out near bottom
            if (y > _canvasHeight * 0.7)
            {
                double fade = 1.0 - (y - _canvasHeight * 0.7) / (_canvasHeight * 0.3);
                el.Opacity = Math.Max(0, fade);
            }

            // Remove if off screen
            if (y > _canvasHeight + 50 || el.Opacity <= 0)
            {
                _canvas.Children.Remove(el);
                _particles.RemoveAt(i);
            }
            else
            {
                _particles[i] = (el, newVX, newVY, newRot, rotSpeed);
            }
        }

        if (_particles.Count == 0)
            _timer.Stop();
    }
}
