using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QuickLook.Plugin.AudioViewer.Audio;
using QuickLook.Plugin.AudioViewer.Settings;

namespace QuickLook.Plugin.AudioViewer.Controls
{
    /// <summary>
    /// Piano-roll note overlay drawn on top of the spectrogram. It lives inside the shared
    /// Timeline grid (same Grid.Row as the spectrogram image), so its local coordinates equal
    /// the spectrogram-image space and it scrolls with the content — notes map through the same
    /// <see cref="NoteGeometry"/> / <see cref="FrequencyScaleMapper"/> the image and rulers use,
    /// aligning on any scale/max-freq. A thin render + event shell over the shared note list:
    /// all coordinate/snap math stays in the pure, unit-tested <see cref="NoteGeometry"/>.
    ///
    /// In view mode the overlay is not hit-test-visible, so the spectrogram's hover readout is
    /// untouched; enabling draw mode makes it hit-testable so it can draw / move / resize / select.
    /// Internal (matching <see cref="NoteModel"/>) — instantiated from same-assembly XAML by x:Name.
    /// </summary>
    internal sealed class NoteOverlay : FrameworkElement
    {
        // Within this many px of an edge counts as a resize grab (also pads the hit area).
        private const double EdgeGrabPx = 4.0;
        private const double MinDurationSeconds = 0.03;   // shortest note a drag can make
        private const double ClickDurationSeconds = 0.25; // a bare click makes a note this long
        private const double ClickPixelThreshold = 3.0;   // drag vs click discrimination

        private FrequencyScale _scale = FrequencyScale.Linear;
        private double _fMin, _fMax, _durationSeconds;
        private IList<NoteModel> _notes;

        private Brush _fill = Frozen(Color.FromArgb(0x80, 0x22, 0xD3, 0xEE));
        private Brush _fillHover = Frozen(Color.FromArgb(0xB0, 0x22, 0xD3, 0xEE));
        private Pen _border = FrozenPen(Color.FromRgb(0x67, 0xE8, 0xF9), 1.2);
        private Pen _borderSelected = FrozenPen(Color.FromRgb(0xFD, 0xE0, 0x47), 2.2);
        private Brush _handleFill = Frozen(Color.FromRgb(0xFD, 0xE0, 0x47));
        private Brush _labelBrush = Frozen(Color.FromRgb(0xFF, 0xFF, 0xFF));
        private readonly Typeface _face = new Typeface("Segoe UI");

        private bool _drawMode;
        private Gesture _gesture = Gesture.None;
        private NoteModel _selected;
        private NoteModel _hover;
        private Point _dragStart;          // press point in overlay pixels
        private double _dragAnchorSeconds;  // fixed time the gesture began at
        private double _grabStartSeconds;   // note.StartSeconds captured at gesture start
        private double _grabDurSeconds;      // note.DurationSeconds captured at gesture start
        private int _grabMidi;               // note.Midi captured at gesture start
        private bool _changed;               // note data changed during the active gesture

        private enum Gesture { None, Draw, Move, ResizeLeft, ResizeRight }

        /// <summary>Raised whenever a note is created, edited, or deleted.</summary>
        public event Action NotesChanged;

        public NoteOverlay()
        {
            IsHitTestVisible = false; // view mode: let the spectrogram hover readout work
        }

        /// <summary>Bind the overlay to the current vertical scale, range, timeline length and note list.</summary>
        public void Configure(FrequencyScale scale, double fMin, double fMax, double durationSeconds,
            IList<NoteModel> notes)
        {
            _scale = scale;
            _fMin = fMin;
            _fMax = fMax;
            _durationSeconds = durationSeconds;
            _notes = notes;
            InvalidateVisual();
        }

        /// <summary>Recolor the notes for the current light/dark theme; fill carries its own alpha.</summary>
        public void SetTheme(Color fill, Color border, Color selected)
        {
            _fill = Frozen(fill);
            _fillHover = Frozen(Lighten(fill));
            _border = FrozenPen(border, 1.2);
            _borderSelected = FrozenPen(selected, 2.2);
            _handleFill = Frozen(selected);
            InvalidateVisual();
        }

        /// <summary>
        /// Draw mode gates hit-testing: off -> transparent to the mouse so the spectrogram hover
        /// readout keeps working; on -> the overlay captures the mouse to draw / edit notes.
        /// </summary>
        public bool DrawMode
        {
            get => _drawMode;
            set
            {
                _drawMode = value;
                IsHitTestVisible = value;
                Cursor = value ? Cursors.Cross : Cursors.Arrow;
                if (!value)
                {
                    if (IsMouseCaptured) ReleaseMouseCapture();
                    _gesture = Gesture.None;
                    _hover = null;
                }
                InvalidateVisual();
            }
        }

        public NoteModel SelectedNote => _selected;
        public bool HasSelection => _selected != null;

        /// <summary>Delete the selected note (if any); returns true when one was removed.</summary>
        public bool DeleteSelected()
        {
            if (_selected == null || _notes == null) return false;
            _notes.Remove(_selected);
            _selected = null;
            _hover = null;
            InvalidateVisual();
            NotesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Cancel an in-progress gesture: a draw discards the nascent note; a move/resize reverts to
        /// the pre-drag values. Returns true only when something was cancelled (so Escape otherwise
        /// bubbles to the host).
        /// </summary>
        public bool CancelDraw()
        {
            if (_gesture == Gesture.None) return false;

            if (_gesture == Gesture.Draw && _selected != null && _notes != null)
            {
                _notes.Remove(_selected);
                _selected = null;
            }
            else if (_selected != null) // revert a move/resize
            {
                _selected.StartSeconds = _grabStartSeconds;
                _selected.DurationSeconds = _grabDurSeconds;
                _selected.Midi = _grabMidi;
            }

            _gesture = Gesture.None;
            _changed = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            InvalidateVisual();
            NotesChanged?.Invoke();
            return true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // A FrameworkElement receives mouse events only where it has painted. To stay
            // visually transparent yet still capture draw clicks across the whole pane (so
            // you can draw the FIRST note on empty space), paint a fully-transparent fill.
            // Hit-testing is still gated by IsHitTestVisible (draw mode), so the view-mode
            // hover readout is unaffected.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            if (_notes == null || _notes.Count == 0 || _fMax <= _fMin || _durationSeconds <= 0) return;

            double dpi = PixelsPerDip();
            foreach (var n in _notes)
            {
                double x0 = NoteGeometry.TimeToX(n.StartSeconds, _durationSeconds, w);
                double x1 = NoteGeometry.TimeToX(n.EndSeconds, _durationSeconds, w);
                NoteGeometry.NoteYBounds(n.Midi, _scale, _fMin, _fMax, h, out double yTop, out double yBottom);

                double rw = Math.Max(1.5, x1 - x0);
                double rh = Math.Max(2.0, yBottom - yTop);
                var rect = new Rect(x0, yTop, rw, rh);

                bool isSel = ReferenceEquals(n, _selected);
                bool isHover = ReferenceEquals(n, _hover);
                dc.DrawRoundedRectangle(isHover ? _fillHover : _fill, isSel ? _borderSelected : _border, rect, 2, 2);

                // Label with the note name when the block is wide/tall enough to be legible.
                if (rw >= 22 && rh >= 11)
                {
                    var ft = new FormattedText(MidiNote.NoteName(n.Midi), CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, _face, 9.5, _labelBrush, dpi);
                    if (ft.Width + 4 <= rw)
                        dc.DrawText(ft, new Point(x0 + 3, yTop + (rh - ft.Height) / 2));
                }

                if (isSel)
                {
                    double cy = (yTop + yBottom) / 2;
                    const double hs = 3;
                    dc.DrawRectangle(_handleFill, null, new Rect(x0 - hs, cy - hs, 2 * hs, 2 * hs));
                    dc.DrawRectangle(_handleFill, null, new Rect(x1 - hs, cy - hs, 2 * hs, 2 * hs));
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!_drawMode || _notes == null) return;
            double w = ActualWidth, h = ActualHeight;
            var p = e.GetPosition(this);
            _dragStart = p;
            _changed = false;

            var hit = HitTest(p, out NoteGeometry.NoteEdge edge);
            if (hit != null)
            {
                _selected = hit;
                _grabStartSeconds = hit.StartSeconds;
                _grabDurSeconds = hit.DurationSeconds;
                _grabMidi = hit.Midi;
                _dragAnchorSeconds = NoteGeometry.XToTime(p.X, w, _durationSeconds);
                switch (edge)
                {
                    case NoteGeometry.NoteEdge.Left: _gesture = Gesture.ResizeLeft; Cursor = Cursors.SizeWE; break;
                    case NoteGeometry.NoteEdge.Right: _gesture = Gesture.ResizeRight; Cursor = Cursors.SizeWE; break;
                    default: _gesture = Gesture.Move; Cursor = Cursors.SizeAll; break;
                }
            }
            else
            {
                double startSec = NoteGeometry.XToTime(p.X, w, _durationSeconds);
                var note = new NoteModel
                {
                    Id = NextId(),
                    Midi = NoteGeometry.YToMidi(p.Y, _scale, _fMin, _fMax, h),
                    StartSeconds = startSec,
                    DurationSeconds = MinDurationSeconds,
                    Velocity = 80
                };
                _notes.Add(note);
                _selected = note;
                _grabMidi = note.Midi;
                _dragAnchorSeconds = startSec;
                _gesture = Gesture.Draw;
                Cursor = Cursors.SizeWE;
            }

            CaptureMouse();
            e.Handled = true;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            double w = ActualWidth, h = ActualHeight;
            var p = e.GetPosition(this);

            if (_gesture == Gesture.None)
            {
                if (_drawMode) UpdateHoverCursor(p, w, h);
                return;
            }
            if (_selected == null) return;

            double curSec = NoteGeometry.XToTime(p.X, w, _durationSeconds);
            switch (_gesture)
            {
                case Gesture.Draw:
                    NoteGeometry.DrawSpan(_dragAnchorSeconds, curSec, MinDurationSeconds,
                        out double dStart, out double dDur);
                    _selected.StartSeconds = dStart;
                    _selected.DurationSeconds = dDur;
                    _selected.Midi = _grabMidi; // pitch is fixed by the press point while dragging duration
                    break;

                case Gesture.Move:
                    double newStart = _grabStartSeconds + (curSec - _dragAnchorSeconds);
                    if (newStart < 0) newStart = 0;
                    double maxStart = Math.Max(0, _durationSeconds - _selected.DurationSeconds);
                    if (newStart > maxStart) newStart = maxStart;
                    _selected.StartSeconds = newStart;
                    _selected.Midi = NoteGeometry.YToMidi(p.Y, _scale, _fMin, _fMax, h);
                    break;

                case Gesture.ResizeRight:
                    _selected.DurationSeconds = NoteGeometry.ResizeRight(_selected.StartSeconds, curSec, MinDurationSeconds);
                    break;

                case Gesture.ResizeLeft:
                    double fixedEnd = _grabStartSeconds + _grabDurSeconds;
                    NoteGeometry.ResizeLeft(fixedEnd, curSec, MinDurationSeconds,
                        out double rStart, out double rDur);
                    _selected.StartSeconds = rStart;
                    _selected.DurationSeconds = rDur;
                    break;
            }

            _changed = true;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_gesture == Gesture.None) return;
            var gesture = _gesture;
            var p = e.GetPosition(this);
            bool moved = (p - _dragStart).Length >= ClickPixelThreshold;

            // A bare click (no real drag) in Draw mode yields a default-length note.
            if (gesture == Gesture.Draw && _selected != null && !moved)
                _selected.DurationSeconds = ClickDurationSeconds;

            _gesture = Gesture.None;
            if (IsMouseCaptured) ReleaseMouseCapture();
            Cursor = _drawMode ? Cursors.Cross : Cursors.Arrow;
            e.Handled = true;
            InvalidateVisual();

            if (gesture == Gesture.Draw || _changed) NotesChanged?.Invoke();
            _changed = false;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            if (_gesture == Gesture.None && _hover != null)
            {
                _hover = null;
                InvalidateVisual();
            }
        }

        private void UpdateHoverCursor(Point p, double w, double h)
        {
            var hit = HitTest(p, out NoteGeometry.NoteEdge edge);
            if (!ReferenceEquals(hit, _hover)) { _hover = hit; InvalidateVisual(); }
            if (hit == null) Cursor = Cursors.Cross;
            else if (edge == NoteGeometry.NoteEdge.Left || edge == NoteGeometry.NoteEdge.Right) Cursor = Cursors.SizeWE;
            else Cursor = Cursors.SizeAll;
        }

        /// <summary>Topmost note under a point (searched front-to-back) and which edge/body was hit.</summary>
        private NoteModel HitTest(Point p, out NoteGeometry.NoteEdge edge)
        {
            edge = NoteGeometry.NoteEdge.Outside;
            if (_notes == null) return null;
            double w = ActualWidth, h = ActualHeight;
            for (int i = _notes.Count - 1; i >= 0; i--)
            {
                var n = _notes[i];
                NoteGeometry.NoteYBounds(n.Midi, _scale, _fMin, _fMax, h, out double yTop, out double yBottom);
                if (p.Y < yTop || p.Y > yBottom) continue;
                double x0 = NoteGeometry.TimeToX(n.StartSeconds, _durationSeconds, w);
                double x1 = NoteGeometry.TimeToX(n.EndSeconds, _durationSeconds, w);
                var e = NoteGeometry.HitEdge(p.X, x0, x1, EdgeGrabPx);
                if (e != NoteGeometry.NoteEdge.Outside) { edge = e; return n; }
            }
            return null;
        }

        private int NextId()
        {
            int max = 0;
            if (_notes != null)
                foreach (var n in _notes) if (n.Id > max) max = n.Id;
            return max + 1;
        }

        private double PixelsPerDip()
        {
            try { return VisualTreeHelper.GetDpi(this).PixelsPerDip; }
            catch { return 1.0; }
        }

        private static Color Lighten(Color c)
        {
            // Raise alpha toward opaque for a "lit" hover fill over the spectrogram.
            int a = Math.Min(0xFF, c.A + 0x30);
            return Color.FromArgb((byte)a, c.R, c.G, c.B);
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Pen FrozenPen(Color c, double thickness)
        {
            var p = new Pen(Frozen(c), thickness);
            p.Freeze();
            return p;
        }
    }
}
