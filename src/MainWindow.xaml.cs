using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Asistanim
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Entry> _notes;
        private readonly ObservableCollection<Entry> _ideas;
        private ObservableCollection<Entry> _current;
        private string _savedText;
        private Entry _editing;
        private bool _loadingDetail;

        private readonly DispatcherTimer _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
        private Border _pressCard;
        private Point _pressPos;
        private bool _dragging;
        private bool _held;

        public MainWindow()
        {
            InitializeComponent();
            var n = new List<Entry>();
            var i = new List<Entry>();
            Storage.Load(n, i);
            _notes = new ObservableCollection<Entry>(n);
            _ideas = new ObservableCollection<Entry>(i);
            _notes.CollectionChanged += OnCollectionChanged;
            _ideas.CollectionChanged += OnCollectionChanged;
            foreach (var en in n.Concat(i)) en.PropertyChanged += OnEntryChanged;
            _savedText = Snapshot();
            _holdTimer.Tick += HoldTimer_Tick;
            ShowCurrentTab();
            Closing += MainWindow_Closing;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private void OnCollectionChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            UpdateDirty();
            if (s == _current) RefreshEmpty();
        }

        private void OnEntryChanged(object s, PropertyChangedEventArgs e) => UpdateDirty();

        private string Snapshot() => Storage.Serialize(_notes, _ideas);
        private bool IsDirty => Snapshot() != _savedText;

        private void UpdateDirty()
        {
            bool d = IsDirty;
            DirtyDot.Visibility = d ? Visibility.Visible : Visibility.Collapsed;
            SaveBtn.IsEnabled = d;
        }

        private bool SaveNow()
        {
            try
            {
                var text = Snapshot();
                Storage.Save(text);
                _savedText = text;
                UpdateDirty();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kaydedilemedi: " + ex.Message, "Asistanım", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { SaveNow(); e.Handled = true; }
            else if (e.Key == Key.Escape && DetailPane.Visibility == Visibility.Visible) { CloseDetail(); e.Handled = true; }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!IsDirty) return;
            var dlg = new ConfirmDialog { Owner = this };
            dlg.ShowDialog();
            switch (dlg.Result)
            {
                case ConfirmDialog.Choice.Save:
                    if (!SaveNow()) e.Cancel = true;
                    break;
                case ConfirmDialog.Choice.Discard:
                    break;
                default:
                    e.Cancel = true;
                    break;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Save_Click(object sender, RoutedEventArgs e) => SaveNow();

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (Items == null) return;
            ShowCurrentTab();
        }

        private void ShowCurrentTab()
        {
            _current = TabIdeas.IsChecked == true ? _ideas : _notes;
            Items.ItemsSource = _current;
            EmptyText.Text = _current == _ideas ? "Henüz fikir yok" : "Henüz not yok";
            RefreshEmpty();
        }

        private void RefreshEmpty()
            => EmptyState.Visibility = _current.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        private void AddBox_TextChanged(object sender, TextChangedEventArgs e)
            => AddHint.Visibility = string.IsNullOrEmpty(AddBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        private void AddBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { AddItem(); e.Handled = true; }
        }

        private void Add_Click(object sender, RoutedEventArgs e) => AddItem();

        private void AddItem()
        {
            var t = AddBox.Text.Trim();
            if (t.Length == 0) return;
            var en = new Entry { Title = t };
            en.PropertyChanged += OnEntryChanged;
            _current.Add(en);
            AddBox.Clear();
            Scroller.ScrollToEnd();
            AddBox.Focus();
        }

        // ---------- Press-and-hold drag reorder / click to open ----------
        private void Card_Down(object sender, MouseButtonEventArgs e)
        {
            _pressCard = (Border)sender;
            _pressPos = e.GetPosition(Items);
            _held = false;
            _dragging = false;
            _pressCard.CaptureMouse();
            _holdTimer.Stop();
            _holdTimer.Start();
        }

        private void HoldTimer_Tick(object sender, EventArgs e)
        {
            _holdTimer.Stop();
            if (_pressCard == null) return;
            _held = true;
            _dragging = true;
            ApplyDragVisual(_pressCard);
        }

        private void ApplyDragVisual(Border c)
        {
            c.Opacity = 0.8;
            c.BorderBrush = (Brush)FindResource("AccentBrush");
            c.RenderTransform = new ScaleTransform(1.015, 1.015);
            c.RenderTransformOrigin = new Point(0.5, 0.5);
            Panel.SetZIndex(c, 10);
        }

        private void ClearDragVisual(Border c)
        {
            c.Opacity = 1;
            c.RenderTransform = null;
            c.BorderBrush = (Brush)FindResource("BorderBrushSoft");
            Panel.SetZIndex(c, 0);
        }

        private void Card_Move(object sender, MouseEventArgs e)
        {
            if (_pressCard == null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(Items);
            if (!_dragging)
            {
                if (Math.Abs(pos.Y - _pressPos.Y) > 8 || Math.Abs(pos.X - _pressPos.X) > 8)
                {
                    _holdTimer.Stop();
                    _held = true; // moved before hold: not a click
                }
                return;
            }

            var item = (Entry)_pressCard.Tag;
            int from = _current.IndexOf(item);
            int to = from;
            for (int i = 0; i < _current.Count; i++)
            {
                var c = Items.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (c == null || i == from) continue;
                var top = c.TranslatePoint(new Point(0, 0), Items).Y;
                double mid = top + c.ActualHeight / 2;
                if (i < from && pos.Y < mid) { to = i; break; }
                if (i > from && pos.Y > mid) to = i;
            }
            if (to != from)
            {
                _current.Move(from, to);
                Items.UpdateLayout();
                var cont = Items.ItemContainerGenerator.ContainerFromIndex(to) as FrameworkElement;
                var card = cont == null ? null : FindCard(cont);
                if (card != null && card != _pressCard)
                {
                    var old = _pressCard;
                    _pressCard = card;
                    ClearDragVisual(old);
                    old.ReleaseMouseCapture();
                    ApplyDragVisual(card);
                    card.CaptureMouse();
                }
            }
            var p = e.GetPosition(Scroller);
            if (p.Y < 30) Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset - 12);
            else if (p.Y > Scroller.ActualHeight - 30) Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + 12);
        }

        private static Border FindCard(DependencyObject root)
        {
            if (root is Border b && b.Name == "card") return b;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var r = FindCard(VisualTreeHelper.GetChild(root, i));
                if (r != null) return r;
            }
            return null;
        }

        private void Card_Up(object sender, MouseButtonEventArgs e)
        {
            _holdTimer.Stop();
            var card = _pressCard;
            bool click = card != null && !_held;
            if (card != null)
            {
                ClearDragVisual(card);
                card.ReleaseMouseCapture();
            }
            _pressCard = null;
            _dragging = false;
            _held = false;
            if (click && card.Tag is Entry en) OpenDetail(en);
        }

        // ---------- Detail ----------
        private void OpenDetail(Entry en)
        {
            _editing = en;
            _loadingDetail = true;
            DetTitle.Text = en.Title;
            DetBody.Text = en.Detail;
            DetHint.Visibility = string.IsNullOrEmpty(en.Detail) ? Visibility.Visible : Visibility.Collapsed;
            _loadingDetail = false;
            ListPane.Visibility = Visibility.Collapsed;
            DetailPane.Visibility = Visibility.Visible;
            DetBody.Focus();
            DetBody.CaretIndex = DetBody.Text.Length;
        }

        private void Detail_Changed(object sender, TextChangedEventArgs e)
        {
            DetHint.Visibility = string.IsNullOrEmpty(DetBody.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (_loadingDetail || _editing == null) return;
            _editing.Title = DetTitle.Text;
            _editing.Detail = DetBody.Text;
        }

        private void CloseDetail()
        {
            if (_editing != null && string.IsNullOrWhiteSpace(_editing.Title)) _editing.Title = "Başlıksız";
            _editing = null;
            DetailPane.Visibility = Visibility.Collapsed;
            ListPane.Visibility = Visibility.Visible;
        }

        private void Back_Click(object sender, RoutedEventArgs e) => CloseDetail();

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) return;
            var en = _editing;
            _editing = null;
            if (!_notes.Remove(en)) _ideas.Remove(en);
            DetailPane.Visibility = Visibility.Collapsed;
            ListPane.Visibility = Visibility.Visible;
        }
    }
}
