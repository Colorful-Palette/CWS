using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CWS
{
    public partial class BgEditorWindow : Window
    {
        private Point _lastMousePosition;
        private bool _isDragging = false;

        // 供 MainWindow 讀取的結果
        public ImageBrush ResultBrush { get; private set; }

        public BgEditorWindow(string imagePath)
        {
            InitializeComponent();
            LoadImage(imagePath);
        }

        private void LoadImage(string path)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                TargetImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show("圖片載入失敗: " + ex.Message);
                this.Close();
            }
        }

        // --- 核心算法：縮放 (以滑鼠為中心) ---
        private void ImageContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 1. 獲取相對於圖片的鼠標位置
            Point mousePos = e.GetPosition(TargetImage);

            // 2. 計算縮放比例
            double zoom = e.Delta > 0 ? 1.1 : 0.9;
            double newScale = imgScale.ScaleX * zoom;

            // 限制縮放範圍 (10% 到 1000%)
            if (newScale < 0.1 || newScale > 10) return;

            // 3. 補償平移量：確保鼠標下的點在縮放後依然在鼠標下
            imgTranslate.X -= mousePos.X * (newScale - imgScale.ScaleX);
            imgTranslate.Y -= mousePos.Y * (newScale - imgScale.ScaleY);

            // 4. 應用縮放
            imgScale.ScaleX = newScale;
            imgScale.ScaleY = newScale;
        }

        // --- 核心算法：拖動 ---
        private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(ViewportBorder); // 相對於容器
            ImageContainer.CaptureMouse(); // 鎖定滑鼠，防止移出邊界失靈
        }

        private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPos = e.GetPosition(ViewportBorder);
            Vector delta = currentPos - _lastMousePosition;

            // 累加平移數值
            imgTranslate.X += delta.X;
            imgTranslate.Y += delta.Y;

            _lastMousePosition = currentPos;
        }

        private void ImageContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ImageContainer.ReleaseMouseCapture();
        }

        // --- 導出與應用 ---
        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // 創建結果筆刷
            var brush = new ImageBrush(TargetImage.Source)
            {
                Stretch = Stretch.UniformToFill, // 這裡用 UniformToFill 以填充主視窗
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };

            // 關鍵：將「絕對像素平移」轉換為「相對比例平移」
            // 這樣在不同解析度的主視窗下，圖片顯示的位置才會一致
            double relX = imgTranslate.X / ViewportBorder.ActualWidth;
            double relY = imgTranslate.Y / ViewportBorder.ActualHeight;

            TransformGroup group = new TransformGroup();
            group.Children.Add(new ScaleTransform(imgScale.ScaleX, imgScale.ScaleY));
            group.Children.Add(new TranslateTransform(relX, relY));

            // 使用 RelativeTransform，變換數值會自動根據目標容器大小縮放
            brush.RelativeTransform = group;

            this.ResultBrush = brush;
            this.DialogResult = true;
        }

        // --- 基礎控制 ---
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            imgScale.ScaleX = 1.0;
            imgScale.ScaleY = 1.0;
            imgTranslate.X = 0;
            imgTranslate.Y = 0;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource == this)
                this.DragMove();
        }
    }
}