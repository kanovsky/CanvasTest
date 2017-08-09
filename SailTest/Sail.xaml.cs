using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Pear.RiaServices.Server;
using System.Diagnostics;
using System.Windows;

namespace Pear.RiaServices.Client.DataComponent
{
    public partial class Sail : UserControl
    {
        public SailVM ViewModel
        {
            get { return (SailVM)DataContext; }
        }
        
        private IDisposable AreaSelectionDisposable { get; set; }

        public Sail()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this)) return;

            DataContextChanged += Sail_DataContextChanged;
            Loaded += Sail_Loaded;
        }

        void Sail_DataContextChanged( object sender, DependencyPropertyChangedEventArgs e )
        {
            if (!(e.NewValue is SailVM)) return;

            if (AreaSelectionDisposable != null) AreaSelectionDisposable.Dispose();
            AreaSelectionDisposable = SubscribeAreaSelection();
            
            ViewModel.DrawNewSail = DrawNewSail;
            ViewModel.DrawFilteredSail = DrawSail;
        }
        void Sail_Loaded( object sender, RoutedEventArgs e )
        {
            if (ViewModel == null) return;

            ViewModel.Redraw();
        }

        private void DrawNewSail(List<Facility> facilities)
        {
            scrollViewerEx.ColHeaderContent.Children.Clear();

            DrawSail(facilities);
            DrawCalendar(scrollViewerEx.ColHeaderContent, ViewModel.SailBegin, ViewModel.SailEnd);
        }

        private void DrawSail( IEnumerable<Facility> facilities )
        {
            var span = ViewModel.SailEnd - ViewModel.SailBegin;
            int days = (int)span.TotalDays + 1;
            int totalRows = facilities.Count();

            SelectionBag.Clear();
            hide_side_selection();
            
            scrollViewerEx.ClearAllContent();

            scrollViewerEx.TotalCols = days;
            scrollViewerEx.TotalRows = totalRows;
            
            SpaceHeight = CellHeight * totalRows;
            SpaceWidth = CellWidth * days;

            var rowHeaderWidth = DrawContentAndHeaderRows(scrollViewerEx.ElementContent, facilities);
            
            var nd = (DateTime.Today - ViewModel.SailBegin).Days;
            DrawVerticalGuidelines(scrollViewerEx, nd);

            SetupSelection();

            scrollViewerEx.InvalidateLayout();
        }

        #region Draw content

        private double Zoom { get { return scrollViewerEx.Zoom; } }

        private int CellWidth { get { return scrollViewerEx.cellWidth_const; } }
        private int CellHeight { get { return scrollViewerEx.cellHeight_const; } }

        private int SpaceHeight { get; set; }
        private int SpaceWidth { get; set; }

        private readonly List<Rectangle> ElementListFacilities = new List<Rectangle>();
        private readonly List<Rectangle> ElementListCalendar = new List<Rectangle>();
        int LostColorsFacilities1 = 0;
        int LostColorsFacilities2 = 0;
        int LostColorsCalendar1 = 0;
        int LostColorsCalendar2 = 0;
        List<Tuple<Rectangle, Brush>> LostColorsFacilities = new List<Tuple<Rectangle, Brush>>();
        List<Tuple<Rectangle, Brush>> LostColorsCalendar = new List<Tuple<Rectangle, Brush>>();

        private double DrawContentAndHeaderRows( Canvas canvasRows, IEnumerable<Facility> facilities )
        {
            ElementListFacilities.Clear();

            int row = 0, colorSwitcher = 0;
            Facility prev = null;

            if (!facilities.Any()) return 0;

            var tbDevices = GetDevicesTextBlocks(facilities.Select(f=>f.Type));
            var tbPositions = GetPostionTextBlocks(facilities).ToArray();

            var maxDeviceWidth = 
                tbDevices
               .Select(tb=> { tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); return tb; })
               .Max(tb => tb.DesiredSize.Width);

            var maxPositionWidth = 
                tbPositions
                .Select(tb => { tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); return tb; })
                .Max(tb => tb.DesiredSize.Width);

            var rowHeaderWidth = maxDeviceWidth + 22 + maxPositionWidth;
            scrollViewerEx.RowHeaderWidth = rowHeaderWidth;

            foreach (var curr in facilities)
            {
                var groupOrDeviceChange = prev == null || curr.Group != prev.Group || curr.Type != prev.Type;
                colorSwitcher = groupOrDeviceChange ? colorSwitcher + 1 : colorSwitcher;
                
                Canvas canvasRow = new Canvas();
                DrawContentRow(canvasRow, row, curr, "");
                canvasRow.Visibility = Visibility.Collapsed;
                canvasRows.Children.Add(canvasRow);

                DrawFacility(scrollViewerEx.RowHeaderContent, curr.Type, tbPositions[row], maxDeviceWidth + 16, row, colorSwitcher, groupOrDeviceChange);

                row++; 
                prev = curr;
            }

            return rowHeaderWidth;
        }
        private void DrawContentRow( Canvas canvasRow, int row, Facility facility, string surface )
        {
            Brush stroke = Facility.GetStroke(facility);

            foreach (var ts in facility.TimeSliceList)
            {
                var spanCol = ts.Enter - ViewModel.SailBegin;
                int col = (int)spanCol.Value.TotalDays;
                
                var end = ts.End > ViewModel.SailEnd ? ViewModel.SailEnd : ts.End;
                var spanLen = end - ts.Enter; // here must be end; exit is + 1 day => incorrect display
                int len = (int)spanLen.Value.TotalDays + 1;

                var tsBackground = (Brush)Application.Current.Resources[ts.ColorName];
                var characterOfSale = string.IsNullOrWhiteSpace(ts.CharacterOfSale) ? string.Empty : $"({ts.CharacterOfSale}) ";

                string descr = string.Empty;
                if (!string.IsNullOrWhiteSpace(surface))
                    descr = $"{characterOfSale}{ts.SurfaceDescr}";
                else if (ts.BookingNo != null)
                    descr = $"{(ts.IsPresent ? "* " : string.Empty)}{ts.BookingNo} {characterOfSale} - {ts.Persons}";
                else if (!string.IsNullOrEmpty(ts.Blocation))
                    descr = ts.Blocation;
                else
                    descr = characterOfSale;

                AddTimeSlice(ts, canvasRow, row, col, len, tsBackground, descr, stroke, GroupType.is_ceiling, surface);
            }
        }
        private void AddTimeSlice( TimeSlice ts,
            Canvas canvas, int r, int c, int days, Brush tsBackground, string text, Brush stroke, GroupType draw_hint, string surface )
        {
            int y = r * CellHeight;
            int x = c * CellWidth;
            int fx = 1;
            int f1 = (draw_hint == GroupType.is_middle || draw_hint == GroupType.is_floor) ? 0 : 1;
            int f2 = (draw_hint == GroupType.is_middle || draw_hint == GroupType.is_ceiling) ? 0 : 1;

            Polyline pl = new Polyline
            {
                Stroke = stroke,
                StrokeThickness = 1,
                Fill = tsBackground, 
                Opacity = string.IsNullOrWhiteSpace(surface) || text.Contains(surface) ? 1 : .25f
            };

            pl.Points.Add(new Point(fx, f1));
            pl.Points.Add(new Point(CellWidth - fx, CellHeight - f2));
            pl.Points.Add(new Point((days + 1) * CellWidth - 3 * fx, CellHeight - f2));
            pl.Points.Add(new Point((days) * CellWidth - fx, f1));
            pl.Points.Add(new Point(fx, f1));

            Action setGui = () =>
            {
                canvas.Children.Add(pl);
                Canvas.SetLeft(pl, x);
                Canvas.SetTop(pl, y);
            };

            TextBlock tb = null;
            Action setDescr = null;

            if (!string.IsNullOrEmpty(text))
            {
                int tw = CellWidth * (days - 1) - 6;
                tw = (tw < 0) ? (int)(CellWidth * 0.8f) : tw;
                // aby se to veslo, dáme menší font
                float fsf = 1f;
                float minusx = 0;
                switch (days)
                {
                    case 1: { minusx = -CellWidth * 0.4f; fsf = 0.5f; break; }
                    case 2: { minusx = -CellWidth * 0.3f; fsf = 0.6f; break; }
                    case 3: { minusx = -CellWidth * 0.2f; fsf = 0.7f; break; }
                    case 4: { minusx = -CellWidth * 0.1f; fsf = 0.8f; break; }
                }
                var isOpacityMode = !string.IsNullOrWhiteSpace(surface) && text != surface;
                tb = new TextBlock
                {
                    Foreground = isOpacityMode ? new SolidColorBrush(Colors.Black) : (Brush)Application.Current.Resources["WhiteCalendar"],
                    Text = text,
                    Width = CellWidth * (days <= 0 ? 0 : days - 1),
                    Height = CellHeight,
                    Visibility = Visibility.Visible,
                    FontSize = 10 * fsf,
                    FontWeight = isOpacityMode ? FontWeights.Light : FontWeights.Bold,
                    Clip = new RectangleGeometry
                    {
                        Rect = new Rect
                        {
                            X = 0,
                            Y = 0,
                            Width = tw,
                            Height = CellHeight
                        }
                    }
                };

                var txtX = x < 0 ? 0 : x;

                setDescr = () =>
                {
                    canvas.Children.Add(tb);
                    Canvas.SetLeft(tb, txtX + CellWidth + minusx);
                    Canvas.SetTop(tb, y + 2);
                };
            }

            ts.GuiElement = pl;
            ts.GuiDescr = tb;
            ts.RemoveGuiFromVisualTree = () => 
            {
                canvas.Children.Remove(pl);
                canvas.Children.Remove(tb);
            };
            ts.SetGuiInVisualTree = () =>
            {
                setGui();
                setDescr?.Invoke();
            };

            ts.SetGuiInVisualTree();
        }

        private IEnumerable<TextBlock> GetPostionTextBlocks( IEnumerable<Facility> facilities )
        {
            var q =
                (from f in facilities
                 select new TextBlock
                 {
                     Foreground = f.IsExtraBed ?
                         new SolidColorBrush(Colors.Red) // extra bed in red
                         : (Brush)Application.Current.Resources["BlueText"],
                     Text = f.Position
                 });

            return q;
        }
        private IEnumerable<TextBlock> GetDevicesTextBlocks( IEnumerable<string> facilities )
        {
            var q =
                (from dev in facilities.Distinct()
                 select new TextBlock
                 {
                     Foreground = (Brush)Application.Current.Resources["BlueText"],
                     Text = dev
                 });

            return q;
        }
        private void DrawFacility( Canvas canvas, string device, TextBlock position, double posOffset, int row, int colorSwitcher, bool groupOrDeviceChange )
        {
            Canvas rowCanvas = new Canvas();

            Rectangle rh = new Rectangle();
            rh.Width = scrollViewerEx.RowHeaderWidth;
            rh.Height = CellHeight;
            rh.Fill = (Brush)Application.Current.Resources[colorSwitcher % 2 == 0 ? "BlueHeavy" : "BlueLight"];
            rowCanvas.Children.Add(rh);
            Canvas.SetLeft(rh, 0);

            ElementListFacilities.Add(rh); // for later eventual selection signing

            // type
            if (groupOrDeviceChange)
            {
                var tbDevice = new TextBlock
                {
                    Foreground = (Brush)Application.Current.Resources["BlueText"],
                    Text = device
                };
                rowCanvas.Children.Add(tbDevice);
                Canvas.SetLeft(tbDevice, 4);
                Canvas.SetTop(tbDevice, 2);
            }
            // position
            rowCanvas.Children.Add(position);
            Canvas.SetLeft(position, posOffset);
            Canvas.SetTop(position, 2);

            rowCanvas.Visibility = Visibility.Collapsed;
            canvas.Children.Add(rowCanvas);
            Canvas.SetLeft(rowCanvas, 0);
            Canvas.SetTop(rowCanvas, row * CellHeight);
        }

        private void DrawCalendar( Canvas canvas, DateTime StartDate, DateTime EndDate )
        {
            ElementListCalendar.Clear();

            int x = 0;
            for (DateTime d = StartDate; d <= EndDate; d = d.AddDays(1))
            {
                // month-year line
                if (x == 0 || d.Day == 1)
                {
                    Rectangle rh = new Rectangle();
                    rh.Width = new DateTime(d.AddMonths(1).Year, d.AddMonths(1).Month, 1).Subtract(d).Days * CellWidth;
                    if ((x + rh.Width) > SpaceWidth)
                        rh.Width = SpaceWidth - x;
                    rh.Height = CellHeight;
                    rh.Fill = (Brush)Application.Current.Resources[(d.Month % 2 == 0) ? "BlueLight" : "BlueHeavy"];
                    canvas.Children.Add(rh);
                    Canvas.SetLeft(rh, x);
                    Canvas.SetTop(rh, 0);
                }
                if ((d.Day % 8) == 0)
                {
                    TextBlock tbh = new TextBlock();
                    tbh.Foreground = (Brush)Application.Current.Resources["BlueText"];
                    tbh.Text = d.ToString("MMM yyyy");
                    tbh.Height = CellHeight;
                    tbh.FontSize = tbh.FontSize;
                    canvas.Children.Add(tbh);
                    Canvas.SetLeft(tbh, x);
                    Canvas.SetTop(tbh, 4); // top margin
                }

                // 2nd row - days
                Rectangle rh3 = new Rectangle();
                rh3.Width = CellWidth;
                rh3.Height = CellHeight;
                rh3.Fill = (Brush)Application.Current.Resources["WhiteCalendar"];
                canvas.Children.Add(rh3);
                Canvas.SetLeft(rh3, x);
                Canvas.SetTop(rh3, CellHeight);

                TextBlock tb = new TextBlock();
                tb.Foreground = (Brush)Application.Current.Resources[(d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) ? "RedText" : "BlueText"];
                tb.Text = d.Day.ToString();
                tb.Width = CellWidth;
                tb.Height = CellHeight;
                canvas.Children.Add(tb);
                Canvas.SetLeft(tb, x + 4); // left margin
                Canvas.SetTop(tb, CellHeight + 4); // top margin

                x += CellWidth;

                ElementListCalendar.Add(rh3);
            }
        }

        private void DrawVerticalGuidelines( ScrollViewerEx sv, int today )
        {
            SolidColorBrush str = new SolidColorBrush(Color.FromArgb(255, 80, 80, 80));
            SolidColorBrush str_strong = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));

            for (int col = 1; col <= sv.TotalCols; col++)
            {
                bool strong = today == col || today == (col - 1);
                Line l = new Line();
                l.X1 = col * CellWidth;
                l.X2 = l.X1;
                l.Y1 = 0;
                l.Y2 = SpaceHeight;
                l.Stroke = strong ? str_strong : str;
                l.StrokeThickness = ((strong) ? 2 : 1);
                l.StrokeDashArray.Add(1.0);
                l.StrokeDashArray.Add(4.0);

                l.Visibility = 
                    col * CellWidth <= sv.ScreenWidth ? Visibility.Visible : 
                    Visibility.Collapsed;

                sv.ElementContent.Children.Add(l);
            }
        }

        #endregion

        #region Mouse area selection

        private IDisposable SubscribeAreaSelection()
        {
            IObservable<Point> mouseLBDown;
            IObservable<Point> mouseMove;
            IObservable<Point> mouseLBUp;
            GetMouseEventsAsObservable(out mouseLBDown, out mouseMove, out mouseLBUp);

            Rectangle selectionRect = GetSelectionRect();

            var disp =
                (from md in mouseLBDown // it is possible to click outside working area (where cond. is missing) - it is treated by conversion to > 0 (cols,rows) in Do statement below
                 let rcDown = GetRowColCoordinates(md)
                 let slice = GetSliceByRowCol(rcDown.Item1, rcDown.Item2)
                 let mouseUp =
                    (from mu in mouseLBUp
                     let rcUp = GetRowColCoordinates(mu)
                     let rcDown1 = GetRowColCoordinates(md)
                     select new { md, mu, rcDown, rcUp, rcDown1, slice })
                    .Do(t =>
                    {
                        scrollViewerEx.ElementContent.Children.Remove(selectionRect);

                        SetAndRememberSideSelection(t.rcDown, t.rcUp); // is here because of only click case

                        var less = t.rcUp.Item2 <= t.rcDown.Item2 ? t.rcUp.Item2 : t.rcDown.Item2;
                        var more = t.rcUp.Item2 > t.rcDown.Item2 ? t.rcUp.Item2 : t.rcDown.Item2;
                        var dtFrom = ViewModel.SailBegin.AddDays(less);
                        var dtTo = ViewModel.SailBegin.AddDays(more);

                        var coords = GetOrderedCoordinates(t.rcDown, t.rcUp); // x1, y1, x2, y2

                        ViewModel.SelectedFacilities =
                            from r in Enumerable.Range(coords.Item1, coords.Item3 - coords.Item1 + 1)
                            where r < ViewModel.Sail.Count
                            select ViewModel.Sail[r];

                        var selectedTimeSlice = // click handler
                            (from f in ViewModel.SelectedFacilities
                             from ts in f.TimeSliceList
                             where dtFrom <= ts.Exit && dtTo >= ts.Enter && (ts.BookingNo != null)
                             select ts)
                            .FirstOrDefault();

                        if (selectedTimeSlice != null)
                            SetTimeSliceSelection(selectedTimeSlice);

                        set_selection(t.md.X, t.md.Y, t.mu.X, t.mu.Y);
                    })
                 let mouseMoveBound = mouseMove.TakeUntil(mouseUp)
                 from mm in mouseMoveBound
                 let _ = GetRowColCoordinates(md)
                 let rcMM = GetRowColCoordinates(mm)
                 select new { md, mm, rcDown, rcMM, slice })
                .Retry()
                .Subscribe(t => 
                {
                    DrawSelection(t.md, t.mm, selectionRect);
                    SetAndRememberSideSelection(t.rcDown, t.rcMM);
                });

            return disp;
        }

        private void GetMouseEventsAsObservable(out IObservable<Point> mouseLBDown, out IObservable<Point> mouseMove, out IObservable<Point> mouseLBUp)
        {
            mouseLBDown = 
                Observable.FromEventPattern<MouseButtonEventHandler, MouseButtonEventArgs>(
                    h => scrollViewerEx.LayoutRoot.MouseLeftButtonDown += h,
                    h => scrollViewerEx.LayoutRoot.MouseLeftButtonDown -= h)
                .Select(ep => ep.EventArgs.GetPosition(scrollViewerEx.ElementContent));

            mouseMove = 
                Observable.FromEventPattern<MouseEventHandler, MouseEventArgs>(
                    h => scrollViewerEx.LayoutRoot.MouseMove += h,
                    h => scrollViewerEx.LayoutRoot.MouseMove -= h)
                .Select(e => e.EventArgs.GetPosition(scrollViewerEx.ElementContent));

            mouseLBUp = 
                Observable.FromEventPattern<MouseButtonEventHandler, MouseButtonEventArgs>(
                    h => scrollViewerEx.LayoutRoot.MouseLeftButtonUp += h,
                    h => scrollViewerEx.LayoutRoot.MouseLeftButtonUp -= h)
                .Select(e => e.EventArgs.GetPosition(scrollViewerEx.ElementContent));
        }

        private Tuple<int, int> GetRowColCoordinates(Point p, string dbg = null)
        {
            var row = (int)Math.Floor(p.Y / CellHeight);
            var col = (int)Math.Floor(p.X / CellWidth);

            var t = Tuple.Create(row < 0 ? 0 : row, col < 0 ? 0 : col);

            if (dbg!=null)
                Debug.WriteLine(string.Format("{0} P: {1} RC: {2}", dbg, p.GetHashCode(), t));

            return t;
        }
        private Tuple<int, int, int, int> GetOrderedCoordinates(Tuple<int,int> p1, Tuple<int,int> p2)
        {
            var x1 = p1.Item1 <= p2.Item1 ? p1.Item1 : p2.Item1;
            var x2 = p1.Item1  > p2.Item1 ? p1.Item1 : p2.Item1;

            var y1 = p1.Item2 <= p2.Item2 ? p1.Item2 : p2.Item2;
            var y2 = p1.Item2  > p2.Item2 ? p1.Item2 : p2.Item2;

            return Tuple.Create(x1, y1, x2, y2);
        }
        private TimeSlice GetSliceByRowCol(int row, int col)
        {
            if (row >= ViewModel.Sail.Count)
                return null;

            var colDate = ViewModel.SailBegin.AddDays(col);

            var q =
                 from ts in ViewModel.Sail[row].TimeSliceList
                 where ts.Enter <= colDate && ts.End >= colDate
                 select ts;

            var fst = q.FirstOrDefault();
            return fst;
        }

        private void SetAndRememberSideSelection(Tuple<int, int> p1, Tuple<int, int> p2)
        {
            var coords = GetOrderedCoordinates(p1, p2); // x1, y1, x2, y2
            SetAndRememberSideSelection(coords.Item1, coords.Item3, coords.Item2, coords.Item4);
        }
        private void SetAndRememberSideSelection(int rowStart, int rowEnd, int col1, int col2)
        {
            LostColorsFacilities1 = rowStart;
            LostColorsFacilities2 = rowEnd;
            LostColorsCalendar1 = col1;
            LostColorsCalendar2 = col2;

            set_side_selection(rowStart, rowEnd, col1, col2);
        }

        private Rectangle GetSelectionRect()
        {
            var rect = new Rectangle();
            rect.Fill = new SolidColorBrush(Color.FromArgb(40, 20, 20, 140));
            rect.Stroke = new SolidColorBrush(Color.FromArgb(200, 20, 20, 140));
            rect.StrokeThickness = 3;
            return rect;
        }
        private void DrawSelection(Point p1, Point p2, Rectangle rect)
        {
            if (!scrollViewerEx.ElementContent.Children.Contains(rect))
                scrollViewerEx.ElementContent.Children.Add(rect);

            Canvas.SetLeft(rect, Math.Min(p1.X, p2.X));
            rect.Width = Math.Abs(p1.X - p2.X);

            Canvas.SetTop(rect, Math.Min(p1.Y, p2.Y));
            rect.Height = Math.Abs(p1.Y - p2.Y);

            Debug.WriteLine($"DrawSelection: {p1.X} {p1.Y} {p2.X} {p2.Y}");

            //if (Zoom < 1)
            //    scrollViewerEx.InvalidateLayout(); // hack due to strange behavior after zooming
        }

        Polygon Selection = null;
        List<TimeSlice> SelectionBag = new List<TimeSlice>();
        
        private void SetTimeSliceSelection( TimeSlice timeSlice )
        {
            bool isActionControl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (!isActionControl)
            {
                // clear selection
                foreach (var ts in SelectionBag)
                {
                    ts.IsSelected = false;
                    Polyline p = (Polyline)ts.GuiElement;
                    if (p != null)
                    {
                        p.Stroke = Facility.GetStroke(ts.ParentFacility);
                        p.StrokeThickness = 1;
                        p.StrokeDashArray = null;
                    }
                };
                SelectionBag.Clear();
            }

            // switch state
            if (timeSlice != null)
            {
                timeSlice.IsSelected = true;

                // recalc selection bag
                if (!SelectionBag.Contains(timeSlice) && timeSlice.IsSelected)
                    SelectionBag.Add(timeSlice);

                if (timeSlice.GuiElement != null)
                {
                    // set look for the recent state
                    Polyline pl = (timeSlice.GuiElement as Polyline);
                    pl.Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
                    pl.StrokeThickness = 4;
                    pl.StrokeDashArray = new DoubleCollection { 5, 1 };
                }
            }
        }

        private void set_side_selection(int facilityFrom, int facilityTo, int calendarFrom, int calendarTo)
        {
            hide_side_selection();

            facilityFrom = facilityFrom < 0 ? 0 : facilityFrom;
            facilityTo = facilityTo < 0 ? 0 : facilityTo;
            calendarFrom = calendarFrom < 0 ? 0 : calendarFrom;
            calendarTo = calendarTo < 0 ? 0 : calendarTo;

            for (int ir = facilityFrom; ir <= facilityTo && ir < ElementListFacilities.Count; ir++)
            {
                LostColorsFacilities.Add(Tuple.Create(ElementListFacilities[ir], ElementListFacilities[ir].Fill));

                var res = (SolidColorBrush)Application.Current.Resources["BlueLight"];
                var c = (SolidColorBrush)ElementListFacilities[ir].Fill;
                if (c.Color == res.Color)
                    ElementListFacilities[ir].Fill = (Brush)Application.Current.Resources["GreenLight"];
                else
                    ElementListFacilities[ir].Fill = (Brush)Application.Current.Resources["GreenHeavy"];
            }
            for (int ic = calendarFrom; ic <= calendarTo && ic < ElementListCalendar.Count; ic++)
            {
                LostColorsCalendar.Add(Tuple.Create(ElementListCalendar[ic], ElementListCalendar[ic].Fill));
                ElementListCalendar[ic].Fill = (Brush)Application.Current.Resources[(ic % 2 == 0) ? "GreenLight" : "GreenHeavy"];
            }
        }
        private void hide_side_selection()
        {
            foreach (var lc in LostColorsCalendar)
                lc.Item1.Fill = lc.Item2;

            LostColorsCalendar.Clear();

            foreach (var lf in LostColorsFacilities)
                lf.Item1.Fill = lf.Item2;

            LostColorsFacilities.Clear();
        }
        
        #endregion

        private void set_selection(double x1, double y1, double x2, double y2)
        {
            double swp;
            if (y1 > y2)
            {
                swp = y1; y1 = y2; y2 = swp;
            }
            if (x1 > x2)
            {
                swp = x1; x1 = x2; x2 = swp;
            }

            x1 = (int)(x1 / CellWidth) * CellWidth;
            x2 = (int)(x2 / CellWidth + 1) * CellWidth;

            y1 = (int)(y1 / CellHeight) * CellHeight;
            y2 = (int)(y2 / CellHeight + 1) * CellHeight;

            Selection.Points.Clear();
            for (double y = 0; y != y2 - y1; y += CellHeight)
            {
                Selection.Points.Add(new Point(0, y));
                Selection.Points.Add(new Point(CellWidth, y + CellHeight));
            }
            for (double y = y2 - y1; y != 0; y -= CellHeight)
            {
                Selection.Points.Add(new Point(x2 - x1, y));
                Selection.Points.Add(new Point(x2 - x1 - CellWidth, y - CellHeight));
            }

            Selection.Width = x2 - x1;
            Selection.Height = y2 - y1;

            Canvas.SetLeft(Selection, x1);
            Canvas.SetTop(Selection, y1);

            Selection.Visibility = Visibility.Visible;

            //Debug.WriteLine($"set_selection: {x1} {y1} {x2} {y2}");

            //if (Zoom < 1)
            //    scrollViewerEx.InvalidateLayout(); // hack due to strange behavior after zooming
        }

        private void SetupSelection()
        {
            Selection = new Polygon();
            Selection.Visibility = Visibility.Collapsed;
            Selection.Fill = new SolidColorBrush(Color.FromArgb(40, 20, 20, 140));
            Selection.Stroke = new SolidColorBrush(Color.FromArgb(200, 20, 20, 140));
            Selection.StrokeThickness = 3;

            scrollViewerEx.ElementContent.Children.Add(Selection);
        }
    }
}
