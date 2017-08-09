using System;
using System.Collections.Generic;
using System.Linq;

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows;

namespace Pear.RiaServices.Client.DataComponent
{
    // to help with clipping
    public class CanvasClipper : Grid
    {
        private RectangleGeometry _clippingRectangle;
    
        public CanvasClipper()
        {
            _clippingRectangle = new RectangleGeometry();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Debug.WriteLine($"CanvasClipper-ArrangeOverride");

            finalSize = base.ArrangeOverride(finalSize);
            ClippingRect = new Rect(0, 0, finalSize.Width, finalSize.Height);
            _clippingRectangle.Rect = ClippingRect;
            Clip = _clippingRectangle;
            return finalSize;
        }
        
        // so we know the final size of the clipping rect and thus the true display size
        public Rect ClippingRect { get; set; }
    }

    /// <summary>
    /// ScrollViewerEx - here we also inherit from the MouseWheelObserver interface so we
    /// can scroll using the wheel. See MouseWheel.cs for details
    /// </summary>
    public partial class ScrollViewerEx : UserControl
    { 
        private const int headerRows_const = 2;
        private const int colHeaderHeight_const = 20;
        public int ColHeaderHeight
        {
            get { return (int)(cellHeight_const * headerRows_const); }
        }

        private double rowHeaderWidth = 120;
        public double RowHeaderWidth
        {
            get { return rowHeaderWidth; }
            set { RowHeaderContent.Width = rowHeaderWidth = value; }
        }
        
        public double Zoom
        {
            get
            {
                return (double)GetValue(ZoomProperty);
            }
            set
            {
                SetValue(ZoomProperty, value);

                ZoomInPercent = value * 100;

                //ZoomChangedSubject.OnNext(value);

                Debug.WriteLine($"Zoom change: {value}");
            }
        }
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register("Zoom", typeof(double), typeof(ScrollViewerEx),
            new PropertyMetadata(new PropertyChangedCallback(Zoom_Changed)));
        private static void Zoom_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == null || e.NewValue == e.OldValue) return;

            var sv = (ScrollViewerEx)d;
            sv.InvalidateLayout();
        }
        
        public double ZoomInPercent
        {
            get { return (double)GetValue(ZoomInPercentProperty); }
            set { SetValue(ZoomInPercentProperty, value); }
        }
        public static readonly DependencyProperty ZoomInPercentProperty =
            DependencyProperty.Register("ZoomInPercent", typeof(double), typeof(ScrollViewerEx), new PropertyMetadata(100D));
        
        public readonly int cellHeight_const = 20;
        public int CellHeight
        {
            get
            {
                return (int)( cellHeight_const);
            }
        }
        public readonly int cellWidth_const = 20;
        public int CellWidth
        {
            get
            {
                return (int)(cellWidth_const);
            }
        }
        
        public int TotalRows { get; set; }
        public int TotalCols { get; set; }

        public int ScreenHeight { get { return (int)ElementContentClipper.ClippingRect.Height; } }
        public int ScreenWidth { get { return (int)ElementContentClipper.ClippingRect.Width; } }

        /// <summary>
        /// Hows many rows can we display on a page? N.B. assumes fixed height
        /// </summary>
        public int RowsPerPage
        {
            get
            {
                return (int)(ScreenHeight / (CellHeight * Zoom));
            }
        }
        /// <summary>
        /// How many columns can we display on a page? N.B. assumes fixed width
        /// </summary>
        public int ColsPerPage
        {
            get
            {
                return (int)(ScreenWidth / (CellWidth * Zoom ));
            }
        }

        /// <summary>
        /// Stores the current scroll bar position as an integral index
        /// </summary>

        public double VertPosition
        {
            get
            {
                return (double)GetValue(VertPositionProperty);
            }
            set
            {
                SetValue(VertPositionProperty, value);
            }
        }
        public static readonly DependencyProperty VertPositionProperty =
            DependencyProperty.Register("VertPosition", typeof(double), typeof(ScrollViewerEx)
            , new PropertyMetadata(new PropertyChangedCallback(PositionProperty_Changed)));
        
        private double PrevVertPosition { get; set; }

        /// <summary>
        /// Get the maximum range of the vertical scrollbar
        /// </summary>
        private int VertRange
        {
            get { return (int)VScroll.Maximum; }
        }

        /// <summary>
        /// Stores the current horizontal scrollbar position as an integral index
        /// </summary>
        
        public double HorzPosition
        {
            get
            {
                return (double)GetValue(HorzPositionProperty);
            }
            set
            {
                SetValue(HorzPositionProperty, value);
            }
        }
        public static readonly DependencyProperty HorzPositionProperty =
            DependencyProperty.Register("HorzPosition", typeof(double), typeof(ScrollViewerEx)
            , new PropertyMetadata(new PropertyChangedCallback(PositionProperty_Changed)));

        private static void PositionProperty_Changed(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == null || e.NewValue == e.OldValue) return;

            var sv = (ScrollViewerEx)o;

            sv.InvalidateLayout();
        }

        public Visibility ColHeaderVisibility
        {
            get
            {
                return (Visibility)GetValue(ColHeaderVisibilityProperty);
            }
            set
            {
                SetValue(ColHeaderVisibilityProperty, value);
            }
        }
        public static readonly DependencyProperty ColHeaderVisibilityProperty =
            DependencyProperty.Register("ColHeaderVisibility", typeof(Visibility), typeof(ScrollViewerEx),
            new PropertyMetadata(Visibility.Visible));
            
        public Visibility BottomHorizontalBarVisibility
        {
            get
            {
                return (Visibility)GetValue(BottomHorizontalBarVisibilityProperty);
            }
            set
            {
                SetValue(BottomHorizontalBarVisibilityProperty, value);
            }
        }
        public static readonly DependencyProperty BottomHorizontalBarVisibilityProperty =
            DependencyProperty.Register("BottomHorizontalBarVisibility", typeof(Visibility), typeof(ScrollViewerEx),
            new PropertyMetadata(Visibility.Visible));
        
        private double PrevHorzPosition { get; set; }
        
        /// <summary>
        /// List of all visible items
        /// </summary>
        private readonly List<UIElement> VisibleContentRows = new List<UIElement>();
        private readonly List<UIElement> VisibleRowHeaderRows = new List<UIElement>();

        private readonly List<UIElement> VisibleContentCols = new List<UIElement>();
        private List<UIElement> ColElementContent;

        /// <summary>
        /// Lock for recursion in ArrangeOverride
        /// </summary>
        protected bool Locked
        {
            get; set;
        }

        public TranslateTransform TranslationContent { get { return translationContent; } }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ScrollViewerEx()
        {
            InitializeComponent();

            LayoutRoot.DataContext = this;

            MouseLeftButtonDown += new MouseButtonEventHandler(ScrollViewerEx_MouseLeftButtonDown);
            // event handlers
            KeyDown += (s, e) => OnKeyDown(e);
            GotFocus += ( s, e ) => OnGotFocus(e);
            LostFocus += ( s, e ) => OnLostFocus(e);

            //ElementContent.LayoutUpdated += (s, a) => { Debug.WriteLine("ElementContent LayoutUpdated"); };
            
            Zoom = 1;

            ColHeaderContent.Height = ColHeaderHeight;
            RowHeaderContent.Width = RowHeaderWidth;

            SubscribeSliderChanges();
        }

        private void SubscribeSliderChanges()
        {
            var sliderChanges = Observable.FromEventPattern<RoutedPropertyChangedEventHandler<double>, RoutedPropertyChangedEventArgs<double>>(
                h => sliderZoom.ValueChanged += h,
                h => sliderZoom.ValueChanged -= h);

            (from z in sliderChanges
                .Select(e => Math.Round(e.EventArgs.NewValue / 10, 1))
                .Throttle(TimeSpan.FromMilliseconds(100))
                .DistinctUntilChanged()
                .ObserveOnDispatcher()
             select z)
            .Subscribe(z =>
            {
                Zoom = z;
            });
        }

        void ScrollViewerEx_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //Debug.WriteLine("LeftMouseDown");
            this.Focus();
        }

        /// <summary>
        /// Ultra-minimal keyboard interface for scroller
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            Debug.WriteLine("OnKeyDown");
            if (!e.Handled) 
            {
                bool handled = false;
                // Shift + s switches scrolling strategies
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    if (e.Key == Key.L)
                    {
                        // run the layout test - tell the runtime the layout needs arranging
                        InvalidateLayout();
                    }
                }
                else
                {
                    bool modified = true;
                    var newVScrollValue = VertPosition;
                    var newHScrollValue = HorzPosition;
                    var oldVScrollValue = VertPosition;
                    var oldHScrollValue = HorzPosition;
                    switch (e.Key)
                    {
                        case Key.Home:
                            newVScrollValue = 0;
                            break;
                        case Key.End:
                            newVScrollValue = VertRange - 1;
                            break;
                        case Key.PageUp:
                            newVScrollValue = oldVScrollValue - RowsPerPage;
                            break;
                        case Key.PageDown:
                            newVScrollValue = oldVScrollValue + RowsPerPage;
                            break;
                        case Key.Up:
                            newVScrollValue = oldVScrollValue - 1;
                            break;
                        case Key.Down:
                            newVScrollValue = oldVScrollValue + 1;
                            break;
                        case Key.Left:
                            newHScrollValue = oldHScrollValue - 1;
                            break;
                        case Key.Right:
                            newHScrollValue = oldHScrollValue + 1;
                            break;
                        default:
                            modified = false;
                            break;
                    }
                    //
                    if (modified == true)
                    {
                        // i.e. new value is line number * height of line
                        VScroll.Value = (newVScrollValue);
                        HScroll.Value = (newHScrollValue);
                        //
                        InvalidateLayout();
                    }
                }

                if (handled)
                {
                    e.Handled = true;
                }
            }
        }
                    
        protected override void OnGotFocus(RoutedEventArgs e)
        {
            //Debug.WriteLine("OnGotFocus");
            //SetIsSelectionActive(this, true);
        }

        /// <summary> 
        /// Called when the control lost focus.
        /// </summary>
        /// <param name="e">The event data.</param> 
        protected override void OnLostFocus(RoutedEventArgs e)
        {
            //Debug.WriteLine("OnLostFocus");
            //SetIsSelectionActive(this, false);
        }

        private void LayoutRoot_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // update the scrollbar thumb according to wheel motion
            double pos = VScroll.Value;
            var delta = Math.Sign(e.Delta);
            pos += -delta;
            VScroll.Value = pos;
            //
            InvalidateLayout();
        }

        protected override Size MeasureOverride(Size availableSize) 
        {
            Debug.WriteLine("MeasureOverride");

            InvalidateVisual();

            return base.MeasureOverride(availableSize);
        }

        public void InvalidateLayout()
        {
            // should trigger ArrangeOverride
            InvalidateVisual();
        }
        
        // establish how many rows and columns we can display and
        // set scroll bars accordingly
        protected override Size ArrangeOverride(Size finalSize)
        {
            Debug.WriteLine("ArrangeOverride");

            // let the base class handle the arranging
            finalSize = base.ArrangeOverride(finalSize);
            
            // here's the magic ...
            ApplyLayoutOptimizer();
            
            return finalSize;
        }

        protected void ApplyLayoutOptimizer()
        {
            // beware recursion - settings visibility will trigger 
            // another ArrangeOverride invocation
            if (Locked == false)
            {
                // lock
                Locked = true;
                // set up the scroll bars
                SetScrollRanges();

                var vscrolVisible = TotalRows > RowsPerPage;
                VScroll.Visibility = vscrolVisible ? Visibility.Visible : Visibility.Collapsed;

                DrawRows();
                DrawColumns();

                HandleTransform();

                //PrevVertPosition = VertPosition; // not used now
                //PrevHorzPosition = HorzPosition; // not used now

                //Debug.WriteLine($"LayoutOptimizer{Environment.NewLine}");

                //ElementContentClipper.InvalidateArrange();
                //Debug.WriteLine("Refresh ElementContentClipper");

                Locked = false;
            }
        }

        /// <summary>
        /// Set the vertical and horizontal scroll bar ranges
        /// </summary>
        protected void SetScrollRanges()
        {
            // set the scroll count
            VScroll.Maximum = (TotalRows - RowsPerPage);
            // and do the same for the columns
            HScroll.Maximum = (TotalCols - ColsPerPage);
        }                

        /// <summary>
        /// Use the Translation to scroll the content canvas
        /// </summary>
        protected void HandleTransform()
        {
            scaleColHeader.ScaleX = scaleContent.ScaleX = Zoom;
            scaleRowHeader.ScaleY = scaleContent.ScaleY = Zoom;

            translationColHeader.X = translationContent.X = -(Math.Floor(HScroll.Value) * CellWidth);
            translationRowHeader.Y = translationContent.Y = -(Math.Floor(VScroll.Value) * CellHeight);
        }

        private void DrawRows()
        {
            // hide the visible items
            foreach (UIElement uie in VisibleContentRows) uie.Visibility = Visibility.Collapsed;
            foreach (UIElement uie in VisibleRowHeaderRows) uie.Visibility = Visibility.Collapsed;
            // remove from list
            VisibleContentRows.Clear();
            VisibleRowHeaderRows.Clear();

            // layout a page worth of rows
            var maxRow = Math.Min(VertPosition + RowsPerPage, TotalRows);
            var vertPos = TotalRows > RowsPerPage ? (int)VertPosition : 0;
            for (var row = vertPos; row < maxRow; row++)
            {
                if(row < ElementContent.Children.Count && row >= 0)
                {
                    var contentElem = ElementContent.Children[row];
                    contentElem.Visibility = Visibility.Visible;
                    VisibleContentRows.Add(contentElem);
                }
                if (row < RowHeaderContent.Children.Count && row >= 0)
                {
                    var rowHeaderElem = RowHeaderContent.Children[row];
                    rowHeaderElem.Visibility = Visibility.Visible;
                    VisibleRowHeaderRows.Add(rowHeaderElem);
                }
            }
        }

        private void DrawColumns()
        {
            if (ColElementContent == null)
                ColElementContent = ElementContent.Children.Cast<UIElement>().Skip(TotalRows).ToList();

            foreach (UIElement uie in VisibleContentCols) uie.Visibility = Visibility.Collapsed;
            VisibleContentCols.Clear();

            var maxCol = Math.Min(HorzPosition + ColsPerPage, TotalCols);
            var horizPos = TotalCols > ColsPerPage ? (int)HorzPosition : 0;
            for (var col = horizPos; col < maxCol; col++)
            {
                if (col >= ColElementContent.Count) break;

                var columnElem = ColElementContent[col];
                columnElem.Visibility = Visibility.Visible;
                VisibleContentCols.Add(columnElem);
            }

            //var dbg =
            //        from e in ElementContent.Children
            //        where e is Line && e.Visibility == Visibility.Visible
            //        select e;
            //var n = dbg.Count();
        }

        public void ClearAllContent()
        {
            RowHeaderContent.Children.Clear();
            ElementContent.Children.Clear();

            VisibleContentRows.Clear();
            VisibleRowHeaderRows.Clear();

            VisibleContentCols.Clear();
            ColElementContent = null;
        }
    }
}
