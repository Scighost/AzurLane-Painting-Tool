using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AzurLane_Painting_Tool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            // 列表数据源
            listView.ItemsSource = PaintingInfos;
            // IndexName 递增排序
            listView.Items.SortDescriptions.Add(new SortDescription("IndexName", ListSortDirection.Ascending));
            // 列表过滤源
            collectionView = (CollectionView)CollectionViewSource.GetDefaultView(PaintingInfos);
        }


        #region 属性修改时通知

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        #endregion


        #region 私有字段

        // 立绘索引
        private Dictionary<string, string> paintingIndex;
        // 立绘数据源
        private ObservableCollection<PaintingInfo> PaintingInfos = new ObservableCollection<PaintingInfo>();
        // 索引与数据对照，防止重复添加数据
        private Dictionary<string, PaintingInfo> PaintingInfosDictionary = new Dictionary<string, PaintingInfo>();
        // 列表过滤源
        private CollectionView collectionView;

        #endregion


        #region 控件属性
        // 进度条当前值
        private int _ProgressValue;
        public int ProgressValue
        {
            get { return _ProgressValue; }
            set { _ProgressValue = value; OnPropertyChanged("ProgressValue"); }
        }

        // 搜索框文本
        private string _SearchText;
        public string SearchText
        {
            get { return _SearchText; }
            set
            {
                _SearchText = value;
                OnPropertyChanged("SearchText");
                // 匹配过滤
                if (String.IsNullOrEmpty(value))
                    collectionView.Filter = null;
                else
                    // TachieName 和 IndexName 之一匹配
                    collectionView.Filter = new Predicate<object>(x =>
                    ((PaintingInfo)x).PaintingName.Contains(value) || ((PaintingInfo)x).IndexName.Contains(value));
            }
        }

        // 是否启用导出按钮
        private bool _BtnExportEnable = true;
        public bool BtnExportEnable
        {
            get { return _BtnExportEnable; }
            set { _BtnExportEnable = value; OnPropertyChanged("BtnExportEnbale"); }
        }

        // 进度条颜色
        private string _ProgressColor = "RoyalBlue";
        public string ProgressColor
        {
            get { return _ProgressColor; }
            set { _ProgressColor = value; OnPropertyChanged("ProgressColor"); }
        }

        // 进度条最大值
        private double _ProgressMax = 100;
        public double ProgressMax
        {
            get { return _ProgressMax; }
            set { _ProgressMax = value; OnPropertyChanged("ProgressMax"); }
        }

        // 老图片源
        private BitmapImage _OldImageSource = new BitmapImage();
        public BitmapImage OldImageSource
        {
            get { return _OldImageSource; }
            set { _OldImageSource = value; OnPropertyChanged("OldImageSource"); }
        }

        // 新图片源
        private BitmapImage _NewImageSource = new BitmapImage();
        public BitmapImage NewImageSource
        {
            get { return _NewImageSource; }
            set { _NewImageSource = value; OnPropertyChanged("NewImageSource"); }
        }

        #endregion


        #region 窗体事件 创建缓存临时文件夹，加载并更新立绘索引文件，删除临时文件夹

        /// <summary>
        /// 创建缓存临时文件夹，加载并更新立绘索引文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 创建缓存临时文件夹
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurlanePaintingTool"));
            if (paintingIndex == null)
            {
                Task.Run(() => LoadPaintingsIndex());
            }
        }

        // 加载立绘索引
        private void LoadPaintingsIndex()
        {
            string path = Path.Combine(Environment.CurrentDirectory, "filematches.txt");
            string url = "https://github.com/scighost/AzurLane-Painting-Tool/blob/gui-ver/assets/filematches.txt";
            try
            {
                // 不存在立绘索引文件则直接下载
                if (File.Exists(path) && paintingIndex == null)
                {
                    paintingIndex = ReadIndexFile(path);
                }
                // 更新新立绘索引文件并重新读取
                WebClient webClient = new WebClient();
                webClient.DownloadFile(url, path);
                webClient.Dispose();
                paintingIndex = ReadIndexFile(path);
            }
            catch
            {
                // 失败重来直到成功
                LoadPaintingsIndex();
            }
        }

        // 从文件读取立绘索引
        private Dictionary<string, string> ReadIndexFile(string path)
        {
            StreamReader reader = new StreamReader(path, Encoding.UTF8);
            Dictionary<string, string> index = new Dictionary<string, string>();
            for (string tmp = reader.ReadLine(); tmp != null; tmp = reader.ReadLine())
            {
                try
                {
                    index.Add(tmp.Substring(0, tmp.IndexOf("(")), Regex.Match(tmp, @"\((.+)\)").Groups[1].Value);
                }
                catch { }
            }
            reader.Dispose();
            return index;
        }

        // 删除临时文件夹
        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurlanePaintingTool"), true);
            }
            catch { }
        }

        #endregion


        #region 添加文件
        private void listView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void listView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        // 添加拖入的文件
        private void listView_Drop(object sender, DragEventArgs e)
        {
            string[] pathes = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var path in pathes)
            {
                string indexName = System.IO.Path.GetFileName(path);
                indexName = indexName.Replace(".png", "").Replace("-mesh.obj", "");
                string paintingName = paintingIndex.ContainsKey(indexName) ? paintingIndex[indexName] == "" ? indexName : paintingIndex[indexName] : indexName;
                if (PaintingInfosDictionary.ContainsKey(indexName))
                {
                    if (PaintingInfosDictionary[indexName].IsContainsPng && PaintingInfosDictionary[indexName].IsContainsObj)
                    {
                        continue;
                    }
                    else
                    {
                        PaintingInfosDictionary[indexName].AddFile(path);
                    }
                }
                else
                {
                    PaintingInfo PaintingInfo = new PaintingInfo(path, indexName, paintingName);
                    PaintingInfos.Add(PaintingInfo);
                    PaintingInfosDictionary.Add(indexName, PaintingInfo);
                }
            }
        }

        // 从文件夹添加文件
        private void btn_AddFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DirectoryInfo directory = new DirectoryInfo(folderBrowserDialog.SelectedPath);
                FileInfo[] fileInfos = directory.GetFiles();
                foreach (var fileinfo in fileInfos)
                {
                    if (fileinfo.Extension != ".png" && fileinfo.Extension != ".obj")
                    {
                        continue;
                    }
                    string indexName = fileinfo.Name.Replace(".png", "").Replace("-mesh.obj", "");
                    string paintingName = paintingIndex.ContainsKey(indexName) ? paintingIndex[indexName] : indexName;
                    if (PaintingInfosDictionary.ContainsKey(indexName))
                    {
                        if (PaintingInfosDictionary[indexName].IsContainsPng && PaintingInfosDictionary[indexName].IsContainsObj)
                        {
                            continue;
                        }
                        else
                        {
                            PaintingInfosDictionary[indexName].AddFile(fileinfo.FullName);
                        }
                    }
                    else
                    {
                        PaintingInfo PaintingInfo = new PaintingInfo(fileinfo.FullName, indexName, paintingName);
                        PaintingInfos.Add(PaintingInfo);
                        PaintingInfosDictionary.Add(indexName, PaintingInfo);
                    }
                }
            }
        }

        #endregion


        #region 预览立绘

        // 列表选择改变
        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = listView.SelectedItem;
            if (item != null)
            {
                var PaintingInfo = item as PaintingInfo;
                // 首次预览则解密
                if (PaintingInfo.IsTransformFinished == false)
                {
                    PaintingInfo.GoStart();
                }
                PaintingInfoPrint(PaintingInfo);
                // 读取老文件并预览
                try
                {
                    OldImageSource = new BitmapImage();
                    OldImageSource.BeginInit();
                    OldImageSource.CacheOption = BitmapCacheOption.OnLoad;
                    OldImageSource.UriSource = new Uri(PaintingInfo.pngFile.FullName);
                    OldImageSource.EndInit();
                }
                catch
                {
                    OldImageSource = null;
                }
                // 读取新文件并预览
                try
                {
                    NewImageSource = new BitmapImage();
                    NewImageSource.BeginInit();
                    NewImageSource.CacheOption = BitmapCacheOption.OnLoad;
                    NewImageSource.UriSource = new Uri(PaintingInfo.TempPath);
                    NewImageSource.EndInit();
                }
                catch
                {
                    NewImageSource = null;
                }
            }
        }

        // 立绘文件信息
        void PaintingInfoPrint(PaintingInfo PaintingInfo)
        {
            textBlock.Text = "";
            textBlock.Text += $"Name:    {PaintingInfo.PaintingName}\n";
            textBlock.Text += $"Index:     {PaintingInfo.IndexName}\n";
            textBlock.Text += $"Texture:  {PaintingInfo.pngFile?.FullName}\n";
            textBlock.Text += $"Mesh:     {PaintingInfo.objFile?.FullName}\n";
            textBlock.Text += $"Size:       {PaintingInfo.NewBitmapSize}";
        }

        #endregion


        #region 列表前选择框相关事件

        // 单项选择
        private void itemCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in PaintingInfos)
            {
                if (item.IsSelected == false)
                {
                    // 复杂状态
                    allCheckBox.IsChecked = null;
                    return;
                }
            }
            // 全选状态
            allCheckBox.IsChecked = true;
        }

        // 单项取消选择
        private void itemCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in PaintingInfos)
            {
                if (item.IsSelected == true)
                {
                    allCheckBox.IsChecked = null;
                    return;
                }
            }
            // 全不选状态
            allCheckBox.IsChecked = false;
        }

        // 全选
        private void allCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in PaintingInfos)
            {
                item.IsSelected = true;
            }
            allCheckBox.IsChecked = true;
        }

        // 全不选
        private void allCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in PaintingInfos)
            {
                item.IsSelected = false;
            }
            allCheckBox.IsChecked = false;
        }

        #endregion


        #region 导出立绘
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string savePath = folderBrowserDialog.SelectedPath;
                // 导出文件数
                ProgressMax = 0;
                foreach (var item in PaintingInfos)
                {
                    if (item.IsSelected == true)
                    {
                        ProgressMax += 1;
                    }
                }
                Task.Run(() => ExportFiles(savePath));
            }

        }

        void ExportFiles(string savePath)
        {
            // 禁用导出按钮
            BtnExportEnable = false;
            ProgressValue = 0;
            ProgressColor = "RoyalBlue";
            // 并行处理
            Parallel.ForEach(PaintingInfos, (x) =>
            {
                if (x.IsSelected == true)
                {
                    x.ExportFile(savePath);
                    ProgressValue++;
                }
            });
            ProgressColor = "ForestGreen";
            // 进度条拉满
            ProgressValue = (int)ProgressMax;
            BtnExportEnable = true;
            GC.Collect();
        }

        #endregion

    }
}

